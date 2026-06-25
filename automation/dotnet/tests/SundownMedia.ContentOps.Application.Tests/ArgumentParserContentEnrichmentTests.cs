using FluentAssertions;
using SundownMedia.ContentOps.Cli;

namespace SundownMedia.ContentOps.Application.Tests;

public sealed class ArgumentParserContentEnrichmentTests
{
    [Fact]
    public void TryParse_ReturnsContentEnrichmentOptions()
    {
        var parsed = ArgumentParser.TryParse(
            [
                "content",
                "enrich",
                "--site-root",
                "src",
                "--report-path",
                "reports/content-enrichment-report.md",
                "--changed-paths",
                "src/content/shows/1/playlist.md,src/content/shows/1/track-info.md",
                "--changed-path",
                "src/content/shows/2/playlist.md",
                "--correlation-id",
                "abc",
            ],
            out var options);

        parsed.Should().BeTrue();
        var enrichOptions = options.Should().BeOfType<ContentEnrichmentCliOptions>().Subject;
        enrichOptions.SiteRoot.Should().Be("src");
        enrichOptions.ReportPath.Should().Be("reports/content-enrichment-report.md");
        enrichOptions.CorrelationId.Should().Be("abc");
        enrichOptions.ChangedPaths.Should().ContainInOrder(
            "src/content/shows/2/playlist.md",
            "src/content/shows/1/playlist.md",
            "src/content/shows/1/track-info.md");
    }

    [Fact]
    public void TryParse_ReturnsFalse_WhenRequiredContentEnrichmentArgumentsAreMissing()
    {
        var parsed = ArgumentParser.TryParse(["content", "enrich", "--site-root", "src"], out var options);

        parsed.Should().BeFalse();
        options.Should().BeNull();
    }
}
