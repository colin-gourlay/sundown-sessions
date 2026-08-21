using Microsoft.Data.Sqlite;

namespace SundownSessions.Showrunner.Tests;

public sealed class MixxxPlaybackEvidenceReaderTests
{
    [Fact]
    public async Task ReaderUsesOfficialMixxxHistorySchemaAndSelectsOnlyTheShowDate()
    {
        using var fixture = new MixxxSqliteFixture();
        fixture.SeedSession(
            "2026-08-20",
            2,
            ("Old track", "Old artist", "/music/old.flac", "2026-08-20T18:00:00"));
        fixture.SeedSession(
            "2026-08-21",
            2,
            ("Track 1", "Artist 1", "/music/show/one.flac", "2026-08-21T18:00:00"),
            ("Track 2", "Artist 2", "/music/show/two.flac", "2026-08-21T18:04:00"));
        fixture.SeedSession(
            "2026-08-21 playlist",
            0,
            ("Not history", "Artist", "/music/not-history.flac", "2026-08-21T18:08:00"));
        var reader = new SqliteMixxxPlaybackEvidenceReader(fixture.DatabasePath);

        var result = await reader.ReadPlaybackEvidenceAsync(new DateOnly(2026, 8, 21));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsIncomplete);
        Assert.Equal("2026-08-21", result.Value.HistorySessionName);
        Assert.Equal(2, result.Value.Candidates.Count);
        Assert.Equal("Track 1", result.Value.Candidates[0].Title);
        Assert.Equal("/music/show/two.flac", result.Value.Candidates[1].FileLocation);
        Assert.DoesNotContain(result.Value.Candidates, item => item.Title == "Old track" || item.Title == "Not history");
    }

    [Fact]
    public async Task ReaderReturnsMultipleSameDaySessionsAsUnresolvedEvidence()
    {
        using var fixture = new MixxxSqliteFixture();
        fixture.SeedSession(
            "2026-08-21 first",
            2,
            ("First", "Artist", "/music/first.flac", "2026-08-21T18:00:00"));
        fixture.SeedSession(
            "2026-08-21 second",
            2,
            ("Second", "Artist", "/music/second.flac", "2026-08-21T19:00:00"));
        var reader = new SqliteMixxxPlaybackEvidenceReader(fixture.DatabasePath);

        var result = await reader.ReadPlaybackEvidenceAsync(new DateOnly(2026, 8, 21));

        Assert.True(result.Value!.IsIncomplete);
        Assert.Empty(result.Value.Candidates);
        Assert.Equal(2, result.Value.Sessions.Count);
        Assert.Contains("mixxx_multiple_history_sessions", result.Value.Warnings);
    }

    [Fact]
    public async Task ReaderTreatsUnsupportedSchemaAsIncompleteEvidence()
    {
        using var fixture = new MixxxSqliteFixture(createSchema: false);
        fixture.CreateUnsupportedSchema();
        var reader = new SqliteMixxxPlaybackEvidenceReader(fixture.DatabasePath);

        var result = await reader.ReadPlaybackEvidenceAsync(new DateOnly(2026, 8, 21));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsIncomplete);
        Assert.Contains("mixxx_schema_unsupported", result.Value.Warnings);
    }

    [Fact]
    public async Task ReaderReportsWhenNoHistorySessionMatchesTheShowDate()
    {
        using var fixture = new MixxxSqliteFixture();
        fixture.SeedSession(
            "2026-08-20",
            2,
            ("Old track", "Artist", "/music/old.flac", "2026-08-20T18:00:00"));
        var reader = new SqliteMixxxPlaybackEvidenceReader(fixture.DatabasePath);

        var result = await reader.ReadPlaybackEvidenceAsync(new DateOnly(2026, 8, 21));

        Assert.True(result.Value!.IsIncomplete);
        Assert.Empty(result.Value.Candidates);
        Assert.Contains("mixxx_history_session_not_found", result.Value.Warnings);
        Assert.Single(result.Value.Sessions);
    }

    [Fact]
    public async Task ReaderDoesNotModifyTheMixxxDatabase()
    {
        using var fixture = new MixxxSqliteFixture();
        fixture.SeedSession(
            "2026-08-21",
            2,
            ("Read only", "Artist", "/music/read-only.flac", "2026-08-21T19:00:00"));
        File.SetLastWriteTimeUtc(fixture.DatabasePath, new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc));
        var bytesBefore = File.ReadAllBytes(fixture.DatabasePath);
        var writeBefore = File.GetLastWriteTimeUtc(fixture.DatabasePath);
        var reader = new SqliteMixxxPlaybackEvidenceReader(fixture.DatabasePath);

        _ = await reader.ReadPlaybackEvidenceAsync(new DateOnly(2026, 8, 21));

        Assert.Equal(writeBefore, File.GetLastWriteTimeUtc(fixture.DatabasePath));
        Assert.Equal(bytesBefore, File.ReadAllBytes(fixture.DatabasePath));
    }

    private sealed class MixxxSqliteFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "mixxx-reader-tests", Guid.NewGuid().ToString("N"));
        private int nextSessionId = 1;
        private int nextTrackId = 1;

        public MixxxSqliteFixture(bool createSchema = true)
        {
            Directory.CreateDirectory(root);
            DatabasePath = Path.Combine(root, "mixxxdb.sqlite");
            if (createSchema)
            {
                Execute("""
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
                    """);
            }
        }

        public string DatabasePath { get; }

        public void SeedSession(
            string name,
            int hidden,
            params (string Title, string Artist, string Location, string PlayedAt)[] rows)
        {
            var sessionId = nextSessionId++;
            using var connection = Open();
            using (var session = connection.CreateCommand())
            {
                session.CommandText = """
                    INSERT INTO Playlists (id, name, hidden, date_created, date_modified)
                    VALUES ($id, $name, $hidden, $created, $modified);
                    """;
                session.Parameters.AddWithValue("$id", sessionId);
                session.Parameters.AddWithValue("$name", name);
                session.Parameters.AddWithValue("$hidden", hidden);
                session.Parameters.AddWithValue("$created", rows.FirstOrDefault().PlayedAt ?? name);
                session.Parameters.AddWithValue("$modified", rows.LastOrDefault().PlayedAt ?? name);
                session.ExecuteNonQuery();
            }

            for (var index = 0; index < rows.Length; index++)
            {
                var trackId = nextTrackId++;
                var row = rows[index];
                using var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO track_locations (id, location) VALUES ($trackId, $location);
                    INSERT INTO library (id, title, artist, location) VALUES ($trackId, $title, $artist, $trackId);
                    INSERT INTO PlaylistTracks (id, playlist_id, track_id, position, pl_datetime_added)
                    VALUES ($trackId, $sessionId, $trackId, $position, $playedAt);
                    """;
                insert.Parameters.AddWithValue("$trackId", trackId);
                insert.Parameters.AddWithValue("$sessionId", sessionId);
                insert.Parameters.AddWithValue("$position", index + 1);
                insert.Parameters.AddWithValue("$title", row.Title);
                insert.Parameters.AddWithValue("$artist", row.Artist);
                insert.Parameters.AddWithValue("$location", row.Location);
                insert.Parameters.AddWithValue("$playedAt", row.PlayedAt);
                insert.ExecuteNonQuery();
            }
        }

        public void CreateUnsupportedSchema()
            => Execute("CREATE TABLE something_else (id INTEGER PRIMARY KEY);");

        private SqliteConnection Open()
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString());
            connection.Open();
            return connection;
        }

        private void Execute(string sql)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
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
