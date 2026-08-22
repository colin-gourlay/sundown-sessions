namespace Sundown.Showrunner.Domain.Entities;

public sealed class PlayHistory
{
    public int Id { get; init; }
    public int RecordingId { get; init; }
    public int ShowId { get; init; }
    public DateOnly BroadcastDate { get; init; }
    public string? ArtistName { get; init; }
    public string? TrackTitle { get; init; }
}
