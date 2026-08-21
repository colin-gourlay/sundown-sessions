using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SundownSessions.Showrunner;

public interface IMixxxPlaybackEvidenceReader
{
    Task<ApplicationResult<MixxxPlaybackReadModel>> ReadPlaybackEvidenceAsync(CancellationToken cancellationToken = default);
}

public sealed record MixxxPlaybackReadModel(
    bool IsIncomplete,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<MixxxPlaybackCandidateModel> Candidates);

public sealed record MixxxPlaybackCandidateModel(
    string? Title,
    string? Artist,
    DateTimeOffset? PlayedAtUtc);

public sealed class SqliteMixxxPlaybackEvidenceReader(string? databasePath = null) : IMixxxPlaybackEvidenceReader
{
    public const string MixxxDatabasePathEnvironmentVariable = "SUNDOWN_SHOWRUNNER_MIXXX_DB_PATH";
    private readonly string resolvedDatabasePath = ResolveDatabasePath(databasePath);

    public async Task<ApplicationResult<MixxxPlaybackReadModel>> ReadPlaybackEvidenceAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(resolvedDatabasePath))
        {
            return ApplicationResult<MixxxPlaybackReadModel>.Success(
                new MixxxPlaybackReadModel(
                    true,
                    ["mixxx_database_unavailable"],
                    []));
        }

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = resolvedDatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using (var queryOnly = connection.CreateCommand())
            {
                queryOnly.CommandText = "PRAGMA query_only = 1;";
                await queryOnly.ExecuteNonQueryAsync(cancellationToken);
            }

            var tableColumns = await LoadTableColumnsAsync(connection, cancellationToken);
            var warnings = new List<string>();

            var historyRows = await TryReadHistoryListRowsAsync(connection, tableColumns, cancellationToken)
                ?? await TryReadPlayHistoryRowsAsync(connection, tableColumns, cancellationToken);

            if (historyRows is null)
            {
                warnings.Add("mixxx_schema_unsupported");
                return ApplicationResult<MixxxPlaybackReadModel>.Success(
                    new MixxxPlaybackReadModel(
                        true,
                        warnings,
                        []));
            }

            var candidates = historyRows
                .Select(row => new MixxxPlaybackCandidateModel(
                    Clean(row.Title),
                    Clean(row.Artist),
                    ParseTimestamp(row.PlayedAt)))
                .ToArray();

            if (candidates.Length == 0)
            {
                warnings.Add("mixxx_history_empty");
            }

            return ApplicationResult<MixxxPlaybackReadModel>.Success(
                new MixxxPlaybackReadModel(
                    false,
                    warnings,
                    candidates));
        }
        catch (SqliteException)
        {
            return ApplicationResult<MixxxPlaybackReadModel>.Success(
                new MixxxPlaybackReadModel(
                    true,
                    ["mixxx_read_failed"],
                    []));
        }
    }

    private static async Task<Dictionary<string, HashSet<string>>> LoadTableColumnsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var tables = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        await using var tableReader = await tableCommand.ExecuteReaderAsync(cancellationToken);
        while (await tableReader.ReadAsync(cancellationToken))
        {
            var tableName = tableReader.GetString(0);
            tables[tableName] = [];
        }

        foreach (var tableName in tables.Keys.ToArray())
        {
            await using var columnCommand = connection.CreateCommand();
            columnCommand.CommandText = $"PRAGMA table_info({QuoteIdentifier(tableName)});";
            await using var columnReader = await columnCommand.ExecuteReaderAsync(cancellationToken);
            while (await columnReader.ReadAsync(cancellationToken))
            {
                tables[tableName].Add(columnReader.GetString(1));
            }
        }

        return tables;
    }

    private static async Task<List<HistoryRow>?> TryReadHistoryListRowsAsync(
        SqliteConnection connection,
        Dictionary<string, HashSet<string>> tableColumns,
        CancellationToken cancellationToken)
    {
        if (!TryGetTable(tableColumns, "historylist_tracks", out var historyTracksTable) ||
            !TryGetTable(tableColumns, "historylists", out var historyListTable) ||
            !TryGetTable(tableColumns, "library", out var libraryTable))
        {
            return null;
        }

        var historyTracksColumns = tableColumns[historyTracksTable];
        var historyListColumns = tableColumns[historyListTable];
        var libraryColumns = tableColumns[libraryTable];

        if (!TryGetColumn(historyTracksColumns, ["track_id", "trackid", "track"], out var trackIdColumn) ||
            !TryGetColumn(historyTracksColumns, ["historylist_id", "history_id", "historylist"], out var historyListIdColumn) ||
            !TryGetColumn(historyListColumns, ["id", "historylist_id"], out var historyListPrimaryColumn) ||
            !TryGetColumn(historyListColumns, ["datetime_added", "created_at", "timestamp", "played_at"], out var playedAtColumn) ||
            !TryGetColumn(libraryColumns, ["id", "track_id"], out var libraryIdColumn) ||
            !TryGetColumn(libraryColumns, ["title"], out var titleColumn))
        {
            return null;
        }

        var artistColumn = TryGetColumn(libraryColumns, ["artist"], out var artist)
            ? artist
            : null;
        var orderColumn = TryGetColumn(historyTracksColumns, ["position", "track_position", "id"], out var order)
            ? order
            : "rowid";
        var artistSelection = artistColumn is null
            ? "NULL"
            : $"l.{QuoteIdentifier(artistColumn)}";

        var sql = $"""
            SELECT l.{QuoteIdentifier(titleColumn)} AS title,
                   {artistSelection} AS artist,
                   hl.{QuoteIdentifier(playedAtColumn)} AS played_at
            FROM {QuoteIdentifier(historyTracksTable)} hlt
            INNER JOIN {QuoteIdentifier(historyListTable)} hl
                ON hl.{QuoteIdentifier(historyListPrimaryColumn)} = hlt.{QuoteIdentifier(historyListIdColumn)}
            INNER JOIN {QuoteIdentifier(libraryTable)} l
                ON l.{QuoteIdentifier(libraryIdColumn)} = hlt.{QuoteIdentifier(trackIdColumn)}
            ORDER BY hl.{QuoteIdentifier(playedAtColumn)}, hlt.{QuoteIdentifier(orderColumn)};
            """;
        return await ExecuteHistoryQueryAsync(connection, sql, cancellationToken);
    }

    private static async Task<List<HistoryRow>?> TryReadPlayHistoryRowsAsync(
        SqliteConnection connection,
        Dictionary<string, HashSet<string>> tableColumns,
        CancellationToken cancellationToken)
    {
        if (!TryGetTable(tableColumns, "play_history", out var playHistoryTable) ||
            !TryGetTable(tableColumns, "library", out var libraryTable))
        {
            return null;
        }

        var playHistoryColumns = tableColumns[playHistoryTable];
        var libraryColumns = tableColumns[libraryTable];
        if (!TryGetColumn(playHistoryColumns, ["track_id", "trackid", "track"], out var trackIdColumn) ||
            !TryGetColumn(playHistoryColumns, ["played_at", "playedat", "datetime_played", "timestamp"], out var playedAtColumn) ||
            !TryGetColumn(libraryColumns, ["id", "track_id"], out var libraryIdColumn) ||
            !TryGetColumn(libraryColumns, ["title"], out var titleColumn))
        {
            return null;
        }

        var artistColumn = TryGetColumn(libraryColumns, ["artist"], out var artist)
            ? artist
            : null;
        var artistSelection = artistColumn is null
            ? "NULL"
            : $"l.{QuoteIdentifier(artistColumn)}";
        var sql = $"""
            SELECT l.{QuoteIdentifier(titleColumn)} AS title,
                   {artistSelection} AS artist,
                   ph.{QuoteIdentifier(playedAtColumn)} AS played_at
            FROM {QuoteIdentifier(playHistoryTable)} ph
            INNER JOIN {QuoteIdentifier(libraryTable)} l
                ON l.{QuoteIdentifier(libraryIdColumn)} = ph.{QuoteIdentifier(trackIdColumn)}
            ORDER BY ph.{QuoteIdentifier(playedAtColumn)}, ph.rowid;
            """;
        return await ExecuteHistoryQueryAsync(connection, sql, cancellationToken);
    }

    private static async Task<List<HistoryRow>> ExecuteHistoryQueryAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        var rows = new List<HistoryRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new HistoryRow(
                reader.IsDBNull(0) ? null : reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetValue(2)));
        }

        return rows;
    }

    private static string ResolveDatabasePath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(MixxxDatabasePathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".mixxx", "mixxxdb.sqlite");
    }

    private static bool TryGetTable(IReadOnlyDictionary<string, HashSet<string>> tables, string desiredName, out string tableName)
    {
        tableName = tables.Keys.FirstOrDefault(item => string.Equals(item, desiredName, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(tableName);
    }

    private static bool TryGetColumn(HashSet<string> columns, IEnumerable<string> candidates, out string columnName)
    {
        columnName = candidates.FirstOrDefault(candidate => columns.Contains(candidate)) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(columnName);
    }

    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset? ParseTimestamp(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is long longValue)
        {
            return FromUnix(longValue);
        }

        if (value is int intValue)
        {
            return FromUnix(intValue);
        }

        if (value is double doubleValue)
        {
            return FromUnix((long)doubleValue);
        }

        if (value is string text)
        {
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                return FromUnix(integer);
            }

            if (DateTimeOffset.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var utcTimestamp))
            {
                return utcTimestamp;
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var localTimestamp))
            {
                return new DateTimeOffset(localTimestamp).ToUniversalTime();
            }
        }

        return null;
    }

    private static DateTimeOffset? FromUnix(long value)
    {
        try
        {
            return value > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(value)
                : DateTimeOffset.FromUnixTimeSeconds(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private sealed record HistoryRow(string? Title, string? Artist, object? PlayedAt);
}
