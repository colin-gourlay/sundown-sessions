using System.Text;
using Microsoft.EntityFrameworkCore;
using SundownSessions.Showrunner.Persistence;

namespace SundownSessions.Showrunner;

public interface IFlacMetadataReader
{
    FlacMetadata? TryRead(string filePath);
}

public sealed record FlacMetadata(
    string? Title,
    string? Artist,
    string? Album,
    TimeSpan Duration,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Identifiers);

public sealed class TagLibFlacMetadataReader : IFlacMetadataReader
{
    public FlacMetadata? TryRead(string filePath)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            var identifiers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            var xiph = file.GetTag(TagLib.TagTypes.Xiph, create: false) as TagLib.Ogg.XiphComment;
            if (xiph is not null)
            {
                foreach (var key in xiph)
                {
                    var values = xiph.GetField(key)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    if (values.Length > 0)
                    {
                        identifiers[key] = values;
                    }
                }
            }

            return new FlacMetadata(
                file.Tag.Title?.Trim(),
                file.Tag.FirstPerformer?.Trim(),
                file.Tag.Album?.Trim(),
                file.Properties.Duration,
                identifiers);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
    }
}

public sealed class ShowPreparationService
{
    private const string LocalFileIdentifierSource = "local-file";
    private const int MaximumFileNameBytes = 240;
    private readonly ShowrunnerDbContext dbContext;
    private readonly ShowPreparationOptions options;
    private readonly IFlacMetadataReader metadataReader;
    private readonly string musicRoot;
    private readonly string preparationRoot;

