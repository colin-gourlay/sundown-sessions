namespace SundownMedia.ContentOps.Contracts.Correlation;

public interface ICorrelationContext
{
    string CorrelationId { get; }

    void Set(string correlationId);
}
