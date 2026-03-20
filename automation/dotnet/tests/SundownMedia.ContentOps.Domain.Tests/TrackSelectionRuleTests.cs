using FluentAssertions;
using SundownMedia.ContentOps.Domain.Tracks;

namespace SundownMedia.ContentOps.Domain.Tests;

public sealed class TrackSelectionRuleTests
{
    [Fact]
    public void ResolveFallbackTrackIndex_ReturnsShortestTrackIndex()
    {
        var index = TrackSelectionRule.ResolveFallbackTrackIndex([231, 125, 189]);

        index.Should().Be(1);
    }
}
