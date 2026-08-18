using Microsoft.Data.Sqlite;
using Sundown.Showrunner.Domain.Entities;
using Sundown.Showrunner.Domain.Repositories;
using Sundown.Showrunner.Infrastructure.Persistence;

namespace Sundown.Showrunner.Infrastructure.Repositories;

public sealed class SqliteRecordingRepository : IRecordingRepository
{
    private readonly ShowrunnerDatabase _db;

    public SqliteRecordingRepository(ShowrunnerDatabase db)
    {
        _db = db;
    }

    public async Task<Recording?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT Id, ArtistName, TrackTitle, AlbumTitle, Isrc, LocalFilePath FROM Recordings WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return ReadRecording(reader);
    }

    public async Task<IReadOnlyList<Recording>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        await using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ArtistName, TrackTitle, AlbumTitle, Isrc, LocalFilePath
            FROM Recordings
            WHERE lower(ArtistName) LIKE lower($query)
               OR lower(TrackTitle) LIKE lower($query)
               OR lower(AlbumTitle) LIKE lower($query)
            ORDER BY ArtistName, TrackTitle
            LIMIT 20
            """;
        cmd.Parameters.AddWithValue("$query", $"%{query}%");

        var results = new List<Recording>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(ReadRecording(reader));

        return results;
    }

    public async Task<IReadOnlyList<PlayHistory>> GetHistoryAsync(int recordingId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, RecordingId, ShowId, BroadcastDate, ArtistName, TrackTitle
            FROM PlayHistory
            WHERE RecordingId = $recordingId
            ORDER BY BroadcastDate DESC
            """;
        cmd.Parameters.AddWithValue("$recordingId", recordingId);

        var results = new List<PlayHistory>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PlayHistory
            {
                Id = reader.GetInt32(0),
                RecordingId = reader.GetInt32(1),
                ShowId = reader.GetInt32(2),
                BroadcastDate = DateOnly.ParseExact(reader.GetString(3), "yyyy-MM-dd"),
                ArtistName = reader.IsDBNull(4) ? null : reader.GetString(4),
                TrackTitle = reader.IsDBNull(5) ? null : reader.GetString(5),
            });
        }
        return results;
    }

    public async Task<Recording> SaveAsync(Recording recording, CancellationToken cancellationToken = default)
    {
        if (recording.Id == 0)
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Recordings (ArtistName, TrackTitle, AlbumTitle, Isrc, LocalFilePath)
                VALUES ($artistName, $trackTitle, $albumTitle, $isrc, $localFilePath)
                RETURNING Id
                """;
            cmd.Parameters.AddWithValue("$artistName", recording.ArtistName);
            cmd.Parameters.AddWithValue("$trackTitle", recording.TrackTitle);
            cmd.Parameters.AddWithValue("$albumTitle", (object?)recording.AlbumTitle ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$isrc", (object?)recording.Isrc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$localFilePath", (object?)recording.LocalFilePath ?? DBNull.Value);

            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
            return recording with { Id = newId };
        }
        else
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Recordings (Id, ArtistName, TrackTitle, AlbumTitle, Isrc, LocalFilePath)
                VALUES ($id, $artistName, $trackTitle, $albumTitle, $isrc, $localFilePath)
                ON CONFLICT(Id) DO UPDATE SET
                    ArtistName = excluded.ArtistName,
                    TrackTitle = excluded.TrackTitle,
                    AlbumTitle = excluded.AlbumTitle,
                    Isrc = excluded.Isrc,
                    LocalFilePath = excluded.LocalFilePath
                """;
            cmd.Parameters.AddWithValue("$id", recording.Id);
            cmd.Parameters.AddWithValue("$artistName", recording.ArtistName);
            cmd.Parameters.AddWithValue("$trackTitle", recording.TrackTitle);
            cmd.Parameters.AddWithValue("$albumTitle", (object?)recording.AlbumTitle ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$isrc", (object?)recording.Isrc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$localFilePath", (object?)recording.LocalFilePath ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return recording;
        }
    }

    private static Recording ReadRecording(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        ArtistName = reader.GetString(1),
        TrackTitle = reader.GetString(2),
        AlbumTitle = reader.IsDBNull(3) ? null : reader.GetString(3),
        Isrc = reader.IsDBNull(4) ? null : reader.GetString(4),
        LocalFilePath = reader.IsDBNull(5) ? null : reader.GetString(5),
    };
}
