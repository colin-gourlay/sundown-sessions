using FluentAssertions;
using Respawn;

namespace SundownMedia.ContentOps.Infrastructure.Tests;

public sealed class RespawnAvailabilityTests
{
    [Fact]
    public void RespawnType_IsAvailable()
    {
        typeof(Respawner).Should().NotBeNull();
    }
}
