using Sundown.Showrunner.Domain.Entities;
using Sundown.Showrunner.Infrastructure.Persistence;
using Sundown.Showrunner.Infrastructure.Repositories;
using Xunit;

namespace Sundown.Showrunner.Application.Tests;

public class SqliteRepositoryTests : IDisposable
{
    private readonly ShowrunnerDatabase _db;
    private readonly SqliteShowRepository _showRepo;
    private readonly SqliteRecordingRepository _recordingRepo;
    private readonly SqliteRepeatExceptionRepository _repeatExceptionRepo;

    public SqliteRepositoryTests()
    {
        _db = new ShowrunnerDatabase("Data Source=:memory:");
        _showRepo = new SqliteShowRepository(_db);
        _recordingRepo = new SqliteRecordingRepository(_db);
        _repeatExceptionRepo = new SqliteRepeatExceptionRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Show_SaveAndRetrieveById_RoundTrips()
    {
        var show = new Show
        {
            Id = 0,
            BroadcastDate = new DateOnly(2026, 9, 1),
            Title = "Episode 99",
            Status = ShowStatus.Planned,
            Slots = [new ShowSlot { Position = 1, ArtistName = "Air", TrackTitle = "Sexy Boy" }],
        };

        var saved = await _showRepo.SaveAsync(show);
        Assert.NotEqual(0, saved.Id);

        var retrieved = await _showRepo.GetByIdAsync(saved.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Episode 99", retrieved.Title);
        Assert.Single(retrieved.Slots);
        Assert.Equal("Air", retrieved.Slots[0].ArtistName);
    }

    [Fact]
    public async Task Show_GetByDate_ReturnsCorrectShow()
    {
        var date = new DateOnly(2026, 10, 15);
        var show = new Show { Id = 0, BroadcastDate = date, Title = "Show X", Status = ShowStatus.Planned };
        await _showRepo.SaveAsync(show);

        var found = await _showRepo.GetByDateAsync(date);
        Assert.NotNull(found);
        Assert.Equal("Show X", found.Title);
    }

    [Fact]
    public async Task Recording_SaveAndSearch_FindsByArtist()
    {
        await _recordingRepo.SaveAsync(new Recording { Id = 0, ArtistName = "Aphex Twin", TrackTitle = "Windowlicker" });
        await _recordingRepo.SaveAsync(new Recording { Id = 0, ArtistName = "Boards of Canada", TrackTitle = "Roygbiv" });

        var results = await _recordingRepo.SearchAsync("aphex");
        Assert.Single(results);
        Assert.Equal("Aphex Twin", results[0].ArtistName);
    }

    [Fact]
    public async Task PlayHistory_AfterSeedingRecording_CanBeQueried()
    {
        var recording = await _recordingRepo.SaveAsync(new Recording { Id = 0, ArtistName = "Arca", TrackTitle = "Piel" });

        var showId = 1;
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Shows (Id, BroadcastDate, Title, Status) VALUES (1, '2025-01-01', 'Old Show', 'Broadcast');
            INSERT INTO PlayHistory (RecordingId, ShowId, BroadcastDate, ArtistName, TrackTitle)
            VALUES ($recId, $showId, '2025-01-01', 'Arca', 'Piel');
            """;
        cmd.Parameters.AddWithValue("$recId", recording.Id);
        cmd.Parameters.AddWithValue("$showId", showId);
        await cmd.ExecuteNonQueryAsync();

        var history = await _recordingRepo.GetHistoryAsync(recording.Id);
        Assert.Single(history);
        Assert.Equal(new DateOnly(2025, 1, 1), history[0].BroadcastDate);
    }

    [Fact]
    public async Task RepeatException_CreateAndRetrieve_RoundTrips()
    {
        var recording = await _recordingRepo.SaveAsync(new Recording { Id = 0, ArtistName = "Burial", TrackTitle = "Archangel" });

        using var showCmd = _db.Connection.CreateCommand();
        showCmd.CommandText = "INSERT INTO Shows (Id, BroadcastDate, Title, Status) VALUES (1, '2026-05-05', 'Show', 'Planned')";
        await showCmd.ExecuteNonQueryAsync();

        var created = await _repeatExceptionRepo.CreateAsync(new RepeatException
        {
            RecordingId = recording.Id,
            ShowId = 1,
            Reason = "Classic",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        Assert.NotEqual(0, created.Id);
        var found = await _repeatExceptionRepo.GetAsync(recording.Id, 1);
        Assert.NotNull(found);
        Assert.Equal("Classic", found.Reason);
    }
}
