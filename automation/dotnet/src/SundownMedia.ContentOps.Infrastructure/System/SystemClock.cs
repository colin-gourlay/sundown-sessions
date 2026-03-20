using SundownMedia.ContentOps.Application.Abstractions;

namespace SundownMedia.ContentOps.Infrastructure.System;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
