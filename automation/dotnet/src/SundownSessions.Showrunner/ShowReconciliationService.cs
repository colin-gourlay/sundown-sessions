using Microsoft.EntityFrameworkCore;
using SundownSessions.Showrunner.Persistence;

namespace SundownSessions.Showrunner;

public sealed class ShowReconciliationService
{
    private const string LocalFileIdentifierSource = "local-file";
    private readonly ShowrunnerDbContext dbContext;
    private readonly IMixxxPlaybackEvidenceReader evidenceReader;
    private readonly IShowrunnerClock clock;

    public ShowReconciliationService(
        ShowrunnerDbContext dbContext,
        IMixxxPlaybackEvidenceReader evidenceReader,
        IShowrunnerClock? clock = null)
    {
        this.dbContext = dbContext;
        this.evidenceReader = evidenceReader;
        this.clock = clock ?? new SystemShowrunnerClock();
    }

    public async Task<ApplicationResult<PlaybackEvidenceModel>> GetPlaybackEvidenceAsync(
        Guid showId,
        CancellationToken cancellationToken = default)
    {
        var show = await dbContext.Shows
            .AsNoTracking()
            .Include(item => item.PlannedRecordings.OrderBy(recording => recording.Position))
            .SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);
        if (show is null)
        {
            return ApplicationResult<PlaybackEvidenceModel>.Failure(ApplicationError.NotFound("show", showId));
        }

        var recordings = await dbContext.Recordings
            .AsNoTracking()
            .Include(item => item.ExternalIdentifiers)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var evidenceResult = await evidenceReader.ReadPlaybackEvidenceAsync(show.ShowDate, cancellationToken);
        if (!evidenceResult.IsSuccess)
        {
            return ApplicationResult<PlaybackEvidenceModel>.Failure(evidenceResult.Error!);
        }

        var readModel = evidenceResult.Value!;
        var warnings = readModel.Warnings.ToList();
        var evidence = CollapseHistoryNoise(readModel.Candidates, out var discardedUnusableEvidence);
        if (discardedUnusableEvidence)
        {
            warnings.Add("mixxx_unusable_track_metadata");
        }

        var planned = show.PlannedRecordings.OrderBy(item => item.Position).ToArray();
        var remainingPlanned = planned.Select(item => item.Id).ToHashSet();
        var assignments = new Dictionary<Guid, int>();
        var ambiguousPlannedIds = new HashSet<Guid>();
        var unassignedEvidence = new HashSet<int>(Enumerable.Range(0, evidence.Count));

        for (var evidenceIndex = 0; evidenceIndex < evidence.Count; evidenceIndex++)
        {
            var recordingCandidates = FindRecordingCandidates(recordings.Values, evidence[evidenceIndex]);
            var matchingPlans = planned
                .Where(item => remainingPlanned.Contains(item.Id) && recordingCandidates.Contains(item.RecordingId))
                .OrderBy(item => item.Position)
                .ToArray();
            if (recordingCandidates.Count == 0 || matchingPlans.Length == 0)
            {
                continue;
            }

            if (recordingCandidates.Count > 1)
            {
                ambiguousPlannedIds.UnionWith(matchingPlans.Select(item => item.Id));
                continue;
            }

            var selected = matchingPlans[0];
            assignments[selected.Id] = evidenceIndex;
            remainingPlanned.Remove(selected.Id);
            unassignedEvidence.Remove(evidenceIndex);
        }

        var plannedItems = planned.Select(item =>
        {
            if (!recordings.TryGetValue(item.RecordingId, out var recording))
            {
                warnings.Add("planned_recording_missing");
                return new PlannedPlaybackEvidenceItemModel(
                    item.Id,
                    item.RecordingId,
                    item.Position,
                    "(missing recording)",
                    null,
                    false,
                    null,
                    null,
                    false);
            }

            var isDetected = assignments.TryGetValue(item.Id, out var evidenceIndex);
            return new PlannedPlaybackEvidenceItemModel(
                item.Id,
                item.RecordingId,
                item.Position,
                recording.Title,
                recording.Artist,
                isDetected,
                isDetected ? evidenceIndex + 1 : null,
                isDetected ? evidence[evidenceIndex].PlayedAtUtc : null,
                ambiguousPlannedIds.Contains(item.Id));
        }).ToArray();

