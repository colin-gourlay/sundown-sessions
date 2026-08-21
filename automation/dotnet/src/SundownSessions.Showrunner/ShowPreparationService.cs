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
    IReadOnlyDictionary<string, string> Identifiers);

public sealed class TagLibFlacMetadataReader : IFlacMetadataReader
{
    public FlacMetadata? TryRead(string filePath)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            var identifiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var xiph = file.GetTag(TagLib.TagTypes.Xiph, create: false) as TagLib.Ogg.XiphComment;
            if (xiph is not null)
            {
                foreach (var key in xiph)
                {
                    var values = xiph.GetField(key);
                    if (values is { Length: > 0 } && !string.IsNullOrWhiteSpace(values[0]))
                    {
                        identifiers[key] = values[0].Trim();
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }
}

public sealed class ShowPreparationService
{
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
        this.dbContext = dbContext;
        this.options = options;
        this.metadataReader = metadataReader ?? new TagLibFlacMetadataReader();
        musicRoot = NormaliseRoot(options.MusicRootPath);
        preparationRoot = NormaliseRoot(options.PreparationRootPath);
    }

    public async Task<ApplicationResult<ShowPreparationResultModel>> PrepareShowAsync(Guid showId, CancellationToken cancellationToken = default)
    {
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

        var priorBroadcastsByRecordingId = await dbContext.BroadcastRecordings
            .AsNoTracking()
            .Where(item => item.ShowId != show.Id && recordingIds.Contains(item.RecordingId))
            .Select(item => new
            {
                item.RecordingId,
                Entry = new BroadcastHistoryEntry(item.Id, item.ShowId, item.Show.Slug, item.Show.ShowDate, item.BroadcastAtUtc),
            })
            .ToListAsync(cancellationToken);
        priorBroadcastsByRecordingId = priorBroadcastsByRecordingId
            .OrderBy(item => item.Entry.ShowDate)
            .ThenBy(item => item.Entry.BroadcastAtUtc)
            .ToList();

        var catalogue = BuildFlacCatalogue();
        var matchedTracks = new List<PreparedTrackModel>(show.PlannedRecordings.Count);
        var unresolvedTracks = new List<UnresolvedPreparedTrackModel>();
        var repeatConflicts = new List<RepeatConflictModel>();
        var timingTracks = new List<PreparedTrackTimingModel>();
        var cumulative = TimeSpan.Zero;
        var positionWidth = Math.Max(2, show.PlannedRecordings.Count.ToString().Length);

        foreach (var planned in show.PlannedRecordings.OrderBy(item => item.Position))
        {
            if (!recordings.TryGetValue(planned.RecordingId, out var recording))
            {
                unresolvedTracks.Add(new UnresolvedPreparedTrackModel(
                    planned.Id,
                    planned.RecordingId,
                    planned.Position,
                    "missing_recording",
                    "The planned recording no longer exists in authoritative state.",
                    []));
                continue;
            }

            var identifierCandidates = FindIdentifierCandidates(recording, catalogue);
            List<CatalogueEntry> selectedCandidates;
            var matchKind = "metadata_identifier";
            if (identifierCandidates.Count > 0)
            {
                selectedCandidates = identifierCandidates;
            }
            else
            {
                matchKind = "normalised_text";
                selectedCandidates = FindNormalisedCandidates(recording, catalogue);
            }

            if (selectedCandidates.Count == 0)
            {
                unresolvedTracks.Add(new UnresolvedPreparedTrackModel(
                    planned.Id,
                    planned.RecordingId,
                    planned.Position,
                    "missing_file",
                    "No local FLAC file matched this planned recording.",
                    []));
                continue;
            }

            if (selectedCandidates.Count > 1)
            {
                unresolvedTracks.Add(new UnresolvedPreparedTrackModel(
                    planned.Id,
                    planned.RecordingId,
                    planned.Position,
                    "ambiguous_match",
                    "Multiple local FLAC files matched this planned recording.",
                    selectedCandidates
                        .OrderBy(item => item.Path, StringComparer.Ordinal)
                        .Select(item => new UnresolvedCandidateModel(item.Path, matchKind, item.Metadata.Title, item.Metadata.Artist, item.Metadata.Album))
                        .ToArray()));
                continue;
            }

            var selected = selectedCandidates[0];
            cumulative += selected.Metadata.Duration;
            var outputFileName = BuildOutputFileName(positionWidth, planned.Position, recording, selected.Metadata);
            matchedTracks.Add(new PreparedTrackModel(
                planned.Id,
                recording.Id,
                planned.Position,
                matchKind,
                selected.Path,
                outputFileName,
                selected.Metadata.Duration,
                cumulative));
            timingTracks.Add(new PreparedTrackTimingModel(planned.Id, planned.Position, selected.Metadata.Duration, cumulative));
        }

        var matchedCountByRecordingId = matchedTracks
            .GroupBy(item => item.RecordingId)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var track in matchedTracks)
        {
            var priorBroadcasts = priorBroadcastsByRecordingId
                .Where(item => item.RecordingId == track.RecordingId)
                .Select(item => item.Entry)
                .ToArray();

            var repeatedWithinShow = matchedCountByRecordingId.GetValueOrDefault(track.RecordingId, 0) > 1;
            if ((priorBroadcasts.Length > 0 || repeatedWithinShow) && !repeatExceptions.Contains(track.RecordingId))
            {
                repeatConflicts.Add(new RepeatConflictModel(track.PlannedRecordingId, track.RecordingId, priorBroadcasts));
            }
        }

        var trackPositionByPlannedId = matchedTracks.ToDictionary(item => item.PlannedRecordingId, item => item.Position);
        repeatConflicts = repeatConflicts
            .GroupBy(item => item.RecordingId)
            .Select(group =>
            {
                var primary = group.OrderBy(item => trackPositionByPlannedId[item.PlannedRecordingId]).First();
                var prior = group
                    .SelectMany(item => item.PriorBroadcasts)
                    .DistinctBy(item => item.BroadcastRecordingId)
                    .OrderBy(item => item.ShowDate)
                    .ThenBy(item => item.BroadcastAtUtc)
                    .ToArray();
                return new RepeatConflictModel(primary.PlannedRecordingId, group.Key, prior);
            })
            .OrderBy(item => trackPositionByPlannedId[item.PlannedRecordingId])
            .ToList();

        PreparedBroadcastFolderModel? folder = null;
        if (unresolvedTracks.Count == 0 && repeatConflicts.Count == 0)
        {
            folder = PrepareFolder(show.Slug, matchedTracks);
        }

        var total = timingTracks.LastOrDefault()?.CumulativeDuration ?? TimeSpan.Zero;
        var configuredShowDuration = options.ConfiguredShowDuration;
        var remaining = configuredShowDuration.HasValue ? (TimeSpan?)(configuredShowDuration.Value - total) : null;

        return ApplicationResult<ShowPreparationResultModel>.Success(new ShowPreparationResultModel(
            show.Id,
            show.Slug,
            show.PlannedRecordings.Count,
            matchedTracks.OrderBy(item => item.Position).ToArray(),
            unresolvedTracks.OrderBy(item => item.Position).ToArray(),
            repeatConflicts,
            new PreparationTimingModel(
                show.PlannedRecordings.Count,
                timingTracks.OrderBy(item => item.Position).ToArray(),
                total,
                configuredShowDuration,
                remaining),
            folder));
    }

