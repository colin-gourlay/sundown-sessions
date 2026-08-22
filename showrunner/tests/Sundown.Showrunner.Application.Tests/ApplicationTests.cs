using Sundown.Showrunner.Application.Exceptions;
using Sundown.Showrunner.Application.Queries;
using Sundown.Showrunner.TestHelpers.Fakes;
using Sundown.Showrunner.Domain.Entities;
using Xunit;

namespace Sundown.Showrunner.Application.Tests;

public class ShowGetQueryTests
{
    [Fact]
    public async Task ByIdAsync_KnownShow_ReturnsShowResult()
    {
        var repo = new FakeShowRepository();
        repo.Seed(new Show
        {
            Id = 1,
            BroadcastDate = new DateOnly(2026, 6, 10),
            Title = "Episode 1",
            Status = ShowStatus.Planned,
        });

        var query = new ShowGetQuery(repo);
        var result = await query.ByIdAsync(1);

        Assert.Equal(1, result.Id);
        Assert.Equal("Episode 1", result.Title);
        Assert.Equal("Planned", result.Status);
        Assert.Equal("2026-06-10", result.BroadcastDate);
    }

    [Fact]
    public async Task ByIdAsync_UnknownId_ThrowsShowNotFoundException()
    {
        var repo = new FakeShowRepository();
        var query = new ShowGetQuery(repo);

        await Assert.ThrowsAsync<ShowNotFoundException>(() => query.ByIdAsync(99));
    }

    [Fact]
    public async Task ByDateAsync_KnownDate_ReturnsShow()
    {
        var repo = new FakeShowRepository();
        repo.Seed(new Show
        {
            Id = 2,
            BroadcastDate = new DateOnly(2026, 7, 1),
            Title = "Episode 2",
            Status = ShowStatus.Prepared,
        });

        var query = new ShowGetQuery(repo);
        var result = await query.ByDateAsync(new DateOnly(2026, 7, 1));

        Assert.Equal(2, result.Id);
    }
}

