namespace Sundown.Showrunner.Domain.Entities;

public sealed record Show
{
    public int Id { get; init; }
    public DateOnly BroadcastDate { get; init; }
    public string Title { get; init; } = string.Empty;
    public ShowStatus Status { get; init; }
    public IReadOnlyList<ShowSlot> Slots { get; init; } = [];
}

public enum ShowStatus
{
    Planned,
    Prepared,
    Broadcast,
    Finalised,
}

public sealed record ShowSlot
{
    public int Position { get; init; }
    public int? RecordingId { get; init; }
    public string? ArtistName { get; init; }
    public string? TrackTitle { get; init; }
    public string? AlbumTitle { get; init; }
}
