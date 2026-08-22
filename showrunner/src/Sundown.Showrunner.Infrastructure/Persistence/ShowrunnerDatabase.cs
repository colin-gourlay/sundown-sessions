using Microsoft.Data.Sqlite;

namespace Sundown.Showrunner.Infrastructure.Persistence;

public sealed class ShowrunnerDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public ShowrunnerDatabase(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        EnsureSchema();
    }

    public SqliteConnection Connection => _connection;

    private void EnsureSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Shows (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BroadcastDate TEXT NOT NULL,
                Title TEXT NOT NULL,
                Status TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ShowSlots (
                ShowId INTEGER NOT NULL,
                Position INTEGER NOT NULL,
                RecordingId INTEGER,
                ArtistName TEXT,
                TrackTitle TEXT,
                AlbumTitle TEXT,
                PRIMARY KEY (ShowId, Position),
                FOREIGN KEY (ShowId) REFERENCES Shows(Id)
            );

            CREATE TABLE IF NOT EXISTS Recordings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ArtistName TEXT NOT NULL,
                TrackTitle TEXT NOT NULL,
                AlbumTitle TEXT,
                Isrc TEXT,
                LocalFilePath TEXT
            );

            CREATE TABLE IF NOT EXISTS PlayHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RecordingId INTEGER NOT NULL,
                ShowId INTEGER NOT NULL,
                BroadcastDate TEXT NOT NULL,
                ArtistName TEXT,
                TrackTitle TEXT,
                FOREIGN KEY (RecordingId) REFERENCES Recordings(Id),
                FOREIGN KEY (ShowId) REFERENCES Shows(Id)
            );

            CREATE TABLE IF NOT EXISTS RepeatExceptions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RecordingId INTEGER NOT NULL,
                ShowId INTEGER NOT NULL,
                Reason TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UNIQUE (RecordingId, ShowId),
                FOREIGN KEY (RecordingId) REFERENCES Recordings(Id),
                FOREIGN KEY (ShowId) REFERENCES Shows(Id)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
