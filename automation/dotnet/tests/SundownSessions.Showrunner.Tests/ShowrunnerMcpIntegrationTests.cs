using ModelContextProtocol.Client;
using Microsoft.Data.Sqlite;
using SundownSessions.Showrunner.Persistence;

namespace SundownSessions.Showrunner.Tests;

public sealed class ShowrunnerMcpIntegrationTests
{
    [Fact]
    public async Task StdioServerDiscoversAndInvokesPreparationToolsWithoutExposingConfiguredRoots()
    {
        using var harness = new SqliteTestHarness();
        using var files = new McpFileFixture();
        using var mixxx = new MixxxFixture();
        Guid showId;
        Guid detectedRecordingId;
        await using (var context = harness.CreateContext())
        {
            var service = new ShowrunnerService(context);
            showId = (await service.CreateShowAsync(
                new CreateShowCommand("mcp-show", "MCP Show", new DateOnly(2026, 8, 21)))).Value!.Id;
            var recording = (await service.CreateRecordingAsync(
                new CreateRecordingCommand("Missing locally", "Test Artist"))).Value!;
            await service.PlanRecordingAsync(showId, new PlanRecordingCommand(recording.Id, 1));
            detectedRecordingId = (await service.CreateRecordingAsync(
                new CreateRecordingCommand("Detected title", "Detected artist"))).Value!.Id;
            await service.CreateRecordingAsync(new CreateRecordingCommand("Detected title", "Different artist"));
        }

        var dotnetDirectory = FindDotnetDirectory();
        var projectPath = Path.Combine(
            dotnetDirectory,
            "src",
            "SundownSessions.Showrunner.Mcp",
            "SundownSessions.Showrunner.Mcp.csproj");
        var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
        environment[ShowrunnerDbContextFactory.DatabasePathEnvironmentVariable] = harness.DatabasePath;
        environment["SUNDOWN_SHOWRUNNER_MUSIC_ROOT"] = files.MusicRoot;
        environment["SUNDOWN_SHOWRUNNER_PREPARATION_ROOT"] = files.PreparationRoot;
        environment[SqliteMixxxPlaybackEvidenceReader.MixxxDatabasePathEnvironmentVariable] = mixxx.DatabasePath;

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "Sundown Showrunner integration test",
            Command = "dotnet",
            Arguments = ["run", "--no-build", "--configuration", "Release", "--project", projectPath],
            WorkingDirectory = dotnetDirectory,
            InheritEnvironmentVariables = false,
            EnvironmentVariables = environment,
            ShutdownTimeout = TimeSpan.FromSeconds(10),
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);

        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        Assert.Equal(
            [
                "recording_history",
                "recording_resolve",
                "repeat_exception_create",
                "show_prepare",
                "show_reconciliation_confirm",
                "show_reconciliation_evidence",
                "show_reconciliation_finalise",
            ],
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal).ToArray());

        var result = await client.CallToolAsync(
            "show_prepare",
            new Dictionary<string, object?> { ["showId"] = showId },
            cancellationToken: timeout.Token);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var json = result.StructuredContent.Value;
        Assert.True(json.GetProperty("isSuccess").GetBoolean());
        var preparationResult = json.GetProperty("result");
        Assert.Equal("Unresolved", preparationResult.GetProperty("status").GetString());
        var unresolvedTrack = Assert.Single(preparationResult.GetProperty("unresolvedTracks").EnumerateArray());
        Assert.Equal("MissingFile", unresolvedTrack.GetProperty("kind").GetString());
        Assert.False(preparationResult.TryGetProperty("broadcastFolder", out _));
        Assert.DoesNotContain(files.MusicRoot, json.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(files.PreparationRoot, json.ToString(), StringComparison.Ordinal);

        var bytesBeforeEvidence = File.ReadAllBytes(mixxx.DatabasePath);
        var writeBeforeEvidence = File.GetLastWriteTimeUtc(mixxx.DatabasePath);
        var evidenceResult = await client.CallToolAsync(
            "show_reconciliation_evidence",
            new Dictionary<string, object?> { ["showId"] = showId },
            cancellationToken: timeout.Token);
        var writeAfterEvidence = File.GetLastWriteTimeUtc(mixxx.DatabasePath);

        Assert.NotEqual(true, evidenceResult.IsError);
        Assert.Equal(writeBeforeEvidence, writeAfterEvidence);
        Assert.Equal(bytesBeforeEvidence, File.ReadAllBytes(mixxx.DatabasePath));
        Assert.NotNull(evidenceResult.StructuredContent);
        var evidenceJson = evidenceResult.StructuredContent.Value.GetProperty("result");
        Assert.Equal("2026-08-21", evidenceJson.GetProperty("historySessionName").GetString());
        Assert.Equal(detectedRecordingId, evidenceJson.GetProperty("unexpected")[0].GetProperty("recordingId").GetGuid());
        Assert.DoesNotContain(mixxx.DatabasePath, evidenceJson.ToString(), StringComparison.Ordinal);

        var confirmationResult = await client.CallToolAsync(
            "show_reconciliation_confirm",
            new Dictionary<string, object?>
            {
                ["showId"] = showId,
                ["command"] = new
                {
                    operatorConfirmed = true,
                    hasUnresolvedAmbiguity = false,
                    items = new[]
                    {
                        new { recordingId = detectedRecordingId, position = 1, plannedRecordingId = (Guid?)null },
                    },
                },
            },
            cancellationToken: timeout.Token);

        Assert.NotEqual(true, confirmationResult.IsError);
        var confirmationJson = confirmationResult.StructuredContent!.Value.GetProperty("result");
        Assert.True(confirmationJson.GetProperty("isOperatorConfirmed").GetBoolean());
        Assert.False(confirmationJson.GetProperty("isConfirmed").GetBoolean());
        await using var verificationContext = harness.CreateContext();
        var persisted = await new ShowrunnerService(verificationContext).GetReconciliationAsync(showId);
        Assert.True(persisted.Value!.IsOperatorConfirmed);
        Assert.Equal(detectedRecordingId, persisted.Value.ConfirmedPlayback.Single().RecordingId);
        Assert.Empty((await new ShowrunnerService(verificationContext).GetBroadcastHistoryAsync(detectedRecordingId)).Value!);

        var finalisationResult = await client.CallToolAsync(
            "show_reconciliation_finalise",
            new Dictionary<string, object?> { ["showId"] = showId },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, finalisationResult.IsError);
        var finalisationJson = finalisationResult.StructuredContent!.Value.GetProperty("result");
        Assert.True(finalisationJson.GetProperty("isFinalised").GetBoolean());
        Assert.False(finalisationJson.GetProperty("isNoOp").GetBoolean());
        Assert.Equal(1, finalisationJson.GetProperty("addedToPermanentHistory").GetArrayLength());

        var historyByIdResult = await client.CallToolAsync(
            "recording_history",
            new Dictionary<string, object?> { ["query"] = new { recordingId = detectedRecordingId } },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, historyByIdResult.IsError);
        var historyByIdJson = historyByIdResult.StructuredContent!.Value.GetProperty("result");
        Assert.False(historyByIdJson.GetProperty("isAmbiguous").GetBoolean());
        var exactCandidate = Assert.Single(historyByIdJson.GetProperty("candidates").EnumerateArray());
        Assert.Equal(1, exactCandidate.GetProperty("broadcastHistory").GetArrayLength());

        var ambiguousHistoryResult = await client.CallToolAsync(
            "recording_history",
            new Dictionary<string, object?> { ["query"] = new { title = "Detected title" } },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, ambiguousHistoryResult.IsError);
        var ambiguousHistoryJson = ambiguousHistoryResult.StructuredContent!.Value.GetProperty("result");
        Assert.True(ambiguousHistoryJson.GetProperty("isAmbiguous").GetBoolean());
        Assert.Equal(2, ambiguousHistoryJson.GetProperty("candidates").GetArrayLength());
    }

    private static string FindDotnetDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Showrunner.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate Showrunner.sln.");
    }

    private sealed class McpFileFixture : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "sundown-showrunner-mcp-tests",
            Guid.NewGuid().ToString("N"));

        public McpFileFixture()
        {
            MusicRoot = Path.Combine(root, "music");
            PreparationRoot = Path.Combine(root, "prepared");
            Directory.CreateDirectory(MusicRoot);
            Directory.CreateDirectory(PreparationRoot);
        }

        public string MusicRoot { get; }

        public string PreparationRoot { get; }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class MixxxFixture : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "sundown-showrunner-mixxx-tests",
            Guid.NewGuid().ToString("N"));

        public MixxxFixture()
        {
            Directory.CreateDirectory(root);
            DatabasePath = Path.Combine(root, "mixxxdb.sqlite");

            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE Playlists (
                    id INTEGER PRIMARY KEY,
                    name TEXT,
                    hidden INTEGER NOT NULL,
                    date_created TEXT,
                    date_modified TEXT
                );
                CREATE TABLE PlaylistTracks (
                    id INTEGER PRIMARY KEY,
                    playlist_id INTEGER NOT NULL,
                    track_id INTEGER NOT NULL,
                    position INTEGER NOT NULL,
                    pl_datetime_added TEXT
                );
                CREATE TABLE library (
                    id INTEGER PRIMARY KEY,
                    title TEXT,
                    artist TEXT,
                    location INTEGER
                );
                CREATE TABLE track_locations (
                    id INTEGER PRIMARY KEY,
                    location TEXT
                );
                INSERT INTO Playlists (id, name, hidden, date_created, date_modified)
                VALUES (1, '2026-08-21', 2, '2026-08-21T19:00:00', '2026-08-21T19:04:00');
                INSERT INTO track_locations (id, location) VALUES (1, '/music/detected.flac');
                INSERT INTO library (id, title, artist, location)
                VALUES (1, 'Detected title', 'Detected artist', 1);
                INSERT INTO PlaylistTracks (id, playlist_id, track_id, position, pl_datetime_added)
                VALUES (1, 1, 1, 1, '2026-08-21T19:00:00');
                """;
            command.ExecuteNonQuery();
        }

        public string DatabasePath { get; }

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
