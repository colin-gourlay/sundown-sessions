namespace SundownSessions.Showrunner;

public sealed class ShowrunnerMcpTools(ShowPreparationService preparationService)
{
    public async Task<ShowPrepareToolResult> ShowPrepareAsync(ShowPrepareToolRequest request, CancellationToken cancellationToken = default)
    {
        var result = await preparationService.PrepareShowAsync(request.ShowId, cancellationToken);
        return result.IsSuccess
            ? new ShowPrepareToolResult(true, result.Value, null)
            : new ShowPrepareToolResult(false, null, result.Error);
    }
}
