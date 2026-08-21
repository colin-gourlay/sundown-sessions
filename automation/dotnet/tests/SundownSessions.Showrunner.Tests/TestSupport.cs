using Microsoft.EntityFrameworkCore;
using SundownSessions.Showrunner.Persistence;

namespace SundownSessions.Showrunner.Tests;

internal sealed class TestClock(DateTimeOffset utcNow) : IShowrunnerClock
{
    public DateTimeOffset UtcNow { get; private set; } = utcNow;

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

internal sealed class SqliteTestHarness : IDisposable
{
    private readonly string directoryPath;

    public SqliteTestHarness()
    {
        directoryPath = Path.Combine(Path.GetTempPath(), "sundown-showrunner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        DatabasePath = Path.Combine(directoryPath, "showrunner.db");
    }

    public string DatabasePath { get; }

    public ShowrunnerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ShowrunnerDbContext>()
            .UseSqlite($"Data Source={DatabasePath}")
            .Options;

        var context = new ShowrunnerDbContext(options);
        context.Database.Migrate();
        return context;
    }

    public void Dispose()
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
