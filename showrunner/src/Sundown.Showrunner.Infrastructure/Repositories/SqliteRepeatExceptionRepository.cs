using Microsoft.Data.Sqlite;
using Sundown.Showrunner.Domain.Entities;
using Sundown.Showrunner.Domain.Repositories;
using Sundown.Showrunner.Infrastructure.Persistence;

namespace Sundown.Showrunner.Infrastructure.Repositories;

public sealed class SqliteRepeatExceptionRepository : IRepeatExceptionRepository
{
    private readonly ShowrunnerDatabase _db;

    public SqliteRepeatExceptionRepository(ShowrunnerDatabase db)
    {
        _db = db;
    }

    public async Task<RepeatException?> GetAsync(int recordingId, int showId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, RecordingId, ShowId, Reason, CreatedAt
            FROM RepeatExceptions
            WHERE RecordingId = $recordingId AND ShowId = $showId
            """;
        cmd.Parameters.AddWithValue("$recordingId", recordingId);
        cmd.Parameters.AddWithValue("$showId", showId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new RepeatException
        {
            Id = reader.GetInt32(0),
            RecordingId = reader.GetInt32(1),
            ShowId = reader.GetInt32(2),
            Reason = reader.GetString(3),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(4)),
        };
    }

    public async Task<RepeatException> CreateAsync(RepeatException exception, CancellationToken cancellationToken = default)
    {
        await using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO RepeatExceptions (RecordingId, ShowId, Reason, CreatedAt)
            VALUES ($recordingId, $showId, $reason, $createdAt)
            RETURNING Id
            """;
        cmd.Parameters.AddWithValue("$recordingId", exception.RecordingId);
        cmd.Parameters.AddWithValue("$showId", exception.ShowId);
        cmd.Parameters.AddWithValue("$reason", exception.Reason);
        cmd.Parameters.AddWithValue("$createdAt", exception.CreatedAt.ToString("O"));

        var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
        return exception with { Id = newId };
    }
}
