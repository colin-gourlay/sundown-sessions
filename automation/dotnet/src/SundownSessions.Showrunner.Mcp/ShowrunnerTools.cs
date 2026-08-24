using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SundownSessions.Showrunner.Mcp;

[McpServerToolType]
public sealed class ShowrunnerTools
{
    [McpServerTool(Name = "show_reconciliation_evidence", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Reads Mixxx playback evidence and compares it with the authoritative Showrunner plan without mutating Showrunner or Mixxx state.")]
    public static async Task<ShowReconciliationEvidenceToolResult> GetShowReconciliationEvidenceAsync(
        ShowReconciliationService reconciliationService,
        [Description("The authoritative Showrunner show identifier.")] Guid showId,
        CancellationToken cancellationToken = default)
    {
        var result = await reconciliationService.GetPlaybackEvidenceAsync(showId, cancellationToken);
        return result.IsSuccess
            ? new ShowReconciliationEvidenceToolResult(true, result.Value, null)
            : new ShowReconciliationEvidenceToolResult(false, null, result.Error);
    }

    [McpServerTool(Name = "show_reconciliation_confirm", ReadOnly = false, Idempotent = false, UseStructuredContent = true)]
    [Description("Persists an explicit operator-approved final playback order for later finalisation. It does not create permanent broadcast history and rejects unresolved ambiguity.")]
    public static async Task<ShowReconciliationConfirmToolResult> ConfirmShowReconciliationAsync(
        ShowReconciliationService reconciliationService,
        [Description("The authoritative Showrunner show identifier.")] Guid showId,
        [Description("The explicit operator-approved reconciliation payload.")] ConfirmReconciliationCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await reconciliationService.ConfirmReconciliationAsync(showId, command, cancellationToken);
        return result.IsSuccess
            ? new ShowReconciliationConfirmToolResult(true, result.Value, null)
            : new ShowReconciliationConfirmToolResult(false, null, result.Error);
    }

    [McpServerTool(Name = "show_reconciliation_finalise", ReadOnly = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Finalises an operator-confirmed reconciliation into permanent broadcast history. This is consequential and refuses unconfirmed reconciliations.")]
    public static async Task<ShowReconciliationFinaliseToolResult> FinaliseShowReconciliationAsync(
        ShowReconciliationService reconciliationService,
        [Description("The authoritative Showrunner show identifier.")] Guid showId,
        CancellationToken cancellationToken = default)
    {
        var result = await reconciliationService.FinaliseReconciliationAsync(showId, cancellationToken);
        return result.IsSuccess
            ? new ShowReconciliationFinaliseToolResult(true, result.Value, null)
            : new ShowReconciliationFinaliseToolResult(false, null, result.Error);
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

    [McpServerTool(Name = "show_plan_refresh", ReadOnly = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Intentionally replaces the mutable Showrunner plan with an explicit ordered recording list so an agent can sync the current Spotify-visible running order without making Spotify authoritative.")]
    public static async Task<ShowPlanRefreshToolResult> RefreshShowPlanAsync(
        ShowrunnerService showrunnerService,
        [Description("The authoritative Showrunner show identifier.")] Guid showId,
        [Description("The explicit ordered plan to persist for the show.")] RefreshShowPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await showrunnerService.RefreshShowPlanAsync(showId, command, cancellationToken);
        return result.IsSuccess
            ? new ShowPlanRefreshToolResult(true, result.Value, null)
            : new ShowPlanRefreshToolResult(false, null, result.Error);
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

    [McpServerTool(Name = "recording_external_identifier_add", ReadOnly = false, Idempotent = false, UseStructuredContent = true)]
    [Description("Associates one external identifier, such as a Spotify track identifier, with an authoritative Showrunner recording without making that external identifier the recording's identity.")]
    public static async Task<RecordingExternalIdentifierAddToolResult> AddRecordingExternalIdentifierAsync(
        ShowrunnerService showrunnerService,
        [Description("The authoritative Showrunner recording identifier.")] Guid recordingId,
        [Description("The external identifier source, for example spotify.")] string source,
        [Description("The external identifier value, for example a Spotify URI, track ID, or canonical URL.")] string value,
        CancellationToken cancellationToken = default)
    {
        var result = await showrunnerService.AddExternalIdentifierAsync(
            recordingId,
            new AddExternalIdentifierCommand(source, value),
            cancellationToken);
        return result.IsSuccess
            ? new RecordingExternalIdentifierAddToolResult(true, result.Value, null)
            : new RecordingExternalIdentifierAddToolResult(false, null, result.Error);
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

    [McpServerTool(Name = "recording_history", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns structured broadcast history for an exact recordingId, an exact external identifier such as a Spotify track ID, or a title/artist query, surfacing ambiguous candidate recordings instead of guessing.")]
    public static async Task<RecordingHistoryToolResult> GetRecordingHistoryAsync(
        ShowrunnerService showrunnerService,
        [Description("Recording history lookup input. Provide recordingId, or externalIdentifierSource with externalIdentifierValue, or title with optional artist for candidate matching.")]
        RecordingHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await showrunnerService.QueryRecordingHistoryAsync(query, cancellationToken);
        return result.IsSuccess
            ? new RecordingHistoryToolResult(true, result.Value, null)
            : new RecordingHistoryToolResult(false, null, result.Error);
    }

    [McpServerTool(Name = "recording_create", ReadOnly = false, Idempotent = false, UseStructuredContent = true)]
    [Description("Creates a new authoritative Showrunner recording for a non-Spotify candidate such as a demo, Bandcamp release, or local FLAC. Use recording_history first to avoid duplicates. After creation, use recording_external_identifier_add to attach a source reference such as a Todoist task identifier.")]
    public static async Task<RecordingCreateToolResult> CreateRecordingAsync(
        ShowrunnerService showrunnerService,
        [Description("The recording to create.")] CreateRecordingCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await showrunnerService.CreateRecordingAsync(command, cancellationToken);
        return result.IsSuccess
            ? new RecordingCreateToolResult(true, result.Value, null)
            : new RecordingCreateToolResult(false, null, result.Error);
    }

    [McpServerTool(Name = "backlog_item_create", ReadOnly = false, Idempotent = false, UseStructuredContent = true)]
    [Description("Adds a candidate to the Showrunner planning backlog, optionally linked to an authoritative recording. Use this after recording_create or recording_history to import a non-Spotify candidate for show planning consideration.")]
    public static async Task<BacklogItemCreateToolResult> CreateBacklogItemAsync(
        ShowrunnerService showrunnerService,
        [Description("The backlog item to create.")] CreateBacklogItemCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await showrunnerService.CreateBacklogItemAsync(command, cancellationToken);
        return result.IsSuccess
            ? new BacklogItemCreateToolResult(true, result.Value, null)
            : new BacklogItemCreateToolResult(false, null, result.Error);
    }

    [McpServerTool(Name = "backlog_item_list", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns all Showrunner backlog items in creation order. Use this to see outstanding non-Spotify candidates before planning a show.")]
    public static async Task<BacklogItemListToolResult> ListBacklogItemsAsync(
        ShowrunnerService showrunnerService,
        CancellationToken cancellationToken = default)
    {
        var result = await showrunnerService.ListBacklogItemsAsync(cancellationToken);
        return result.IsSuccess
            ? new BacklogItemListToolResult(true, result.Value, null)
            : new BacklogItemListToolResult(false, null, result.Error);
    }
}
