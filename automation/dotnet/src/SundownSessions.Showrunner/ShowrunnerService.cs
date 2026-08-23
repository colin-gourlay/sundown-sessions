using Microsoft.EntityFrameworkCore;
using SundownSessions.Showrunner.Persistence;

namespace SundownSessions.Showrunner;

public sealed class ShowrunnerService
{
    private readonly ShowrunnerDbContext dbContext;
    private readonly IShowrunnerClock clock;

    public ShowrunnerService(ShowrunnerDbContext dbContext, IShowrunnerClock? clock = null)
    {
        this.dbContext = dbContext;
        this.clock = clock ?? new SystemShowrunnerClock();
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
        var value = CanonicaliseExternalIdentifierValue(source, command.Value);
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

        if (show.Reconciliation is { ConfirmedAtUtc: not null } or { OperatorConfirmedAtUtc: not null })
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

    public async Task<ApplicationResult<ShowPlanRefreshResult>> RefreshShowPlanAsync(
        Guid showId,
        RefreshShowPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Items is null)
        {
            return ApplicationResult<ShowPlanRefreshResult>.Failure(
                ApplicationError.Validation("items", "A show plan refresh requires an explicit ordered item list."));
        }

        var notesLengthError = command.Items
            .Select(item => ValidateLength(item.Notes, "notes", FieldLimits.Notes))
            .FirstOrDefault(error => error is not null);
        if (notesLengthError is not null)
        {
            return ApplicationResult<ShowPlanRefreshResult>.Failure(notesLengthError);
        }

        var show = await dbContext.Shows
            .Include(item => item.PlannedRecordings)
            .Include(item => item.Reconciliation)
            .SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);

        if (show is null)
        {
            return ApplicationResult<ShowPlanRefreshResult>.Failure(ApplicationError.NotFound("show", showId));
        }

        if (show.Reconciliation is { ConfirmedAtUtc: not null } or { OperatorConfirmedAtUtc: not null })
        {
            return ApplicationResult<ShowPlanRefreshResult>.Failure(
                ApplicationError.Conflict(
                    "show_already_finalised",
                    "The show plan cannot be refreshed after the show's reconciliation has been confirmed.",
                    "showId",
                    showId.ToString()));
        }

        var recordingIds = command.Items.Select(item => item.RecordingId).Distinct().ToArray();
        if (recordingIds.Length > 0)
        {
            var existingRecordingIds = await dbContext.Recordings
                .Where(item => recordingIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToHashSetAsync(cancellationToken);
            var missingRecordingId = recordingIds.FirstOrDefault(item => !existingRecordingIds.Contains(item));
            if (missingRecordingId != Guid.Empty)
            {
                return ApplicationResult<ShowPlanRefreshResult>.Failure(ApplicationError.NotFound("recording", missingRecordingId));
            }
        }

        if (show.PlannedRecordings.Count > 0)
        {
            dbContext.RemoveRange(show.PlannedRecordings);
            show.PlannedRecordings.Clear();
        }

        var position = 1;
        foreach (var item in command.Items)
        {
            show.PlannedRecordings.Add(new PlannedRecordingEntity
            {
                Id = Guid.NewGuid(),
                ShowId = showId,
                RecordingId = item.RecordingId,
                Position = position++,
                Notes = item.Notes?.Trim(),
                CreatedAtUtc = clock.UtcNow,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildShowPlanRefreshResultAsync(showId, cancellationToken);
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

    public async Task<ApplicationResult<ReconciliationModel>> SaveReconciliationAsync(Guid showId, SaveReconciliationCommand command, CancellationToken cancellationToken = default)
    {
        var show = await dbContext.Shows
            .Include(item => item.PlannedRecordings)
            .Include(item => item.Reconciliation)
                .ThenInclude(item => item!.Items)
            .Include(item => item.Reconciliation)
                .ThenInclude(item => item!.ConfirmedPlayback)
            .SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);

        if (show is null)
        {
            return ApplicationResult<ReconciliationModel>.Failure(ApplicationError.NotFound("show", showId));
        }

        if (show.Reconciliation is { ConfirmedAtUtc: not null })
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Conflict(
                    "reconciliation_already_confirmed",
                    "The show's reconciliation has already been confirmed.",
                    "showId",
                    showId.ToString()));
        }

        if (show.Reconciliation is { OperatorConfirmedAtUtc: not null })
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Conflict(
                    "reconciliation_already_operator_confirmed",
                    "An operator-confirmed reconciliation cannot be replaced by a draft or finalisation payload.",
                    "showId",
                    showId.ToString()));
        }