    private PreparedBroadcastFolderModel PrepareFolder(string showSlug, IReadOnlyList<PreparedTrackModel> matchedTracks)
    {
        var folderPath = Path.Combine(preparationRoot, SanitisePathSegment(showSlug));
        var rebuilt = Directory.Exists(folderPath);
        var temporaryPath = Path.Combine(preparationRoot, $"{Path.GetFileName(folderPath)}.tmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryPath);
        var copiedFiles = new List<string>(matchedTracks.Count);
        try
        {
            foreach (var track in matchedTracks.OrderBy(item => item.Position))
            {
                var temporaryDestinationPath = Path.Combine(temporaryPath, track.OutputFileName);
                AssertInRoot(temporaryDestinationPath, temporaryPath);
                File.Copy(track.SourceFilePath, temporaryDestinationPath, overwrite: true);
                copiedFiles.Add(Path.Combine(folderPath, track.OutputFileName));
            }

            if (rebuilt)
            {
                Directory.Delete(folderPath, recursive: true);
            }

            Directory.Move(temporaryPath, folderPath);
        }
        finally
        {
            if (Directory.Exists(temporaryPath))
            {
                Directory.Delete(temporaryPath, recursive: true);
            }
        }

        return new PreparedBroadcastFolderModel(folderPath, rebuilt, copiedFiles);
    }

    private List<CatalogueEntry> BuildFlacCatalogue()
    {
        var files = Directory.Exists(musicRoot)
            ? Directory.EnumerateFiles(musicRoot, "*.flac", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();

        return files
            .Select(Path.GetFullPath)
            .Where(path => IsInRoot(path, musicRoot) && !IsInRoot(path, preparationRoot))
            .Distinct(StringComparer.Ordinal)
            .Select(path => new { Path = path, Metadata = metadataReader.TryRead(path) })
            .Where(item => item.Metadata is not null)
            .Select(item => new CatalogueEntry(item.Path, item.Metadata!))
            .ToList();
    }

    private static List<CatalogueEntry> FindIdentifierCandidates(RecordingEntity recording, IReadOnlyList<CatalogueEntry> catalogue)
    {
        if (recording.ExternalIdentifiers.Count == 0)
        {
            return [];
        }

        return catalogue
            .Where(entry => recording.ExternalIdentifiers.Any(identifier => MatchesIdentifier(identifier.Source, identifier.Value, entry.Metadata.Identifiers)))
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ToList();
    }

    private static List<CatalogueEntry> FindNormalisedCandidates(RecordingEntity recording, IReadOnlyList<CatalogueEntry> catalogue)
    {
        var normalisedTitle = Normalise(recording.Title);
        var normalisedArtist = Normalise(recording.Artist);
        return catalogue
            .Where(item =>
                string.Equals(Normalise(item.Metadata.Title), normalisedTitle, StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(normalisedArtist) || string.Equals(Normalise(item.Metadata.Artist), normalisedArtist, StringComparison.Ordinal)))
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ToList();
    }

    private static bool MatchesIdentifier(string source, string value, IReadOnlyDictionary<string, string> identifiers)
    {
        var normalisedSource = source.Trim().ToLowerInvariant();
        var normalisedValue = CanonicaliseIdentifierValue(normalisedSource, value);
        var sourceSpecificMatches = identifiers
            .Where(pair => IsLikelySourceKey(normalisedSource, pair.Key.Trim()))
            .Select(pair => CanonicaliseIdentifierValue(normalisedSource, pair.Value))
            .ToArray();

        if (sourceSpecificMatches.Length > 0)
        {
            return sourceSpecificMatches.Any(candidate => string.Equals(candidate, normalisedValue, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var pair in identifiers)
        {
            var candidate = CanonicaliseIdentifierValue(normalisedSource, pair.Value);
            if (string.Equals(candidate, normalisedValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLikelySourceKey(string source, string key)
    {
        var uppercaseKey = key.ToUpperInvariant();
        return source switch
        {
            "spotify" => uppercaseKey is "SPOTIFY_TRACK_ID" or "SPOTIFY_TRACK_URI",
            "musicbrainz" => uppercaseKey is "MUSICBRAINZ_TRACKID" or "MUSICBRAINZ_RELEASETRACKID",
            "isrc" => uppercaseKey == "ISRC",
            _ => string.Equals(uppercaseKey, source, StringComparison.OrdinalIgnoreCase),
        };
    }

    private static string CanonicaliseIdentifierValue(string source, string value)
    {
        var trimmed = value.Trim();
        if (source == "spotify")
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

    private static string BuildOutputFileName(int positionWidth, int position, RecordingEntity recording, FlacMetadata metadata)
    {
        var artist = recording.Artist ?? metadata.Artist ?? "Unknown Artist";
        var title = recording.Title;
        return $"{position.ToString($"D{positionWidth}")} - {SanitisePathSegment(artist)} - {SanitisePathSegment(title)}.flac";
    }

    private static string SanitisePathSegment(string value)
    {
        var chars = value.Trim().Select(ch =>
            char.IsControl(ch) || ch is '/' or '\\' or ':' ? '-' : ch).ToArray();
        var sanitised = new string(chars);
        var collapsed = string.Join(' ', sanitised.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(collapsed))
        {
            collapsed = "untitled";
        }

        return collapsed.Length > 200 ? collapsed[..200] : collapsed;
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
            .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)));
        return string.Join(' ', filtered.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormaliseRoot(string path)
    {
        var full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

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

    private sealed record CatalogueEntry(string Path, FlacMetadata Metadata);
}
