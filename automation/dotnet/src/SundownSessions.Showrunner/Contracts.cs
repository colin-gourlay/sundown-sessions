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

public sealed record RefreshShowPlanCommand(IReadOnlyCollection<RefreshShowPlanItemCommand> Items);

public sealed record RefreshShowPlanItemCommand(Guid RecordingId, string? Notes = null);

public sealed record RecordRepeatExceptionCommand(Guid RecordingId, string Reason);

public sealed record SaveReconciliationCommand(bool Confirmed, IReadOnlyCollection<ReconciliationItemCommand> Items);

public sealed record ReconciliationItemCommand(Guid PlannedRecordingId, ReconciliationItemOutcome Outcome);

public sealed record ConfirmReconciliationCommand(
    bool OperatorConfirmed,
    bool HasUnresolvedAmbiguity,
    IReadOnlyCollection<ConfirmedPlaybackItemCommand> Items);

public sealed record ConfirmedPlaybackItemCommand(
    Guid RecordingId,
    int Position,
    Guid? PlannedRecordingId = null);

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

public sealed record ShowPlanRefreshResult(
    Guid ShowId,
    string ShowSlug,
    int PlannedCount,
    IReadOnlyList<PlannedShowRecordingDetailModel> PlannedRecordings);

public sealed record PlannedShowRecordingDetailModel(
    Guid PlannedRecordingId,
    Guid RecordingId,
    int Position,
    string Title,
    string? Artist,
    string? ReleaseTitle,
    string? Notes,
    IReadOnlyList<ExternalIdentifierModel> ExternalIdentifiers,
    IReadOnlyList<BroadcastHistoryEntry> BroadcastHistory);

public sealed record RepeatExceptionModel(Guid Id, Guid ShowId, Guid RecordingId, string Reason, DateTimeOffset CreatedAtUtc);

public sealed record BroadcastHistoryEntry(
    Guid BroadcastRecordingId,
    Guid ShowId,
    string ShowSlug,
    DateOnly ShowDate,
    DateTimeOffset BroadcastAtUtc,
    Guid? PlannedRecordingId = null,
    int? Position = null);

public sealed record RecordingHistoryQuery(
    Guid? RecordingId = null,
    string? ExternalIdentifierSource = null,
    string? ExternalIdentifierValue = null,
    string? Title = null,
    string? Artist = null);

public sealed record BacklogItemListResult(IReadOnlyList<BacklogItemModel> Items);

public sealed record RecordingHistoryQueryResult(
    bool IsAmbiguous,
    IReadOnlyList<RecordingHistoryCandidateModel> Candidates);

public sealed record RecordingHistoryCandidateModel(
    Guid RecordingId,
    string Title,
    string? Artist,
    IReadOnlyList<ExternalIdentifierModel> ExternalIdentifiers,
    IReadOnlyList<BroadcastHistoryEntry> BroadcastHistory);

public sealed record ReconciliationFinalisationSummary(
    Guid ShowId,
    Guid ReconciliationId,
    bool IsFinalised,
    bool IsNoOp,
    DateTimeOffset? FinalisedAtUtc,
    IReadOnlyList<FinalisedBroadcastRecordingModel> AddedToPermanentHistory,
    IReadOnlyList<DroppedPlannedRecordingModel> DroppedPlannedRecordings,
    IReadOnlyList<RepeatExceptionModel> RepeatExceptionsUsed);

public sealed record FinalisedBroadcastRecordingModel(
    Guid BroadcastRecordingId,
    Guid RecordingId,
    Guid? PlannedRecordingId,
    int Position,
    DateTimeOffset BroadcastAtUtc,
    string Title,
    string? Artist,
    IReadOnlyList<ExternalIdentifierModel> ExternalIdentifiers);

public sealed record DroppedPlannedRecordingModel(
    Guid PlannedRecordingId,
    Guid RecordingId,
    int PlannedPosition,
    string Title,
    string? Artist,
    IReadOnlyList<ExternalIdentifierModel> ExternalIdentifiers);

public sealed record ReconciliationModel(
    Guid Id,
    Guid ShowId,
    bool IsConfirmed,
    bool IsOperatorConfirmed,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ConfirmedAtUtc,
    DateTimeOffset? OperatorConfirmedAtUtc,
    IReadOnlyList<ReconciliationItemModel> Items,
    IReadOnlyList<ConfirmedPlaybackItemModel> ConfirmedPlayback);

public sealed record ReconciliationItemModel(
    Guid PlannedRecordingId,
    Guid RecordingId,
    int PlannedPosition,
    ReconciliationItemOutcome Outcome);

public sealed record ConfirmedPlaybackItemModel(
    Guid RecordingId,
    int Position,
    Guid? PlannedRecordingId);

public sealed record PlaybackEvidenceModel(
    Guid ShowId,
    DateOnly EvidenceDate,
    string? HistorySessionName,
    int PlannedCount,
    int DetectedPlannedCount,
    bool IsIncompleteEvidence,
    bool HasAmbiguousMatches,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<MixxxHistorySessionSummaryModel> CandidateSessions,
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
    DateTimeOffset? DetectedAtUtc,
    Guid? RecordingId,
    IReadOnlyList<Guid> RecordingCandidates,
    bool IsAmbiguousMatch);

public sealed record OrderDifferenceModel(
    Guid PlannedRecordingId,
    int PlannedPosition,
    int DetectedPosition);

public sealed record MixxxHistorySessionSummaryModel(
    string Name,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int TrackCount);
