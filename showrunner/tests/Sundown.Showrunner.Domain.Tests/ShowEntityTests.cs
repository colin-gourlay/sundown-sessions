using Sundown.Showrunner.Domain.Entities;
using Xunit;

namespace Sundown.Showrunner.Domain.Tests;

public class ShowEntityTests
{
    [Fact]
    public void Show_WithSlots_ReturnsSlots()
    {
        var show = new Show
        {
            Id = 1,
            BroadcastDate = new DateOnly(2026, 6, 10),
            Title = "Episode 42",
            Status = ShowStatus.Planned,
            Slots =
            [
                new ShowSlot { Position = 1, ArtistName = "The Cure", TrackTitle = "Lovesong" },
                new ShowSlot { Position = 2, ArtistName = "Radiohead", TrackTitle = "Creep" },
            ],
        };

        Assert.Equal(2, show.Slots.Count);
        Assert.Equal("The Cure", show.Slots[0].ArtistName);
    }

    [Fact]
    public void Show_WithExpression_ProducesUpdatedRecord()
    {
        var original = new Show
        {
            Id = 1,
            BroadcastDate = new DateOnly(2026, 6, 10),
            Title = "Episode 42",
            Status = ShowStatus.Planned,
        };

        var prepared = original with { Status = ShowStatus.Prepared };

        Assert.Equal(ShowStatus.Prepared, prepared.Status);
        Assert.Equal(ShowStatus.Planned, original.Status);
        Assert.Equal(original.Id, prepared.Id);
    }

    [Fact]
    public void Recording_WithExpression_PreservesFields()
    {
        var recording = new Recording
        {
            Id = 1,
            ArtistName = "Massive Attack",
            TrackTitle = "Teardrop",
            AlbumTitle = "Mezzanine",
        };

        var withFile = recording with { LocalFilePath = "/music/teardrop.flac" };

        Assert.Equal("Massive Attack", withFile.ArtistName);
        Assert.Equal("/music/teardrop.flac", withFile.LocalFilePath);
        Assert.Null(recording.LocalFilePath);
    }

    [Fact]
    public void RepeatException_StoresReason()
    {
        var exception = new RepeatException
        {
            Id = 1,
            RecordingId = 10,
            ShowId = 5,
            Reason = "Classic track re-requested by listeners",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        Assert.Equal("Classic track re-requested by listeners", exception.Reason);
    }
}
