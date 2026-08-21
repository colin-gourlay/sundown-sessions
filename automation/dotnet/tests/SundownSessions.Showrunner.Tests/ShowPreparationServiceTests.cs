using SundownSessions.Showrunner.Persistence;

namespace SundownSessions.Showrunner.Tests;

public sealed class ShowPreparationServiceTests
{
    [Fact]
    public async Task PrepareShowMatchesByMetadataIdentifierAndBuildsNumberedFolder()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        var sourcePath = files.CreateFlac("library/track-a.flac");
        var metadata = new StubFlacMetadataReader()
            .Add(sourcePath, new FlacMetadata("Track A", "Artist A", "Release A", TimeSpan.FromMinutes(4), new Dictionary<string, string>
            {
                ["SPOTIFY_TRACK_ID"] = "abc123",
            }));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Track A", "Artist A"))).Value!;
        await service.AddExternalIdentifierAsync(recording.Id, new AddExternalIdentifierCommand("spotify", "spotify:track:abc123"));
        var show = (await service.CreateShowAsync(new CreateShowCommand("show-a", "Show A", new DateOnly(2026, 8, 21)))).Value!;
        var plan = (await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;

        var preparation = new ShowPreparationService(
            context,
            new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot, TimeSpan.FromMinutes(60)),
            metadata);
        var result = await preparation.PrepareShowAsync(show.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.UnresolvedTracks);
        Assert.Empty(result.Value.RepeatConflicts);
        Assert.NotNull(result.Value.BroadcastFolder);
        Assert.Single(result.Value.BroadcastFolder!.CopiedFiles);
        Assert.EndsWith("01 - Artist A - Track A.flac", result.Value.BroadcastFolder.CopiedFiles[0], StringComparison.Ordinal);
        Assert.True(File.Exists(result.Value.BroadcastFolder.CopiedFiles[0]));
        Assert.Equal(TimeSpan.FromMinutes(4), result.Value.Timing.TotalMusicDuration);
        Assert.Equal(TimeSpan.FromMinutes(56), result.Value.Timing.RemainingDuration);
        Assert.Equal(plan.PlannedRecordings.Single().Id, result.Value.MatchedTracks.Single().PlannedRecordingId);
    }

    [Fact]
    public async Task PrepareShowReturnsAmbiguousAndMissingMatchesWithoutGuessing()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        var firstPath = files.CreateFlac("library/one.flac");
        var secondPath = files.CreateFlac("library/two.flac");
        var metadata = new StubFlacMetadataReader()
            .Add(firstPath, new FlacMetadata("Same Title", "Same Artist", "Album One", TimeSpan.FromMinutes(3), new Dictionary<string, string>()))
            .Add(secondPath, new FlacMetadata("Same Title", "Same Artist", "Album Two", TimeSpan.FromMinutes(5), new Dictionary<string, string>()));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var ambiguousRecording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Same Title", "Same Artist"))).Value!;
        var missingRecording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Missing", "Nobody"))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("show-b", "Show B", new DateOnly(2026, 8, 21)))).Value!;
        var showWithOne = (await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(ambiguousRecording.Id, 1))).Value!;
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(missingRecording.Id, 2));

        var preparation = new ShowPreparationService(
            context,
            new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot),
            metadata);
        var result = await preparation.PrepareShowAsync(show.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.MatchedTracks);
        Assert.Null(result.Value.BroadcastFolder);
        Assert.Equal(2, result.Value.UnresolvedTracks.Count);
        Assert.Equal("ambiguous_match", result.Value.UnresolvedTracks[0].Kind);
        Assert.Equal(2, result.Value.UnresolvedTracks[0].Candidates.Count);
        Assert.Equal(showWithOne.PlannedRecordings.Single().Id, result.Value.UnresolvedTracks[0].PlannedRecordingId);
        Assert.Equal("missing_file", result.Value.UnresolvedTracks[1].Kind);
    }

    [Fact]
    public async Task PrepareShowDetectsOnlyConfirmedBroadcastRepeatsAndSupportsException()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        var sourcePath = files.CreateFlac("library/repeat.flac");
        var metadata = new StubFlacMetadataReader()
            .Add(sourcePath, new FlacMetadata("Repeat", "Artist", "Album", TimeSpan.FromMinutes(4), new Dictionary<string, string>()));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Repeat", "Artist"))).Value!;

        var plannedOnly = (await service.CreateShowAsync(new CreateShowCommand("planned-only-2", "Planned", new DateOnly(2026, 8, 20)))).Value!;
        await service.PlanRecordingAsync(plannedOnly.Id, new PlanRecordingCommand(recording.Id, 1));

        var prior = (await service.CreateShowAsync(new CreateShowCommand("prior-show", "Prior", new DateOnly(2026, 8, 21)))).Value!;
        var priorPlan = (await service.PlanRecordingAsync(prior.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        await service.SaveReconciliationAsync(
            prior.Id,
            new SaveReconciliationCommand(true, [new ReconciliationItemCommand(priorPlan.PlannedRecordings.Single().Id, ReconciliationItemOutcome.Broadcast)]));

        var target = (await service.CreateShowAsync(new CreateShowCommand("target-show", "Target", new DateOnly(2026, 8, 22)))).Value!;
        await service.PlanRecordingAsync(target.Id, new PlanRecordingCommand(recording.Id, 1));

        var preparation = new ShowPreparationService(
            context,
            new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot),
            metadata);
        var blocked = await preparation.PrepareShowAsync(target.Id);
        await service.RecordRepeatExceptionAsync(target.Id, new RecordRepeatExceptionCommand(recording.Id, "Deliberate repeat for retrospective"));
        var allowed = await preparation.PrepareShowAsync(target.Id);

        Assert.True(blocked.IsSuccess);
        Assert.Single(blocked.Value!.RepeatConflicts);
        Assert.Equal(prior.Id, blocked.Value.RepeatConflicts[0].PriorBroadcasts.Single().ShowId);
        Assert.Null(blocked.Value.BroadcastFolder);
        Assert.True(allowed.IsSuccess);
        Assert.Empty(allowed.Value!.RepeatConflicts);
        Assert.NotNull(allowed.Value.BroadcastFolder);
    }

    [Fact]
    public async Task PrepareShowRebuildsFolderWhenRunningOrderChanges()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        var one = files.CreateFlac("library/a.flac");
        var two = files.CreateFlac("library/b.flac");
        var metadata = new StubFlacMetadataReader()
            .Add(one, new FlacMetadata("A", "Artist", "Album", TimeSpan.FromMinutes(2), new Dictionary<string, string>()))
            .Add(two, new FlacMetadata("B", "Artist", "Album", TimeSpan.FromMinutes(3), new Dictionary<string, string>()));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recordingA = (await service.CreateRecordingAsync(new CreateRecordingCommand("A", "Artist"))).Value!;
        var recordingB = (await service.CreateRecordingAsync(new CreateRecordingCommand("B", "Artist"))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("show-c", "Show C", new DateOnly(2026, 8, 21)))).Value!;
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recordingA.Id, 1));

        var preparation = new ShowPreparationService(context, new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot), metadata);
        var firstRun = await preparation.PrepareShowAsync(show.Id);
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recordingB.Id, 2));
        var secondRun = await preparation.PrepareShowAsync(show.Id);

        Assert.NotNull(firstRun.Value!.BroadcastFolder);
        Assert.Single(firstRun.Value.BroadcastFolder!.CopiedFiles);
        Assert.NotNull(secondRun.Value!.BroadcastFolder);
        Assert.True(secondRun.Value.BroadcastFolder!.Rebuilt);
        Assert.Equal(2, secondRun.Value.BroadcastFolder.CopiedFiles.Count);
        Assert.True(File.Exists(Path.Combine(secondRun.Value.BroadcastFolder.FolderPath, "01 - Artist - A.flac")));
        Assert.True(File.Exists(Path.Combine(secondRun.Value.BroadcastFolder.FolderPath, "02 - Artist - B.flac")));
    }

    [Fact]
    public async Task PrepareShowDoesNotModifySourceFilesAndEnforcesConfiguredRoots()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        var insidePath = files.CreateFlac("library/inside.flac", "inside");
        var outsideDirectory = Path.Combine(Path.GetTempPath(), "showrunner-outside", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDirectory);
        var outsidePath = Path.Combine(outsideDirectory, "outside.flac");
        await File.WriteAllTextAsync(outsidePath, "outside");

        var metadata = new StubFlacMetadataReader()
            .Add(insidePath, new FlacMetadata("Inside", "Artist", "Album", TimeSpan.FromMinutes(4), new Dictionary<string, string>()))
            .Add(outsidePath, new FlacMetadata("Outside", "Artist", "Album", TimeSpan.FromMinutes(4), new Dictionary<string, string>()));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Inside", "Artist"))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("show-d", "Show D", new DateOnly(2026, 8, 21)))).Value!;
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1));

        var before = await File.ReadAllTextAsync(insidePath);
        var preparation = new ShowPreparationService(context, new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot), metadata);
        var result = await preparation.PrepareShowAsync(show.Id);
        var after = await File.ReadAllTextAsync(insidePath);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.BroadcastFolder);
        Assert.Equal(before, after);
        Assert.DoesNotContain(result.Value.MatchedTracks, item => item.SourceFilePath == outsidePath);
    }

    [Fact]
    public async Task PrepareShowReturnsStructuredNotFoundErrorForMissingShow()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        await using var context = harness.CreateContext();
        var preparation = new ShowPreparationService(context, new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot), new StubFlacMetadataReader());

        var result = await preparation.PrepareShowAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("not_found", result.Error!.Code);
        Assert.Contains("show", result.Error.Details.Keys);
    }

    [Fact]
    public async Task ShowPrepareMcpToolMapsApplicationResults()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        var sourcePath = files.CreateFlac("library/tool.flac");
        var metadata = new StubFlacMetadataReader()
            .Add(sourcePath, new FlacMetadata("Tool", "Artist", "Album", TimeSpan.FromMinutes(3), new Dictionary<string, string>()));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Tool", "Artist"))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("show-tool", "Show Tool", new DateOnly(2026, 8, 22)))).Value!;
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1));

        var preparation = new ShowPreparationService(context, new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot), metadata);
        var tools = new ShowrunnerMcpTools(preparation);

        var success = await tools.ShowPrepareAsync(new ShowPrepareToolRequest(show.Id));
        var failure = await tools.ShowPrepareAsync(new ShowPrepareToolRequest(Guid.NewGuid()));

        Assert.True(success.IsSuccess);
        Assert.NotNull(success.Result);
        Assert.Null(success.Error);
        Assert.False(failure.IsSuccess);
        Assert.Null(failure.Result);
        Assert.Equal("not_found", failure.Error!.Code);
    }

    private sealed class StubFlacMetadataReader : IFlacMetadataReader
    {
        private readonly Dictionary<string, FlacMetadata> values = new(StringComparer.Ordinal);

        public StubFlacMetadataReader Add(string path, FlacMetadata metadata)
        {
            values[Path.GetFullPath(path)] = metadata;
            return this;
        }

        public FlacMetadata? TryRead(string filePath)
        {
            values.TryGetValue(Path.GetFullPath(filePath), out var metadata);
            return metadata;
        }
    }

    private sealed class FileFixture : IDisposable
    {
        private readonly string root;

        public FileFixture()
        {
            root = Path.Combine(Path.GetTempPath(), "showrunner-files", Guid.NewGuid().ToString("N"));
            MusicRoot = Path.Combine(root, "music");
            PreparationRoot = Path.Combine(root, "prepared");
            Directory.CreateDirectory(MusicRoot);
            Directory.CreateDirectory(PreparationRoot);
        }

        public string MusicRoot { get; }

        public string PreparationRoot { get; }

        public string CreateFlac(string relativePath, string content = "")
        {
            var path = Path.Combine(MusicRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
