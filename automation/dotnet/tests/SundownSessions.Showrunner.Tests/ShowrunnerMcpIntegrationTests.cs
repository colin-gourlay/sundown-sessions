using ModelContextProtocol.Client;
using SundownSessions.Showrunner.Persistence;

namespace SundownSessions.Showrunner.Tests;

public sealed class ShowrunnerMcpIntegrationTests
{
    [Fact]
    public async Task StdioServerDiscoversAndInvokesPreparationToolsWithoutExposingConfiguredRoots()
    {
        using var harness = new SqliteTestHarness();
        using var files = new McpFileFixture();
        Guid showId;
        await using (var context = harness.CreateContext())
        {
            var service = new ShowrunnerService(context);
            showId = (await service.CreateShowAsync(
                new CreateShowCommand("mcp-show", "MCP Show", new DateOnly(2026, 8, 21)))).Value!.Id;
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
            ["recording_resolve", "repeat_exception_create", "show_prepare"],
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal).ToArray());

        var result = await client.CallToolAsync(
            "show_prepare",
            new Dictionary<string, object?> { ["showId"] = showId },
            cancellationToken: timeout.Token);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var json = result.StructuredContent.Value;
        Assert.True(json.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("Prepared", json.GetProperty("result").GetProperty("status").GetString());
        Assert.DoesNotContain(files.MusicRoot, json.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(files.PreparationRoot, json.ToString(), StringComparison.Ordinal);
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
}
