namespace SundownSessions.Showrunner.Tests;

public sealed class TodoistBacklogWorkflowTests
{
    [Fact]
    public async Task CandidateImportCreatesRecordingReferenceAndBacklogAtomically()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context, clock);

        var result = await service.ImportBacklogCandidateAsync(
            new ImportBacklogCandidateCommand(
                "The Band – Demo Track",
                " Todoist ",
                " task-12345 ",
                NewRecording: new CreateRecordingCommand(
                    "Demo Track",
                    "The Band",
                    Notes: "Sent directly by band"),
                Notes: "Captured from the to playlist project"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RecordingCreated);
        Assert.True(result.Value.ExternalIdentifierAdded);
        Assert.True(result.Value.BacklogItemCreated);
        Assert.False(result.Value.IsNoOp);
        Assert.Equal(result.Value.Recording.Id, result.Value.BacklogItem.RecordingId);
        Assert.Equal("The Band – Demo Track", result.Value.BacklogItem.Summary);
        Assert.Equal("Captured from the to playlist project", result.Value.BacklogItem.Notes);
        var sourceReference = Assert.Single(result.Value.Recording.ExternalIdentifiers);
        Assert.Equal("todoist", sourceReference.Source);
        Assert.Equal("task-12345", sourceReference.Value);
    }

    [Fact]
    public async Task CandidateImportCanLinkAnExistingResolvedRecording()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(
            new CreateRecordingCommand("Bandcamp Release", "Indie Artist"))).Value!;

        var result = await service.ImportBacklogCandidateAsync(
            new ImportBacklogCandidateCommand(
                "Indie Artist – Bandcamp Release",
                "todoist",
                "task-existing",
                RecordingId: recording.Id));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.RecordingCreated);
        Assert.True(result.Value.ExternalIdentifierAdded);
        Assert.True(result.Value.BacklogItemCreated);
        Assert.Equal(recording.Id, result.Value.Recording.Id);
        Assert.Contains(
            result.Value.Recording.ExternalIdentifiers,
            item => item.Source == "todoist" && item.Value == "task-existing");
    }

    [Fact]
    public async Task IdenticalCandidateImportRetryIsANoOpWithStableIdentity()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var command = new ImportBacklogCandidateCommand(
            "Retryable demo",
            "todoist",
            "task-retry",
            NewRecording: new CreateRecordingCommand("Retryable demo", "Artist"));

        var first = (await service.ImportBacklogCandidateAsync(command)).Value!;
        var retry = (await service.ImportBacklogCandidateAsync(command)).Value!;

        Assert.True(retry.IsNoOp);
        Assert.False(retry.RecordingCreated);
        Assert.False(retry.ExternalIdentifierAdded);
        Assert.False(retry.BacklogItemCreated);
        Assert.Equal(first.Recording.Id, retry.Recording.Id);
        Assert.Equal(first.BacklogItem.Id, retry.BacklogItem.Id);
        Assert.Single((await service.ListBacklogItemsAsync()).Value!.Items);
    }

    [Fact]
    public async Task ExistingBacklogItemIsReusedWhenAnotherSourceReferenceResolvesToItsRecording()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(
            new CreateRecordingCommand("Already queued", "Artist"))).Value!;
        var backlogItem = (await service.CreateBacklogItemAsync(
            new CreateBacklogItemCommand("Existing candidate", recording.Id))).Value!;

        var result = (await service.ImportBacklogCandidateAsync(
            new ImportBacklogCandidateCommand(
                "Duplicate capture text",
                "todoist",
                "task-existing-backlog",
                RecordingId: recording.Id))).Value!;

        Assert.False(result.RecordingCreated);
        Assert.True(result.ExternalIdentifierAdded);
        Assert.False(result.BacklogItemCreated);
        Assert.False(result.IsNoOp);
        Assert.Equal(backlogItem.Id, result.BacklogItem.Id);
        Assert.Single((await service.ListBacklogItemsAsync()).Value!.Items);
    }

    [Fact]
    public async Task SourceReferenceCannotBeSilentlyReassignedToDifferentRecording()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var first = (await service.CreateRecordingAsync(new CreateRecordingCommand("First", "Artist"))).Value!;
        var second = (await service.CreateRecordingAsync(new CreateRecordingCommand("Second", "Artist"))).Value!;
        await service.ImportBacklogCandidateAsync(
            new ImportBacklogCandidateCommand("First", "todoist", "task-conflict", RecordingId: first.Id));

        var conflict = await service.ImportBacklogCandidateAsync(
            new ImportBacklogCandidateCommand("Second", "todoist", "task-conflict", RecordingId: second.Id));

        Assert.False(conflict.IsSuccess);
        Assert.Equal("external_identifier_in_use", conflict.Error!.Code);
        var secondPersisted = (await service.GetRecordingAsync(second.Id)).Value!;
        Assert.Empty(secondPersisted.ExternalIdentifiers);
        Assert.Single((await service.ListBacklogItemsAsync()).Value!.Items);
    }

    [Fact]
    public async Task CandidateImportRequiresAnExplicitlyResolvedRecordingIdentityBeforeWriting()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);

        var unresolved = await service.ImportBacklogCandidateAsync(
            new ImportBacklogCandidateCommand("Ambiguous demo", "todoist", "task-ambiguous"));
        var bothSelectors = await service.ImportBacklogCandidateAsync(
            new ImportBacklogCandidateCommand(
                "Conflicting identity",
                "todoist",
                "task-both",
                RecordingId: Guid.NewGuid(),
                NewRecording: new CreateRecordingCommand("Conflicting identity", "Artist")));

        Assert.False(unresolved.IsSuccess);
        Assert.Equal("validation_failed", unresolved.Error!.Code);
        Assert.False(bothSelectors.IsSuccess);
        Assert.Equal("validation_failed", bothSelectors.Error!.Code);
        Assert.Empty((await service.ListBacklogItemsAsync()).Value!.Items);
    }

    [Fact]
    public async Task BacklogListUsesStableChronologicalOrder()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context, clock);

        await service.CreateBacklogItemAsync(new CreateBacklogItemCommand("Candidate A"));
        clock.Advance(TimeSpan.FromMinutes(1));
        await service.CreateBacklogItemAsync(new CreateBacklogItemCommand("Candidate B"));

        var result = (await service.ListBacklogItemsAsync()).Value!;

        Assert.Equal(["Candidate A", "Candidate B"], result.Items.Select(item => item.Summary).ToArray());
    }

    [Fact]
    public async Task FinalisationSeparatesAiredAndDroppedTodoistCandidatesForHousekeeping()
    {
        using var harness = new SqliteTestHarness();
        var clock = new TestClock(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero));
        Guid showId;
        Guid airedRecordingId;
        Guid droppedRecordingId;

        await using (var context = harness.CreateContext())
        {
            var service = new ShowrunnerService(context, clock);
            var aired = (await service.ImportBacklogCandidateAsync(
                new ImportBacklogCandidateCommand(
                    "Aired",
                    "todoist",
                    "task-aired",
                    NewRecording: new CreateRecordingCommand("Aired Track", "Artist A")))).Value!;
            var dropped = (await service.ImportBacklogCandidateAsync(
                new ImportBacklogCandidateCommand(
                    "Dropped",
                    "todoist",
                    "task-dropped",
                    NewRecording: new CreateRecordingCommand("Dropped Track", "Artist B")))).Value!;
            airedRecordingId = aired.Recording.Id;
            droppedRecordingId = dropped.Recording.Id;

            var show = (await service.CreateShowAsync(
                new CreateShowCommand("todoist-show", "Todoist Show", new DateOnly(2026, 8, 19)))).Value!;
            showId = show.Id;
            var airedPlan = (await service.PlanRecordingAsync(
                showId,
                new PlanRecordingCommand(airedRecordingId, 1))).Value!;
            await service.PlanRecordingAsync(showId, new PlanRecordingCommand(droppedRecordingId, 2));
            await ShowrunnerTestOperations.FinaliseShowAsync(
                context,
                showId,
                [new ConfirmedPlaybackItemCommand(
                    airedRecordingId,
                    1,
                    airedPlan.PlannedRecordings.Single().Id)],
                clock);
        }

        await using var finalContext = harness.CreateContext();
        var reconciliation = new ShowReconciliationService(
            finalContext,
            new EmptyMixxxPlaybackEvidenceReader(),
            clock);
        var result = (await reconciliation.FinaliseReconciliationAsync(showId)).Value!;

        var airedResult = Assert.Single(result.AddedToPermanentHistory);
        Assert.Equal(airedRecordingId, airedResult.RecordingId);
        Assert.Contains(airedResult.ExternalIdentifiers, item => item.Source == "todoist" && item.Value == "task-aired");
        var droppedResult = Assert.Single(result.DroppedPlannedRecordings);
        Assert.Equal(droppedRecordingId, droppedResult.RecordingId);
        Assert.Contains(droppedResult.ExternalIdentifiers, item => item.Source == "todoist" && item.Value == "task-dropped");
    }
}