    public ShowPreparationService(
        ShowrunnerDbContext dbContext,
        ShowPreparationOptions options,
        IFlacMetadataReader? metadataReader = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.MusicRootPath))
        {
            throw new ArgumentException("A music root path is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.PreparationRootPath))
        {
            throw new ArgumentException("A preparation root path is required.", nameof(options));
        }

        if (options.ConfiguredShowDuration <= TimeSpan.Zero)
        {
            throw new ArgumentException("The configured show duration must be greater than zero.", nameof(options));
        }

        this.dbContext = dbContext;
        this.options = options;
        this.metadataReader = metadataReader ?? new TagLibFlacMetadataReader();
        musicRoot = NormaliseRoot(options.MusicRootPath);
        preparationRoot = NormaliseRoot(options.PreparationRootPath);

        if (IsInRoot(musicRoot, preparationRoot))
        {
            throw new ArgumentException("The music root cannot be inside the preparation root.", nameof(options));
        }
    }

    public async Task<ApplicationResult<ShowPreparationResultModel>> PrepareShowAsync(
        Guid showId,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(musicRoot))
        {
            return ApplicationResult<ShowPreparationResultModel>.Failure(
                ApplicationError.OperationFailed(
                    "music_root_unavailable",
                    "The configured music root is unavailable."));
        }

        var show = await dbContext.Shows
            .AsNoTracking()
            .Include(item => item.PlannedRecordings.OrderBy(recording => recording.Position))
            .SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);
        if (show is null)
        {
            return ApplicationResult<ShowPreparationResultModel>.Failure(ApplicationError.NotFound("show", showId));
        }

        var recordingIds = show.PlannedRecordings.Select(item => item.RecordingId).Distinct().ToArray();
        var recordings = await dbContext.Recordings
            .AsNoTracking()
            .Include(item => item.ExternalIdentifiers)
            .Where(item => recordingIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var repeatExceptions = await dbContext.RepeatExceptions
            .AsNoTracking()
            .Where(item => item.ShowId == show.Id)
            .Select(item => item.RecordingId)
            .ToHashSetAsync(cancellationToken);

        var priorBroadcastRows = await dbContext.BroadcastRecordings
            .AsNoTracking()
            .Where(item => item.ShowId != show.Id && recordingIds.Contains(item.RecordingId))
            .Select(item => new
            {
                item.RecordingId,
                BroadcastRecordingId = item.Id,
                item.ShowId,
                ShowSlug = item.Show.Slug,
                item.Show.ShowDate,
                item.BroadcastAtUtc,
            })
            .ToListAsync(cancellationToken);
        var priorBroadcastsByRecordingId = priorBroadcastRows
            .OrderBy(item => item.ShowDate)
            .ThenBy(item => item.BroadcastAtUtc)
            .Select(item => new
            {
                item.RecordingId,
                Entry = new BroadcastHistoryEntry(
                    item.BroadcastRecordingId,
                    item.ShowId,
                    item.ShowSlug,
                    item.ShowDate,
                    item.BroadcastAtUtc),
            })
            .ToArray();

        var catalogueResult = BuildFlacCatalogue(cancellationToken);
        if (!catalogueResult.IsSuccess)
        {
            return ApplicationResult<ShowPreparationResultModel>.Failure(catalogueResult.Error!);
        }

        var catalogue = catalogueResult.Value!;
        var matched = new List<MatchedTrack>(show.PlannedRecordings.Count);
        var unresolvedTracks = new List<UnresolvedPreparedTrackModel>();
        var timingTracks = new List<PreparedTrackTimingModel>();
        var cumulative = TimeSpan.Zero;
        var positionWidth = Math.Max(2, show.PlannedRecordings.Count.ToString().Length);

        foreach (var planned in show.PlannedRecordings.OrderBy(item => item.Position))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!recordings.TryGetValue(planned.RecordingId, out var recording))
            {
                unresolvedTracks.Add(new UnresolvedPreparedTrackModel(
                    planned.Id,
                    planned.RecordingId,
                    planned.Position,
                    UnresolvedTrackKind.MissingRecording,
                    "The planned recording no longer exists in authoritative state.",
                    []));
                continue;
            }

            var decision = FindCandidates(recording, catalogue);
            if (decision.Candidates.Count == 0)
            {
                unresolvedTracks.Add(new UnresolvedPreparedTrackModel(
                    planned.Id,
                    planned.RecordingId,
                    planned.Position,
                    decision.UnresolvedKind ?? UnresolvedTrackKind.MissingFile,
                    decision.UnresolvedKind == UnresolvedTrackKind.IdentifierConflict
                        ? "Local metadata conflicts with the recording's stable identifier."
                        : "No local FLAC file matched this planned recording.",
                    decision.Evidence.Select(candidate => MapCandidate(candidate, decision.MatchKind)).ToArray()));
                continue;
            }

            if (decision.Candidates.Count > 1)
            {
                unresolvedTracks.Add(new UnresolvedPreparedTrackModel(
                    planned.Id,
                    planned.RecordingId,
                    planned.Position,
                    UnresolvedTrackKind.AmbiguousMatch,
                    "Multiple local FLAC files matched this planned recording.",
                    decision.Candidates.Select(candidate => MapCandidate(candidate, decision.MatchKind)).ToArray()));
                continue;
            }

            var selected = decision.Candidates[0];
            cumulative += selected.Metadata.Duration;
            var model = new PreparedTrackModel(
                planned.Id,
                recording.Id,
                planned.Position,
                decision.MatchKind,
                selected.RelativePath,
                BuildOutputFileName(positionWidth, planned.Position, recording, selected.Metadata),
                selected.Metadata.Duration,
                cumulative);
            matched.Add(new MatchedTrack(model, selected.AbsolutePath));
            timingTracks.Add(new PreparedTrackTimingModel(planned.Id, planned.Position, selected.Metadata.Duration, cumulative));
        }

        var matchedTracks = matched.Select(item => item.Model).ToArray();
        var matchedCountByRecordingId = matchedTracks
            .GroupBy(item => item.RecordingId)
            .ToDictionary(group => group.Key, group => group.Count());
        var repeatConflicts = matchedTracks
            .Where(track =>
            {
                var hasPriorBroadcast = priorBroadcastsByRecordingId.Any(item => item.RecordingId == track.RecordingId);
                var repeatedWithinShow = matchedCountByRecordingId.GetValueOrDefault(track.RecordingId) > 1;
                return (hasPriorBroadcast || repeatedWithinShow) && !repeatExceptions.Contains(track.RecordingId);
            })
            .GroupBy(track => track.RecordingId)
            .Select(group =>
            {
                var primary = group.OrderBy(item => item.Position).First();
                var prior = priorBroadcastsByRecordingId
                    .Where(item => item.RecordingId == group.Key)
                    .Select(item => item.Entry)
                    .DistinctBy(item => item.BroadcastRecordingId)
                    .ToArray();
                return new RepeatConflictModel(primary.PlannedRecordingId, group.Key, prior);
            })
            .OrderBy(conflict => matchedTracks.Single(track => track.PlannedRecordingId == conflict.PlannedRecordingId).Position)
            .ToArray();

        PreparedBroadcastFolderModel? folder = null;
        if (unresolvedTracks.Count == 0 && repeatConflicts.Length == 0)
        {
            var folderResult = PrepareFolder(show.Id, show.Slug, matched, cancellationToken);
            if (!folderResult.IsSuccess)
            {
                return ApplicationResult<ShowPreparationResultModel>.Failure(folderResult.Error!);
            }

            folder = folderResult.Value;
        }

        var total = timingTracks.LastOrDefault()?.CumulativeDuration ?? TimeSpan.Zero;
        var configuredDuration = options.ConfiguredShowDuration;
        var difference = configuredDuration - total;
        var remaining = difference >= TimeSpan.Zero ? difference : null;
        var overrun = difference < TimeSpan.Zero ? -difference : null;
        var status = unresolvedTracks.Count > 0
            ? ShowPreparationStatus.Unresolved
            : repeatConflicts.Length > 0
                ? ShowPreparationStatus.RepeatConflict
                : ShowPreparationStatus.Prepared;

        return ApplicationResult<ShowPreparationResultModel>.Success(new ShowPreparationResultModel(
            show.Id,
            show.Slug,
            status,
            show.PlannedRecordings.Count,
            matchedTracks.OrderBy(item => item.Position).ToArray(),
            unresolvedTracks.OrderBy(item => item.Position).ToArray(),
            repeatConflicts,
            new PreparationTimingModel(
                show.PlannedRecordings.Count,
                timingTracks.Count,
                timingTracks.OrderBy(item => item.Position).ToArray(),
                total,
                configuredDuration,
                remaining,
                overrun),
            folder));
    }

    public async Task<ApplicationResult<RecordingResolutionModel>> ResolveRecordingAsync(
        Guid recordingId,
        string sourceLibraryPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceLibraryPath) || Path.IsPathRooted(sourceLibraryPath))
        {
            return ApplicationResult<RecordingResolutionModel>.Failure(
                ApplicationError.Validation("sourceLibraryPath", "A relative candidate path from show preparation is required."));
        }

        var catalogueResult = BuildFlacCatalogue(cancellationToken);
        if (!catalogueResult.IsSuccess)
        {
            return ApplicationResult<RecordingResolutionModel>.Failure(catalogueResult.Error!);
        }

        var normalisedRelativePath = NormaliseRelativePath(sourceLibraryPath);
        var candidate = catalogueResult.Value!.SingleOrDefault(
            item => string.Equals(item.RelativePath, normalisedRelativePath, StringComparison.Ordinal));
        if (candidate is null)
        {
            return ApplicationResult<RecordingResolutionModel>.Failure(
                ApplicationError.OperationFailed(
                    "candidate_unavailable",
                    "The selected FLAC candidate is unavailable inside the configured music root."));
        }

        var recording = await dbContext.Recordings
            .Include(item => item.ExternalIdentifiers)
            .SingleOrDefaultAsync(item => item.Id == recordingId, cancellationToken);
        if (recording is null)
        {
            return ApplicationResult<RecordingResolutionModel>.Failure(ApplicationError.NotFound("recording", recordingId));
        }

        var usedByAnotherRecording = await dbContext.RecordingExternalIdentifiers.AnyAsync(
            item => item.RecordingId != recordingId &&
                    item.Source == LocalFileIdentifierSource &&
                    item.Value == normalisedRelativePath,
            cancellationToken);
        if (usedByAnotherRecording)
        {
            return ApplicationResult<RecordingResolutionModel>.Failure(
                ApplicationError.Conflict(
                    "local_file_in_use",
                    "That local FLAC is already resolved to another recording.",
                    "sourceLibraryPath",
                    normalisedRelativePath));
        }

        foreach (var existing in recording.ExternalIdentifiers
                     .Where(item => item.Source == LocalFileIdentifierSource)
                     .ToArray())
        {
            dbContext.RecordingExternalIdentifiers.Remove(existing);
        }

        recording.ExternalIdentifiers.Add(new RecordingExternalIdentifierEntity
        {
            Id = Guid.NewGuid(),
            RecordingId = recording.Id,
            Source = LocalFileIdentifierSource,
            Value = normalisedRelativePath,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RecordingResolutionModel>.Success(new RecordingResolutionModel(
            recording.Id,
            candidate.RelativePath,
            candidate.Metadata.Title,
            candidate.Metadata.Artist,
            candidate.Metadata.Album,
            candidate.Metadata.Duration));
    }

    private ApplicationResult<IReadOnlyList<CatalogueEntry>> BuildFlacCatalogue(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(musicRoot))
        {
            return ApplicationResult<IReadOnlyList<CatalogueEntry>>.Failure(
                ApplicationError.OperationFailed("music_root_unavailable", "The configured music root is unavailable."));
        }

        try
        {
            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = false,
                ReturnSpecialDirectories = false,
            };
            var catalogue = new List<CatalogueEntry>();
            foreach (var path in Directory.EnumerateFiles(musicRoot, "*", enumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals(Path.GetExtension(path), ".flac", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var absolutePath = Path.GetFullPath(path);
                if (!IsInRoot(absolutePath, musicRoot) || IsInRoot(absolutePath, preparationRoot))
                {
                    continue;
                }

                var metadata = metadataReader.TryRead(absolutePath);
                if (metadata is null || metadata.Duration < TimeSpan.Zero)
                {
                    continue;
                }

                catalogue.Add(new CatalogueEntry(
                    absolutePath,
                    NormaliseRelativePath(Path.GetRelativePath(musicRoot, absolutePath)),
                    metadata));
            }

            return ApplicationResult<IReadOnlyList<CatalogueEntry>>.Success(catalogue
                .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
                .ToArray());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return ApplicationResult<IReadOnlyList<CatalogueEntry>>.Failure(
                ApplicationError.OperationFailed(
                    "music_scan_failed",
                    "The configured music root could not be scanned safely."));
        }
    }

    private static CandidateDecision FindCandidates(RecordingEntity recording, IReadOnlyList<CatalogueEntry> catalogue)
    {
        var explicitCandidates = catalogue
            .Where(entry => recording.ExternalIdentifiers.Any(identifier =>
                identifier.Source == LocalFileIdentifierSource &&
                string.Equals(NormaliseRelativePath(identifier.Value), entry.RelativePath, StringComparison.Ordinal)))
            .ToArray();
        if (explicitCandidates.Length > 0)
        {
            return new CandidateDecision(explicitCandidates, [], RecordingMatchKind.ExplicitResolution, null);
        }

        var identifierCandidates = catalogue
            .Where(entry => recording.ExternalIdentifiers.Any(identifier => MatchesIdentifier(identifier, entry.Metadata)))
            .ToArray();
        if (identifierCandidates.Length > 0)
        {
            var consistentCandidates = identifierCandidates
                .Where(entry => !HasIdentifierConflict(recording, entry.Metadata))
                .ToArray();
            return consistentCandidates.Length > 0
                ? new CandidateDecision(consistentCandidates, [], RecordingMatchKind.MetadataIdentifier, null)
                : new CandidateDecision(
                    [],
                    identifierCandidates,
                    RecordingMatchKind.MetadataIdentifier,
                    UnresolvedTrackKind.IdentifierConflict);
        }

        var textCandidates = FindNormalisedCandidates(recording, catalogue);
        var conflictingCandidates = textCandidates.Where(entry => HasIdentifierConflict(recording, entry.Metadata)).ToArray();
        var safeCandidates = textCandidates.Except(conflictingCandidates).ToArray();
        if (safeCandidates.Length == 0 && conflictingCandidates.Length > 0)
        {
            return new CandidateDecision(
                [],
                conflictingCandidates,
                RecordingMatchKind.NormalisedMetadata,
                UnresolvedTrackKind.IdentifierConflict);
        }

        return new CandidateDecision(safeCandidates, [], RecordingMatchKind.NormalisedMetadata, null);
    }

    private static CatalogueEntry[] FindNormalisedCandidates(
        RecordingEntity recording,
        IReadOnlyList<CatalogueEntry> catalogue)
    {
        var normalisedTitle = Normalise(recording.Title);
        var normalisedArtist = Normalise(recording.Artist);
        var normalisedRelease = Normalise(recording.ReleaseTitle);
        return catalogue
            .Where(item =>
                string.Equals(Normalise(item.Metadata.Title), normalisedTitle, StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(normalisedArtist) ||
                 string.Equals(Normalise(item.Metadata.Artist), normalisedArtist, StringComparison.Ordinal)) &&
                (string.IsNullOrWhiteSpace(normalisedRelease) ||
                 string.Equals(Normalise(item.Metadata.Album), normalisedRelease, StringComparison.Ordinal)))
            .ToArray();
    }

    private static bool MatchesIdentifier(RecordingExternalIdentifierEntity identifier, FlacMetadata metadata)
    {
        if (identifier.Source == LocalFileIdentifierSource)
        {
            return false;
        }

        var values = GetMetadataIdentifierValues(identifier.Source, metadata.Identifiers);
        var expected = CanonicaliseIdentifierValue(identifier.Source, identifier.Value);
        return values.Any(value => string.Equals(
            CanonicaliseIdentifierValue(identifier.Source, value),
            expected,
            StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasIdentifierConflict(RecordingEntity recording, FlacMetadata metadata)
    {
        foreach (var identifier in recording.ExternalIdentifiers.Where(item => item.Source != LocalFileIdentifierSource))
        {
            var values = GetMetadataIdentifierValues(identifier.Source, metadata.Identifiers).ToArray();
            if (values.Length > 0 && !MatchesIdentifier(identifier, metadata))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetMetadataIdentifierValues(
        string source,
        IReadOnlyDictionary<string, IReadOnlyList<string>> identifiers)
    {
        var expectedKeys = source.Trim().ToLowerInvariant() switch
        {
            "spotify" => new[] { "SPOTIFY_TRACK_ID", "SPOTIFY_TRACK_URI" },
            "musicbrainz" or "musicbrainz-recording" => new[] { "MUSICBRAINZ_TRACKID" },
            "musicbrainz-release-track" => new[] { "MUSICBRAINZ_RELEASETRACKID" },
            "isrc" => new[] { "ISRC" },
            _ => new[] { source },
        };

        return identifiers
            .Where(pair => expectedKeys.Any(key => string.Equals(
                NormaliseIdentifierKey(pair.Key),
                NormaliseIdentifierKey(key),
                StringComparison.Ordinal)))
            .SelectMany(pair => pair.Value);
    }

    private static string NormaliseIdentifierKey(string value)
        => string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private static string CanonicaliseIdentifierValue(string source, string value)
    {
        var trimmed = value.Trim();
        if (source.Equals("spotify", StringComparison.OrdinalIgnoreCase))
        {
            if (trimmed.StartsWith("spotify:track:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed["spotify:track:".Length..];
            }

            if (trimmed.StartsWith("https://open.spotify.com/track/", StringComparison.OrdinalIgnoreCase))
            {
                var trackPart = trimmed["https://open.spotify.com/track/".Length..];
                return trackPart.Split('?', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
            }
        }

        return trimmed;
    }

    private ApplicationResult<PreparedBroadcastFolderModel> PrepareFolder(
        Guid showId,
        string showSlug,
        IReadOnlyList<MatchedTrack> matchedTracks,
        CancellationToken cancellationToken)
    {
        var folderName = $"{SanitisePathSegment(showSlug)}--{showId:N}";
        var folderPath = Path.Combine(preparationRoot, folderName);
        var temporaryPath = Path.Combine(preparationRoot, $".{folderName}.tmp-{Guid.NewGuid():N}");
        var backupPath = Path.Combine(preparationRoot, $".{folderName}.backup-{Guid.NewGuid():N}");
        var rebuilt = Directory.Exists(folderPath);
        var oldFolderMoved = false;
        var replacementInstalled = false;

        try
        {
            Directory.CreateDirectory(preparationRoot);
            AssertInRoot(folderPath, preparationRoot);
            AssertInRoot(temporaryPath, preparationRoot);
            AssertInRoot(backupPath, preparationRoot);
            if (rebuilt && new DirectoryInfo(folderPath).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return ApplicationResult<PreparedBroadcastFolderModel>.Failure(
                    ApplicationError.OperationFailed(
                        "unsafe_preparation_folder",
                        "The existing preparation folder is a symbolic link and cannot be rebuilt safely."));
            }

            Directory.CreateDirectory(temporaryPath);
            foreach (var track in matchedTracks.OrderBy(item => item.Model.Position))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destinationPath = Path.Combine(temporaryPath, track.Model.OutputFileName);
                AssertInRoot(destinationPath, temporaryPath);
                File.Copy(track.AbsoluteSourcePath, destinationPath, overwrite: false);
            }

            if (rebuilt)
            {
                Directory.Move(folderPath, backupPath);
                oldFolderMoved = true;
            }

            Directory.Move(temporaryPath, folderPath);
            replacementInstalled = true;
            if (oldFolderMoved)
            {
                TryDeleteDirectory(backupPath);
                oldFolderMoved = false;
            }

            return ApplicationResult<PreparedBroadcastFolderModel>.Success(new PreparedBroadcastFolderModel(
                folderName,
                rebuilt,
                matchedTracks.OrderBy(item => item.Model.Position).Select(item => item.Model.OutputFileName).ToArray()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            if (oldFolderMoved && !replacementInstalled && !Directory.Exists(folderPath))
            {
                try
                {
                    Directory.Move(backupPath, folderPath);
                    oldFolderMoved = false;
                }
                catch (Exception restoreException) when (restoreException is IOException or UnauthorizedAccessException)
                {
                    // Preserve the backup for manual recovery.
                }
            }

            return ApplicationResult<PreparedBroadcastFolderModel>.Failure(
                ApplicationError.OperationFailed(
                    "preparation_folder_failed",
                    "The broadcast preparation folder could not be rebuilt safely."));
        }
        finally
        {
            if (Directory.Exists(temporaryPath))
            {
                TryDeleteDirectory(temporaryPath);
            }

            if (oldFolderMoved && replacementInstalled && Directory.Exists(backupPath))
            {
                TryDeleteDirectory(backupPath);
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A failed cleanup must not hide the operation result or remove a recoverable backup.
        }
    }

    private static UnresolvedCandidateModel MapCandidate(CatalogueEntry candidate, RecordingMatchKind matchKind)
        => new(
            candidate.RelativePath,
            matchKind,
            candidate.Metadata.Title,
            candidate.Metadata.Artist,
            candidate.Metadata.Album);

    private static string BuildOutputFileName(
        int positionWidth,
        int position,
        RecordingEntity recording,
        FlacMetadata metadata)
    {
        var prefix = $"{position.ToString($"D{positionWidth}")} - ";
        const string separator = " - ";
        const string extension = ".flac";
        var availableBytes = MaximumFileNameBytes
            - Encoding.UTF8.GetByteCount(prefix)
            - Encoding.UTF8.GetByteCount(separator)
            - Encoding.UTF8.GetByteCount(extension);
        var artist = TruncateUtf8(SanitisePathSegment(recording.Artist ?? metadata.Artist ?? "Unknown Artist"), availableBytes / 2);
        var title = TruncateUtf8(
            SanitisePathSegment(recording.Title),
            availableBytes - Encoding.UTF8.GetByteCount(artist));
        return $"{prefix}{artist}{separator}{title}{extension}";
    }

    private static string TruncateUtf8(string value, int maximumBytes)
    {
        var builder = new StringBuilder(value.Length);
        var byteCount = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var runeBytes = rune.Utf8SequenceLength;
            if (byteCount + runeBytes > maximumBytes)
            {
                break;
            }

            builder.Append(rune);
            byteCount += runeBytes;
        }

        return builder.Length == 0 ? "untitled" : builder.ToString();
    }

    private static string SanitisePathSegment(string value)
    {
        var chars = value.Trim().Select(character =>
            char.IsControl(character) || character is '/' or '\\' or ':' ? '-' : character).ToArray();
        var collapsed = string.Join(
            ' ',
            new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return string.IsNullOrWhiteSpace(collapsed) ? "untitled" : collapsed;
    }

    private static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var filtered = string.Concat(value
            .Trim()
            .ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character)));
        return string.Join(' ', filtered.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormaliseRoot(string path)
    {
        var fullPath = Path.GetFullPath(path.Trim());
        if (Directory.Exists(fullPath))
        {
            fullPath = new DirectoryInfo(fullPath).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? fullPath;
        }

        return Path.TrimEndingDirectorySeparator(fullPath);
    }

    private static string NormaliseRelativePath(string path)
        => path.Trim().Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    private static bool IsInRoot(string path, string root)
    {
        var normalised = Path.GetFullPath(path);
        return normalised.Equals(root, StringComparison.Ordinal) ||
               normalised.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static void AssertInRoot(string path, string root)
    {
        if (!IsInRoot(path, root))
        {
            throw new InvalidOperationException("Prepared output path is outside the configured preparation root.");
        }
    }

    private sealed record CatalogueEntry(string AbsolutePath, string RelativePath, FlacMetadata Metadata);

    private sealed record CandidateDecision(
        IReadOnlyList<CatalogueEntry> Candidates,
        IReadOnlyList<CatalogueEntry> Evidence,
        RecordingMatchKind MatchKind,
        UnresolvedTrackKind? UnresolvedKind);

    private sealed record MatchedTrack(PreparedTrackModel Model, string AbsoluteSourcePath);
}
