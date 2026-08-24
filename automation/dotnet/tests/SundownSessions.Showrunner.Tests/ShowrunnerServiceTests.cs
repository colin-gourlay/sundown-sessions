using SundownSessions.Showrunner.Persistence;

namespace SundownSessions.Showrunner.Tests;

public sealed class ShowrunnerServiceTests
{
    [Fact]
    public async Task RecordingsCanExistWithoutSpotifyIdentifiers()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context, clock);

        var created = await service.CreateRecordingAsync(new CreateRecordingCommand("Live at Sundown", "The Quartet"));

        Assert.True(created.IsSuccess);
        Assert.NotNull(created.Value);
        Assert.Empty(created.Value!.ExternalIdentifiers);
        Assert.Equal("Live at Sundown", created.Value.Title);
    }

    [Fact]
    public async Task PlannedRecordingsDoNotAppearInBroadcastHistory()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context, clock);

        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Unbroadcast Plan", null))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("show-1", "Show 1", new DateOnly(2026, 8, 19)))).Value!;
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1));

        var history = await service.GetBroadcastHistoryAsync(recording.Id);

        Assert.True(history.IsSuccess);
        Assert.Empty(history.Value!);
    }

    [Fact]
    public async Task ConfirmedReconciledBroadcastCanBePersistedAndReadBack()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));
        Guid showId;
        Guid recordingId;
        Guid plannedRecordingId;

        await using (var setupContext = harness.CreateContext())
        {
            var service = new ShowrunnerService(setupContext, clock);
            var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Broadcasted", null))).Value!;
            var show = (await service.CreateShowAsync(new CreateShowCommand("show-2", "Show 2", new DateOnly(2026, 8, 20)))).Value!;
            var plannedShow = (await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
            showId = show.Id;
            recordingId = recording.Id;
            plannedRecordingId = plannedShow.PlannedRecordings.Single().Id;

            await ShowrunnerTestOperations.FinaliseShowAsync(
                setupContext,
                showId,
                [new ConfirmedPlaybackItemCommand(recordingId, 1, plannedRecordingId)],
                clock);
        }

        await using var verificationContext = harness.CreateContext();
        var verificationService = new ShowrunnerService(verificationContext, clock);
        var persistedReconciliation = await verificationService.GetReconciliationAsync(showId);
        var history = await verificationService.GetBroadcastHistoryAsync(recordingId);

        Assert.True(persistedReconciliation.IsSuccess);
        Assert.True(persistedReconciliation.Value!.IsConfirmed);
        Assert.True(history.IsSuccess);
        Assert.Single(history.Value!);
        Assert.Equal(showId, history.Value![0].ShowId);
    }

    [Fact]
    public async Task RepeatDetectionDistinguishesBroadcastFromMerePlan()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context, clock);

        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Repeat Candidate", null))).Value!;
        var plannedOnlyShow = (await service.CreateShowAsync(new CreateShowCommand("planned-only", "Planned Only", new DateOnly(2026, 8, 21)))).Value!;
        await service.PlanRecordingAsync(plannedOnlyShow.Id, new PlanRecordingCommand(recording.Id, 1));

        var firstBroadcastShow = (await service.CreateShowAsync(new CreateShowCommand("broadcast-one", "Broadcast One", new DateOnly(2026, 8, 22)))).Value!;
        var firstBroadcastPlan = (await service.PlanRecordingAsync(firstBroadcastShow.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        var firstPlannedRecordingId = firstBroadcastPlan.PlannedRecordings.Single().Id;
        await ShowrunnerTestOperations.FinaliseShowAsync(
            context,
            firstBroadcastShow.Id,
            [new ConfirmedPlaybackItemCommand(recording.Id, 1, firstPlannedRecordingId)],
            clock);

        var secondBroadcastShow = (await service.CreateShowAsync(new CreateShowCommand("broadcast-two", "Broadcast Two", new DateOnly(2026, 8, 23)))).Value!;
        var secondBroadcastPlan = (await service.PlanRecordingAsync(secondBroadcastShow.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        var secondPlannedRecordingId = secondBroadcastPlan.PlannedRecordings.Single().Id;

        var reconciliation = new ShowReconciliationService(context, new EmptyMixxxPlaybackEvidenceReader(), clock);
        await reconciliation.ConfirmReconciliationAsync(
            secondBroadcastShow.Id,
            new ConfirmReconciliationCommand(
                true,
                false,
                [new ConfirmedPlaybackItemCommand(recording.Id, 1, secondPlannedRecordingId)]));
        var repeatAttempt = await reconciliation.FinaliseReconciliationAsync(secondBroadcastShow.Id);

        Assert.False(repeatAttempt.IsSuccess);
        Assert.Equal("repeat_detected", repeatAttempt.Error!.Code);
    }

    [Fact]
    public async Task ExplicitRepeatExceptionRecordsItsReason()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context, clock);

        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Allowed Repeat", null))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("repeat-exception", "Repeat Exception", new DateOnly(2026, 8, 24)))).Value!;

        var result = await service.RecordRepeatExceptionAsync(show.Id, new RecordRepeatExceptionCommand(recording.Id, "Editorial reprise for anniversary programme"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Editorial reprise for anniversary programme", result.Value!.Reason);
    }

    [Fact]
    public async Task SpotifyExternalIdentifiersAreCanonicalisedForAssociationAndHistoryLookup()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Canonical Spotify", "Artist"))).Value!;
        var otherRecording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Different Recording", "Artist"))).Value!;

        var associated = await service.AddExternalIdentifierAsync(
            recording.Id,
            new AddExternalIdentifierCommand("spotify", "spotify:track:abc123"));
        var duplicate = await service.AddExternalIdentifierAsync(
            otherRecording.Id,
            new AddExternalIdentifierCommand("spotify", "https://open.spotify.com/track/abc123?si=test"));
        var invalid = await service.AddExternalIdentifierAsync(
            otherRecording.Id,
            new AddExternalIdentifierCommand("spotify", "spotify:track:"));
        var history = await service.QueryRecordingHistoryAsync(new RecordingHistoryQuery(
            ExternalIdentifierSource: "spotify",
            ExternalIdentifierValue: "https://open.spotify.com/track/abc123"));
        var invalidHistory = await service.QueryRecordingHistoryAsync(new RecordingHistoryQuery(
            ExternalIdentifierSource: "spotify",
            ExternalIdentifierValue: "https://open.spotify.com/track/?si=test"));
        var conflictingLookup = await service.QueryRecordingHistoryAsync(new RecordingHistoryQuery(
            RecordingId: recording.Id,
            ExternalIdentifierSource: "spotify",
            ExternalIdentifierValue: "abc123"));

        Assert.True(associated.IsSuccess);
        Assert.Equal("abc123", associated.Value!.ExternalIdentifiers.Single().Value);
        Assert.False(duplicate.IsSuccess);
        Assert.Equal("external_identifier_in_use", duplicate.Error!.Code);
        Assert.False(invalid.IsSuccess);
        Assert.Equal("validation_failed", invalid.Error!.Code);
        Assert.True(history.IsSuccess);
        Assert.False(history.Value!.IsAmbiguous);
        var candidate = Assert.Single(history.Value.Candidates);
        Assert.Equal(recording.Id, candidate.RecordingId);
        Assert.Equal("abc123", candidate.ExternalIdentifiers.Single().Value);
        Assert.False(invalidHistory.IsSuccess);
        Assert.Equal("validation_failed", invalidHistory.Error!.Code);
        Assert.False(conflictingLookup.IsSuccess);
        Assert.Equal("validation_failed", conflictingLookup.Error!.Code);
    }

    [Fact]
    public async Task ShowPlanRefreshReplacesMutableOrderAndReturnsAuthoritativeRepeatHistory()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context, clock);
        var first = (await service.CreateRecordingAsync(new CreateRecordingCommand("First Refresh", "Artist"))).Value!;
        var second = (await service.CreateRecordingAsync(new CreateRecordingCommand("Second Refresh", "Artist"))).Value!;
        await service.AddExternalIdentifierAsync(first.Id, new AddExternalIdentifierCommand("spotify", "spotify:track:first-refresh"));

        var priorShow = (await service.CreateShowAsync(new CreateShowCommand("refresh-prior", "Refresh Prior", new DateOnly(2026, 8, 18)))).Value!;
        var priorPlan = (await service.PlanRecordingAsync(priorShow.Id, new PlanRecordingCommand(first.Id, 1))).Value!;
        await ShowrunnerTestOperations.FinaliseShowAsync(
            context,
            priorShow.Id,
            [new ConfirmedPlaybackItemCommand(first.Id, 1, priorPlan.PlannedRecordings.Single().Id)],
            clock);

        var show = (await service.CreateShowAsync(new CreateShowCommand("refresh-target", "Refresh Target", new DateOnly(2026, 8, 19)))).Value!;
        var originalPlan = (await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(first.Id, 1))).Value!;

        var refreshed = await service.RefreshShowPlanAsync(
            show.Id,
            new RefreshShowPlanCommand(
                [
                    new RefreshShowPlanItemCommand(second.Id),
                    new RefreshShowPlanItemCommand(first.Id, "Closing track"),
                ]));
        var retried = await service.RefreshShowPlanAsync(
            show.Id,
            new RefreshShowPlanCommand(
                [
                    new RefreshShowPlanItemCommand(second.Id),
                    new RefreshShowPlanItemCommand(first.Id, "Closing track"),
                ]));
        var persisted = await service.GetShowAsync(show.Id);

        Assert.True(refreshed.IsSuccess);
        Assert.Equal(2, refreshed.Value!.PlannedCount);
        Assert.Equal(second.Id, refreshed.Value.PlannedRecordings[0].RecordingId);
        Assert.Equal(first.Id, refreshed.Value.PlannedRecordings[1].RecordingId);
        Assert.Equal("Closing track", refreshed.Value.PlannedRecordings[1].Notes);
        Assert.Single(refreshed.Value.PlannedRecordings[1].BroadcastHistory);
        Assert.Equal("first-refresh", refreshed.Value.PlannedRecordings[1].ExternalIdentifiers.Single().Value);
        Assert.True(retried.IsSuccess);
        Assert.Equal(
            refreshed.Value.PlannedRecordings.Select(item => item.PlannedRecordingId),
            retried.Value!.PlannedRecordings.Select(item => item.PlannedRecordingId));
        Assert.True(persisted.IsSuccess);
        Assert.Equal([second.Id, first.Id], persisted.Value!.PlannedRecordings.Select(item => item.RecordingId).ToArray());
        Assert.Contains(persisted.Value.PlannedRecordings, item => item.Id == originalPlan.PlannedRecordings.Single().Id);
    }

    [Fact]
    public async Task ShowPlanRefreshRefusesToInvalidateDraftReconciliationReferences()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Draft plan", "Artist"))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("draft-plan", "Draft plan", new DateOnly(2026, 8, 20)))).Value!;
        var plan = (await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        await service.SaveReconciliationAsync(
            show.Id,
            new SaveReconciliationCommand(
                false,
                [new ReconciliationItemCommand(plan.PlannedRecordings.Single().Id, ReconciliationItemOutcome.NotBroadcast)]));

        var result = await service.RefreshShowPlanAsync(show.Id, new RefreshShowPlanCommand([]));
        var appended = await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 2));

        Assert.False(result.IsSuccess);
        Assert.Equal("show_reconciliation_started", result.Error!.Code);
        Assert.False(appended.IsSuccess);
        Assert.Equal("show_reconciliation_started", appended.Error!.Code);
        Assert.Single((await service.GetShowAsync(show.Id)).Value!.PlannedRecordings);
    }

    [Fact]
    public async Task ShowPlanRefreshCanShrinkAPlanWithoutTransientPositionConflicts()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recordings = new[]
        {
            (await service.CreateRecordingAsync(new CreateRecordingCommand("First", null))).Value!,
            (await service.CreateRecordingAsync(new CreateRecordingCommand("Second", null))).Value!,
            (await service.CreateRecordingAsync(new CreateRecordingCommand("Third", null))).Value!,
        };
        var show = (await service.CreateShowAsync(new CreateShowCommand("shrink-plan", "Shrink plan", new DateOnly(2026, 8, 20)))).Value!;
        for (var index = 0; index < recordings.Length; index++)
        {
            await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recordings[index].Id, index + 1));
        }

        var result = await service.RefreshShowPlanAsync(
            show.Id,
            new RefreshShowPlanCommand([new RefreshShowPlanItemCommand(recordings[2].Id)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(recordings[2].Id, result.Value!.PlannedRecordings.Single().RecordingId);
        Assert.Equal(1, result.Value.PlannedRecordings.Single().Position);
    }

    [Fact]
    public async Task FinalisationSummaryIncludesExternalIdentifiersForHousekeeping()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var dropped = (await service.CreateRecordingAsync(new CreateRecordingCommand("Dropped Spotify", "Artist"))).Value!;
        var played = (await service.CreateRecordingAsync(new CreateRecordingCommand("Played Spotify", "Artist"))).Value!;
        await service.AddExternalIdentifierAsync(dropped.Id, new AddExternalIdentifierCommand("spotify", "spotify:track:dropped-id"));
        await service.AddExternalIdentifierAsync(played.Id, new AddExternalIdentifierCommand("spotify", "spotify:track:played-id"));
        var show = (await service.CreateShowAsync(new CreateShowCommand("spotify-housekeeping", "Spotify housekeeping", new DateOnly(2026, 8, 26)))).Value!;
        var firstPlan = (await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(dropped.Id, 1))).Value!;
        var secondPlan = (await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(played.Id, 2))).Value!;

        var reconciliation = new ShowReconciliationService(context, new EmptyMixxxPlaybackEvidenceReader());
        await reconciliation.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(
                true,
                false,
                [new ConfirmedPlaybackItemCommand(played.Id, 1, secondPlan.PlannedRecordings.Single(item => item.RecordingId == played.Id).Id)]));
        var finalised = await reconciliation.FinaliseReconciliationAsync(show.Id);
        var retried = await reconciliation.FinaliseReconciliationAsync(show.Id);

        Assert.True(finalised.IsSuccess);
        var broadcast = Assert.Single(finalised.Value!.AddedToPermanentHistory);
        Assert.Equal("Played Spotify", broadcast.Title);
        Assert.Equal("played-id", broadcast.ExternalIdentifiers.Single().Value);
        var droppedSummary = Assert.Single(finalised.Value.DroppedPlannedRecordings);
        Assert.Equal(firstPlan.PlannedRecordings.Single().Id, droppedSummary.PlannedRecordingId);
        Assert.Equal("Dropped Spotify", droppedSummary.Title);
        Assert.Equal("dropped-id", droppedSummary.ExternalIdentifiers.Single().Value);
        Assert.True(retried.IsSuccess);
        Assert.True(retried.Value!.IsNoOp);
        Assert.Equal("played-id", retried.Value.AddedToPermanentHistory.Single().ExternalIdentifiers.Single().Value);
    }

    [Fact]
    public async Task AuthoritativeStateSurvivesIndependentOfExternalServiceAvailability()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));
        Guid recordingId;
        Guid showId;

        await using (var initialContext = harness.CreateContext())
        {
            var service = new ShowrunnerService(initialContext, clock);
            var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Offline Safe", null))).Value!;
            var show = (await service.CreateShowAsync(new CreateShowCommand("offline-safe", "Offline Safe", new DateOnly(2026, 8, 25)))).Value!;
            await service.AddExternalIdentifierAsync(recording.Id, new AddExternalIdentifierCommand("spotify", "spotify:track:123"));
            await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1));
            recordingId = recording.Id;
            showId = show.Id;
        }

        await using var reopenedContext = harness.CreateContext();
        var reopenedService = new ShowrunnerService(reopenedContext, clock);
        var recordingResult = await reopenedService.GetRecordingAsync(recordingId);
        var showResult = await reopenedService.GetShowAsync(showId);

        Assert.True(recordingResult.IsSuccess);
        Assert.True(showResult.IsSuccess);
        Assert.Single(recordingResult.Value!.ExternalIdentifiers);
        Assert.Single(showResult.Value!.PlannedRecordings);
    }

    [Fact]
    public async Task ApplicationOperationsReturnStructuredFailures()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context, clock);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Structured Failure", null))).Value!;

        await service.AddExternalIdentifierAsync(recording.Id, new AddExternalIdentifierCommand("spotify", "spotify:track:abc"));
        var duplicateIdentifier = await service.AddExternalIdentifierAsync(recording.Id, new AddExternalIdentifierCommand("spotify", "spotify:track:abc"));
        var repeatReason = RepeatExceptionReason.Create("   ");

        Assert.False(duplicateIdentifier.IsSuccess);
        Assert.Equal("duplicate_external_identifier", duplicateIdentifier.Error!.Code);
        Assert.Equal("spotify", duplicateIdentifier.Error.Details["externalIdentifier"][0]);
        Assert.False(repeatReason.IsSuccess);
        Assert.Equal("validation_failed", repeatReason.Error!.Code);
        Assert.Equal("A repeat exception requires an explicit reason.", repeatReason.Error.Details["reason"][0]);
    }

    [Fact]
    public async Task LegacyConfirmedReconciliationCannotBypassOperatorConfirmationAndFinalisation()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var firstRecording = (await service.CreateRecordingAsync(new CreateRecordingCommand("First", null))).Value!;
        var secondRecording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Second", null))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("complete", "Complete", new DateOnly(2026, 8, 26)))).Value!;
        var firstPlan = await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(firstRecording.Id, 1));
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(secondRecording.Id, 2));

        var result = await service.SaveReconciliationAsync(
            show.Id,
            new SaveReconciliationCommand(
                true,
                [new ReconciliationItemCommand(firstPlan.Value!.PlannedRecordings[0].Id, ReconciliationItemOutcome.Broadcast)]));

        Assert.False(result.IsSuccess);
        Assert.Equal("operator_confirmation_required", result.Error!.Code);
        Assert.Empty((await service.GetBroadcastHistoryAsync(firstRecording.Id)).Value!);
    }

    [Fact]
    public async Task DraftReconciliationAllowsPendingButRejectsUnknownOutcomes()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Outcome", null))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("outcomes", "Outcomes", new DateOnly(2026, 8, 27)))).Value!;
        var plan = (await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        var plannedRecordingId = plan.PlannedRecordings.Single().Id;

        var pending = await service.SaveReconciliationAsync(
            show.Id,
            new SaveReconciliationCommand(false, [new ReconciliationItemCommand(plannedRecordingId, ReconciliationItemOutcome.Pending)]));
        var unknown = await service.SaveReconciliationAsync(
            show.Id,
            new SaveReconciliationCommand(false, [new ReconciliationItemCommand(plannedRecordingId, (ReconciliationItemOutcome)99)]));

        Assert.True(pending.IsSuccess);
        Assert.Equal(ReconciliationItemOutcome.Pending, pending.Value!.Items.Single().Outcome);
        Assert.False(unknown.IsSuccess);
        Assert.Contains("recognised outcome", unknown.Error!.Details["items"][0]);
    }

    [Fact]
    public async Task DuplicateBroadcastWithinOneShowRequiresRepeatException()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Twice", null))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("twice", "Twice", new DateOnly(2026, 8, 28)))).Value!;
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1));
        var plan = (await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 2))).Value!;
        var items = plan.PlannedRecordings
            .Select(item => new ConfirmedPlaybackItemCommand(item.RecordingId, item.Position, item.Id))
            .ToArray();

        var reconciliation = new ShowReconciliationService(context, new EmptyMixxxPlaybackEvidenceReader());
        await reconciliation.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(true, false, items));
        var blocked = await reconciliation.FinaliseReconciliationAsync(show.Id);
        await service.RecordRepeatExceptionAsync(
            show.Id,
            new RecordRepeatExceptionCommand(recording.Id, "The recording intentionally opens and closes the programme."));
        var allowed = await reconciliation.FinaliseReconciliationAsync(show.Id);

        Assert.False(blocked.IsSuccess);
        Assert.Equal("repeat_detected", blocked.Error!.Code);
        Assert.True(allowed.IsSuccess);
        var history = await service.GetBroadcastHistoryAsync(recording.Id);
        Assert.Equal(2, history.Value!.Count);
    }

    [Fact]
    public async Task FailedFinalisationDoesNotCreateHistoryOrMarkReconciliationFinalised()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Protected draft", null))).Value!;

        var priorShow = (await service.CreateShowAsync(new CreateShowCommand("prior", "Prior", new DateOnly(2026, 8, 1)))).Value!;
        var priorPlan = (await service.PlanRecordingAsync(priorShow.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        await ShowrunnerTestOperations.FinaliseShowAsync(
            context,
            priorShow.Id,
            [new ConfirmedPlaybackItemCommand(recording.Id, 1, priorPlan.PlannedRecordings.Single().Id)]);

        var currentShow = (await service.CreateShowAsync(new CreateShowCommand("current", "Current", new DateOnly(2026, 8, 29)))).Value!;
        var currentPlan = (await service.PlanRecordingAsync(currentShow.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        var plannedRecordingId = currentPlan.PlannedRecordings.Single().Id;
        await service.SaveReconciliationAsync(
            currentShow.Id,
            new SaveReconciliationCommand(
                false,
                [new ReconciliationItemCommand(plannedRecordingId, ReconciliationItemOutcome.NotBroadcast)]));

        var reconciliation = new ShowReconciliationService(context, new EmptyMixxxPlaybackEvidenceReader());
        await reconciliation.ConfirmReconciliationAsync(
            currentShow.Id,
            new ConfirmReconciliationCommand(
                true,
                false,
                [new ConfirmedPlaybackItemCommand(recording.Id, 1, plannedRecordingId)]));
        var failed = await reconciliation.FinaliseReconciliationAsync(currentShow.Id);
        var persistedDraft = await service.GetReconciliationAsync(currentShow.Id);

        Assert.False(failed.IsSuccess);
        Assert.False(persistedDraft.Value!.IsConfirmed);
        Assert.True(persistedDraft.Value.IsOperatorConfirmed);
        Assert.DoesNotContain(
            (await service.GetBroadcastHistoryAsync(recording.Id)).Value!,
            item => item.ShowId == currentShow.Id);
    }

    [Fact]
    public async Task ConfirmedShowCannotBeModified()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Final", null))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("final", "Final", new DateOnly(2026, 8, 30)))).Value!;
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1));
        await ShowrunnerTestOperations.FinaliseShowAsync(context, show.Id, []);

        var addedPlan = await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 2));
        var repeatException = await service.RecordRepeatExceptionAsync(
            show.Id,
            new RecordRepeatExceptionCommand(recording.Id, "Too late"));

        Assert.Equal("show_already_finalised", addedPlan.Error!.Code);
        Assert.Equal("show_already_finalised", repeatException.Error!.Code);
    }

    [Fact]
    public async Task ExternalIdentifiersAreCanonicalAndCannotIdentifyTwoRecordings()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var first = (await service.CreateRecordingAsync(new CreateRecordingCommand("First identity", null))).Value!;
        var second = (await service.CreateRecordingAsync(new CreateRecordingCommand("Second identity", null))).Value!;

        var associated = await service.AddExternalIdentifierAsync(
            first.Id,
            new AddExternalIdentifierCommand("  Spotify ", "spotify:track:unique"));
        var duplicate = await service.AddExternalIdentifierAsync(
            second.Id,
            new AddExternalIdentifierCommand("SPOTIFY", "spotify:track:unique"));

        Assert.Equal("spotify", associated.Value!.ExternalIdentifiers.Single().Source);
        Assert.Equal("external_identifier_in_use", duplicate.Error!.Code);
    }

    [Fact]
    public async Task PlaybackEvidenceReportsAnExactPlanWithoutDiscrepancies()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var setup = new ShowrunnerService(context);
        var first = (await setup.CreateRecordingAsync(new CreateRecordingCommand("First", "Artist"))).Value!;
        var second = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Second", "Artist"))).Value!;
        var show = (await setup.CreateShowAsync(new CreateShowCommand("mixxx-exact", "Mixxx exact", new DateOnly(2026, 8, 28)))).Value!;
        await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(first.Id, 1));
        await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(second.Id, 2));
        var service = new ShowReconciliationService(
            context,
            new StubMixxxPlaybackEvidenceReader(new MixxxPlaybackReadModel(
                false,
                [],
                [
                    new MixxxPlaybackCandidateModel("First", "Artist", null),
                    new MixxxPlaybackCandidateModel("Second", "Artist", null),
                ])));

        var result = await service.GetPlaybackEvidenceAsync(show.Id);

        Assert.Equal(2, result.Value!.DetectedPlannedCount);
        Assert.Empty(result.Value.Unexpected);
        Assert.Empty(result.Value.OrderingDifferences);
        Assert.False(result.Value.HasAmbiguousMatches);
        Assert.All(result.Value.Planned, item => Assert.True(item.IsDetected));
    }

    [Fact]
    public async Task PlaybackEvidenceComparesPlanWithDetectedPlaybackAndHighlightsDifferences()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var recordingA = new CreateRecordingCommand("Track A", "Artist");
        var recordingB = new CreateRecordingCommand("Track B", "Artist");
        var recordingC = new CreateRecordingCommand("Track C", "Artist");
        var setup = new ShowrunnerService(context);
        var a = (await setup.CreateRecordingAsync(recordingA)).Value!;
        var b = (await setup.CreateRecordingAsync(recordingB)).Value!;
        var c = (await setup.CreateRecordingAsync(recordingC)).Value!;
        var show = (await setup.CreateShowAsync(new CreateShowCommand("mixxx-evidence", "Mixxx evidence", new DateOnly(2026, 8, 28)))).Value!;
        await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(a.Id, 1));
        await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(b.Id, 2));
        await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(c.Id, 3));

        var unexpected = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Unexpected", "Guest"))).Value!;
        var evidence = new StubMixxxPlaybackEvidenceReader(new MixxxPlaybackReadModel(
            false,
            [],
            [
                new MixxxPlaybackCandidateModel("Track B", "Artist", new DateTimeOffset(2026, 8, 28, 19, 00, 00, TimeSpan.Zero)),
                new MixxxPlaybackCandidateModel("Track B", "Artist", new DateTimeOffset(2026, 8, 28, 19, 00, 01, TimeSpan.Zero)),
                new MixxxPlaybackCandidateModel("Track A", "Artist", new DateTimeOffset(2026, 8, 28, 19, 04, 00, TimeSpan.Zero)),
                new MixxxPlaybackCandidateModel("Unexpected", "Guest", new DateTimeOffset(2026, 8, 28, 19, 08, 00, TimeSpan.Zero)),
            ]));
        var service = new ShowReconciliationService(context, evidence);

        var result = await service.GetPlaybackEvidenceAsync(show.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.PlannedCount);
        Assert.Equal(2, result.Value.DetectedPlannedCount);
        Assert.Single(result.Value.Unexpected);
        Assert.Equal("Unexpected", result.Value.Unexpected[0].Title);
        Assert.Equal(unexpected.Id, result.Value.Unexpected[0].RecordingId);
        Assert.Contains(result.Value.Planned, item => item.Title == "Track C" && !item.IsDetected);
        Assert.Contains(result.Value.OrderingDifferences, item => item.PlannedPosition == 1 && item.DetectedPosition == 2);
        Assert.Contains(result.Value.OrderingDifferences, item => item.PlannedPosition == 2 && item.DetectedPosition == 1);
        Assert.Equal(show.ShowDate, evidence.RequestedDate);
    }

    [Fact]
    public async Task PlaybackEvidenceUsesLocalFileIdentityAndSurfacesTextAmbiguity()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var setup = new ShowrunnerService(context);
        var first = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Same", "Artist"))).Value!;
        var second = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Same", "Artist"))).Value!;
        await setup.AddExternalIdentifierAsync(first.Id, new AddExternalIdentifierCommand("local-file", "album/first.flac"));
        await setup.AddExternalIdentifierAsync(second.Id, new AddExternalIdentifierCommand("local-file", "album/second.flac"));
        var show = (await setup.CreateShowAsync(new CreateShowCommand("mixxx-identity", "Mixxx identity", new DateOnly(2026, 8, 29)))).Value!;
        await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(first.Id, 1));
        await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(second.Id, 2));

        var ambiguousService = new ShowReconciliationService(
            context,
            new StubMixxxPlaybackEvidenceReader(new MixxxPlaybackReadModel(
                false,
                [],
                [new MixxxPlaybackCandidateModel("Same", "Artist", null)])));
        var resolvedService = new ShowReconciliationService(
            context,
            new StubMixxxPlaybackEvidenceReader(new MixxxPlaybackReadModel(
                false,
                [],
                [new MixxxPlaybackCandidateModel("Same", "Artist", null, "file:///music/album/second.flac")])));

        var ambiguous = await ambiguousService.GetPlaybackEvidenceAsync(show.Id);
        var resolved = await resolvedService.GetPlaybackEvidenceAsync(show.Id);

        Assert.True(ambiguous.Value!.HasAmbiguousMatches);
        Assert.All(ambiguous.Value.Planned, item => Assert.True(item.IsAmbiguousMatch));
        Assert.Equal(2, ambiguous.Value.Unexpected.Single().RecordingCandidates.Count);
        Assert.False(resolved.Value!.HasAmbiguousMatches);
        Assert.Equal(second.Id, resolved.Value.Planned.Single(item => item.IsDetected).RecordingId);
    }

    [Fact]
    public async Task PlaybackEvidenceSurfacesIncompleteMixxxState()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var setup = new ShowrunnerService(context);
        var recording = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Track", "Artist"))).Value!;
        var show = (await setup.CreateShowAsync(new CreateShowCommand("mixxx-incomplete", "Mixxx incomplete", new DateOnly(2026, 8, 29)))).Value!;
        await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1));
        var service = new ShowReconciliationService(
            context,
            new StubMixxxPlaybackEvidenceReader(
                new MixxxPlaybackReadModel(true, ["mixxx_schema_unsupported"], [])));

        var result = await service.GetPlaybackEvidenceAsync(show.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsIncompleteEvidence);
        Assert.Contains("mixxx_schema_unsupported", result.Value.Warnings);
        Assert.Equal(0, result.Value.DetectedPlannedCount);
    }

    [Fact]
    public async Task OperatorConfirmationPersistsCorrectedOrderWithoutCreatingBroadcastHistory()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var setup = new ShowrunnerService(context);
        var dropped = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Dropped", "Artist"))).Value!;
        var played = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Played", "Artist"))).Value!;
        var unexpected = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Unexpected", "Guest"))).Value!;
        var show = (await setup.CreateShowAsync(new CreateShowCommand("mixxx-confirm", "Mixxx confirm", new DateOnly(2026, 8, 30)))).Value!;
        var firstPlan = (await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(dropped.Id, 1))).Value!;
        var secondPlan = (await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(played.Id, 2))).Value!;
        var firstPlanId = firstPlan.PlannedRecordings.Single().Id;
        var secondPlanId = secondPlan.PlannedRecordings.Single(item => item.RecordingId == played.Id).Id;
        var service = new ShowReconciliationService(context, new StubMixxxPlaybackEvidenceReader());

        var result = await service.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(
                true,
                false,
                [
                    new ConfirmedPlaybackItemCommand(played.Id, 1, secondPlanId),
                    new ConfirmedPlaybackItemCommand(unexpected.Id, 2),
                ]));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsOperatorConfirmed);
        Assert.False(result.Value.IsConfirmed);
        Assert.Null(result.Value.ConfirmedAtUtc);
        Assert.Equal([played.Id, unexpected.Id], result.Value.ConfirmedPlayback.Select(item => item.RecordingId));
        Assert.Equal(ReconciliationItemOutcome.NotBroadcast, result.Value.Items.Single(item => item.PlannedRecordingId == firstPlanId).Outcome);
        Assert.Equal(ReconciliationItemOutcome.Broadcast, result.Value.Items.Single(item => item.PlannedRecordingId == secondPlanId).Outcome);
        Assert.Empty((await setup.GetBroadcastHistoryAsync(played.Id)).Value!);
        Assert.Empty((await setup.GetBroadcastHistoryAsync(unexpected.Id)).Value!);
    }

    [Fact]
    public async Task OperatorConfirmationRejectsAmbiguityInvalidOrderAndMismatchedPlanLinks()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var setup = new ShowrunnerService(context);
        var first = (await setup.CreateRecordingAsync(new CreateRecordingCommand("First", "Artist"))).Value!;
        var second = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Second", "Artist"))).Value!;
        var show = (await setup.CreateShowAsync(new CreateShowCommand("mixxx-invalid", "Mixxx invalid", new DateOnly(2026, 8, 30)))).Value!;
        var plan = (await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(first.Id, 1))).Value!;
        var plannedId = plan.PlannedRecordings.Single().Id;
        var service = new ShowReconciliationService(context, new StubMixxxPlaybackEvidenceReader());

        var ambiguous = await service.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(true, true, []));
        var invalidOrder = await service.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(
                true,
                false,
                [new ConfirmedPlaybackItemCommand(first.Id, 2, plannedId)]));
        var mismatched = await service.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(
                true,
                false,
                [new ConfirmedPlaybackItemCommand(second.Id, 1, plannedId)]));

        Assert.Equal("validation_failed", ambiguous.Error!.Code);
        Assert.Equal("validation_failed", invalidOrder.Error!.Code);
        Assert.Equal("planned_recording_mismatch", mismatched.Error!.Code);
    }

    [Fact]
    public async Task FinalisationPersistsConfirmedPlaybackDroppedPlansAndUnexpectedAiredTracks()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 31, 20, 0, 0, TimeSpan.Zero));
        await using var context = harness.CreateContext();
        var setup = new ShowrunnerService(context, clock);
        var dropped = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Dropped", "Artist"))).Value!;
        var played = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Played", "Artist"))).Value!;
        var unexpected = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Unexpected", "Guest"))).Value!;
        var show = (await setup.CreateShowAsync(new CreateShowCommand("finalise-mixed", "Finalise mixed", new DateOnly(2026, 8, 31)))).Value!;
        var firstPlan = (await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(dropped.Id, 1))).Value!;
        var secondPlan = (await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(played.Id, 2))).Value!;
        var firstPlanId = firstPlan.PlannedRecordings.Single().Id;
        var secondPlanId = secondPlan.PlannedRecordings.Single(item => item.RecordingId == played.Id).Id;
        var reconciliation = new ShowReconciliationService(context, new StubMixxxPlaybackEvidenceReader(), clock);
        await reconciliation.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(
                true,
                false,
                [
                    new ConfirmedPlaybackItemCommand(played.Id, 1, secondPlanId),
                    new ConfirmedPlaybackItemCommand(unexpected.Id, 2),
                ]));

        var result = await reconciliation.FinaliseReconciliationAsync(show.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsFinalised);
        Assert.False(result.Value.IsNoOp);
        Assert.Equal(2, result.Value.AddedToPermanentHistory.Count);
        Assert.Equal([played.Id, unexpected.Id], result.Value.AddedToPermanentHistory.OrderBy(item => item.Position).Select(item => item.RecordingId));
        var droppedPlanned = Assert.Single(result.Value.DroppedPlannedRecordings);
        Assert.Equal(firstPlanId, droppedPlanned.PlannedRecordingId);
        Assert.Equal(dropped.Id, droppedPlanned.RecordingId);
        var playedHistory = await setup.GetBroadcastHistoryAsync(played.Id);
        var unexpectedHistory = await setup.GetBroadcastHistoryAsync(unexpected.Id);
        var droppedHistory = await setup.GetBroadcastHistoryAsync(dropped.Id);
        Assert.Single(playedHistory.Value!);
        Assert.Equal(secondPlanId, playedHistory.Value![0].PlannedRecordingId);
        Assert.Equal(1, playedHistory.Value![0].Position);
        Assert.Single(unexpectedHistory.Value!);
        Assert.Null(unexpectedHistory.Value![0].PlannedRecordingId);
        Assert.Equal(2, unexpectedHistory.Value![0].Position);
        Assert.Empty(droppedHistory.Value!);
    }

    [Fact]
    public async Task FinalisationRequiresOperatorConfirmedReconciliation()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var setup = new ShowrunnerService(context);
        var recording = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Unconfirmed", null))).Value!;
        var show = (await setup.CreateShowAsync(new CreateShowCommand("finalise-unconfirmed", "Finalise unconfirmed", new DateOnly(2026, 8, 31)))).Value!;
        await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1));
        var reconciliation = new ShowReconciliationService(context, new StubMixxxPlaybackEvidenceReader());

        var result = await reconciliation.FinaliseReconciliationAsync(show.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("reconciliation_not_operator_confirmed", result.Error!.Code);
    }

    [Fact]
    public async Task FinalisationDoesNotConsultPlaybackEvidenceOrExternalIntegrations()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var setup = new ShowrunnerService(context);
        var recording = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Offline finalisation", null))).Value!;
        var show = (await setup.CreateShowAsync(new CreateShowCommand("offline-finalisation", "Offline finalisation", new DateOnly(2026, 8, 31)))).Value!;
        var plan = (await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        var reconciliation = new ShowReconciliationService(context, new ThrowingMixxxPlaybackEvidenceReader());
        await reconciliation.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(
                true,
                false,
                [new ConfirmedPlaybackItemCommand(recording.Id, 1, plan.PlannedRecordings.Single().Id)]));

        var result = await reconciliation.FinaliseReconciliationAsync(show.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.AddedToPermanentHistory);
    }

    [Fact]
    public async Task FinalisationIsIdempotentAndDoesNotDuplicateBroadcastHistory()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 9, 1, 19, 0, 0, TimeSpan.Zero));
        await using var context = harness.CreateContext();
        var setup = new ShowrunnerService(context, clock);
        var recording = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Idempotent", null))).Value!;
        var show = (await setup.CreateShowAsync(new CreateShowCommand("finalise-idempotent", "Finalise idempotent", new DateOnly(2026, 9, 1)))).Value!;
        var plan = (await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        var plannedId = plan.PlannedRecordings.Single().Id;
        var reconciliation = new ShowReconciliationService(context, new StubMixxxPlaybackEvidenceReader(), clock);
        await reconciliation.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(true, false, [new ConfirmedPlaybackItemCommand(recording.Id, 1, plannedId)]));
        var first = await reconciliation.FinaliseReconciliationAsync(show.Id);
        clock.Advance(TimeSpan.FromMinutes(10));

        var second = await reconciliation.FinaliseReconciliationAsync(show.Id);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.False(first.Value!.IsNoOp);
        Assert.True(second.Value!.IsNoOp);
        Assert.Equal(
            first.Value.AddedToPermanentHistory.Single().BroadcastRecordingId,
            second.Value.AddedToPermanentHistory.Single().BroadcastRecordingId);
        Assert.Equal(first.Value.FinalisedAtUtc, second.Value.FinalisedAtUtc);
        var history = await setup.GetBroadcastHistoryAsync(recording.Id);
        Assert.Single(history.Value!);
    }

    [Fact]
    public async Task FinalisationSummaryIncludesUsedRepeatExceptions()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var setup = new ShowrunnerService(context);
        var recording = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Repeat", "Artist"))).Value!;
        var priorShow = (await setup.CreateShowAsync(new CreateShowCommand("repeat-prior", "Repeat prior", new DateOnly(2026, 9, 2)))).Value!;
        var priorPlan = (await setup.PlanRecordingAsync(priorShow.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        await ShowrunnerTestOperations.FinaliseShowAsync(
            context,
            priorShow.Id,
            [new ConfirmedPlaybackItemCommand(recording.Id, 1, priorPlan.PlannedRecordings.Single().Id)]);
        var show = (await setup.CreateShowAsync(new CreateShowCommand("repeat-current", "Repeat current", new DateOnly(2026, 9, 3)))).Value!;
        var plan = (await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        var plannedId = plan.PlannedRecordings.Single().Id;
        const string reason = "Editorial reprise";
        await setup.RecordRepeatExceptionAsync(show.Id, new RecordRepeatExceptionCommand(recording.Id, reason));
        var reconciliation = new ShowReconciliationService(context, new StubMixxxPlaybackEvidenceReader());
        await reconciliation.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(true, false, [new ConfirmedPlaybackItemCommand(recording.Id, 1, plannedId)]));

        var result = await reconciliation.FinaliseReconciliationAsync(show.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.RepeatExceptionsUsed);
        Assert.Equal(reason, result.Value.RepeatExceptionsUsed[0].Reason);
    }

    [Fact]
    public async Task FinalisationSummaryExcludesUnusedRepeatExceptions()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var setup = new ShowrunnerService(context);
        var recording = (await setup.CreateRecordingAsync(new CreateRecordingCommand("First play", "Artist"))).Value!;
        var show = (await setup.CreateShowAsync(new CreateShowCommand("unused-repeat", "Unused repeat", new DateOnly(2026, 9, 3)))).Value!;
        var plan = (await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        await setup.RecordRepeatExceptionAsync(show.Id, new RecordRepeatExceptionCommand(recording.Id, "Recorded in advance"));
        var reconciliation = new ShowReconciliationService(context, new EmptyMixxxPlaybackEvidenceReader());
        await reconciliation.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(
                true,
                false,
                [new ConfirmedPlaybackItemCommand(recording.Id, 1, plan.PlannedRecordings.Single().Id)]));

        var result = await reconciliation.FinaliseReconciliationAsync(show.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.RepeatExceptionsUsed);
    }

    [Fact]
    public async Task RecordingHistoryLookupSupportsExactAndAmbiguousQueries()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var first = (await service.CreateRecordingAsync(new CreateRecordingCommand("Shared title", "Shared artist"))).Value!;
        var second = (await service.CreateRecordingAsync(new CreateRecordingCommand("Shared title", "Shared artist"))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("history-query", "History query", new DateOnly(2026, 9, 2)))).Value!;
        var plan = (await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(first.Id, 1))).Value!;
        await ShowrunnerTestOperations.FinaliseShowAsync(
            context,
            show.Id,
            [new ConfirmedPlaybackItemCommand(first.Id, 1, plan.PlannedRecordings.Single().Id)]);

        var exact = await service.QueryRecordingHistoryAsync(new RecordingHistoryQuery(RecordingId: first.Id));
        var ambiguous = await service.QueryRecordingHistoryAsync(new RecordingHistoryQuery(Title: "Shared title", Artist: "Shared artist"));

        Assert.True(exact.IsSuccess);
        Assert.False(exact.Value!.IsAmbiguous);
        Assert.Single(exact.Value.Candidates);
        Assert.Single(exact.Value.Candidates[0].BroadcastHistory);
        Assert.True(ambiguous.IsSuccess);
        Assert.True(ambiguous.Value!.IsAmbiguous);
        Assert.Equal(2, ambiguous.Value.Candidates.Count);
        Assert.Single(ambiguous.Value.Candidates.Single(item => item.RecordingId == first.Id).BroadcastHistory);
        Assert.Empty(ambiguous.Value.Candidates.Single(item => item.RecordingId == second.Id).BroadcastHistory);
    }

    [Fact]
    public async Task RecordingHistoryTextLookupUsesUnicodeCaseInsensitiveIdentity()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Élan", "Björk"))).Value!;

        var result = await service.QueryRecordingHistoryAsync(
            new RecordingHistoryQuery(Title: " éLAN ", Artist: " BJÖRK "));

        Assert.True(result.IsSuccess);
        Assert.Equal(recording.Id, Assert.Single(result.Value!.Candidates).RecordingId);
    }

    [Fact]
    public async Task FinalisedReconciliationRejectsOrdinaryCorrectionAttempts()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var setup = new ShowrunnerService(context);
        var original = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Original", null))).Value!;
        var correction = (await setup.CreateRecordingAsync(new CreateRecordingCommand("Correction", null))).Value!;
        var show = (await setup.CreateShowAsync(new CreateShowCommand("correction-rejected", "Correction rejected", new DateOnly(2026, 9, 2)))).Value!;
        var plan = (await setup.PlanRecordingAsync(show.Id, new PlanRecordingCommand(original.Id, 1))).Value!;
        var plannedId = plan.PlannedRecordings.Single().Id;
        var reconciliation = new ShowReconciliationService(context, new StubMixxxPlaybackEvidenceReader());
        await reconciliation.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(true, false, [new ConfirmedPlaybackItemCommand(original.Id, 1, plannedId)]));
        await reconciliation.FinaliseReconciliationAsync(show.Id);

        var correctionAttempt = await reconciliation.ConfirmReconciliationAsync(
            show.Id,
            new ConfirmReconciliationCommand(true, false, [new ConfirmedPlaybackItemCommand(correction.Id, 1)]));

        Assert.False(correctionAttempt.IsSuccess);
        Assert.Equal("show_already_finalised", correctionAttempt.Error!.Code);
    }

    [Fact]
    public async Task BoundaryValidatesPersistenceLimitsAndMissingHistoryRecording()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);

        var tooLong = await service.CreateRecordingAsync(new CreateRecordingCommand(new string('x', 257), null));
        var missingHistory = await service.GetBroadcastHistoryAsync(Guid.NewGuid());

        Assert.Equal("validation_failed", tooLong.Error!.Code);
        Assert.Equal("not_found", missingHistory.Error!.Code);
    }

    private sealed class StubMixxxPlaybackEvidenceReader(MixxxPlaybackReadModel? model = null) : IMixxxPlaybackEvidenceReader
    {
        public DateOnly? RequestedDate { get; private set; }

        public Task<ApplicationResult<MixxxPlaybackReadModel>> ReadPlaybackEvidenceAsync(
            DateOnly showDate,
            CancellationToken cancellationToken = default)
        {
            RequestedDate = showDate;
            return Task.FromResult(ApplicationResult<MixxxPlaybackReadModel>.Success(
                model ?? new MixxxPlaybackReadModel(false, [], [])));
        }
    }

    private sealed class ThrowingMixxxPlaybackEvidenceReader : IMixxxPlaybackEvidenceReader
    {
        public Task<ApplicationResult<MixxxPlaybackReadModel>> ReadPlaybackEvidenceAsync(
            DateOnly showDate,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Finalisation must not read Mixxx evidence.");
    }
}
