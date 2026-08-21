using Microsoft.Data.Sqlite;

namespace SundownSessions.Showrunner.Tests;

public sealed class MixxxPlaybackEvidenceReaderTests
{
    [Fact]
    public async Task ReaderReturnsPlayableCandidatesFromSupportedSchema()
    {
        using var fixture = new MixxxSqliteFixture();
        fixture.SeedPlayHistory(
            ("Track 1", "Artist 1", "2026-08-21T18:00:00Z"),
            ("Track 2", "Artist 2", "2026-08-21T18:04:00Z"));
        var reader = new SqliteMixxxPlaybackEvidenceReader(fixture.DatabasePath);

        var result = await reader.ReadPlaybackEvidenceAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsIncomplete);
        Assert.Equal(2, result.Value.Candidates.Count);
        Assert.Equal("Track 1", result.Value.Candidates[0].Title);
        Assert.Equal("Artist 2", result.Value.Candidates[1].Artist);
    }

    [Fact]
    public async Task ReaderTreatsUnsupportedSchemaAsIncompleteEvidence()
    {
        using var fixture = new MixxxSqliteFixture();
        fixture.CreateUnsupportedSchema();
        var reader = new SqliteMixxxPlaybackEvidenceReader(fixture.DatabasePath);

        var result = await reader.ReadPlaybackEvidenceAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsIncomplete);
        Assert.Contains("mixxx_schema_unsupported", result.Value.Warnings);
    }

    [Fact]
    public async Task ReaderRunsInReadOnlyMode()
    {
        using var fixture = new MixxxSqliteFixture();
        fixture.SeedPlayHistory(("Read only", "Artist", "2026-08-21T19:00:00Z"));
        File.SetLastWriteTimeUtc(fixture.DatabasePath, new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc));
        var before = File.GetLastWriteTimeUtc(fixture.DatabasePath);
        var reader = new SqliteMixxxPlaybackEvidenceReader(fixture.DatabasePath);

        _ = await reader.ReadPlaybackEvidenceAsync();

        var after = File.GetLastWriteTimeUtc(fixture.DatabasePath);
        Assert.Equal(before, after);
    }

    private sealed class MixxxSqliteFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "mixxx-reader-tests", Guid.NewGuid().ToString("N"));

        public MixxxSqliteFixture()
        {
            Directory.CreateDirectory(root);
            DatabasePath = Path.Combine(root, "mixxxdb.sqlite");
        }

        public string DatabasePath { get; }

        public void SeedPlayHistory(params (string Title, string Artist, string PlayedAt)[] rows)
        {
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
                """;
            command.ExecuteNonQuery();

            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                using var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO library (id, title, artist) VALUES ($id, $title, $artist);
                    INSERT INTO play_history (track_id, played_at) VALUES ($id, $playedAt);
                    """;
                insert.Parameters.AddWithValue("$id", index + 1);
                insert.Parameters.AddWithValue("$title", row.Title);
                insert.Parameters.AddWithValue("$artist", row.Artist);
                insert.Parameters.AddWithValue("$playedAt", row.PlayedAt);
                insert.ExecuteNonQuery();
            }
        }

        public void CreateUnsupportedSchema()
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """CREATE TABLE something_else (id INTEGER PRIMARY KEY);""";
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
