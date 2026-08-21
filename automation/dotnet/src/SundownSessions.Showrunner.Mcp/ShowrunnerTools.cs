using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SundownSessions.Showrunner.Mcp;

[McpServerToolType]
public sealed class ShowrunnerTools
{
    [McpServerTool(Name = "show_reconciliation_evidence", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Reads Mixxx playback evidence and compares it with the authoritative Showrunner plan without mutating Showrunner or Mixxx state.")]
    public static async Task<ShowReconciliationEvidenceToolResult> GetShowReconciliationEvidenceAsync(
        ShowrunnerService showrunnerService,
        [Description("The authoritative Showrunner show identifier.")] Guid showId,
        CancellationToken cancellationToken = default)
    {
        var result = await showrunnerService.GetPlaybackEvidenceAsync(showId, cancellationToken);
        return result.IsSuccess
            ? new ShowReconciliationEvidenceToolResult(true, result.Value, null)
            : new ShowReconciliationEvidenceToolResult(false, null, result.Error);
    }

    [McpServerTool(Name = "show_reconciliation_confirm", ReadOnly = false, Idempotent = false, UseStructuredContent = true)]
    [Description("Confirms operator-approved reconciliation for a show. Confirmation is rejected if ambiguity is unresolved.")]
    public static async Task<ShowReconciliationConfirmToolResult> ConfirmShowReconciliationAsync(
        ShowrunnerService showrunnerService,
        [Description("The authoritative Showrunner show identifier.")] Guid showId,
        [Description("The explicit operator-approved reconciliation payload.")] ConfirmReconciliationCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await showrunnerService.ConfirmReconciliationAsync(showId, command, cancellationToken);
        return result.IsSuccess
            ? new ShowReconciliationConfirmToolResult(true, result.Value, null)
            : new ShowReconciliationConfirmToolResult(false, null, result.Error);
    }

    [McpServerTool(Name = "show_prepare", ReadOnly = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Matches a Showrunner plan to configured local FLAC metadata, checks repeat history and safely rebuilds its numbered preparation folder when every item is resolved.")]
    public static async Task<ShowPrepareToolResult> PrepareShowAsync(
        ShowPreparationService preparationService,
        [Description("The authoritative Showrunner show identifier.")] Guid showId,
        CancellationToken cancellationToken = default)
    {
        var result = await preparationService.PrepareShowAsync(showId, cancellationToken);
        return result.IsSuccess
            ? new ShowPrepareToolResult(true, result.Value, null)
            : new ShowPrepareToolResult(false, null, result.Error);
    }

    [McpServerTool(Name = "recording_resolve", ReadOnly = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Persists an explicit operator choice between a Showrunner recording and one relative FLAC candidate returned by show_prepare.")]
    public static async Task<RecordingResolveToolResult> ResolveRecordingAsync(
        ShowPreparationService preparationService,
        [Description("The authoritative Showrunner recording identifier.")] Guid recordingId,
        [Description("The relative sourceLibraryPath returned for a candidate by show_prepare; absolute paths are rejected.")] string sourceLibraryPath,
        CancellationToken cancellationToken = default)
    {
        var result = await preparationService.ResolveRecordingAsync(recordingId, sourceLibraryPath, cancellationToken);
        return result.IsSuccess
            ? new RecordingResolveToolResult(true, result.Value, null)
            : new RecordingResolveToolResult(false, null, result.Error);
    }

    [McpServerTool(Name = "repeat_exception_create", ReadOnly = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Records the explicit reason that a particular recording may be repeated in a show. This is separate from show_prepare so repeat protection cannot be bypassed implicitly.")]
    public static async Task<RepeatExceptionCreateToolResult> CreateRepeatExceptionAsync(
        ShowrunnerService showrunnerService,
        [Description("The authoritative Showrunner show identifier.")] Guid showId,
        [Description("The recording that may deliberately be repeated.")] Guid recordingId,
        [Description("The operator's explicit reason for allowing the repeat.")] string reason,
        CancellationToken cancellationToken = default)
    {
        var result = await showrunnerService.RecordRepeatExceptionAsync(
            showId,
            new RecordRepeatExceptionCommand(recordingId, reason),
            cancellationToken);
        return result.IsSuccess
            ? new RepeatExceptionCreateToolResult(true, result.Value, null)
            : new RepeatExceptionCreateToolResult(false, null, result.Error);
    }
}
