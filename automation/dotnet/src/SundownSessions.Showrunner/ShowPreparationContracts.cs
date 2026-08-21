using System.Text.Json.Serialization;

namespace SundownSessions.Showrunner;

[JsonConverter(typeof(JsonStringEnumConverter<ShowPreparationStatus>))]
public enum ShowPreparationStatus
{
    Prepared,
    Unresolved,
    RepeatConflict,
}

[JsonConverter(typeof(JsonStringEnumConverter<RecordingMatchKind>))]
public enum RecordingMatchKind
{
    MetadataIdentifier,
    ExplicitResolution,
    NormalisedMetadata,
}

[JsonConverter(typeof(JsonStringEnumConverter<UnresolvedTrackKind>))]
public enum UnresolvedTrackKind
{
    MissingRecording,
    MissingFile,
    AmbiguousMatch,
    IdentifierConflict,
}

public sealed record ShowPreparationResultModel(
    Guid ShowId,
    string ShowSlug,
    ShowPreparationStatus Status,
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
    RecordingMatchKind MatchKind,
    string SourceLibraryPath,
    string OutputFileName,
    TimeSpan Duration,
    TimeSpan CumulativeDuration);

public sealed record UnresolvedPreparedTrackModel(
    Guid PlannedRecordingId,
    Guid RecordingId,
    int Position,
    UnresolvedTrackKind Kind,
    string Message,
    IReadOnlyList<UnresolvedCandidateModel> Candidates);

public sealed record UnresolvedCandidateModel(
    string SourceLibraryPath,
    RecordingMatchKind MatchKind,
    string? Title,
    string? Artist,
    string? Album);

public sealed record RepeatConflictModel(
    Guid PlannedRecordingId,
    Guid RecordingId,
    IReadOnlyList<BroadcastHistoryEntry> PriorBroadcasts);

public sealed record PreparationTimingModel(
    int TrackCount,
    int MatchedTrackCount,
    IReadOnlyList<PreparedTrackTimingModel> Tracks,
    TimeSpan TotalMusicDuration,
    TimeSpan? ConfiguredShowDuration,
    TimeSpan? RemainingDuration,
    TimeSpan? OverrunDuration);

public sealed record PreparedTrackTimingModel(
    Guid PlannedRecordingId,
    int Position,
    TimeSpan Duration,
    TimeSpan CumulativeDuration);

public sealed record PreparedBroadcastFolderModel(
    string FolderName,
    bool Rebuilt,
    IReadOnlyList<string> CopiedFiles);

public sealed record ShowPreparationOptions(
    string MusicRootPath,
    string PreparationRootPath,
    TimeSpan? ConfiguredShowDuration = null);

public sealed record RecordingResolutionModel(
    Guid RecordingId,
    string SourceLibraryPath,
    string? Title,
    string? Artist,
    string? Album,
    TimeSpan Duration);
