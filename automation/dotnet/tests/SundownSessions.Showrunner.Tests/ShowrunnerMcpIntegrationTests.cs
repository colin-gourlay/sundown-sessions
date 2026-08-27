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
        Guid missingRecordingId;
        Guid detectedRecordingId;
        await using (var context = harness.CreateContext())
        {
            var service = new ShowrunnerService(context);
            showId = (await service.CreateShowAsync(
                new CreateShowCommand("mcp-show", "MCP Show", new DateOnly(2026, 8, 21)))).Value!.Id;
            missingRecordingId = (await service.CreateRecordingAsync(
                new CreateRecordingCommand("Missing locally", "Test Artist"))).Value!.Id;
            await service.PlanRecordingAsync(showId, new PlanRecordingCommand(missingRecordingId, 1));
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
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);

        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        Assert.Equal(
            [
            "backlog_candidate_import",
            "backlog_item_list",
            "recording_external_identifier_add",
            "recording_history",
            "recording_resolve",
            "repeat_exception_create",
            "show_get",
            "show_plan_refresh",
            "show_prepare",
            "show_publication_export",
            "show_reconciliation_confirm",
            "show_reconciliation_evidence",
            "show_reconciliation_finalise",
            ],
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal).ToArray());

        var showGetResult = await client.CallToolAsync(
            "show_get",
            new Dictionary<string, object?>
            {
                ["query"] = new { showDate = "2026-08-21" },
            },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, showGetResult.IsError);
        var showGetJson = showGetResult.StructuredContent!.Value.GetProperty("result");
        Assert.False(showGetJson.GetProperty("isAmbiguous").GetBoolean());
        var foundShow = Assert.Single(showGetJson.GetProperty("matches").EnumerateArray());
        Assert.Equal(showId, foundShow.GetProperty("id").GetGuid());
        Assert.Equal("mcp-show", foundShow.GetProperty("slug").GetString());

        var invalidShowGetResult = await client.CallToolAsync(
            "show_get",
            new Dictionary<string, object?> { ["query"] = new { } },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, invalidShowGetResult.IsError);
        var invalidShowGetJson = invalidShowGetResult.StructuredContent!.Value;
        Assert.False(invalidShowGetJson.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(
            "validation_failed",
            invalidShowGetJson.GetProperty("error").GetProperty("code").GetString());

        var importCandidateArguments = new Dictionary<string, object?>
        {
            ["command"] = new
            {
                summary = "MCP Todoist candidate",
                externalIdentifierSource = "todoist",
                externalIdentifierValue = "mcp-task-1",
                newRecording = new
                {
                    title = "MCP-only demo",
                    artist = "MCP Artist",
                },
            },
        };
        var importCandidateResult = await client.CallToolAsync(
            "backlog_candidate_import",
            importCandidateArguments,
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, importCandidateResult.IsError);
        var importCandidateJson = importCandidateResult.StructuredContent!.Value.GetProperty("result");
        Assert.True(importCandidateJson.GetProperty("recordingCreated").GetBoolean());
        Assert.True(importCandidateJson.GetProperty("externalIdentifierAdded").GetBoolean());
        Assert.True(importCandidateJson.GetProperty("backlogItemCreated").GetBoolean());
        Assert.False(importCandidateJson.GetProperty("isNoOp").GetBoolean());
        var importedRecordingId = importCandidateJson.GetProperty("recording").GetProperty("id").GetGuid();
        var importedBacklogItemId = importCandidateJson.GetProperty("backlogItem").GetProperty("id").GetGuid();

        var retryCandidateResult = await client.CallToolAsync(
            "backlog_candidate_import",
            importCandidateArguments,
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, retryCandidateResult.IsError);
        var retryCandidateJson = retryCandidateResult.StructuredContent!.Value.GetProperty("result");
        Assert.True(retryCandidateJson.GetProperty("isNoOp").GetBoolean());
        Assert.Equal(importedRecordingId, retryCandidateJson.GetProperty("recording").GetProperty("id").GetGuid());
        Assert.Equal(importedBacklogItemId, retryCandidateJson.GetProperty("backlogItem").GetProperty("id").GetGuid());

        var conflictingCandidateResult = await client.CallToolAsync(
            "backlog_candidate_import",
            new Dictionary<string, object?>
            {
                ["command"] = new
                {
                    summary = "Conflicting MCP candidate",
                    externalIdentifierSource = "todoist",
                    externalIdentifierValue = "mcp-task-1",
                    newRecording = new
                    {
                        title = "A different recording",
                        artist = "MCP Artist",
                    },
                },
            },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, conflictingCandidateResult.IsError);
        var conflictingCandidateJson = conflictingCandidateResult.StructuredContent!.Value;
        Assert.False(conflictingCandidateJson.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(
            "external_identifier_in_use",
            conflictingCandidateJson.GetProperty("error").GetProperty("code").GetString());

        var listBacklogResult = await client.CallToolAsync(
            "backlog_item_list",
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, listBacklogResult.IsError);
        var listedBacklogItems = listBacklogResult.StructuredContent!.Value
            .GetProperty("result")
            .GetProperty("items");
        var listedBacklogItem = Assert.Single(listedBacklogItems.EnumerateArray());
        Assert.Equal(importedBacklogItemId, listedBacklogItem.GetProperty("id").GetGuid());
        Assert.Equal(importedRecordingId, listedBacklogItem.GetProperty("recordingId").GetGuid());

        var refreshPlanResult = await client.CallToolAsync(
            "show_plan_refresh",
            new Dictionary<string, object?>
            {
                ["showId"] = showId,
                ["command"] = new
                {
                    items = new[]
                    {
                        new { recordingId = detectedRecordingId, notes = "Imported from Spotify" },
                    },
                },
            },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, refreshPlanResult.IsError);
        var refreshPlanJson = refreshPlanResult.StructuredContent!.Value.GetProperty("result");
        Assert.Equal(1, refreshPlanJson.GetProperty("plannedCount").GetInt32());
        Assert.Equal(detectedRecordingId, refreshPlanJson.GetProperty("plannedRecordings")[0].GetProperty("recordingId").GetGuid());
        Assert.Equal("Imported from Spotify", refreshPlanJson.GetProperty("plannedRecordings")[0].GetProperty("notes").GetString());

        var addExternalIdentifierResult = await client.CallToolAsync(
            "recording_external_identifier_add",
            new Dictionary<string, object?>
            {
                ["recordingId"] = detectedRecordingId,
                ["source"] = "spotify",
                ["value"] = "https://open.spotify.com/track/detected-id?si=123",
            },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, addExternalIdentifierResult.IsError);
        Assert.Equal(
            "detected-id",
            addExternalIdentifierResult.StructuredContent!.Value
                .GetProperty("result")
                .GetProperty("externalIdentifiers")[0]
                .GetProperty("value")
                .GetString());

        var restorePlanResult = await client.CallToolAsync(
            "show_plan_refresh",
            new Dictionary<string, object?>
            {
                ["showId"] = showId,
                ["command"] = new
                {
                    items = new[]
                    {
                        new { recordingId = missingRecordingId },
                    },
                },
            },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, restorePlanResult.IsError);

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
        Assert.Equal(
            "detected-id",
            finalisationJson.GetProperty("addedToPermanentHistory")[0]
                .GetProperty("externalIdentifiers")[0]
                .GetProperty("value")
                .GetString());

        var historyByIdResult = await client.CallToolAsync(
            "recording_history",
            new Dictionary<string, object?> { ["query"] = new { recordingId = detectedRecordingId } },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, historyByIdResult.IsError);
        var historyByIdJson = historyByIdResult.StructuredContent!.Value.GetProperty("result");
        Assert.False(historyByIdJson.GetProperty("isAmbiguous").GetBoolean());
        var exactCandidate = Assert.Single(historyByIdJson.GetProperty("candidates").EnumerateArray());
        Assert.Equal(1, exactCandidate.GetProperty("broadcastHistory").GetArrayLength());
        Assert.Equal("detected-id", exactCandidate.GetProperty("externalIdentifiers")[0].GetProperty("value").GetString());

        var historyByExternalIdentifierResult = await client.CallToolAsync(
            "recording_history",
            new Dictionary<string, object?> { ["query"] = new { externalIdentifierSource = "spotify", externalIdentifierValue = "spotify:track:detected-id" } },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, historyByExternalIdentifierResult.IsError);
        var historyByExternalIdentifierJson = historyByExternalIdentifierResult.StructuredContent!.Value.GetProperty("result");
        Assert.False(historyByExternalIdentifierJson.GetProperty("isAmbiguous").GetBoolean());
        Assert.Equal(
            detectedRecordingId,
            historyByExternalIdentifierJson.GetProperty("candidates")[0].GetProperty("recordingId").GetGuid());

        var ambiguousHistoryResult = await client.CallToolAsync(
            "recording_history",
            new Dictionary<string, object?> { ["query"] = new { title = "Detected title" } },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, ambiguousHistoryResult.IsError);
        var ambiguousHistoryJson = ambiguousHistoryResult.StructuredContent!.Value.GetProperty("result");
        Assert.True(ambiguousHistoryJson.GetProperty("isAmbiguous").GetBoolean());
        Assert.Equal(2, ambiguousHistoryJson.GetProperty("candidates").GetArrayLength());

        var publicationExportResult = await client.CallToolAsync(
            "show_publication_export",
            new Dictionary<string, object?> { ["query"] = new { showId = showId } },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, publicationExportResult.IsError);
        var publicationExportJson = publicationExportResult.StructuredContent!.Value.GetProperty("result");
        Assert.True(publicationExportJson.GetProperty("isFinalised").GetBoolean());
        Assert.NotEqual(default, publicationExportJson.GetProperty("finalisedAtUtc").GetDateTimeOffset());
        Assert.Equal(showId, publicationExportJson.GetProperty("showId").GetGuid());
        Assert.Equal("mcp-show", publicationExportJson.GetProperty("slug").GetString());
        Assert.Equal("2026-08-21", publicationExportJson.GetProperty("showDate").GetString());
        var exportedPlaylist = publicationExportJson.GetProperty("finalPlaylist");
        Assert.Equal(1, exportedPlaylist.GetArrayLength());
        var exportedTrack = exportedPlaylist[0];
        Assert.Equal(detectedRecordingId, exportedTrack.GetProperty("recordingId").GetGuid());
        Assert.Equal(1, exportedTrack.GetProperty("position").GetInt32());
        Assert.Equal("Detected title", exportedTrack.GetProperty("title").GetString());
        Assert.Equal("Detected artist", exportedTrack.GetProperty("artist").GetString());
        Assert.Equal(
            "spotify",
            exportedTrack.GetProperty("externalIdentifiers")[0].GetProperty("source").GetString());
        Assert.False(exportedTrack.TryGetProperty("notes", out _));
        Assert.DoesNotContain(files.MusicRoot, publicationExportJson.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(files.PreparationRoot, publicationExportJson.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(mixxx.DatabasePath, publicationExportJson.ToString(), StringComparison.Ordinal);

        var nonFinalisedShowId = (await new ShowrunnerService(harness.CreateContext()).CreateShowAsync(
            new CreateShowCommand("unfinalised-show", "Unfinalised Show", new DateOnly(2026, 9, 1)))).Value!.Id;
        var nonFinalisedExportResult = await client.CallToolAsync(
            "show_publication_export",
            new Dictionary<string, object?> { ["query"] = new { showId = nonFinalisedShowId } },
            cancellationToken: timeout.Token);
        Assert.NotEqual(true, nonFinalisedExportResult.IsError);
        var nonFinalisedExportJson = nonFinalisedExportResult.StructuredContent!.Value.GetProperty("result");
        Assert.False(nonFinalisedExportJson.GetProperty("isFinalised").GetBoolean());
        Assert.False(nonFinalisedExportJson.TryGetProperty("finalisedAtUtc", out _));
        Assert.Equal(0, nonFinalisedExportJson.GetProperty("finalPlaylist").GetArrayLength());
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
