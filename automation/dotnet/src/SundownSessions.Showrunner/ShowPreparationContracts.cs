namespace SundownSessions.Showrunner;

public sealed record ShowPreparationResultModel(
    Guid ShowId,
    string ShowSlug,
    int TrackCount,
    IReadOnlyList<PreparedTrackModel> MatchedTracks,
    IReadOnlyList<UnresolvedPreparedTrackModel> UnresolvedTracks,
    IReadOnlyList<RepeatConflictModel> RepeatConflicts,
    PreparationTimingModel Timing,
    PreparedBroadcastFolderModel? BroadcastFolder);

public sealed record PreparedTrackModel(
    Guid PlannedRecordingId,
    Guid RecordingId,
    int Position,
    string MatchKind,
    string SourceFilePath,
    string OutputFileName,
    TimeSpan Duration,
    TimeSpan CumulativeDuration);

public sealed record UnresolvedPreparedTrackModel(
    Guid PlannedRecordingId,
    Guid RecordingId,
    int Position,
    string Kind,
    string Message,
    IReadOnlyList<UnresolvedCandidateModel> Candidates);

public sealed record UnresolvedCandidateModel(
    string SourceFilePath,
    string MatchKind,
    string? Title,
    string? Artist,
    string? Album);

public sealed record RepeatConflictModel(
    Guid PlannedRecordingId,
    Guid RecordingId,
    IReadOnlyList<BroadcastHistoryEntry> PriorBroadcasts);

public sealed record PreparationTimingModel(
    int TrackCount,
    IReadOnlyList<PreparedTrackTimingModel> Tracks,
    TimeSpan TotalMusicDuration,
    TimeSpan? ConfiguredShowDuration,
    TimeSpan? RemainingDuration);

public sealed record PreparedTrackTimingModel(
    Guid PlannedRecordingId,
    int Position,
    TimeSpan Duration,
    TimeSpan CumulativeDuration);

public sealed record PreparedBroadcastFolderModel(
    string FolderPath,
    bool Rebuilt,
    IReadOnlyList<string> CopiedFiles);

public sealed record ShowPreparationOptions(
    string MusicRootPath,
    string PreparationRootPath,
    TimeSpan? ConfiguredShowDuration = null);

public sealed record ShowPrepareToolRequest(Guid ShowId);

public sealed record ShowPrepareToolResult(
    bool IsSuccess,
    ShowPreparationResultModel? Result,
    ApplicationError? Error);
