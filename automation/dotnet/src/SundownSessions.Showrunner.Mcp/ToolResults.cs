namespace SundownSessions.Showrunner.Mcp;

public sealed record ShowGetToolResult(
    bool IsSuccess,
    ShowLookupResult? Result,
    ApplicationError? Error);

public sealed record ShowPrepareToolResult(
    bool IsSuccess,
    ShowPreparationResultModel? Result,
    ApplicationError? Error);

public sealed record ShowPlanRefreshToolResult(
    bool IsSuccess,
    ShowPlanRefreshResult? Result,
    ApplicationError? Error);

public sealed record RecordingResolveToolResult(
    bool IsSuccess,
    RecordingResolutionModel? Result,
    ApplicationError? Error);

public sealed record RecordingExternalIdentifierAddToolResult(
    bool IsSuccess,
    RecordingModel? Result,
    ApplicationError? Error);

public sealed record RepeatExceptionCreateToolResult(
    bool IsSuccess,
    RepeatExceptionModel? Result,
    ApplicationError? Error);

public sealed record ShowReconciliationEvidenceToolResult(
    bool IsSuccess,
    PlaybackEvidenceModel? Result,
    ApplicationError? Error);

public sealed record ShowReconciliationConfirmToolResult(
    bool IsSuccess,
    ReconciliationModel? Result,
    ApplicationError? Error);

public sealed record ShowReconciliationFinaliseToolResult(
    bool IsSuccess,
    ReconciliationFinalisationSummary? Result,
    ApplicationError? Error);

public sealed record RecordingHistoryToolResult(
    bool IsSuccess,
    RecordingHistoryQueryResult? Result,
    ApplicationError? Error);

public sealed record BacklogCandidateImportToolResult(
    bool IsSuccess,
    BacklogCandidateImportResult? Result,
    ApplicationError? Error);

public sealed record BacklogItemListToolResult(
    bool IsSuccess,
    BacklogItemListResult? Result,
    ApplicationError? Error);
