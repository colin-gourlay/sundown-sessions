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
        await using (var context = harness.CreateContext())
        {
            var service = new ShowrunnerService(context);
            showId = (await service.CreateShowAsync(
                new CreateShowCommand("mcp-show", "MCP Show", new DateOnly(2026, 8, 21)))).Value!.Id;
            var recording = (await service.CreateRecordingAsync(
                new CreateRecordingCommand("Missing locally", "Test Artist"))).Value!;
            await service.PlanRecordingAsync(showId, new PlanRecordingCommand(recording.Id, 1));
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
            ["recording_resolve", "repeat_exception_create", "show_prepare", "show_reconciliation_confirm", "show_reconciliation_evidence"],
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

        var writeBeforeEvidence = File.GetLastWriteTimeUtc(mixxx.DatabasePath);
        var evidenceResult = await client.CallToolAsync(
            "show_reconciliation_evidence",
            new Dictionary<string, object?> { ["showId"] = showId },
            cancellationToken: timeout.Token);
        var writeAfterEvidence = File.GetLastWriteTimeUtc(mixxx.DatabasePath);

        Assert.NotEqual(true, evidenceResult.IsError);
        Assert.Equal(writeBeforeEvidence, writeAfterEvidence);
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
                CREATE TABLE library (
                    id INTEGER PRIMARY KEY,
                    title TEXT,
                    artist TEXT
                );
                CREATE TABLE play_history (
                    id INTEGER PRIMARY KEY,
                    track_id INTEGER NOT NULL,
                    played_at TEXT NOT NULL
                );
                INSERT INTO library (id, title, artist) VALUES (1, 'Detected title', 'Detected artist');
                INSERT INTO play_history (track_id, played_at) VALUES (1, '2026-08-21T19:00:00Z');
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
