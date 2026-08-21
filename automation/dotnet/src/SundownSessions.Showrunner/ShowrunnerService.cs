using Microsoft.EntityFrameworkCore;
using SundownSessions.Showrunner.Persistence;

namespace SundownSessions.Showrunner;

public sealed class ShowrunnerService
{
    private readonly ShowrunnerDbContext dbContext;
    private readonly IShowrunnerClock clock;
    private readonly IMixxxPlaybackEvidenceReader mixxxPlaybackEvidenceReader;

    public ShowrunnerService(
        ShowrunnerDbContext dbContext,
        IShowrunnerClock? clock = null,
        IMixxxPlaybackEvidenceReader? mixxxPlaybackEvidenceReader = null)
    {
        this.dbContext = dbContext;
        this.clock = clock ?? new SystemShowrunnerClock();
        this.mixxxPlaybackEvidenceReader = mixxxPlaybackEvidenceReader ?? new SqliteMixxxPlaybackEvidenceReader();
    }

    public async Task<ApplicationResult<RecordingModel>> CreateRecordingAsync(CreateRecordingCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return ApplicationResult<RecordingModel>.Failure(
                ApplicationError.Validation("title", "A recording title is required."));
        }

        var lengthError = ValidateLength(command.Title, "title", FieldLimits.Title)
            ?? ValidateLength(command.Artist, "artist", FieldLimits.Artist)
            ?? ValidateLength(command.ReleaseTitle, "releaseTitle", FieldLimits.Title)
            ?? ValidateLength(command.Notes, "notes", FieldLimits.Notes);
        if (lengthError is not null)
        {
            return ApplicationResult<RecordingModel>.Failure(lengthError);
        }

        var recording = new RecordingEntity
        {
            Id = Guid.NewGuid(),
            Title = command.Title.Trim(),
            Artist = command.Artist?.Trim(),
            ReleaseTitle = command.ReleaseTitle?.Trim(),
            Notes = command.Notes?.Trim(),
            CreatedAtUtc = clock.UtcNow,
        };

