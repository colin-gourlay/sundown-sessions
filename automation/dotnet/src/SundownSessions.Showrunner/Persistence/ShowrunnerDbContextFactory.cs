using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SundownSessions.Showrunner.Persistence;

public sealed class ShowrunnerDbContextFactory : IDesignTimeDbContextFactory<ShowrunnerDbContext>
{
    public const string DatabasePathEnvironmentVariable = "SUNDOWN_SHOWRUNNER_DB_PATH";

    public ShowrunnerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ShowrunnerDbContext>();
        optionsBuilder.UseSqlite($"Data Source={ResolveDatabasePath()}");
        return new ShowrunnerDbContext(optionsBuilder.Options);
    }

    public static string ResolveDatabasePath()
    {
        var configuredPath = Environment.GetEnvironmentVariable(DatabasePathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(baseDirectory, "sundown-sessions", "showrunner");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "showrunner.db");
    }
}