        var unexpected = unassignedEvidence.OrderBy(index => index).Select(index =>
        {
            var recordingCandidates = FindRecordingCandidates(recordings.Values, evidence[index]);
            return new UnexpectedPlaybackEvidenceItemModel(
                index + 1,
                evidence[index].Title ?? "(untitled)",
                evidence[index].Artist,
                evidence[index].PlayedAtUtc,
                recordingCandidates.Count == 1 ? recordingCandidates[0] : null,
                recordingCandidates,
                recordingCandidates.Count > 1);
        }).ToArray();
        var orderingDifferences = FindOrderingDifferences(planned, assignments);
        var hasAmbiguity = warnings.Contains("mixxx_multiple_history_sessions", StringComparer.Ordinal) ||
                           ambiguousPlannedIds.Count > 0 ||
                           unexpected.Any(item => item.IsAmbiguousMatch);
        var isIncomplete = readModel.IsIncomplete || discardedUnusableEvidence ||
                           warnings.Contains("planned_recording_missing", StringComparer.Ordinal);

        return ApplicationResult<PlaybackEvidenceModel>.Success(new PlaybackEvidenceModel(
            show.Id,
            show.ShowDate,
            readModel.HistorySessionName,
            planned.Length,
            plannedItems.Count(item => item.IsDetected),
            isIncomplete,
            hasAmbiguity,
            warnings.Distinct(StringComparer.Ordinal).ToArray(),
            readModel.Sessions,
            plannedItems,
            unexpected,
            orderingDifferences));
    }

    public async Task<ApplicationResult<ReconciliationModel>> ConfirmReconciliationAsync(
        Guid showId,
        ConfirmReconciliationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!command.OperatorConfirmed)
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Validation(
                    "operatorConfirmed",
                    "Operator confirmation is required before reconciliation can become authoritative."));
        }

        if (command.HasUnresolvedAmbiguity)
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Validation(
                    "hasUnresolvedAmbiguity",
                    "Reconciliation cannot be confirmed while unresolved ambiguity remains."));
        }

        if (command.Items is null)
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Validation("items", "A confirmed playback order is required."));
        }

        var items = command.Items.OrderBy(item => item.Position).ToArray();
        if (!items.Select(item => item.Position).SequenceEqual(Enumerable.Range(1, items.Length)))
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Validation("items", "Confirmed playback positions must be unique and contiguous from 1."));
        }

        var plannedIds = items.Where(item => item.PlannedRecordingId.HasValue)
            .Select(item => item.PlannedRecordingId!.Value)
            .ToArray();
        if (plannedIds.Distinct().Count() != plannedIds.Length)
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Validation("items", "A planned recording may be linked only once."));
        }

        var show = await dbContext.Shows
            .Include(item => item.PlannedRecordings)
            .Include(item => item.Reconciliation)
                .ThenInclude(item => item!.Items)
            .Include(item => item.Reconciliation)
                .ThenInclude(item => item!.ConfirmedPlayback)
            .Include(item => item.BroadcastRecordings)
            .SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);
        if (show is null)
        {
            return ApplicationResult<ReconciliationModel>.Failure(ApplicationError.NotFound("show", showId));
        }

        if (show.Reconciliation?.ConfirmedAtUtc is not null)
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Conflict(
                    "show_already_finalised",
                    "A permanently finalised show cannot be reconciled again.",
                    "showId",
                    showId.ToString()));
        }

        if (show.Reconciliation?.OperatorConfirmedAtUtc is not null)
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Conflict(
                    "reconciliation_already_operator_confirmed",
                    "The playback order has already been confirmed by an operator.",
                    "showId",
                    showId.ToString()));
        }

        var recordingIds = items.Select(item => item.RecordingId).Distinct().ToArray();
        var existingRecordingIds = await dbContext.Recordings
            .Where(item => recordingIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var missingRecordingId = recordingIds.FirstOrDefault(item => !existingRecordingIds.Contains(item));
        if (missingRecordingId != Guid.Empty)
        {
            return ApplicationResult<ReconciliationModel>.Failure(ApplicationError.NotFound("recording", missingRecordingId));
        }

        var plannedLookup = show.PlannedRecordings.ToDictionary(item => item.Id);
        foreach (var item in items.Where(item => item.PlannedRecordingId.HasValue))
        {
            if (!plannedLookup.TryGetValue(item.PlannedRecordingId!.Value, out var plannedItem))
            {
                return ApplicationResult<ReconciliationModel>.Failure(
                    ApplicationError.Conflict(
                        "planned_recording_not_in_show",
                        "A confirmed playback item references a plan entry from another show.",
                        "plannedRecordingId",
                        item.PlannedRecordingId.Value.ToString()));
            }

            if (plannedItem.RecordingId != item.RecordingId)
            {
                return ApplicationResult<ReconciliationModel>.Failure(
                    ApplicationError.Conflict(
                        "planned_recording_mismatch",
                        "The confirmed recording does not match its referenced plan entry.",
                        "plannedRecordingId",
                        item.PlannedRecordingId.Value.ToString()));
            }
        }

        var reconciliation = show.Reconciliation ?? new ReconciliationEntity
        {
            Id = Guid.NewGuid(),
            ShowId = show.Id,
            CreatedAtUtc = clock.UtcNow,
        };
        if (show.Reconciliation is null)
        {
            show.Reconciliation = reconciliation;
            dbContext.Reconciliations.Add(reconciliation);
        }

        var confirmedPlannedIds = plannedIds.ToHashSet();
        reconciliation.Items.Clear();
        foreach (var plannedItem in show.PlannedRecordings)
        {
            reconciliation.Items.Add(new ReconciliationItemEntity
            {
                Id = Guid.NewGuid(),
                PlannedRecordingId = plannedItem.Id,
                Outcome = confirmedPlannedIds.Contains(plannedItem.Id)
                    ? ReconciliationItemOutcome.Broadcast
                    : ReconciliationItemOutcome.NotBroadcast,
            });
        }

        reconciliation.ConfirmedPlayback.Clear();
        foreach (var item in items)
        {
            reconciliation.ConfirmedPlayback.Add(new ConfirmedPlaybackItemEntity
            {
                Id = Guid.NewGuid(),
                RecordingId = item.RecordingId,
                PlannedRecordingId = item.PlannedRecordingId,
                Position = item.Position,
            });
        }

        reconciliation.OperatorConfirmedAtUtc = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult<ReconciliationModel>.Success(Map(reconciliation, show.PlannedRecordings));
    }

    public async Task<ApplicationResult<ReconciliationFinalisationSummary>> FinaliseReconciliationAsync(
        Guid showId,
        CancellationToken cancellationToken = default)
    {
        var show = await dbContext.Shows
            .Include(item => item.PlannedRecordings)
            .Include(item => item.Reconciliation)
                .ThenInclude(item => item!.Items)
            .Include(item => item.Reconciliation)
                .ThenInclude(item => item!.ConfirmedPlayback)
            .Include(item => item.BroadcastRecordings)
            .SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);
        if (show is null)
        {
            return ApplicationResult<ReconciliationFinalisationSummary>.Failure(ApplicationError.NotFound("show", showId));
        }

        if (show.Reconciliation is null || show.Reconciliation.OperatorConfirmedAtUtc is null)
        {
            return ApplicationResult<ReconciliationFinalisationSummary>.Failure(
                ApplicationError.Conflict(
                    "reconciliation_not_operator_confirmed",
                    "Finalisation requires an operator-confirmed reconciliation.",
                    "showId",
                    showId.ToString()));
        }

        var reconciliation = show.Reconciliation;
        var expectedPlayback = reconciliation.ConfirmedPlayback.OrderBy(item => item.Position).ToArray();
        if (reconciliation.ConfirmedAtUtc is not null)
        {
            if (!HasMatchingBroadcastHistory(show.BroadcastRecordings, expectedPlayback))
            {
                return ApplicationResult<ReconciliationFinalisationSummary>.Failure(
                    ApplicationError.Conflict(
                        "historical_correction_required",
                        "The show is already finalised but persisted history no longer matches confirmed playback. Use an explicit audited correction workflow.",
                        "showId",
                        showId.ToString()));
            }

            var usedRepeatExceptions = await GetUsedRepeatExceptionsAsync(showId, expectedPlayback, cancellationToken);
            return ApplicationResult<ReconciliationFinalisationSummary>.Success(new ReconciliationFinalisationSummary(
                showId,
                reconciliation.Id,
                true,
                true,
                reconciliation.ConfirmedAtUtc,
                [],
                BuildDroppedPlannedRecordings(reconciliation, show.PlannedRecordings),
                usedRepeatExceptions));
        }

        var recordingIdsToBroadcast = expectedPlayback.Select(item => item.RecordingId).ToArray();
        var repeatExceptionRecordingIds = await dbContext.RepeatExceptions
            .Where(item => item.ShowId == showId)
            .Select(item => item.RecordingId)
            .ToHashSetAsync(cancellationToken);
        var previouslyBroadcastRecordingIds = await dbContext.BroadcastRecordings
            .AsNoTracking()
            .Where(item => recordingIdsToBroadcast.Contains(item.RecordingId) && item.ShowId != showId)
            .Select(item => item.RecordingId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var repeatedWithinShowRecordingIds = recordingIdsToBroadcast
            .GroupBy(recordingId => recordingId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        var repeatedRecordingId = previouslyBroadcastRecordingIds
            .Concat(repeatedWithinShowRecordingIds)
            .Distinct()
            .FirstOrDefault(recordingId => !repeatExceptionRecordingIds.Contains(recordingId));
        if (repeatedRecordingId != Guid.Empty)
        {
            return ApplicationResult<ReconciliationFinalisationSummary>.Failure(
                ApplicationError.Conflict(
                    "repeat_detected",
                    "The recording would be broadcast more than once and requires an explicit repeat exception.",
                    "recordingId",
                    repeatedRecordingId.ToString()));
        }

        var finalisedAtUtc = clock.UtcNow;
        show.BroadcastRecordings.Clear();
        var addedToHistory = new List<FinalisedBroadcastRecordingModel>(expectedPlayback.Length);
        for (var index = 0; index < expectedPlayback.Length; index++)
        {
            var item = expectedPlayback[index];
            var entity = new BroadcastRecordingEntity
            {
                Id = Guid.NewGuid(),
                ShowId = showId,
                RecordingId = item.RecordingId,
                PlannedRecordingId = item.PlannedRecordingId,
                Position = index + 1,
                BroadcastAtUtc = finalisedAtUtc,
            };
            show.BroadcastRecordings.Add(entity);
            addedToHistory.Add(new FinalisedBroadcastRecordingModel(
                entity.Id,
                entity.RecordingId,
                entity.PlannedRecordingId,
                entity.Position,
                entity.BroadcastAtUtc));
        }

        reconciliation.ConfirmedAtUtc = finalisedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);

        var repeatExceptionsUsed = await GetUsedRepeatExceptionsAsync(showId, expectedPlayback, cancellationToken);
        return ApplicationResult<ReconciliationFinalisationSummary>.Success(new ReconciliationFinalisationSummary(
            showId,
            reconciliation.Id,
            true,
            false,
            finalisedAtUtc,
            addedToHistory,
            BuildDroppedPlannedRecordings(reconciliation, show.PlannedRecordings),
            repeatExceptionsUsed));
    }

    private static IReadOnlyList<MixxxPlaybackCandidateModel> CollapseHistoryNoise(
        IReadOnlyList<MixxxPlaybackCandidateModel> candidates,
        out bool discardedUnusableEvidence)
    {
        discardedUnusableEvidence = false;
        var cleaned = new List<MixxxPlaybackCandidateModel>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var title = string.IsNullOrWhiteSpace(candidate.Title) ? null : candidate.Title.Trim();
            if (title is null)
            {
                discardedUnusableEvidence = true;
                continue;
            }

            var current = candidate with
            {
                Title = title,
                Artist = string.IsNullOrWhiteSpace(candidate.Artist) ? null : candidate.Artist.Trim(),
            };
            var previous = cleaned.LastOrDefault();
            if (previous is not null && SameTrackEvidence(previous, current) &&
                previous.PlayedAtUtc.HasValue && current.PlayedAtUtc.HasValue &&
                Math.Abs((current.PlayedAtUtc.Value - previous.PlayedAtUtc.Value).TotalSeconds) <= 2)
            {
                continue;
            }

            cleaned.Add(current);
        }

        return cleaned;
    }

    private static bool HasMatchingBroadcastHistory(
        IEnumerable<BroadcastRecordingEntity> persisted,
        IReadOnlyList<ConfirmedPlaybackItemEntity> expected)
    {
        var persistedItems = persisted.OrderBy(item => item.Position).ToArray();
        if (persistedItems.Length != expected.Count)
        {
            return false;
        }

        for (var index = 0; index < persistedItems.Length; index++)
        {
            var persistedItem = persistedItems[index];
            var expectedItem = expected[index];
            if (persistedItem.Position != index + 1 ||
                persistedItem.RecordingId != expectedItem.RecordingId ||
                persistedItem.PlannedRecordingId != expectedItem.PlannedRecordingId)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<DroppedPlannedRecordingModel> BuildDroppedPlannedRecordings(
        ReconciliationEntity reconciliation,
        IEnumerable<PlannedRecordingEntity> plannedRecordings)
    {
        var plannedLookup = plannedRecordings.ToDictionary(item => item.Id);
        return reconciliation.Items
            .Where(item => item.Outcome == ReconciliationItemOutcome.NotBroadcast)
            .OrderBy(item => plannedLookup[item.PlannedRecordingId].Position)
            .Select(item => new DroppedPlannedRecordingModel(
                item.PlannedRecordingId,
                plannedLookup[item.PlannedRecordingId].RecordingId,
                plannedLookup[item.PlannedRecordingId].Position))
            .ToArray();
    }

    private async Task<IReadOnlyList<RepeatExceptionModel>> GetUsedRepeatExceptionsAsync(
        Guid showId,
        IReadOnlyList<ConfirmedPlaybackItemEntity> expectedPlayback,
        CancellationToken cancellationToken)
    {
        var recordingIds = expectedPlayback
            .Select(item => item.RecordingId)
            .Distinct()
            .ToHashSet();

        var exceptions = await dbContext.RepeatExceptions
            .AsNoTracking()
            .Where(item => item.ShowId == showId && recordingIds.Contains(item.RecordingId))
            .Select(item => new RepeatExceptionModel(item.Id, item.ShowId, item.RecordingId, item.Reason, item.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
        return exceptions.OrderBy(item => item.CreatedAtUtc).ToArray();
    }

    private static bool SameTrackEvidence(MixxxPlaybackCandidateModel left, MixxxPlaybackCandidateModel right)
        => string.Equals(NormaliseText(left.Title), NormaliseText(right.Title), StringComparison.Ordinal) &&
           string.Equals(NormaliseText(left.Artist), NormaliseText(right.Artist), StringComparison.Ordinal) &&
           string.Equals(NormaliseLocation(left.FileLocation), NormaliseLocation(right.FileLocation), StringComparison.Ordinal);

    private static MatchQuality GetMatchQuality(RecordingEntity recording, MixxxPlaybackCandidateModel candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate.FileLocation) && recording.ExternalIdentifiers.Any(identifier =>
                identifier.Source == LocalFileIdentifierSource &&
                LocationEndsWith(candidate.FileLocation, identifier.Value)))
        {
            return MatchQuality.LocalFileIdentifier;
        }

        if (!string.Equals(NormaliseText(recording.Title), NormaliseText(candidate.Title), StringComparison.Ordinal))
        {
            return MatchQuality.None;
        }

        var artist = NormaliseText(recording.Artist);
        return string.IsNullOrWhiteSpace(artist) ||
               string.Equals(artist, NormaliseText(candidate.Artist), StringComparison.Ordinal)
            ? MatchQuality.NormalisedMetadata
            : MatchQuality.None;
    }

    private static IReadOnlyList<Guid> FindRecordingCandidates(
        IEnumerable<RecordingEntity> recordings,
        MixxxPlaybackCandidateModel candidate)
    {
        var matches = recordings
            .Select(recording => new { recording.Id, Quality = GetMatchQuality(recording, candidate) })
            .Where(item => item.Quality > MatchQuality.None)
            .ToArray();
        if (matches.Length == 0)
        {
            return [];
        }

        var bestQuality = matches.Max(item => item.Quality);
        return matches.Where(item => item.Quality == bestQuality)
            .Select(item => item.Id)
            .Order()
            .ToArray();
    }

    private static IReadOnlyList<OrderDifferenceModel> FindOrderingDifferences(
        IReadOnlyList<PlannedRecordingEntity> planned,
        IReadOnlyDictionary<Guid, int> assignments)
    {
        var expected = planned.Where(item => assignments.ContainsKey(item.Id)).OrderBy(item => item.Position).ToArray();
        var actual = planned.Where(item => assignments.ContainsKey(item.Id)).OrderBy(item => assignments[item.Id]).ToArray();
        var expectedRanks = expected.Select((item, index) => new { item.Id, Rank = index })
            .ToDictionary(item => item.Id, item => item.Rank);
        return actual.Select((item, actualRank) => new { Item = item, ActualRank = actualRank })
            .Where(item => expectedRanks[item.Item.Id] != item.ActualRank)
            .Select(item => new OrderDifferenceModel(
                item.Item.Id,
                item.Item.Position,
                assignments[item.Item.Id] + 1))
            .OrderBy(item => item.DetectedPosition)
            .ToArray();
    }

    private static bool LocationEndsWith(string absoluteOrUriPath, string relativePath)
    {
        var location = NormaliseLocation(absoluteOrUriPath);
        var relative = NormaliseLocation(relativePath).TrimStart('/');
        return location.Equals(relative, StringComparison.Ordinal) ||
               location.EndsWith('/' + relative, StringComparison.Ordinal);
    }

    private static string NormaliseLocation(string? value)
    {
        var normalised = Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile
            ? uri.LocalPath
            : value ?? string.Empty;
        return normalised.Trim().Replace('\\', '/');
    }

    private static string NormaliseText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var filtered = string.Concat(value.Trim().ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character)));
        return string.Join(' ', filtered.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static ReconciliationModel Map(
        ReconciliationEntity reconciliation,
        IEnumerable<PlannedRecordingEntity> plannedRecordings)
    {
        var plannedLookup = plannedRecordings.ToDictionary(item => item.Id);
        return new ReconciliationModel(
            reconciliation.Id,
            reconciliation.ShowId,
            reconciliation.ConfirmedAtUtc is not null,
            reconciliation.OperatorConfirmedAtUtc is not null,
            reconciliation.CreatedAtUtc,
            reconciliation.ConfirmedAtUtc,
            reconciliation.OperatorConfirmedAtUtc,
            reconciliation.Items.Select(item =>
                {
                    var planned = plannedLookup[item.PlannedRecordingId];
                    return new ReconciliationItemModel(
                        item.PlannedRecordingId,
                        planned.RecordingId,
                        planned.Position,
                        item.Outcome);
                })
                .OrderBy(item => item.PlannedPosition)
                .ToArray(),
            reconciliation.ConfirmedPlayback.OrderBy(item => item.Position)
                .Select(item => new ConfirmedPlaybackItemModel(
                    item.RecordingId,
                    item.Position,
                    item.PlannedRecordingId))
                .ToArray());
    }

    private enum MatchQuality
    {
        None,
        NormalisedMetadata,
        LocalFileIdentifier,
    }
}
