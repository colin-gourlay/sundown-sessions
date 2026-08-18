using System.ComponentModel;
using ModelContextProtocol.Server;
using Sundown.Showrunner.Application.Commands;
using Sundown.Showrunner.Application.Exceptions;
using Sundown.Showrunner.Application.Results;

namespace Sundown.Showrunner.Mcp.Tools;

[McpServerToolType]
public sealed class ShowPreparationTools
{
    private readonly ShowPrepareCommand _showPrepare;
    private readonly RecordingResolveCommand _recordingResolve;
    private readonly RepeatExceptionCreateCommand _repeatExceptionCreate;

    public ShowPreparationTools(
        ShowPrepareCommand showPrepare,
        RecordingResolveCommand recordingResolve,
        RepeatExceptionCreateCommand repeatExceptionCreate)
    {
        _showPrepare = showPrepare;
        _recordingResolve = recordingResolve;
        _repeatExceptionCreate = repeatExceptionCreate;
    }

    [McpServerTool(Name = "show_prepare")]
    [Description(
        "Mark a show as prepared and return any repeat conflicts detected across its slots. " +
        "RepeatConflicts lists recordings that have been broadcast before. " +
        "Conflicts with HasException = true have an approved repeat exception. " +
        "Conflicts with HasException = false require operator review before finalisation. " +
        "This is a consequential state change: the show status is set to Prepared.")]
    public async Task<ShowPrepareResult> ShowPrepareAsync(
        [Description("The numeric show ID to prepare.")] int showId,
        CancellationToken cancellationToken = default)
    {
        return await _showPrepare.ExecuteAsync(showId, cancellationToken);
    }

    [McpServerTool(Name = "recording_resolve")]
    [Description(
        "Assign a specific recording to a slot in a show. " +
        "Use this after recording_search has identified the correct recording for a slot. " +
        "Returns the updated show. This is a consequential state change.")]
    public async Task<ShowResult> RecordingResolveAsync(
        [Description("The numeric show ID.")] int showId,
        [Description("The slot position (1-based) to assign the recording to.")] int slotPosition,
        [Description("The numeric recording ID to assign.")] int recordingId,
        CancellationToken cancellationToken = default)
    {
        return await _recordingResolve.ExecuteAsync(showId, slotPosition, recordingId, cancellationToken);
    }

    [McpServerTool(Name = "repeat_exception_create")]
    [Description(
        "Create a repeat exception that explicitly permits a previously played recording to appear again in a show. " +
        "Requires a non-empty reason explaining why the repeat is approved. " +
        "Will fail if the recording has no play history (no exception needed) or if an exception already exists. " +
        "This is a consequential state change requiring operator approval.")]
    public async Task<RepeatExceptionResult> RepeatExceptionCreateAsync(
        [Description("The numeric show ID.")] int showId,
        [Description("The numeric recording ID that is being replayed.")] int recordingId,
        [Description("The reason the repeat has been approved by the operator.")] string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("A reason must be provided for the repeat exception.");

        return await _repeatExceptionCreate.ExecuteAsync(showId, recordingId, reason, cancellationToken);
    }
}
