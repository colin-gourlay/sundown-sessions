namespace SundownSessions.Showrunner;

public sealed record CreateRecordingCommand(
    string Title,
    string? Artist,
    string? Notes = null,
    string? ReleaseTitle = null);

public sealed record AddExternalIdentifierCommand(string Source, string Value);

public sealed record CreateBacklogItemCommand(string Summary, Guid? RecordingId = null, string? Notes = null);

public sealed record CreateShowCommand(string Slug, string Title, DateOnly ShowDate);

public sealed record PlanRecordingCommand(Guid RecordingId, int Position, string? Notes = null);

public sealed record RecordRepeatExceptionCommand(Guid RecordingId, string Reason);

public sealed record SaveReconciliationCommand(bool Confirmed, IReadOnlyCollection<ReconciliationItemCommand> Items);

public sealed record ReconciliationItemCommand(Guid PlannedRecordingId, ReconciliationItemOutcome Outcome);

public sealed record ConfirmReconciliationCommand(
    bool OperatorConfirmed,
    bool HasUnresolvedAmbiguity,
    IReadOnlyCollection<ReconciliationItemCommand> Items);

public enum ReconciliationItemOutcome
{
    Pending = 0,
    Broadcast = 1,
    NotBroadcast = 2,
}

public sealed record RecordingModel(
    Guid Id,
    string Title,
    string? Artist,
    string? ReleaseTitle,
    string? Notes,
    IReadOnlyList<ExternalIdentifierModel> ExternalIdentifiers,
    DateTimeOffset CreatedAtUtc);

public sealed record ExternalIdentifierModel(string Source, string Value);

public sealed record BacklogItemModel(Guid Id, string Summary, Guid? RecordingId, string? Notes, DateTimeOffset CreatedAtUtc);

public sealed record ShowModel(
    Guid Id,
    string Slug,
    string Title,
    DateOnly ShowDate,
    IReadOnlyList<PlannedRecordingModel> PlannedRecordings,
    DateTimeOffset CreatedAtUtc);

public sealed record PlannedRecordingModel(Guid Id, Guid RecordingId, int Position, string? Notes, DateTimeOffset CreatedAtUtc);

public sealed record RepeatExceptionModel(Guid Id, Guid ShowId, Guid RecordingId, string Reason, DateTimeOffset CreatedAtUtc);

public sealed record BroadcastHistoryEntry(
    Guid BroadcastRecordingId,
    Guid ShowId,
    string ShowSlug,
    DateOnly ShowDate,
    DateTimeOffset BroadcastAtUtc);

public sealed record ReconciliationModel(
    Guid Id,
    Guid ShowId,
    bool IsConfirmed,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ConfirmedAtUtc,
    IReadOnlyList<ReconciliationItemModel> Items);

public sealed record ReconciliationItemModel(
    Guid PlannedRecordingId,
    Guid RecordingId,
    int PlannedPosition,
    ReconciliationItemOutcome Outcome);

public sealed record PlaybackEvidenceModel(
    Guid ShowId,
    int PlannedCount,
    int DetectedPlannedCount,
    bool IsIncompleteEvidence,
    bool HasAmbiguousMatches,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<PlannedPlaybackEvidenceItemModel> Planned,
    IReadOnlyList<UnexpectedPlaybackEvidenceItemModel> Unexpected,
    IReadOnlyList<OrderDifferenceModel> OrderingDifferences);

public sealed record PlannedPlaybackEvidenceItemModel(
    Guid PlannedRecordingId,
    Guid RecordingId,
    int PlannedPosition,
    string Title,
    string? Artist,
    bool IsDetected,
    int? DetectedPosition,
    DateTimeOffset? DetectedAtUtc,
    bool IsAmbiguousMatch);

public sealed record UnexpectedPlaybackEvidenceItemModel(
    int DetectedPosition,
    string Title,
    string? Artist,
    DateTimeOffset? DetectedAtUtc);

public sealed record OrderDifferenceModel(
    Guid PlannedRecordingId,
    int PlannedPosition,
    int DetectedPosition);
