using System.ComponentModel;
using ModelContextProtocol.Server;
using Sundown.Showrunner.Application.Exceptions;
using Sundown.Showrunner.Application.Queries;
using Sundown.Showrunner.Application.Results;

namespace Sundown.Showrunner.Mcp.Tools;

[McpServerToolType]
public sealed class ShowTools
{
    private readonly ShowGetQuery _showGet;

    public ShowTools(ShowGetQuery showGet)
    {
        _showGet = showGet;
    }

    [McpServerTool(Name = "show_get")]
    [Description("Retrieve a planned or prepared show by its numeric ID or broadcast date (YYYY-MM-DD). Returns the show's status, title, broadcast date and ordered slot list with any assigned recordings.")]
    public async Task<ShowResult> ShowGetByIdAsync(
        [Description("The numeric show ID.")] int id,
        CancellationToken cancellationToken = default)
    {
        return await _showGet.ByIdAsync(id, cancellationToken);
    }

    [McpServerTool(Name = "show_get_by_date")]
    [Description("Retrieve a show by its broadcast date in YYYY-MM-DD format. Returns the show's status, title and ordered slot list.")]
    public async Task<ShowResult> ShowGetByDateAsync(
        [Description("The broadcast date in YYYY-MM-DD format.")] string date,
        CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            throw new DomainRuleException($"'{date}' is not a valid date. Use YYYY-MM-DD format.");

        return await _showGet.ByDateAsync(parsedDate, cancellationToken);
    }
}