        if (command.Confirmed)
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Conflict(
                    "operator_confirmation_required",
                    "Permanent history can only be created by confirming the final playback order and invoking reconciliation finalisation. This operation saves draft reconciliation state only.",
                    "showId",
                    showId.ToString()));
        }

        if (command.Items is null)
        {
            return ApplicationResult<ReconciliationModel>.Failure(
                ApplicationError.Validation("items", "Reconciliation items are required."));
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

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetReconciliationAsync(showId, cancellationToken);
    }

    public async Task<ApplicationResult<ReconciliationModel>> GetReconciliationAsync(Guid showId, CancellationToken cancellationToken = default)
    {
        var show = await dbContext.Shows
            .Include(item => item.PlannedRecordings)
            .Include(item => item.Reconciliation)
                .ThenInclude(item => item!.Items)
            .Include(item => item.Reconciliation)
                .ThenInclude(item => item!.ConfirmedPlayback)
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
                item.BroadcastAtUtc,
                item.PlannedRecordingId,
                item.Position))
            .ToListAsync(cancellationToken);

        return ApplicationResult<IReadOnlyList<BroadcastHistoryEntry>>.Success(history
            .OrderBy(item => item.ShowDate)
            .ThenBy(item => item.BroadcastAtUtc)
            .ThenBy(item => item.Position ?? int.MaxValue)
            .ToArray());
    }

    public async Task<ApplicationResult<RecordingHistoryQueryResult>> QueryRecordingHistoryAsync(
        RecordingHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var hasExternalIdentifierSource = !string.IsNullOrWhiteSpace(query.ExternalIdentifierSource);
        var hasExternalIdentifierValue = !string.IsNullOrWhiteSpace(query.ExternalIdentifierValue);
        if (hasExternalIdentifierSource != hasExternalIdentifierValue)
        {
            return ApplicationResult<RecordingHistoryQueryResult>.Failure(
                ApplicationError.Validation(
                    "query",
                    "Provide both externalIdentifierSource and externalIdentifierValue for exact identifier lookup."));
        }

        if (query.RecordingId is null && !hasExternalIdentifierSource && string.IsNullOrWhiteSpace(query.Title))
        {
            return ApplicationResult<RecordingHistoryQueryResult>.Failure(
                ApplicationError.Validation(
                    "query",
                    "Provide recordingId, an external identifier, or title for history lookup."));
        }

        IReadOnlyList<RecordingEntity> candidates;
        if (query.RecordingId.HasValue)
        {
            candidates = await dbContext.Recordings
                .AsNoTracking()
                .Include(item => item.ExternalIdentifiers)
                .Where(item => item.Id == query.RecordingId.Value)
                .ToArrayAsync(cancellationToken);
        }
        else if (hasExternalIdentifierSource)
        {
            var source = query.ExternalIdentifierSource!.Trim().ToLowerInvariant();
            var value = CanonicaliseExternalIdentifierValue(source, query.ExternalIdentifierValue!);
            candidates = await dbContext.Recordings
                .AsNoTracking()
                .Include(item => item.ExternalIdentifiers)
                .Where(item => item.ExternalIdentifiers.Any(identifier =>
                    identifier.Source == source &&
                    identifier.Value == value))
                .ToArrayAsync(cancellationToken);
        }
        else
        {
            var normalisedTitle = query.Title!.Trim();
            var normalisedArtist = string.IsNullOrWhiteSpace(query.Artist)
                ? null
                : query.Artist.Trim();

            // SQLite's lower()/NOCASE handling is ASCII-only. Identity ambiguity must
            // not depend on the host database's limited Unicode case folding.
            candidates = (await dbContext.Recordings
                    .AsNoTracking()
                    .Include(item => item.ExternalIdentifiers)
                    .ToArrayAsync(cancellationToken))
                .Where(item => string.Equals(item.Title.Trim(), normalisedTitle, StringComparison.OrdinalIgnoreCase) &&
                               (normalisedArtist is null ||
                                string.Equals(item.Artist?.Trim(), normalisedArtist, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }

        var orderedCandidates = candidates
            .OrderBy(item => item.Title)
            .ThenBy(item => item.Artist)
            .ThenBy(item => item.Id)
            .ToArray();

        if (query.RecordingId.HasValue && orderedCandidates.Length == 0)
        {
            return ApplicationResult<RecordingHistoryQueryResult>.Failure(
                ApplicationError.NotFound("recording", query.RecordingId.Value));
        }

        var historyByRecordingId = await GetBroadcastHistoryLookupAsync(
            orderedCandidates.Select(item => item.Id).ToArray(),
            cancellationToken);

        var result = orderedCandidates
            .Select(candidate => new RecordingHistoryCandidateModel(
                candidate.Id,
                candidate.Title,
                candidate.Artist,
                MapExternalIdentifiers(candidate.ExternalIdentifiers),
                historyByRecordingId.GetValueOrDefault(candidate.Id, [])))
            .ToArray();

        return ApplicationResult<RecordingHistoryQueryResult>.Success(
            new RecordingHistoryQueryResult(result.Length > 1, result));
    }

    private async Task<ApplicationResult<ShowPlanRefreshResult>> BuildShowPlanRefreshResultAsync(
        Guid showId,
        CancellationToken cancellationToken)
    {
        var show = await dbContext.Shows
            .AsNoTracking()
            .Include(item => item.PlannedRecordings.OrderBy(recording => recording.Position))
            .SingleOrDefaultAsync(item => item.Id == showId, cancellationToken);
        if (show is null)
        {
            return ApplicationResult<ShowPlanRefreshResult>.Failure(ApplicationError.NotFound("show", showId));
        }

        var recordingIds = show.PlannedRecordings.Select(item => item.RecordingId).Distinct().ToArray();
        var recordings = recordingIds.Length == 0
            ? new Dictionary<Guid, RecordingEntity>()
            : await dbContext.Recordings
                .AsNoTracking()
                .Include(item => item.ExternalIdentifiers)
                .Where(item => recordingIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var historyByRecordingId = await GetBroadcastHistoryLookupAsync(recordingIds, cancellationToken);

        var plannedRecordings = show.PlannedRecordings
            .OrderBy(item => item.Position)
            .Select(item =>
            {
                if (!recordings.TryGetValue(item.RecordingId, out var recording))
                {
                    throw new InvalidOperationException("The planned recording no longer exists in authoritative state.");
                }

                return new PlannedShowRecordingDetailModel(
                    item.Id,
                    item.RecordingId,
                    item.Position,
                    recording.Title,
                    recording.Artist,
                    recording.ReleaseTitle,
                    item.Notes,
                    MapExternalIdentifiers(recording.ExternalIdentifiers),
                    historyByRecordingId.GetValueOrDefault(item.RecordingId, []));
            })
            .ToArray();

        return ApplicationResult<ShowPlanRefreshResult>.Success(new ShowPlanRefreshResult(
            show.Id,
            show.Slug,
            plannedRecordings.Length,
            plannedRecordings));
    }

    private async Task<Dictionary<Guid, IReadOnlyList<BroadcastHistoryEntry>>> GetBroadcastHistoryLookupAsync(
        IReadOnlyCollection<Guid> recordingIds,
        CancellationToken cancellationToken)
    {
        if (recordingIds.Count == 0)
        {
            return [];
        }

        var ids = recordingIds.ToArray();
        var historyRows = await dbContext.BroadcastRecordings
            .AsNoTracking()
            .Where(item => ids.Contains(item.RecordingId))
            .Select(item => new
            {
                item.RecordingId,
                Entry = new BroadcastHistoryEntry(
                    item.Id,
                    item.ShowId,
                    item.Show.Slug,
                    item.Show.ShowDate,
                    item.BroadcastAtUtc,
                    item.PlannedRecordingId,
                    item.Position),
            })
            .ToArrayAsync(cancellationToken);

        return historyRows
            .GroupBy(item => item.RecordingId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<BroadcastHistoryEntry>)group.Select(item => item.Entry)
                    .OrderBy(item => item.ShowDate)
                    .ThenBy(item => item.BroadcastAtUtc)
                    .ThenBy(item => item.Position ?? int.MaxValue)
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
            MapExternalIdentifiers(recording.ExternalIdentifiers),
            recording.CreatedAtUtc);
    }

    private static IReadOnlyList<ExternalIdentifierModel> MapExternalIdentifiers(IEnumerable<RecordingExternalIdentifierEntity> identifiers)
        => identifiers
            .OrderBy(item => item.Source, StringComparer.Ordinal)
            .ThenBy(item => item.Value, StringComparer.Ordinal)
            .Select(item => new ExternalIdentifierModel(item.Source, item.Value))
            .ToArray();

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
            reconciliation.OperatorConfirmedAtUtc is not null,
            reconciliation.CreatedAtUtc,
            reconciliation.ConfirmedAtUtc,
            reconciliation.OperatorConfirmedAtUtc,
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
                .ToArray(),
            reconciliation.ConfirmedPlayback
                .OrderBy(item => item.Position)
                .Select(item => new ConfirmedPlaybackItemModel(
                    item.RecordingId,
                    item.Position,
                    item.PlannedRecordingId))
                .ToArray());
    }

    private static ApplicationError? ValidateLength(string? value, string field, int maximumLength)
    {
        return value?.Trim().Length > maximumLength
            ? ApplicationError.Validation(field, $"{field} cannot exceed {maximumLength} characters.")
            : null;
    }

    private static string CanonicaliseExternalIdentifierValue(string source, string value)
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
}
