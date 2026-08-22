namespace SundownSessions.Showrunner.Mcp;

public sealed record ShowPrepareToolResult(
    bool IsSuccess,
    ShowPreparationResultModel? Result,
    ApplicationError? Error);

public sealed record RecordingResolveToolResult(
    bool IsSuccess,
    RecordingResolutionModel? Result,
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
