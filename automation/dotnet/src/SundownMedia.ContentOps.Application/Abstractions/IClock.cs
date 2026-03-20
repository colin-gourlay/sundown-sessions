namespace SundownMedia.ContentOps.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