        dbContext.Recordings.Add(recording);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RecordingModel>.Success(Map(recording));
    }

    public async Task<ApplicationResult<RecordingModel>> GetRecordingAsync(Guid recordingId, CancellationToken cancellationToken = default)
    {
        var recording = await dbContext.Recordings
            .Include(item => item.ExternalIdentifiers.OrderBy(identifier => identifier.Source).ThenBy(identifier => identifier.Value))
            .SingleOrDefaultAsync(item => item.Id == recordingId, cancellationToken);

        return recording is null
            ? ApplicationResult<RecordingModel>.Failure(ApplicationError.NotFound("recording", recordingId))
            : ApplicationResult<RecordingModel>.Success(Map(recording));
    }

    public async Task<ApplicationResult<RecordingModel>> AddExternalIdentifierAsync(Guid recordingId, AddExternalIdentifierCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Source))
        {
            return ApplicationResult<RecordingModel>.Failure(
                ApplicationError.Validation("source", "An external identifier source is required."));
        }

        if (string.IsNullOrWhiteSpace(command.Value))
        {
            return ApplicationResult<RecordingModel>.Failure(
                ApplicationError.Validation("value", "An external identifier value is required."));
        }

        var lengthError = ValidateLength(command.Source, "source", FieldLimits.ExternalIdentifierSource)
            ?? ValidateLength(command.Value, "value", FieldLimits.ExternalIdentifierValue);
        if (lengthError is not null)
        {
            return ApplicationResult<RecordingModel>.Failure(lengthError);
        }

        var recording = await dbContext.Recordings
            .Include(item => item.ExternalIdentifiers)
            .SingleOrDefaultAsync(item => item.Id == recordingId, cancellationToken);

        if (recording is null)
        {
            return ApplicationResult<RecordingModel>.Failure(ApplicationError.NotFound("recording", recordingId));
        }

        var source = command.Source.Trim().ToLowerInvariant();
        var value = command.Value.Trim();
        var exists = recording.ExternalIdentifiers.Any(item =>
            string.Equals(item.Source, source, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Value, value, StringComparison.Ordinal));

        if (exists)
        {
            return ApplicationResult<RecordingModel>.Failure(
                ApplicationError.Conflict(
                    "duplicate_external_identifier",
                    "The recording already has that external identifier.",
                    "externalIdentifier",
                    source,
                    value));
        }

        var belongsToAnotherRecording = await dbContext.RecordingExternalIdentifiers.AnyAsync(
            item => item.RecordingId != recordingId && item.Source == source && item.Value == value,
            cancellationToken);
        if (belongsToAnotherRecording)
        {
            return ApplicationResult<RecordingModel>.Failure(
                ApplicationError.Conflict(
                    "external_identifier_in_use",
                    "That external identifier is already associated with another recording.",
                    "externalIdentifier",
                    source,
                    value));
        }

        recording.ExternalIdentifiers.Add(new RecordingExternalIdentifierEntity
        {
            Id = Guid.NewGuid(),
            RecordingId = recording.Id,
            Source = source,
            Value = value,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetRecordingAsync(recording.Id, cancellationToken);
    }

    public async Task<ApplicationResult<BacklogItemModel>> CreateBacklogItemAsync(CreateBacklogItemCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Summary))
        {
            return ApplicationResult<BacklogItemModel>.Failure(
                ApplicationError.Validation("summary", "A backlog item summary is required."));
        }

        var lengthError = ValidateLength(command.Summary, "summary", FieldLimits.Title)
            ?? ValidateLength(command.Notes, "notes", FieldLimits.Notes);
        if (lengthError is not null)
        {
            return ApplicationResult<BacklogItemModel>.Failure(lengthError);
        }

        if (command.RecordingId.HasValue)
        {
            var recordingExists = await dbContext.Recordings.AnyAsync(item => item.Id == command.RecordingId.Value, cancellationToken);
            if (!recordingExists)
            {
                return ApplicationResult<BacklogItemModel>.Failure(ApplicationError.NotFound("recording", command.RecordingId.Value));
            }
        }

        var backlogItem = new BacklogItemEntity
        {
            Id = Guid.NewGuid(),
            RecordingId = command.RecordingId,
            Summary = command.Summary.Trim(),
            Notes = command.Notes?.Trim(),
            CreatedAtUtc = clock.UtcNow,
        };

        dbContext.BacklogItems.Add(backlogItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<BacklogItemModel>.Success(Map(backlogItem));
    }

    public async Task<ApplicationResult<BacklogItemModel>> GetBacklogItemAsync(Guid backlogItemId, CancellationToken cancellationToken = default)
    {
        var backlogItem = await dbContext.BacklogItems.SingleOrDefaultAsync(item => item.Id == backlogItemId, cancellationToken);
        return backlogItem is null
            ? ApplicationResult<BacklogItemModel>.Failure(ApplicationError.NotFound("backlogItem", backlogItemId))
            : ApplicationResult<BacklogItemModel>.Success(Map(backlogItem));
    }

    public async Task<ApplicationResult<ShowModel>> CreateShowAsync(CreateShowCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Slug))
        {
            return ApplicationResult<ShowModel>.Failure(ApplicationError.Validation("slug", "A show slug is required."));
        }

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return ApplicationResult<ShowModel>.Failure(ApplicationError.Validation("title", "A show title is required."));
        }

        var lengthError = ValidateLength(command.Slug, "slug", FieldLimits.ShowSlug)
            ?? ValidateLength(command.Title, "title", FieldLimits.Title);
        if (lengthError is not null)
        {
            return ApplicationResult<ShowModel>.Failure(lengthError);
        }

        var slug = command.Slug.Trim();
        var slugExists = await dbContext.Shows.AnyAsync(item => item.Slug == slug, cancellationToken);
        if (slugExists)
        {
            return ApplicationResult<ShowModel>.Failure(
                ApplicationError.Conflict("duplicate_show_slug", "The show slug already exists.", "slug", slug));
        }

        var show = new ShowEntity
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Title = command.Title.Trim(),
            ShowDate = command.ShowDate,
            CreatedAtUtc = clock.UtcNow,
        };

        dbContext.Shows.Add(show);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<ShowModel>.Success(Map(show));
    }

    public async Task<ApplicationResult<ShowModel>> GetShowAsync(Guid showId, CancellationToken cancellationToken = default)
    {
        var show = await dbContext.Shows
            .Include(item => item.PlannedRecordings.OrderBy(recording => recording.Position))
            .SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);

        return show is null
            ? ApplicationResult<ShowModel>.Failure(ApplicationError.NotFound("show", showId))
            : ApplicationResult<ShowModel>.Success(Map(show));
    }

    public async Task<ApplicationResult<ShowModel>> PlanRecordingAsync(Guid showId, PlanRecordingCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Position < 1)
        {
            return ApplicationResult<ShowModel>.Failure(
                ApplicationError.Validation("position", "A planned recording position must be 1 or greater."));
        }

        var notesLengthError = ValidateLength(command.Notes, "notes", FieldLimits.Notes);
        if (notesLengthError is not null)
        {
            return ApplicationResult<ShowModel>.Failure(notesLengthError);
        }

        var show = await dbContext.Shows
            .Include(item => item.PlannedRecordings)
            .Include(item => item.Reconciliation)
            .SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);

        if (show is null)
        {
            return ApplicationResult<ShowModel>.Failure(ApplicationError.NotFound("show", showId));
        }

        if (show.Reconciliation?.ConfirmedAtUtc is not null)
        {
            return ApplicationResult<ShowModel>.Failure(
                ApplicationError.Conflict(
                    "show_already_finalised",
                    "Recordings cannot be planned after the show's reconciliation has been confirmed.",
                    "showId",
                    showId.ToString()));
        }

        var recordingExists = await dbContext.Recordings.AnyAsync(item => item.Id == command.RecordingId, cancellationToken);
        if (!recordingExists)
        {
            return ApplicationResult<ShowModel>.Failure(ApplicationError.NotFound("recording", command.RecordingId));
        }

        if (show.PlannedRecordings.Any(item => item.Position == command.Position))
        {
            return ApplicationResult<ShowModel>.Failure(
                ApplicationError.Conflict(
                    "planned_position_in_use",
                    "That planned recording position is already in use for the show.",
                    "position",
                    command.Position.ToString()));
        }

        show.PlannedRecordings.Add(new PlannedRecordingEntity
        {
            Id = Guid.NewGuid(),
            ShowId = showId,
            RecordingId = command.RecordingId,
            Position = command.Position,
            Notes = command.Notes?.Trim(),
            CreatedAtUtc = clock.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetShowAsync(showId, cancellationToken);
    }

    public async Task<ApplicationResult<RepeatExceptionModel>> RecordRepeatExceptionAsync(Guid showId, RecordRepeatExceptionCommand command, CancellationToken cancellationToken = default)
    {
        var reasonResult = RepeatExceptionReason.Create(command.Reason);
        if (!reasonResult.IsSuccess)
        {
            return ApplicationResult<RepeatExceptionModel>.Failure(reasonResult.Error!);
        }

        var show = await dbContext.Shows
            .Include(item => item.Reconciliation)
            .SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);
        if (show is null)
        {
            return ApplicationResult<RepeatExceptionModel>.Failure(ApplicationError.NotFound("show", showId));
        }

        if (show.Reconciliation?.ConfirmedAtUtc is not null)
        {
            return ApplicationResult<RepeatExceptionModel>.Failure(
                ApplicationError.Conflict(
                    "show_already_finalised",
                    "A repeat exception cannot be added after the show's reconciliation has been confirmed.",
                    "showId",
                    showId.ToString()));
        }

        var recordingExists = await dbContext.Recordings.AnyAsync(item => item.Id == command.RecordingId, cancellationToken);
        if (!recordingExists)
        {
            return ApplicationResult<RepeatExceptionModel>.Failure(ApplicationError.NotFound("recording", command.RecordingId));
        }

        var duplicate = await dbContext.RepeatExceptions.AnyAsync(
            item => item.ShowId == showId && item.RecordingId == command.RecordingId,
            cancellationToken);

        if (duplicate)
        {
            return ApplicationResult<RepeatExceptionModel>.Failure(
                ApplicationError.Conflict(
                    "repeat_exception_exists",
                    "A repeat exception already exists for that recording on the show.",
                    "recordingId",
                    command.RecordingId.ToString()));
        }

        var repeatException = new RepeatExceptionEntity
        {
            Id = Guid.NewGuid(),
            ShowId = showId,
            RecordingId = command.RecordingId,
            Reason = reasonResult.Value!.Value,
            CreatedAtUtc = clock.UtcNow,
        };

        dbContext.RepeatExceptions.Add(repeatException);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RepeatExceptionModel>.Success(Map(repeatException));
    }

    public async Task<ApplicationResult<PlaybackEvidenceModel>> GetPlaybackEvidenceAsync(Guid showId, CancellationToken cancellationToken = default)
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
            .Where(item => show.PlannedRecordings.Select(plan => plan.RecordingId).Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var evidenceResult = await mixxxPlaybackEvidenceReader.ReadPlaybackEvidenceAsync(cancellationToken);
        if (!evidenceResult.IsSuccess)
        {
            return ApplicationResult<PlaybackEvidenceModel>.Failure(evidenceResult.Error!);
        }

        var warnings = evidenceResult.Value!.Warnings.ToList();
        var evidence = CollapseHistoryNoise(evidenceResult.Value.Candidates);
        var remainingEvidenceIndexes = new HashSet<int>(Enumerable.Range(0, evidence.Count));
        var plannedItems = new List<PlannedPlaybackEvidenceItemModel>(show.PlannedRecordings.Count);
        var orderingDifferences = new List<OrderDifferenceModel>();
        var hasAmbiguity = false;

        foreach (var planned in show.PlannedRecordings.OrderBy(item => item.Position))
        {
            if (!recordings.TryGetValue(planned.RecordingId, out var recording))
            {
                warnings.Add("planned_recording_missing");
                plannedItems.Add(new PlannedPlaybackEvidenceItemModel(
                    planned.Id,
                    planned.RecordingId,
                    planned.Position,
                    "(missing recording)",
                    null,
                    false,
                    null,
                    null,
                    false));
                continue;
            }

            var candidateIndexes = remainingEvidenceIndexes
                .Where(index => MatchesRecording(recording, evidence[index]))
                .OrderBy(index => index)
                .ToArray();

            if (candidateIndexes.Length == 0)
            {
                plannedItems.Add(new PlannedPlaybackEvidenceItemModel(
                    planned.Id,
                    planned.RecordingId,
                    planned.Position,
                    recording.Title,
                    recording.Artist,
                    false,
                    null,
                    null,
                    false));
                continue;
            }

            var selectedIndex = candidateIndexes[0];
            remainingEvidenceIndexes.Remove(selectedIndex);
            var detectedPosition = selectedIndex + 1;
            var ambiguousMatch = candidateIndexes.Length > 1;
            hasAmbiguity |= ambiguousMatch;
            if (detectedPosition != planned.Position)
            {
                orderingDifferences.Add(new OrderDifferenceModel(planned.Id, planned.Position, detectedPosition));
            }

            plannedItems.Add(new PlannedPlaybackEvidenceItemModel(
                planned.Id,
                planned.RecordingId,
                planned.Position,
                recording.Title,
                recording.Artist,
                true,
                detectedPosition,
                evidence[selectedIndex].PlayedAtUtc,
                ambiguousMatch));
        }

        var unexpected = remainingEvidenceIndexes
            .OrderBy(index => index)
            .Select(index => new UnexpectedPlaybackEvidenceItemModel(
                index + 1,
                evidence[index].Title ?? "(untitled)",
                evidence[index].Artist,
                evidence[index].PlayedAtUtc))
            .ToArray();
        var detectedPlannedCount = plannedItems.Count(item => item.IsDetected);

        if (evidence.Count == 0 && warnings.All(warning => !string.Equals(warning, "mixxx_history_empty", StringComparison.Ordinal)))
        {
            warnings.Add("no_usable_mixxx_data");
        }

        return ApplicationResult<PlaybackEvidenceModel>.Success(new PlaybackEvidenceModel(
            showId,
            show.PlannedRecordings.Count,
            detectedPlannedCount,
            evidenceResult.Value.IsIncomplete,
            hasAmbiguity,
            warnings.Distinct(StringComparer.Ordinal).ToArray(),
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
                    "Operator confirmation is required before reconciliation can be confirmed."));
        }

        if (command.HasUnresolvedAmbiguity)
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Validation(
                    "hasUnresolvedAmbiguity",
                    "Reconciliation cannot be confirmed while unresolved ambiguity remains."));
        }

        return await SaveReconciliationAsync(
            showId,
            new SaveReconciliationCommand(true, command.Items),
            cancellationToken);
    }

    public async Task<ApplicationResult<ReconciliationModel>> SaveReconciliationAsync(Guid showId, SaveReconciliationCommand command, CancellationToken cancellationToken = default)
    {
        var show = await dbContext.Shows
            .Include(item => item.PlannedRecordings)
            .Include(item => item.Reconciliation)
                .ThenInclude(item => item!.Items)
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
                    "reconciliation_already_confirmed",
                    "The show's reconciliation has already been confirmed.",
                    "showId",
                    showId.ToString()));
        }

        var duplicatePlannedIds = command.Items
            .GroupBy(item => item.PlannedRecordingId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicatePlannedIds.Length > 0)
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Validation("items", "A planned recording may appear only once in a reconciliation."));
        }

        if (command.Items.Any(item => !Enum.IsDefined(item.Outcome)))
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Validation("items", "Every reconciliation item must have a recognised outcome."));
        }

        var plannedLookup = show.PlannedRecordings.ToDictionary(item => item.Id, item => item);
        foreach (var item in command.Items)
        {
            if (!plannedLookup.ContainsKey(item.PlannedRecordingId))
            {
                return ApplicationResult<ReconciliationModel>.Failure(
                    ApplicationError.Conflict(
                        "planned_recording_not_in_show",
                        "A reconciliation item referenced a planned recording that does not belong to the show.",
                        "plannedRecordingId",
                        item.PlannedRecordingId.ToString()));
            }
        }

        if (command.Confirmed)
        {
            var reconciledPlannedIds = command.Items.Select(item => item.PlannedRecordingId).ToHashSet();
            if (plannedLookup.Keys.Any(plannedRecordingId => !reconciledPlannedIds.Contains(plannedRecordingId)))
            {
                return ApplicationResult<ReconciliationModel>.Failure(
                    ApplicationError.Validation(
                        "items",
                        "A confirmed reconciliation must include every planned recording."));
            }

            if (command.Items.Any(item => item.Outcome == ReconciliationItemOutcome.Pending))
            {
                return ApplicationResult<ReconciliationModel>.Failure(
                    ApplicationError.Validation(
                        "items",
                        "A confirmed reconciliation cannot contain pending outcomes."));
            }

            var recordingIdsToBroadcast = command.Items
                .Where(item => item.Outcome == ReconciliationItemOutcome.Broadcast)
                .Select(item => plannedLookup[item.PlannedRecordingId].RecordingId)
                .ToArray();

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
                return ApplicationResult<ReconciliationModel>.Failure(
                    ApplicationError.Conflict(
                        "repeat_detected",
                        "The recording would be broadcast more than once and requires an explicit repeat exception.",
                        "recordingId",
                        repeatedRecordingId.ToString()));
            }
        }

        var reconciliation = show.Reconciliation ?? new ReconciliationEntity
        {
            Id = Guid.NewGuid(),
            ShowId = showId,
            CreatedAtUtc = clock.UtcNow,
        };

        if (show.Reconciliation is null)
        {
            show.Reconciliation = reconciliation;
            dbContext.Reconciliations.Add(reconciliation);
        }

        reconciliation.Items.Clear();
        foreach (var item in command.Items)
        {
            reconciliation.Items.Add(new ReconciliationItemEntity
            {
                Id = Guid.NewGuid(),
                PlannedRecordingId = item.PlannedRecordingId,
                Outcome = item.Outcome,
            });
        }

        reconciliation.ConfirmedAtUtc = null;

        if (command.Confirmed)
        {
            var confirmedAtUtc = clock.UtcNow;
            reconciliation.ConfirmedAtUtc = confirmedAtUtc;
            show.BroadcastRecordings.Clear();
            foreach (var item in command.Items.Where(item => item.Outcome == ReconciliationItemOutcome.Broadcast))
            {
                var plannedRecording = plannedLookup[item.PlannedRecordingId];
                show.BroadcastRecordings.Add(new BroadcastRecordingEntity
                {
                    Id = Guid.NewGuid(),
                    ShowId = showId,
                    RecordingId = plannedRecording.RecordingId,
                    PlannedRecordingId = plannedRecording.Id,
                    BroadcastAtUtc = confirmedAtUtc,
                });
            }
        }
        else
        {
            show.BroadcastRecordings.Clear();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetReconciliationAsync(showId, cancellationToken);
    }

    public async Task<ApplicationResult<ReconciliationModel>> GetReconciliationAsync(Guid showId, CancellationToken cancellationToken = default)
    {
        var show = await dbContext.Shows
            .Include(item => item.PlannedRecordings)
            .Include(item => item.Reconciliation)
                .ThenInclude(item => item!.Items)
            .SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);

        if (show is null)
        {
            return ApplicationResult<ReconciliationModel>.Failure(ApplicationError.NotFound("show", showId));
        }

        if (show.Reconciliation is null)
        {
            return ApplicationResult<ReconciliationModel>.Failure(ApplicationError.NotFound("reconciliation", showId));
        }

        return ApplicationResult<ReconciliationModel>.Success(Map(show.Reconciliation, show.PlannedRecordings));
    }

    public async Task<ApplicationResult<IReadOnlyList<BroadcastHistoryEntry>>> GetBroadcastHistoryAsync(Guid recordingId, CancellationToken cancellationToken = default)
    {
        var recordingExists = await dbContext.Recordings.AnyAsync(item => item.Id == recordingId, cancellationToken);
        if (!recordingExists)
        {
            return ApplicationResult<IReadOnlyList<BroadcastHistoryEntry>>.Failure(
                ApplicationError.NotFound("recording", recordingId));
        }

        var history = await dbContext.BroadcastRecordings
            .AsNoTracking()
            .Where(item => item.RecordingId == recordingId)
            .Select(item => new BroadcastHistoryEntry(
                item.Id,
                item.ShowId,
                item.Show.Slug,
                item.Show.ShowDate,
                item.BroadcastAtUtc))
            .ToListAsync(cancellationToken);

        return ApplicationResult<IReadOnlyList<BroadcastHistoryEntry>>.Success(history
            .OrderBy(item => item.ShowDate)
            .ThenBy(item => item.BroadcastAtUtc)
            .ToArray());
    }

    private static RecordingModel Map(RecordingEntity recording)
    {
        return new RecordingModel(
            recording.Id,
            recording.Title,
            recording.Artist,
            recording.ReleaseTitle,
            recording.Notes,
            recording.ExternalIdentifiers
                .OrderBy(item => item.Source, StringComparer.Ordinal)
                .ThenBy(item => item.Value, StringComparer.Ordinal)
                .Select(item => new ExternalIdentifierModel(item.Source, item.Value))
                .ToArray(),
            recording.CreatedAtUtc);
    }

    private static BacklogItemModel Map(BacklogItemEntity backlogItem)
        => new(backlogItem.Id, backlogItem.Summary, backlogItem.RecordingId, backlogItem.Notes, backlogItem.CreatedAtUtc);

    private static ShowModel Map(ShowEntity show)
    {
        return new ShowModel(
            show.Id,
            show.Slug,
            show.Title,
            show.ShowDate,
            show.PlannedRecordings
                .OrderBy(item => item.Position)
                .Select(item => new PlannedRecordingModel(item.Id, item.RecordingId, item.Position, item.Notes, item.CreatedAtUtc))
                .ToArray(),
            show.CreatedAtUtc);
    }

    private static RepeatExceptionModel Map(RepeatExceptionEntity repeatException)
        => new(repeatException.Id, repeatException.ShowId, repeatException.RecordingId, repeatException.Reason, repeatException.CreatedAtUtc);

    private static ReconciliationModel Map(ReconciliationEntity reconciliation, IEnumerable<PlannedRecordingEntity> plannedRecordings)
    {
        var plannedLookup = plannedRecordings.ToDictionary(item => item.Id, item => item);
        return new ReconciliationModel(
            reconciliation.Id,
            reconciliation.ShowId,
            reconciliation.ConfirmedAtUtc is not null,
            reconciliation.CreatedAtUtc,
            reconciliation.ConfirmedAtUtc,
            reconciliation.Items
                .Select(item =>
                {
                    var plannedRecording = plannedLookup[item.PlannedRecordingId];
                    return new ReconciliationItemModel(
                        item.PlannedRecordingId,
                        plannedRecording.RecordingId,
                        plannedRecording.Position,
                        item.Outcome);
                })
                .OrderBy(item => item.PlannedPosition)
                .ToArray());
    }

    private static ApplicationError? ValidateLength(string? value, string field, int maximumLength)
    {
        return value?.Trim().Length > maximumLength
            ? ApplicationError.Validation(field, $"{field} cannot exceed {maximumLength} characters.")
            : null;
    }

    private static bool MatchesRecording(RecordingEntity recording, MixxxPlaybackCandidateModel candidate)
    {
        var plannedTitle = NormaliseText(recording.Title);
        var playedTitle = NormaliseText(candidate.Title);
        if (!string.Equals(plannedTitle, playedTitle, StringComparison.Ordinal))
        {
            return false;
        }

        var plannedArtist = NormaliseText(recording.Artist);
        if (string.IsNullOrWhiteSpace(plannedArtist))
        {
            return true;
        }

        return string.Equals(plannedArtist, NormaliseText(candidate.Artist), StringComparison.Ordinal);
    }

    private static IReadOnlyList<MixxxPlaybackCandidateModel> CollapseHistoryNoise(IReadOnlyList<MixxxPlaybackCandidateModel> candidates)
    {
        var cleaned = new List<MixxxPlaybackCandidateModel>(candidates.Count);
        foreach (var candidate in candidates.OrderBy(item => item.PlayedAtUtc ?? DateTimeOffset.MinValue))
        {
            var title = string.IsNullOrWhiteSpace(candidate.Title) ? null : candidate.Title.Trim();
            if (title is null)
            {
                continue;
            }

            var artist = string.IsNullOrWhiteSpace(candidate.Artist) ? null : candidate.Artist.Trim();
            var current = new MixxxPlaybackCandidateModel(title, artist, candidate.PlayedAtUtc);
            var previous = cleaned.LastOrDefault();
            if (previous is not null &&
                string.Equals(NormaliseText(previous.Title), NormaliseText(current.Title), StringComparison.Ordinal) &&
                string.Equals(NormaliseText(previous.Artist), NormaliseText(current.Artist), StringComparison.Ordinal) &&
                previous.PlayedAtUtc.HasValue &&
                current.PlayedAtUtc.HasValue &&
                Math.Abs((current.PlayedAtUtc.Value - previous.PlayedAtUtc.Value).TotalSeconds) <= 2)
            {
                continue;
            }

            cleaned.Add(current);
        }

        return cleaned;
    }

    private static string NormaliseText(string? value)
    {
        var trimmed = value?.Trim().ToLowerInvariant() ?? string.Empty;
        var parts = trimmed
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts);
    }
}
