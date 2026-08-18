using Microsoft.Data.Sqlite;
using Sundown.Showrunner.Domain.Entities;
using Sundown.Showrunner.Domain.Repositories;
using Sundown.Showrunner.Infrastructure.Persistence;

namespace Sundown.Showrunner.Infrastructure.Repositories;

public sealed class SqliteShowRepository : IShowRepository
{
    private readonly ShowrunnerDatabase _db;

    public SqliteShowRepository(ShowrunnerDatabase db)
    {
        _db = db;
    }

    public async Task<Show?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT Id, BroadcastDate, Title, Status FROM Shows WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var show = ReadShow(reader);
        await reader.CloseAsync();

        return show with { Slots = await GetSlotsAsync(id, cancellationToken) };
    }

    public async Task<Show?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT Id, BroadcastDate, Title, Status FROM Shows WHERE BroadcastDate = $date";
        cmd.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var show = ReadShow(reader);
        await reader.CloseAsync();

        return show with { Slots = await GetSlotsAsync(show.Id, cancellationToken) };
    }

    public async Task<Show> SaveAsync(Show show, CancellationToken cancellationToken = default)
    {
        if (show.Id == 0)
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Shows (BroadcastDate, Title, Status)
                VALUES ($date, $title, $status)
                RETURNING Id
                """;
            cmd.Parameters.AddWithValue("$date", show.BroadcastDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$title", show.Title);
            cmd.Parameters.AddWithValue("$status", show.Status.ToString());

            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
            show = show with { Id = newId };
        }
        else
        {
            await using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Shows (Id, BroadcastDate, Title, Status)
                VALUES ($id, $date, $title, $status)
                ON CONFLICT(Id) DO UPDATE SET
                    BroadcastDate = excluded.BroadcastDate,
                    Title = excluded.Title,
                    Status = excluded.Status
                """;
            cmd.Parameters.AddWithValue("$id", show.Id);
            cmd.Parameters.AddWithValue("$date", show.BroadcastDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$title", show.Title);
            cmd.Parameters.AddWithValue("$status", show.Status.ToString());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await SaveSlotsAsync(show.Id, show.Slots, cancellationToken);
        return show;
    }

    private async Task<IReadOnlyList<ShowSlot>> GetSlotsAsync(int showId, CancellationToken cancellationToken)
    {
        await using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT Position, RecordingId, ArtistName, TrackTitle, AlbumTitle FROM ShowSlots WHERE ShowId = $showId ORDER BY Position";
        cmd.Parameters.AddWithValue("$showId", showId);

        var slots = new List<ShowSlot>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            slots.Add(new ShowSlot
            {
                Position = reader.GetInt32(0),
                RecordingId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                ArtistName = reader.IsDBNull(2) ? null : reader.GetString(2),
                TrackTitle = reader.IsDBNull(3) ? null : reader.GetString(3),
                AlbumTitle = reader.IsDBNull(4) ? null : reader.GetString(4),
            });
        }
        return slots;
    }

    private async Task SaveSlotsAsync(int showId, IReadOnlyList<ShowSlot> slots, CancellationToken cancellationToken)
    {
        await using var deleteCmd = _db.Connection.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM ShowSlots WHERE ShowId = $showId";
        deleteCmd.Parameters.AddWithValue("$showId", showId);
        await deleteCmd.ExecuteNonQueryAsync(cancellationToken);

        foreach (var slot in slots)
        {
            await using var insertCmd = _db.Connection.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO ShowSlots (ShowId, Position, RecordingId, ArtistName, TrackTitle, AlbumTitle)
                VALUES ($showId, $position, $recordingId, $artistName, $trackTitle, $albumTitle)
                """;
            insertCmd.Parameters.AddWithValue("$showId", showId);
            insertCmd.Parameters.AddWithValue("$position", slot.Position);
            insertCmd.Parameters.AddWithValue("$recordingId", (object?)slot.RecordingId ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("$artistName", (object?)slot.ArtistName ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("$trackTitle", (object?)slot.TrackTitle ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("$albumTitle", (object?)slot.AlbumTitle ?? DBNull.Value);
            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static Show ReadShow(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        BroadcastDate = DateOnly.ParseExact(reader.GetString(1), "yyyy-MM-dd"),
        Title = reader.GetString(2),
        Status = Enum.Parse<ShowStatus>(reader.GetString(3)),
    };
}
