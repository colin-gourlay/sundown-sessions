using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SundownSessions.Showrunner.Tests;

public sealed class ShowrunnerMigrationTests
{
    [Fact]
    public async Task FinalisationHistoryMigrationBackfillsAuthoritativePlannedOrder()
    {
        using var harness = new SqliteTestHarness();
        await using var context = harness.CreateContext(migrate: false);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20260821164532_AddOperatorConfirmedPlayback");

        var showId = Guid.NewGuid();
        var firstRecordingId = Guid.NewGuid();
        var secondRecordingId = Guid.NewGuid();
        var firstPlanId = Guid.NewGuid();
        var secondPlanId = Guid.NewGuid();
        var firstHistoryId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var secondHistoryId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Recordings" ("Id", "Title", "Artist", "ReleaseTitle", "Notes", "CreatedAtUtc")
            VALUES ({firstRecordingId}, 'First', NULL, NULL, NULL, '2026-08-01 00:00:00+00:00'),
                   ({secondRecordingId}, 'Second', NULL, NULL, NULL, '2026-08-01 00:00:00+00:00');
            """);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Shows" ("Id", "Slug", "Title", "ShowDate", "CreatedAtUtc")
            VALUES ({showId}, 'migration-order', 'Migration order', '2026-08-01', '2026-08-01 00:00:00+00:00');
            """);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "PlannedRecordings" ("Id", "ShowId", "RecordingId", "Position", "Notes", "CreatedAtUtc")
            VALUES ({firstPlanId}, {showId}, {firstRecordingId}, 1, NULL, '2026-08-01 00:00:00+00:00'),
                   ({secondPlanId}, {showId}, {secondRecordingId}, 2, NULL, '2026-08-01 00:00:00+00:00');
            """);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "BroadcastRecordings" ("Id", "ShowId", "RecordingId", "PlannedRecordingId", "BroadcastAtUtc")
            VALUES ({firstHistoryId}, {showId}, {firstRecordingId}, {firstPlanId}, '2026-08-01 20:00:00+00:00'),
                   ({secondHistoryId}, {showId}, {secondRecordingId}, {secondPlanId}, '2026-08-01 20:00:00+00:00');
            """);

        await migrator.MigrateAsync();

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT RecordingId, Position FROM BroadcastRecordings";
        await context.Database.OpenConnectionAsync();
        await using var reader = await command.ExecuteReaderAsync();
        var positions = new Dictionary<Guid, int>();
        while (await reader.ReadAsync())
        {
            positions.Add(reader.GetGuid(0), reader.GetInt32(1));
        }

        Assert.Equal(1, positions[firstRecordingId]);
        Assert.Equal(2, positions[secondRecordingId]);
    }
}
