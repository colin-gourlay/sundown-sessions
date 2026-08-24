namespace SundownSessions.Showrunner.Tests;

/// <summary>
/// Tests covering the non-Spotify candidate capture and post-show housekeeping workflow
/// that bridges Todoist (as an external capture workspace) with authoritative Showrunner state.
/// Todoist access lives in the agent host; Showrunner owns recording/backlog/history truth.
/// </summary>
public sealed class TodoistBacklogWorkflowTests
{
    [Fact]
    public async Task NonSpotifyCandidateCanBeCreatedAndLinkedToBacklogItem()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);

        var recording = (await service.CreateRecordingAsync(
            new CreateRecordingCommand("Demo Track", "The Band", Notes: "Sent directly by band"))).Value!;

        var backlogItem = (await service.CreateBacklogItemAsync(
            new CreateBacklogItemCommand("The Band – Demo Track", recording.Id))).Value!;

        Assert.Equal(recording.Id, backlogItem.RecordingId);
        Assert.Equal("The Band – Demo Track", backlogItem.Summary);
    }

    [Fact]
    public async Task TodoistSourceReferenceCanBeAttachedToRecording()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);

        var recording = (await service.CreateRecordingAsync(
            new CreateRecordingCommand("Bandcamp Release", "Indie Artist"))).Value!;

        var updated = (await service.AddExternalIdentifierAsync(
            recording.Id,
            new AddExternalIdentifierCommand("todoist", "todoist-task-12345"))).Value!;

        Assert.Single(updated.ExternalIdentifiers);
        Assert.Equal("todoist", updated.ExternalIdentifiers[0].Source);
        Assert.Equal("todoist-task-12345", updated.ExternalIdentifiers[0].Value);
    }

    [Fact]
    public async Task BacklogItemListReturnsAllItemsInCreationOrder()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context, clock);

        await service.CreateBacklogItemAsync(new CreateBacklogItemCommand("Candidate A"));
        clock.Advance(TimeSpan.FromMinutes(1));
        await service.CreateBacklogItemAsync(new CreateBacklogItemCommand("Candidate B"));
        clock.Advance(TimeSpan.FromMinutes(1));
        await service.CreateBacklogItemAsync(new CreateBacklogItemCommand("Candidate C"));

        var result = (await service.ListBacklogItemsAsync()).Value!;

        Assert.Equal(3, result.Items.Count);
        Assert.Equal("Candidate A", result.Items[0].Summary);
        Assert.Equal("Candidate B", result.Items[1].Summary);
        Assert.Equal("Candidate C", result.Items[2].Summary);
    }

    [Fact]
    public async Task BacklogItemListIsEmptyWhenNoCandidatesExist()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);

        var result = (await service.ListBacklogItemsAsync()).Value!;

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task BacklogItemCanExistWithoutLinkedRecording()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);

        var item = (await service.CreateBacklogItemAsync(
            new CreateBacklogItemCommand("Needs investigating", Notes: "Heard on radio"))).Value!;

        Assert.Null(item.RecordingId);
        Assert.Equal("Needs investigating", item.Summary);
        Assert.Equal("Heard on radio", item.Notes);
    }

    [Fact]
    public async Task RecordingHistoryQuerySurfacesAmbiguousCandidatesForOperatorResolution()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);

        await service.CreateRecordingAsync(new CreateRecordingCommand("Demo", "Artist One"));
        await service.CreateRecordingAsync(new CreateRecordingCommand("Demo", "Artist Two"));

        var result = (await service.QueryRecordingHistoryAsync(new RecordingHistoryQuery(Title: "Demo"))).Value!;

        Assert.True(result.IsAmbiguous);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public async Task FinalisedBroadcastIncludesTodoistSourceReferenceForHousekeeping()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));

        Guid recordingId;
        Guid plannedRecordingId;
        Guid showId;

        await using (var context = harness.CreateContext())
        {
            var service = new ShowrunnerService(context, clock);
            var recording = (await service.CreateRecordingAsync(
                new CreateRecordingCommand("Aired Track", "The Band"))).Value!;
            recordingId = recording.Id;

            await service.AddExternalIdentifierAsync(
                recordingId,
                new AddExternalIdentifierCommand("todoist", "todoist-task-99"));

            var show = (await service.CreateShowAsync(
                new CreateShowCommand("show-1", "Show 1", new DateOnly(2026, 8, 19)))).Value!;
            showId = show.Id;

            var plan = (await service.PlanRecordingAsync(
                showId,
                new PlanRecordingCommand(recordingId, 1))).Value!;
            plannedRecordingId = plan.PlannedRecordings.Single().Id;
        }

        await using (var context = harness.CreateContext())
        {
            await ShowrunnerTestOperations.FinaliseShowAsync(
                context,
                showId,
                [new ConfirmedPlaybackItemCommand(recordingId, 1, plannedRecordingId)],
                clock);
        }

        await using (var context = harness.CreateContext())
        {
            var reconciliationService = new ShowReconciliationService(context, new EmptyMixxxPlaybackEvidenceReader(), clock);
            var summary = (await reconciliationService.FinaliseReconciliationAsync(showId)).Value!;

            Assert.True(summary.IsFinalised);
            var aired = Assert.Single(summary.AddedToPermanentHistory);
            Assert.Equal(recordingId, aired.RecordingId);
            var todoistRef = Assert.Single(aired.ExternalIdentifiers, id => id.Source == "todoist");
            Assert.Equal("todoist-task-99", todoistRef.Value);
        }
    }

    [Fact]
    public async Task DroppedCandidatesAreNotCompletedAndRemainAvailable()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));

        Guid droppedRecordingId;
        Guid airedRecordingId;
        Guid showId;

        await using (var context = harness.CreateContext())
        {
            var service = new ShowrunnerService(context, clock);

            var dropped = (await service.CreateRecordingAsync(
                new CreateRecordingCommand("Dropped Track", "Artist A"))).Value!;
            droppedRecordingId = dropped.Id;
            await service.AddExternalIdentifierAsync(droppedRecordingId, new AddExternalIdentifierCommand("todoist", "todoist-task-dropped"));

            var aired = (await service.CreateRecordingAsync(
                new CreateRecordingCommand("Aired Track", "Artist B"))).Value!;
            airedRecordingId = aired.Id;
            await service.AddExternalIdentifierAsync(airedRecordingId, new AddExternalIdentifierCommand("todoist", "todoist-task-aired"));

            var show = (await service.CreateShowAsync(
                new CreateShowCommand("show-drop", "Show Drop", new DateOnly(2026, 8, 19)))).Value!;
            showId = show.Id;

            await service.PlanRecordingAsync(showId, new PlanRecordingCommand(droppedRecordingId, 1));
            var airedPlan = (await service.PlanRecordingAsync(showId, new PlanRecordingCommand(airedRecordingId, 2))).Value!;
            var airedPlannedId = airedPlan.PlannedRecordings.Single(p => p.RecordingId == airedRecordingId).Id;

            await ShowrunnerTestOperations.FinaliseShowAsync(
                context,
                showId,
                [new ConfirmedPlaybackItemCommand(airedRecordingId, 1, airedPlannedId)],
                clock);
        }

        await using (var context = harness.CreateContext())
        {
            var reconciliationService = new ShowReconciliationService(context, new EmptyMixxxPlaybackEvidenceReader(), clock);
            var summary = (await reconciliationService.FinaliseReconciliationAsync(showId)).Value!;

            Assert.True(summary.IsFinalised);

            // Only the aired track should appear in permanent history (with its Todoist reference for housekeeping).
            var aired = Assert.Single(summary.AddedToPermanentHistory);
            Assert.Equal(airedRecordingId, aired.RecordingId);
            Assert.Contains(aired.ExternalIdentifiers, id => id.Source == "todoist" && id.Value == "todoist-task-aired");

            // The dropped track should be identified as dropped, not completed.
            var droppedEntry = Assert.Single(summary.DroppedPlannedRecordings);
            Assert.Equal(droppedRecordingId, droppedEntry.RecordingId);
            Assert.Contains(droppedEntry.ExternalIdentifiers, id => id.Source == "todoist" && id.Value == "todoist-task-dropped");
        }
    }

    [Fact]
    public async Task CreateBacklogItemRejectsEmptySummary()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);

        var result = await service.CreateBacklogItemAsync(new CreateBacklogItemCommand("   "));

        Assert.False(result.IsSuccess);
        Assert.Equal("validation_failed", result.Error!.Code);
    }

    [Fact]
    public async Task CreateBacklogItemRejectsUnknownRecordingId()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);

        var result = await service.CreateBacklogItemAsync(
            new CreateBacklogItemCommand("Candidate", Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal("not_found", result.Error!.Code);
    }
}
