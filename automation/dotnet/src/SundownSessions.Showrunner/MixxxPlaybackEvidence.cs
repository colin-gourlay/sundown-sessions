using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SundownSessions.Showrunner;

public interface IMixxxPlaybackEvidenceReader
{
    Task<ApplicationResult<MixxxPlaybackReadModel>> ReadPlaybackEvidenceAsync(
        DateOnly showDate,
        CancellationToken cancellationToken = default);
}

public sealed record MixxxPlaybackReadModel(
    bool IsIncomplete,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<MixxxPlaybackCandidateModel> Candidates,
    string? HistorySessionName = null,
    IReadOnlyList<MixxxHistorySessionSummaryModel>? CandidateSessions = null)
{
    public IReadOnlyList<MixxxHistorySessionSummaryModel> Sessions { get; } = CandidateSessions ?? [];
}

public sealed record MixxxPlaybackCandidateModel(
    string? Title,
    string? Artist,
    DateTimeOffset? PlayedAtUtc,
    string? FileLocation = null);

public sealed class SqliteMixxxPlaybackEvidenceReader(string? databasePath = null) : IMixxxPlaybackEvidenceReader
{
    public const string MixxxDatabasePathEnvironmentVariable = "SUNDOWN_SHOWRUNNER_MIXXX_DB_PATH";
    private const int MixxxHistoryPlaylistType = 2;
    private readonly string resolvedDatabasePath = ResolveDatabasePath(databasePath);

