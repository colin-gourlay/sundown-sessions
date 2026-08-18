namespace Sundown.Showrunner.Domain.Entities;

public sealed record Recording
{
    public int Id { get; init; }
    public string ArtistName { get; init; } = string.Empty;
    public string TrackTitle { get; init; } = string.Empty;
    public string? AlbumTitle { get; init; }
    public string? Isrc { get; init; }
    public string? LocalFilePath { get; init; }
}
