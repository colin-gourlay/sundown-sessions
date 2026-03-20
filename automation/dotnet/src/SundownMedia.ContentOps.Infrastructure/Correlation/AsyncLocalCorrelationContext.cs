using SundownMedia.ContentOps.Contracts.Correlation;

namespace SundownMedia.ContentOps.Infrastructure.Correlation;

public sealed class AsyncLocalCorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentCorrelationId = new();

    public string CorrelationId => CurrentCorrelationId.Value ?? string.Empty;

    public void Set(string correlationId)
    {
        CurrentCorrelationId.Value = correlationId;
    }
}