    public async Task<ApplicationResult<MixxxPlaybackReadModel>> ReadPlaybackEvidenceAsync(
        DateOnly showDate,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(resolvedDatabasePath))
        {
            return Incomplete("mixxx_database_unavailable");
        }

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = resolvedDatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                DefaultTimeout = 5,
            }.ToString();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using (var queryOnly = connection.CreateCommand())
            {
                queryOnly.CommandText = "PRAGMA query_only = 1;";
                await queryOnly.ExecuteNonQueryAsync(cancellationToken);
            }

            var tables = await LoadTableColumnsAsync(connection, cancellationToken);
            if (!TryResolveOfficialSchema(tables, out var schema))
            {
                return Incomplete("mixxx_schema_unsupported");
            }

            var sessions = await ReadHistorySessionsAsync(connection, schema, cancellationToken);
            var matchingSessions = sessions.Where(session => session.Matches(showDate)).ToArray();
            if (matchingSessions.Length == 0)
            {
                return Incomplete(
                    "mixxx_history_session_not_found",
                    sessions.TakeLast(5).Select(item => item.Summary).ToArray());
            }

            if (matchingSessions.Length > 1)
            {
                return Incomplete(
                    "mixxx_multiple_history_sessions",
                    matchingSessions.Select(item => item.Summary).ToArray());
            }

            var selected = matchingSessions[0];
            var candidates = await ReadHistorySessionRowsAsync(connection, schema, selected.Id, cancellationToken);
            if (candidates.Count == 0)
            {
                return ApplicationResult<MixxxPlaybackReadModel>.Success(new MixxxPlaybackReadModel(
                    true,
                    ["mixxx_history_empty"],
                    [],
                    selected.Summary.Name,
                    [selected.Summary]));
            }

            var warnings = candidates.Any(item => item.PlayedAtUtc is null)
                ? new[] { "mixxx_timestamp_unavailable" }
                : [];
            return ApplicationResult<MixxxPlaybackReadModel>.Success(new MixxxPlaybackReadModel(
                false,
                warnings,
                candidates,
                selected.Summary.Name,
                [selected.Summary]));
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Incomplete("mixxx_read_failed");
        }
    }

    private static ApplicationResult<MixxxPlaybackReadModel> Incomplete(
        string warning,
        IReadOnlyList<MixxxHistorySessionSummaryModel>? sessions = null)
        => ApplicationResult<MixxxPlaybackReadModel>.Success(
            new MixxxPlaybackReadModel(true, [warning], [], CandidateSessions: sessions));

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
            tables[tableReader.GetString(0)] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

    private static bool TryResolveOfficialSchema(
        IReadOnlyDictionary<string, HashSet<string>> tables,
        out MixxxSchema schema)
    {
        schema = default!;
        if (!TryGetTable(tables, "Playlists", out var playlists) ||
            !TryGetTable(tables, "PlaylistTracks", out var playlistTracks) ||
            !TryGetTable(tables, "library", out var library))
        {
            return false;
        }

        var playlistColumns = tables[playlists];
        var playlistTrackColumns = tables[playlistTracks];
        var libraryColumns = tables[library];
        if (!HasColumns(playlistColumns, "id", "name", "hidden") ||
            !HasColumns(playlistTrackColumns, "id", "playlist_id", "track_id", "position", "pl_datetime_added") ||
            !HasColumns(libraryColumns, "id", "title"))
        {
            return false;
        }

        var trackLocations = TryGetTable(tables, "track_locations", out var resolvedTrackLocations) &&
                             HasColumns(tables[resolvedTrackLocations], "id", "location") &&
                             libraryColumns.Contains("location")
            ? resolvedTrackLocations
            : null;
        schema = new MixxxSchema(
            playlists,
            playlistTracks,
            library,
            trackLocations,
            playlistColumns.Contains("date_created"),
            playlistColumns.Contains("date_modified"),
            libraryColumns.Contains("artist"));
        return true;
    }

    private static async Task<IReadOnlyList<HistorySession>> ReadHistorySessionsAsync(
        SqliteConnection connection,
        MixxxSchema schema,
        CancellationToken cancellationToken)
    {
        var createdSelection = schema.HasDateCreated ? "p.date_created" : "NULL";
        var modifiedSelection = schema.HasDateModified ? "p.date_modified" : "NULL";
        var sql = $"""
            SELECT p.id,
                   p.name,
                   {createdSelection} AS date_created,
                   {modifiedSelection} AS date_modified,
                   COUNT(pt.id) AS track_count,
                   MIN(pt.pl_datetime_added) AS first_played,
                   MAX(pt.pl_datetime_added) AS last_played
            FROM {QuoteIdentifier(schema.Playlists)} p
            LEFT JOIN {QuoteIdentifier(schema.PlaylistTracks)} pt ON pt.playlist_id = p.id
            WHERE p.hidden = $historyType
            GROUP BY p.id, p.name, date_created, date_modified
            ORDER BY p.id;
            """;
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$historyType", MixxxHistoryPlaylistType);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var sessions = new List<HistorySession>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.IsDBNull(1) ? "(unnamed Mixxx history)" : reader.GetString(1).Trim();
            var created = reader.IsDBNull(2) ? null : reader.GetValue(2);
            var modified = reader.IsDBNull(3) ? null : reader.GetValue(3);
            var firstPlayed = reader.IsDBNull(5) ? null : reader.GetValue(5);
            var lastPlayed = reader.IsDBNull(6) ? null : reader.GetValue(6);
            var startedAt = ParseTimestamp(firstPlayed) ?? ParseTimestamp(created);
            var endedAt = ParseTimestamp(lastPlayed) ?? ParseTimestamp(modified);
            sessions.Add(new HistorySession(
                reader.GetInt64(0),
                new MixxxHistorySessionSummaryModel(name, startedAt, endedAt, reader.GetInt32(4)),
                [name, created, modified, firstPlayed, lastPlayed]));
        }

        return sessions;
    }

    private static async Task<IReadOnlyList<MixxxPlaybackCandidateModel>> ReadHistorySessionRowsAsync(
        SqliteConnection connection,
        MixxxSchema schema,
        long playlistId,
        CancellationToken cancellationToken)
    {
        var artistSelection = schema.HasArtist ? "l.artist" : "NULL";
        var locationSelection = schema.TrackLocations is null ? "NULL" : "tl.location";
        var locationJoin = schema.TrackLocations is null
            ? string.Empty
            : $"LEFT JOIN {QuoteIdentifier(schema.TrackLocations)} tl ON tl.id = l.location";
        var sql = $"""
            SELECT l.title,
                   {artistSelection} AS artist,
                   pt.pl_datetime_added,
                   {locationSelection} AS file_location
            FROM {QuoteIdentifier(schema.PlaylistTracks)} pt
            INNER JOIN {QuoteIdentifier(schema.Library)} l ON l.id = pt.track_id
            {locationJoin}
            WHERE pt.playlist_id = $playlistId
            ORDER BY pt.position, pt.id;
            """;
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$playlistId", playlistId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<MixxxPlaybackCandidateModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new MixxxPlaybackCandidateModel(
                reader.IsDBNull(0) ? null : Clean(reader.GetString(0)),
                reader.IsDBNull(1) ? null : Clean(reader.GetString(1)),
                reader.IsDBNull(2) ? null : ParseTimestamp(reader.GetValue(2)),
                reader.IsDBNull(3) ? null : Clean(reader.GetString(3))));
        }

        return rows;
    }

    private static bool TryGetTable(
        IReadOnlyDictionary<string, HashSet<string>> tables,
        string desiredName,
        out string tableName)
    {
        tableName = tables.Keys.FirstOrDefault(item => string.Equals(item, desiredName, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return tableName.Length > 0;
    }

    private static bool HasColumns(HashSet<string> columns, params string[] required)
        => required.All(columns.Contains);

    private static string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ResolveDatabasePath(string? configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Environment.GetEnvironmentVariable(MixxxDatabasePathEnvironmentVariable)
            : configuredPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            return Path.GetFullPath(path.Trim());
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mixxx",
            "mixxxdb.sqlite");
    }

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
            try
            {
                return FromUnix(Convert.ToInt64(Math.Round(doubleValue, MidpointRounding.AwayFromZero)));
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return FromUnix(integer);
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var timestamp)
            ? timestamp.ToUniversalTime()
            : null;
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

    private sealed record MixxxSchema(
        string Playlists,
        string PlaylistTracks,
        string Library,
        string? TrackLocations,
        bool HasDateCreated,
        bool HasDateModified,
        bool HasArtist);

    private sealed record HistorySession(
        long Id,
        MixxxHistorySessionSummaryModel Summary,
        IReadOnlyList<object?> DateEvidence)
    {
        public bool Matches(DateOnly showDate)
        {
            var expected = showDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return DateEvidence.Any(value =>
            {
                var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && text.StartsWith(expected, StringComparison.Ordinal))
                {
                    return true;
                }

                var parsed = ParseTimestamp(value);
                return parsed.HasValue && DateOnly.FromDateTime(parsed.Value.LocalDateTime) == showDate;
            });
        }
    }
}
