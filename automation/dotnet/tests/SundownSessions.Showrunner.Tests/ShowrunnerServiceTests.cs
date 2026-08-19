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

        Assert.Empty(history);
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

            var reconciliation = await service.SaveReconciliationAsync(
                showId,
                new SaveReconciliationCommand(true, [new ReconciliationItemCommand(plannedRecordingId, ReconciliationItemOutcome.Broadcast)]));

            Assert.True(reconciliation.IsSuccess);
            Assert.True(reconciliation.Value!.IsConfirmed);
        }

        await using var verificationContext = harness.CreateContext();
        var verificationService = new ShowrunnerService(verificationContext, clock);
        var persistedReconciliation = await verificationService.GetReconciliationAsync(showId);
        var history = await verificationService.GetBroadcastHistoryAsync(recordingId);

        Assert.True(persistedReconciliation.IsSuccess);
        Assert.True(persistedReconciliation.Value!.IsConfirmed);
        Assert.Single(history);
        Assert.Equal(showId, history[0].ShowId);
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
        var firstBroadcast = await service.SaveReconciliationAsync(
            firstBroadcastShow.Id,
            new SaveReconciliationCommand(true, [new ReconciliationItemCommand(firstPlannedRecordingId, ReconciliationItemOutcome.Broadcast)]));

        Assert.True(firstBroadcast.IsSuccess);

        var secondBroadcastShow = (await service.CreateShowAsync(new CreateShowCommand("broadcast-two", "Broadcast Two", new DateOnly(2026, 8, 23)))).Value!;
        var secondBroadcastPlan = (await service.PlanRecordingAsync(secondBroadcastShow.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        var secondPlannedRecordingId = secondBroadcastPlan.PlannedRecordings.Single().Id;

        var repeatAttempt = await service.SaveReconciliationAsync(
            secondBroadcastShow.Id,
            new SaveReconciliationCommand(true, [new ReconciliationItemCommand(secondPlannedRecordingId, ReconciliationItemOutcome.Broadcast)]));

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
}
