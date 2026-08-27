using System.Buffers.Binary;
using System.Text;
using Microsoft.EntityFrameworkCore;
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
            .Add(sourcePath, new FlacMetadata("Track A", "Artist A", "Release A", TimeSpan.FromMinutes(4), new Dictionary<string, IReadOnlyList<string>>
            {
                ["SPOTIFY_TRACK_ID"] = ["abc123"],
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
        Assert.Equal("01 - Artist A - Track A.flac", result.Value.BroadcastFolder.CopiedFiles[0]);
        Assert.True(File.Exists(files.GetPreparedPath(
            result.Value.BroadcastFolder.FolderName,
            result.Value.BroadcastFolder.CopiedFiles[0])));
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
            .Add(firstPath, new FlacMetadata("Same Title", "Same Artist", "Album One", TimeSpan.FromMinutes(3), new Dictionary<string, IReadOnlyList<string>>()))
            .Add(secondPath, new FlacMetadata("Same Title", "Same Artist", "Album Two", TimeSpan.FromMinutes(5), new Dictionary<string, IReadOnlyList<string>>()));

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
        Assert.Equal(UnresolvedTrackKind.AmbiguousMatch, result.Value.UnresolvedTracks[0].Kind);
        Assert.Equal(2, result.Value.UnresolvedTracks[0].Candidates.Count);
        Assert.Equal(showWithOne.PlannedRecordings.Single().Id, result.Value.UnresolvedTracks[0].PlannedRecordingId);
        Assert.Equal(UnresolvedTrackKind.MissingFile, result.Value.UnresolvedTracks[1].Kind);
    }

    [Fact]
    public async Task PrepareShowDetectsOnlyConfirmedBroadcastRepeatsAndSupportsException()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        var sourcePath = files.CreateFlac("library/repeat.flac");
        var metadata = new StubFlacMetadataReader()
            .Add(sourcePath, new FlacMetadata("Repeat", "Artist", "Album", TimeSpan.FromMinutes(4), new Dictionary<string, IReadOnlyList<string>>()));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Repeat", "Artist"))).Value!;

        var plannedOnly = (await service.CreateShowAsync(new CreateShowCommand("planned-only-2", "Planned", new DateOnly(2026, 8, 20)))).Value!;
        await service.PlanRecordingAsync(plannedOnly.Id, new PlanRecordingCommand(recording.Id, 1));

        var prior = (await service.CreateShowAsync(new CreateShowCommand("prior-show", "Prior", new DateOnly(2026, 8, 21)))).Value!;
        var priorPlan = (await service.PlanRecordingAsync(prior.Id, new PlanRecordingCommand(recording.Id, 1))).Value!;
        await ShowrunnerTestOperations.FinaliseShowAsync(
            context,
            prior.Id,
            [new ConfirmedPlaybackItemCommand(recording.Id, 1, priorPlan.PlannedRecordings.Single().Id)]);

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
            .Add(one, new FlacMetadata("A", "Artist", "Album", TimeSpan.FromMinutes(2), new Dictionary<string, IReadOnlyList<string>>()))
            .Add(two, new FlacMetadata("B", "Artist", "Album", TimeSpan.FromMinutes(3), new Dictionary<string, IReadOnlyList<string>>()));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recordingA = (await service.CreateRecordingAsync(new CreateRecordingCommand("A", "Artist"))).Value!;
        var recordingB = (await service.CreateRecordingAsync(new CreateRecordingCommand("B", "Artist"))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("show-c", "Show C", new DateOnly(2026, 8, 21)))).Value!;
        var initialPlan = (await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recordingA.Id, 1))).Value!;

        var preparation = new ShowPreparationService(context, new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot), metadata);
        var firstRun = await preparation.PrepareShowAsync(show.Id);
        var changedPlan = (await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recordingB.Id, 2))).Value!;
        var secondRun = await preparation.PrepareShowAsync(show.Id);

        Assert.NotNull(firstRun.Value!.BroadcastFolder);
        Assert.Single(firstRun.Value.BroadcastFolder!.CopiedFiles);
        Assert.NotNull(secondRun.Value!.BroadcastFolder);
        Assert.True(secondRun.Value.BroadcastFolder!.Rebuilt);
        Assert.Equal(2, secondRun.Value.BroadcastFolder.CopiedFiles.Count);
        Assert.True(File.Exists(files.GetPreparedPath(secondRun.Value.BroadcastFolder.FolderName, "01 - Artist - A.flac")));
        Assert.True(File.Exists(files.GetPreparedPath(secondRun.Value.BroadcastFolder.FolderName, "02 - Artist - B.flac")));

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM "PlannedRecordings" WHERE "Id" = {initialPlan.PlannedRecordings.Single().Id}""");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "PlannedRecordings" SET "Position" = 1 WHERE "Id" = {changedPlan.PlannedRecordings.Single(item => item.RecordingId == recordingB.Id).Id}""");
        var reorderedRun = await preparation.PrepareShowAsync(show.Id);

        Assert.True(reorderedRun.Value!.BroadcastFolder!.Rebuilt);
        Assert.Equal(["01 - Artist - B.flac"], reorderedRun.Value.BroadcastFolder.CopiedFiles);
        Assert.False(File.Exists(files.GetPreparedPath(reorderedRun.Value.BroadcastFolder.FolderName, "01 - Artist - A.flac")));
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
            .Add(insidePath, new FlacMetadata("Inside", "Artist", "Album", TimeSpan.FromMinutes(4), new Dictionary<string, IReadOnlyList<string>>()))
            .Add(outsidePath, new FlacMetadata("Outside", "Artist", "Album", TimeSpan.FromMinutes(4), new Dictionary<string, IReadOnlyList<string>>()));

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
        Assert.All(result.Value.MatchedTracks, item => Assert.False(Path.IsPathRooted(item.SourceLibraryPath)));
        Assert.DoesNotContain(result.Value.MatchedTracks, item => item.SourceLibraryPath.Contains("outside", StringComparison.Ordinal));
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
    public async Task PrepareShowReturnsStructuredErrorWhenConfiguredMusicRootIsUnavailable()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var show = (await service.CreateShowAsync(
            new CreateShowCommand("missing-root", "Missing root", new DateOnly(2026, 8, 22)))).Value!;
        var preparation = new ShowPreparationService(
            context,
            new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot),
            new StubFlacMetadataReader());
        Directory.Delete(files.MusicRoot, recursive: true);

        var result = await preparation.PrepareShowAsync(show.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("music_root_unavailable", result.Error!.Code);
        Assert.Empty(result.Error.Details);
    }

    [Fact]
    public async Task StableIdentifierConflictIsUnresolvedInsteadOfFallingBackToText()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        var sourcePath = files.CreateFlac("library/conflict.flac");
        var metadata = new StubFlacMetadataReader().Add(
            sourcePath,
            new FlacMetadata(
                "Same title",
                "Same artist",
                "Release",
                TimeSpan.FromMinutes(3),
                new Dictionary<string, IReadOnlyList<string>> { ["SPOTIFY_TRACK_ID"] = ["different-id"] }));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Same title", "Same artist"))).Value!;
        await service.AddExternalIdentifierAsync(recording.Id, new AddExternalIdentifierCommand("spotify", "spotify:track:expected-id"));
        var show = (await service.CreateShowAsync(new CreateShowCommand("identifier-conflict", "Conflict", new DateOnly(2026, 8, 23)))).Value!;
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1));

        var result = await new ShowPreparationService(
            context,
            new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot),
            metadata).PrepareShowAsync(show.Id);

        Assert.Equal(ShowPreparationStatus.Unresolved, result.Value!.Status);
        var unresolved = Assert.Single(result.Value.UnresolvedTracks);
        Assert.Equal(UnresolvedTrackKind.IdentifierConflict, unresolved.Kind);
        Assert.Equal("library/conflict.flac", Assert.Single(unresolved.Candidates).SourceLibraryPath);
        Assert.Null(result.Value.BroadcastFolder);
    }

    [Fact]
    public async Task ReleaseMetadataDisambiguatesTextMatches()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        var firstPath = files.CreateFlac("library/first.flac");
        var secondPath = files.CreateFlac("library/second.flac");
        var metadata = new StubFlacMetadataReader()
            .Add(firstPath, new FlacMetadata("Title", "Artist", "First release", TimeSpan.FromMinutes(2), EmptyIdentifiers()))
            .Add(secondPath, new FlacMetadata("Title", "Artist", "Wanted release", TimeSpan.FromMinutes(4), EmptyIdentifiers()));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(
            new CreateRecordingCommand("Title", "Artist", ReleaseTitle: "Wanted release"))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("release-match", "Release", new DateOnly(2026, 8, 24)))).Value!;
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1));

        var result = await new ShowPreparationService(
            context,
            new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot),
            metadata).PrepareShowAsync(show.Id);

        Assert.Equal("library/second.flac", Assert.Single(result.Value!.MatchedTracks).SourceLibraryPath);
        Assert.Equal(RecordingMatchKind.NormalisedMetadata, result.Value.MatchedTracks[0].MatchKind);
    }

    [Fact]
    public async Task ExplicitResolutionPersistsOnlyAConfiguredRelativeCandidate()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        var firstPath = files.CreateFlac("library/one.flac");
        var secondPath = files.CreateFlac("library/two.flac");
        var metadata = new StubFlacMetadataReader()
            .Add(firstPath, new FlacMetadata("Title", "Artist", "One", TimeSpan.FromMinutes(2), EmptyIdentifiers()))
            .Add(secondPath, new FlacMetadata("Title", "Artist", "Two", TimeSpan.FromMinutes(3), EmptyIdentifiers()));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand("Title", "Artist"))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("resolved", "Resolved", new DateOnly(2026, 8, 25)))).Value!;
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1));
        var preparation = new ShowPreparationService(
            context,
            new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot),
            metadata);

        var absoluteAttempt = await preparation.ResolveRecordingAsync(recording.Id, secondPath);
        var resolution = await preparation.ResolveRecordingAsync(recording.Id, "library/two.flac");
        var result = await preparation.PrepareShowAsync(show.Id);

        Assert.Equal("validation_failed", absoluteAttempt.Error!.Code);
        Assert.True(resolution.IsSuccess);
        Assert.Equal("library/two.flac", resolution.Value!.SourceLibraryPath);
        var matched = Assert.Single(result.Value!.MatchedTracks);
        Assert.Equal(RecordingMatchKind.ExplicitResolution, matched.MatchKind);
        Assert.Equal("library/two.flac", matched.SourceLibraryPath);
    }

    [Fact]
    public async Task FailedRebuildPreservesPreviousPreparedFolder()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        var firstPath = files.CreateFlac("library/a.flac", "a");
        var secondPath = files.CreateFlac("library/b.flac", "b");
        var metadata = new StubFlacMetadataReader()
            .Add(firstPath, new FlacMetadata("A", "Artist", null, TimeSpan.FromMinutes(2), EmptyIdentifiers()))
            .Add(secondPath, new FlacMetadata("B", "Artist", null, TimeSpan.FromMinutes(2), EmptyIdentifiers()));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var firstRecording = (await service.CreateRecordingAsync(new CreateRecordingCommand("A", "Artist"))).Value!;
        var secondRecording = (await service.CreateRecordingAsync(new CreateRecordingCommand("B", "Artist"))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("safe-rebuild", "Safe", new DateOnly(2026, 8, 26)))).Value!;
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(firstRecording.Id, 1));
        var preparation = new ShowPreparationService(
            context,
            new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot),
            metadata);
        var firstRun = await preparation.PrepareShowAsync(show.Id);
        var originalPath = files.GetPreparedPath(firstRun.Value!.BroadcastFolder!.FolderName, "01 - Artist - A.flac");
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(secondRecording.Id, 2));
        metadata.OnRead(path =>
        {
            if (path == secondPath && File.Exists(path))
            {
                File.Delete(path);
            }
        });

        var failedRebuild = await preparation.PrepareShowAsync(show.Id);

        Assert.False(failedRebuild.IsSuccess);
        Assert.Equal("preparation_folder_failed", failedRebuild.Error!.Code);
        Assert.True(File.Exists(originalPath));
        Assert.Equal("a", await File.ReadAllTextAsync(originalPath));
    }

    [Fact]
    public async Task OutputFilenameHonoursLinuxByteLimitAndReportsOverrun()
    {
        using var harness = new SqliteTestHarness();
        using var files = new FileFixture();
        var sourcePath = files.CreateFlac("library/long.flac");
        var title = string.Concat(Enumerable.Repeat("é", 120));
        var artist = string.Concat(Enumerable.Repeat("ø", 120));
        var metadata = new StubFlacMetadataReader().Add(
            sourcePath,
            new FlacMetadata(title, artist, null, TimeSpan.FromMinutes(61), EmptyIdentifiers()));

        await using var context = harness.CreateContext();
        var service = new ShowrunnerService(context);
        var recording = (await service.CreateRecordingAsync(new CreateRecordingCommand(title, artist))).Value!;
        var show = (await service.CreateShowAsync(new CreateShowCommand("long-name", "Long", new DateOnly(2026, 8, 27)))).Value!;
        await service.PlanRecordingAsync(show.Id, new PlanRecordingCommand(recording.Id, 1));

        var result = await new ShowPreparationService(
            context,
            new ShowPreparationOptions(files.MusicRoot, files.PreparationRoot, TimeSpan.FromMinutes(60)),
            metadata).PrepareShowAsync(show.Id);

        var outputName = Assert.Single(result.Value!.MatchedTracks).OutputFileName;
        Assert.InRange(Encoding.UTF8.GetByteCount(outputName), 1, 240);
        Assert.Null(result.Value.Timing.RemainingDuration);
        Assert.Equal(TimeSpan.FromMinutes(1), result.Value.Timing.OverrunDuration);
    }

    [Fact]
    public void TagLibReaderReadsEmbeddedFlacMetadataAndAllIdentifierValues()
    {
        using var files = new FileFixture();
        var path = files.CreateMetadataOnlyFlac(
            "library/tagged.flac",
            "Embedded title",
            "Embedded artist",
            "Embedded release",
            "first-id",
            "second-id");

        var metadata = new TagLibFlacMetadataReader().TryRead(path);

        Assert.NotNull(metadata);
        Assert.Equal("Embedded title", metadata.Title);
        Assert.Equal("Embedded artist", metadata.Artist);
        Assert.Equal("Embedded release", metadata.Album);
        Assert.Equal(["first-id", "second-id"], metadata.Identifiers["SPOTIFY_TRACK_ID"]);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyIdentifiers()
        => new Dictionary<string, IReadOnlyList<string>>();

    private sealed class StubFlacMetadataReader : IFlacMetadataReader
    {
        private readonly Dictionary<string, FlacMetadata> values = new(StringComparer.Ordinal);
        private Action<string>? onRead;

        public StubFlacMetadataReader Add(string path, FlacMetadata metadata)
        {
            values[Path.GetFullPath(path)] = metadata;
            return this;
        }

        public StubFlacMetadataReader OnRead(Action<string> callback)
        {
            onRead = callback;
            return this;
        }

        public FlacMetadata? TryRead(string filePath)
        {
            var fullPath = Path.GetFullPath(filePath);
            values.TryGetValue(fullPath, out var metadata);
            onRead?.Invoke(fullPath);
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

        public string GetPreparedPath(string folderName, string fileName)
            => Path.Combine(PreparationRoot, folderName, fileName);

        public string CreateMetadataOnlyFlac(
            string relativePath,
            string title,
            string artist,
            string album,
            params string[] spotifyIds)
        {
            var path = Path.Combine(MusicRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var comments = new List<string>
            {
                $"TITLE={title}",
                $"ARTIST={artist}",
                $"ALBUM={album}",
            };
            comments.AddRange(spotifyIds.Select(identifier => $"SPOTIFY_TRACK_ID={identifier}"));

            using var stream = File.Create(path);
            stream.Write("fLaC"u8);
            stream.Write([0x00, 0x00, 0x00, 0x22]);
            Span<byte> streamInfo = stackalloc byte[34];
            BinaryPrimitives.WriteUInt16BigEndian(streamInfo, 4096);
            BinaryPrimitives.WriteUInt16BigEndian(streamInfo[2..], 4096);
            const ulong sampleRateAndTotalSamples = ((ulong)44100 << 44) | ((ulong)1 << 41) | ((ulong)15 << 36) | 44100;
            BinaryPrimitives.WriteUInt64BigEndian(streamInfo[10..], sampleRateAndTotalSamples);
            stream.Write(streamInfo);

            using var commentPayload = new MemoryStream();
            using (var writer = new BinaryWriter(commentPayload, Encoding.UTF8, leaveOpen: true))
            {
                var vendor = Encoding.UTF8.GetBytes("Sundown Sessions tests");
                writer.Write(vendor.Length);
                writer.Write(vendor);
                writer.Write(comments.Count);
                foreach (var comment in comments)
                {
                    var bytes = Encoding.UTF8.GetBytes(comment);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }
            }

            var payload = commentPayload.ToArray();
            stream.WriteByte(0x84);
            stream.WriteByte((byte)(payload.Length >> 16));
            stream.WriteByte((byte)(payload.Length >> 8));
            stream.WriteByte((byte)payload.Length);
            stream.Write(payload);
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
