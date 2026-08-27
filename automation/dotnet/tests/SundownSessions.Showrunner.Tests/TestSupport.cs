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

    public ShowrunnerDbContext CreateContext(bool migrate = true)
    {
        var options = new DbContextOptionsBuilder<ShowrunnerDbContext>()
            .UseSqlite($"Data Source={DatabasePath}")
            .Options;

        var context = new ShowrunnerDbContext(options);
        if (migrate)
        {
            context.Database.Migrate();
        }

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

internal sealed class EmptyMixxxPlaybackEvidenceReader : IMixxxPlaybackEvidenceReader
{
    public Task<ApplicationResult<MixxxPlaybackReadModel>> ReadPlaybackEvidenceAsync(
        DateOnly showDate,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ApplicationResult<MixxxPlaybackReadModel>.Success(
            new MixxxPlaybackReadModel(false, [], [])));
}

internal static class ShowrunnerTestOperations
{
    public static async Task FinaliseShowAsync(
        ShowrunnerDbContext context,
        Guid showId,
        IReadOnlyCollection<ConfirmedPlaybackItemCommand> playback,
        IShowrunnerClock? clock = null)
    {
        var service = new ShowReconciliationService(context, new EmptyMixxxPlaybackEvidenceReader(), clock);
        var confirmation = await service.ConfirmReconciliationAsync(
            showId,
            new ConfirmReconciliationCommand(true, false, playback));
        if (!confirmation.IsSuccess)
        {
            throw new InvalidOperationException($"Test setup could not confirm reconciliation: {confirmation.Error!.Code}");
        }

        var finalisation = await service.FinaliseReconciliationAsync(showId);
        if (!finalisation.IsSuccess)
        {
            throw new InvalidOperationException($"Test setup could not finalise reconciliation: {finalisation.Error!.Code}");
        }
    }
}
