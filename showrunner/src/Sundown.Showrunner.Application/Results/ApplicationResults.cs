namespace Sundown.Showrunner.Application.Results;

public sealed class ShowResult
{
    public int Id { get; init; }
    public string BroadcastDate { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<ShowSlotResult> Slots { get; init; } = [];
}

public sealed class ShowSlotResult
{
    public int Position { get; init; }
    public int? RecordingId { get; init; }
    public string? ArtistName { get; init; }
    public string? TrackTitle { get; init; }
    public string? AlbumTitle { get; init; }
}

public sealed class RecordingResult
{
    public int Id { get; init; }
    public string ArtistName { get; init; } = string.Empty;
    public string TrackTitle { get; init; } = string.Empty;
    public string? AlbumTitle { get; init; }
    public string? Isrc { get; init; }
    public bool HasLocalFile { get; init; }
}

public sealed class PlayHistoryResult
{
    public int ShowId { get; init; }
    public string BroadcastDate { get; init; } = string.Empty;
    public string ArtistName { get; init; } = string.Empty;
    public string TrackTitle { get; init; } = string.Empty;
}

public sealed class RecordingSearchResult
{
    public IReadOnlyList<RecordingResult> Matches { get; init; } = [];
    public bool IsAmbiguous { get; init; }
}

public sealed class ShowPrepareResult
{
    public ShowResult Show { get; init; } = new();
    public IReadOnlyList<RepeatConflict> RepeatConflicts { get; init; } = [];
}

public sealed class RepeatConflict
{
    public int SlotPosition { get; init; }
    public int RecordingId { get; init; }
    public string ArtistName { get; init; } = string.Empty;
    public string TrackTitle { get; init; } = string.Empty;
    public IReadOnlyList<PlayHistoryResult> PreviousPlays { get; init; } = [];
    public bool HasException { get; init; }
}

public sealed class RepeatExceptionResult
{
    public int Id { get; init; }
    public int RecordingId { get; init; }
    public int ShowId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
