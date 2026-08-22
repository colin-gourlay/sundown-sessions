namespace Sundown.Showrunner.Domain.Entities;

public sealed record RepeatException
{
    public int Id { get; init; }
    public int RecordingId { get; init; }
    public int ShowId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}