public class RecordingSearchQueryTests
{
    [Fact]
    public async Task ExecuteAsync_MatchingQuery_ReturnsMatches()
    {
        var repo = new FakeRecordingRepository();
        repo.Seed(new Recording { Id = 1, ArtistName = "Massive Attack", TrackTitle = "Teardrop" });
        repo.Seed(new Recording { Id = 2, ArtistName = "Portishead", TrackTitle = "Sour Times" });

        var query = new RecordingSearchQuery(repo);
        var result = await query.ExecuteAsync("massive");

        Assert.Single(result.Matches);
        Assert.Equal("Massive Attack", result.Matches[0].ArtistName);
        Assert.False(result.IsAmbiguous);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleMatches_IsAmbiguous()
    {
        var repo = new FakeRecordingRepository();
        repo.Seed(new Recording { Id = 1, ArtistName = "Massive Attack", TrackTitle = "Teardrop" });
        repo.Seed(new Recording { Id = 2, ArtistName = "Massive Attack", TrackTitle = "Unfinished Sympathy" });

        var query = new RecordingSearchQuery(repo);
        var result = await query.ExecuteAsync("massive attack");

        Assert.Equal(2, result.Matches.Count);
        Assert.True(result.IsAmbiguous);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyQuery_ThrowsDomainRuleException()
    {
        var repo = new FakeRecordingRepository();
        var query = new RecordingSearchQuery(repo);

        await Assert.ThrowsAsync<DomainRuleException>(() => query.ExecuteAsync("   "));
    }
}

public class RecordingHistoryQueryTests
{
    [Fact]
    public async Task ExecuteAsync_NoHistory_ReturnsEmpty()
    {
        var recordingRepo = new FakeRecordingRepository();
        recordingRepo.Seed(new Recording { Id = 1, ArtistName = "Bonobo", TrackTitle = "Kiara" });

        var query = new RecordingHistoryQuery(recordingRepo);
        var result = await query.ExecuteAsync(1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithHistory_ReturnsPreviousPlays()
    {
        var recordingRepo = new FakeRecordingRepository();
        recordingRepo.Seed(new Recording { Id = 1, ArtistName = "Bonobo", TrackTitle = "Kiara" });
        recordingRepo.SeedHistory(new PlayHistory
        {
            Id = 1,
            RecordingId = 1,
            ShowId = 5,
            BroadcastDate = new DateOnly(2025, 3, 15),
        });

        var query = new RecordingHistoryQuery(recordingRepo);
        var result = await query.ExecuteAsync(1);

        Assert.Single(result);
        Assert.Equal("2025-03-15", result[0].BroadcastDate);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownRecording_ThrowsRecordingNotFoundException()
    {
        var repo = new FakeRecordingRepository();
        var query = new RecordingHistoryQuery(repo);

        await Assert.ThrowsAsync<RecordingNotFoundException>(() => query.ExecuteAsync(99));
    }
}

public class ShowPrepareCommandTests
{
    [Fact]
    public async Task ExecuteAsync_NoRepeats_ReturnsEmptyConflicts()
    {
        var showRepo = new FakeShowRepository();
        showRepo.Seed(new Show
        {
            Id = 1,
            BroadcastDate = new DateOnly(2026, 8, 5),
            Title = "Episode 10",
            Status = ShowStatus.Planned,
            Slots = [new ShowSlot { Position = 1, RecordingId = 1, ArtistName = "Röyksopp", TrackTitle = "Remind Me" }],
        });

        var recordingRepo = new FakeRecordingRepository();
        recordingRepo.Seed(new Recording { Id = 1, ArtistName = "Röyksopp", TrackTitle = "Remind Me" });

        var repeatRepo = new FakeRepeatExceptionRepository();

        var cmd = new Commands.ShowPrepareCommand(showRepo, recordingRepo, repeatRepo);
        var result = await cmd.ExecuteAsync(1);

        Assert.Empty(result.RepeatConflicts);
        Assert.Equal("Prepared", result.Show.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WithRepeat_SurfacesConflict()
    {
        var showRepo = new FakeShowRepository();
        showRepo.Seed(new Show
        {
            Id = 1,
            BroadcastDate = new DateOnly(2026, 8, 5),
            Title = "Episode 10",
            Status = ShowStatus.Planned,
            Slots = [new ShowSlot { Position = 1, RecordingId = 1, ArtistName = "Portishead", TrackTitle = "Sour Times" }],
        });

        var recordingRepo = new FakeRecordingRepository();
        recordingRepo.Seed(new Recording { Id = 1, ArtistName = "Portishead", TrackTitle = "Sour Times" });
        recordingRepo.SeedHistory(new PlayHistory
        {
            Id = 1, RecordingId = 1, ShowId = 3,
            BroadcastDate = new DateOnly(2024, 11, 12),
            ArtistName = "Portishead", TrackTitle = "Sour Times",
        });

        var repeatRepo = new FakeRepeatExceptionRepository();
        var cmd = new Commands.ShowPrepareCommand(showRepo, recordingRepo, repeatRepo);

        var result = await cmd.ExecuteAsync(1);

        Assert.Single(result.RepeatConflicts);
        Assert.Equal(1, result.RepeatConflicts[0].RecordingId);
        Assert.False(result.RepeatConflicts[0].HasException);
    }

    [Fact]
    public async Task ExecuteAsync_WithRepeatException_MarksHasException()
    {
        var showRepo = new FakeShowRepository();
        showRepo.Seed(new Show
        {
            Id = 1,
            BroadcastDate = new DateOnly(2026, 8, 5),
            Title = "Episode 10",
            Status = ShowStatus.Planned,
            Slots = [new ShowSlot { Position = 1, RecordingId = 1 }],
        });

        var recordingRepo = new FakeRecordingRepository();
        recordingRepo.Seed(new Recording { Id = 1, ArtistName = "Björk", TrackTitle = "Human Behaviour" });
        recordingRepo.SeedHistory(new PlayHistory
        {
            Id = 1, RecordingId = 1, ShowId = 2,
            BroadcastDate = new DateOnly(2023, 5, 1),
        });

        var repeatRepo = new FakeRepeatExceptionRepository();
        repeatRepo.Seed(new Domain.Entities.RepeatException
        {
            Id = 1, RecordingId = 1, ShowId = 1,
            Reason = "Listener favourite", CreatedAt = DateTimeOffset.UtcNow,
        });

        var cmd = new Commands.ShowPrepareCommand(showRepo, recordingRepo, repeatRepo);
        var result = await cmd.ExecuteAsync(1);

        Assert.Single(result.RepeatConflicts);
        Assert.True(result.RepeatConflicts[0].HasException);
    }

    [Fact]
    public async Task ExecuteAsync_BroadcastShow_ThrowsDomainRuleException()
    {
        var showRepo = new FakeShowRepository();
        showRepo.Seed(new Show
        {
            Id = 1, BroadcastDate = new DateOnly(2025, 1, 1),
            Title = "Old Show", Status = ShowStatus.Broadcast,
        });

        var cmd = new Commands.ShowPrepareCommand(showRepo, new FakeRecordingRepository(), new FakeRepeatExceptionRepository());

        await Assert.ThrowsAsync<Exceptions.DomainRuleException>(() => cmd.ExecuteAsync(1));
    }
}

public class RepeatExceptionCreateCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ValidRequest_CreatesException()
    {
        var showRepo = new FakeShowRepository();
        showRepo.Seed(new Show { Id = 1, BroadcastDate = new DateOnly(2026, 6, 1), Title = "Show", Status = ShowStatus.Prepared });

        var recordingRepo = new FakeRecordingRepository();
        recordingRepo.Seed(new Recording { Id = 1, ArtistName = "Warpaint", TrackTitle = "Undertow" });
        recordingRepo.SeedHistory(new PlayHistory { Id = 1, RecordingId = 1, ShowId = 5, BroadcastDate = new DateOnly(2024, 2, 6) });

        var repeatRepo = new FakeRepeatExceptionRepository();
        var cmd = new Commands.RepeatExceptionCreateCommand(showRepo, recordingRepo, repeatRepo);

        var result = await cmd.ExecuteAsync(1, 1, "Classic track approved by operator");

        Assert.Equal(1, result.RecordingId);
        Assert.Equal("Classic track approved by operator", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyReason_ThrowsDomainRuleException()
    {
        var showRepo = new FakeShowRepository();
        showRepo.Seed(new Show { Id = 1, BroadcastDate = DateOnly.MinValue, Title = "Show", Status = ShowStatus.Planned });
        var recordingRepo = new FakeRecordingRepository();
        recordingRepo.Seed(new Recording { Id = 1, ArtistName = "A", TrackTitle = "B" });

        var cmd = new Commands.RepeatExceptionCreateCommand(showRepo, recordingRepo, new FakeRepeatExceptionRepository());

        await Assert.ThrowsAsync<Exceptions.DomainRuleException>(() => cmd.ExecuteAsync(1, 1, "   "));
    }

    [Fact]
    public async Task ExecuteAsync_RecordingWithNoHistory_ThrowsDomainRuleException()
    {
        var showRepo = new FakeShowRepository();
        showRepo.Seed(new Show { Id = 1, BroadcastDate = DateOnly.MinValue, Title = "Show", Status = ShowStatus.Planned });
        var recordingRepo = new FakeRecordingRepository();
        recordingRepo.Seed(new Recording { Id = 1, ArtistName = "A", TrackTitle = "B" });

        var cmd = new Commands.RepeatExceptionCreateCommand(showRepo, recordingRepo, new FakeRepeatExceptionRepository());

        await Assert.ThrowsAsync<Exceptions.DomainRuleException>(() => cmd.ExecuteAsync(1, 1, "A reason"));
    }
}
