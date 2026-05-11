using DotNet.Testcontainers.Builders;
using FluentAssertions;

namespace SundownMedia.ContentOps.Integration.Tests;

public sealed class TestcontainersAvailabilityTests
{
    [Fact]
    public void TestcontainersBuilder_IsAvailable()
    {
        typeof(ContainerBuilder).Should().NotBeNull();
    }
}
