using System.ComponentModel;
using ModelContextProtocol.Server;
using Sundown.Showrunner.Application.Exceptions;
using Sundown.Showrunner.Application.Queries;
using Sundown.Showrunner.Application.Results;

namespace Sundown.Showrunner.Mcp.Tools;

[McpServerToolType]
public sealed class RecordingTools
{
    private readonly RecordingSearchQuery _recordingSearch;
    private readonly RecordingHistoryQuery _recordingHistory;

    public RecordingTools(RecordingSearchQuery recordingSearch, RecordingHistoryQuery recordingHistory)
    {
        _recordingSearch = recordingSearch;
        _recordingHistory = recordingHistory;
    }

    [McpServerTool(Name = "recording_search")]
    [Description(
        "Search the recording catalogue by artist name, track title or album title. " +
        "Returns structured matches. When IsAmbiguous is true, multiple recordings matched " +
        "and the operator must select the correct one before resolving a slot.")]
    public async Task<RecordingSearchResult> RecordingSearchAsync(
        [Description("Free-text search term matched against artist name, track title and album title.")] string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new DomainRuleException("Search query must not be empty.");

        return await _recordingSearch.ExecuteAsync(query, cancellationToken);
    }

    [McpServerTool(Name = "recording_history")]
    [Description(
        "Return the broadcast history for a specific recording by its ID. " +
        "An empty list means the recording has not been played before. " +
        "A non-empty list means it has been played; any planned re-play requires a repeat exception.")]
    public async Task<IReadOnlyList<PlayHistoryResult>> RecordingHistoryAsync(
        [Description("The numeric recording ID.")] int recordingId,
        CancellationToken cancellationToken = default)
    {
        return await _recordingHistory.ExecuteAsync(recordingId, cancellationToken);
    }
}
