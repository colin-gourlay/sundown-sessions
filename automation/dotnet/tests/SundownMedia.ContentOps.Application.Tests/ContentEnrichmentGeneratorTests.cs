using FluentAssertions;
using SundownMedia.ContentOps.Application.Features.ContentEnrichment;

namespace SundownMedia.ContentOps.Application.Tests;

public sealed class ContentEnrichmentGeneratorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string siteRoot;

    public ContentEnrichmentGeneratorTests()
    {
        this.siteRoot = Path.Combine(this.root, "src");
        Directory.CreateDirectory(Path.Combine(this.siteRoot, "content", "shows", "1"));
    }

    [Fact]
    public void Enrich_GeneratesDraftPages_FromPlaylistAndTrackInfo()
    {
        this.WriteShowFiles(
            "1",
            """
            ---
            ---
            1. {{< artist-wikilink "Nick Cave & The Bad Seeds" >}} - Red Right Hand
            """,
            """
            ---
            ---
            | # | Title | Album | Duration | Notes |
            |:-:|-------|-------|:--------:|-------|
            | 1 | {{<title "Red Right Hand--Nick Cave & The Bad Seeds">}} | {{<release "Let Love In (1994)--Nick Cave & The Bad Seeds--let-love-in">}} | 6:10 | |
            """);

        var command = new EnrichContentCommand(
            this.siteRoot,
            [],
            Path.Combine(this.root, "reports", "content-enrichment-report.md"),
            Guid.NewGuid().ToString("D"));

        var result = ContentEnrichmentGenerator.Enrich(command);

        result.ArtistPagesCreated.Should().Be(1);
        result.ReleasePagesCreated.Should().Be(1);
        result.TrackPagesCreated.Should().Be(1);

        var artistPage = this.ReadContent("artists", "n", "nick-cave-and-the-bad-seeds", "index.md");
        artistPage.Should().Contain("title: \"Nick Cave & The Bad Seeds\"");
        artistPage.Should().Contain("artist_page: true");
        artistPage.Should().Contain("draft: true");

        var releasePage = this.ReadContent("releases", "n", "nick-cave-and-the-bad-seeds", "let-love-in", "index.md");
        releasePage.Should().Contain("title: \"Let Love In\"");
        releasePage.Should().Contain("artist: \"Nick Cave & The Bad Seeds\"");
        releasePage.Should().Contain("releaseDate: \"1994\"");
        releasePage.Should().Contain("featuredInShows:");
        releasePage.Should().Contain("- \"1\"");
        releasePage.Should().Contain("duration: \"6:10\"");

        var trackPage = this.ReadContent("tracks", "n", "nick-cave-and-the-bad-seeds", "red-right-hand", "index.md");
        trackPage.Should().Contain("title: \"Red Right Hand\"");
        trackPage.Should().Contain("artist: \"Nick Cave & The Bad Seeds\"");
        trackPage.Should().Contain("release: \"Let Love In\"");
        trackPage.Should().Contain("release_slug: \"let-love-in\"");
        trackPage.Should().Contain("track_page: true");
        trackPage.Should().Contain("draft: true");
    }

    [Fact]
    public void Enrich_IsIdempotent_WhenRunTwice()
    {
        this.WriteShowFiles(
            "1",
            "1. {{< artist-wikilink \"The Cure\" >}} - A Forest",
            "| 1 | {{<title \"A Forest--The Cure\">}} | Seventeen Seconds (1980) | 5:55 | |");

        var command = new EnrichContentCommand(
            this.siteRoot,
            [],
            Path.Combine(this.root, "reports", "content-enrichment-report.md"),
            Guid.NewGuid().ToString("D"));

        var first = ContentEnrichmentGenerator.Enrich(command);
        var second = ContentEnrichmentGenerator.Enrich(command);

        first.PagesCreated.Should().Be(3);
        second.PagesCreated.Should().Be(0);
        second.ExistingArtistsSkipped.Should().Be(1);
        second.ExistingReleasesSkipped.Should().Be(1);
        second.ExistingTracksSkipped.Should().Be(1);
    }

    [Fact]
    public void Enrich_SkipsExistingPages_UsingTolerantNormalization()
    {
        this.WriteShowFiles(
            "1",
            "1. {{< artist-wikilink \"Nick Cave & The Bad Seeds\" >}} - Red Right Hand",
            "| 1 | {{<title \"Red Right Hand--Nick Cave & The Bad Seeds\">}} | Let Love In (1994) | 6:10 | |");

        this.WriteContent(
            "artists",
            "n",
            "nick-cave-and-the-bad-seeds",
            "index.md",
            """
            ---
            title: "Nick Cave and The Bad Seeds"
            ---
            """);
        this.WriteContent(
            "releases",
            "n",
            "nick-cave-and-the-bad-seeds",
            "let-love-in",
            "index.md",
            """
            ---
            title: "Let Love In"
            artist: "Nick Cave and The Bad Seeds"
            ---
            """);
        this.WriteContent(
            "tracks",
            "n",
            "nick-cave-and-the-bad-seeds",
            "red-right-hand",
            "index.md",
            """
            ---
            title: "Red Right Hand"
            artist: "Nick Cave and The Bad Seeds"
            ---
            """);

        var command = new EnrichContentCommand(
            this.siteRoot,
            [],
            Path.Combine(this.root, "reports", "content-enrichment-report.md"),
            Guid.NewGuid().ToString("D"));

        var result = ContentEnrichmentGenerator.Enrich(command);

        result.PagesCreated.Should().Be(0);
        result.ExistingArtistsSkipped.Should().Be(1);
        result.ExistingReleasesSkipped.Should().Be(1);
        result.ExistingTracksSkipped.Should().Be(1);
    }

    [Fact]
    public void Enrich_ReportsAmbiguousMatches_WithoutWritingCandidate()
    {
        this.WriteShowFiles(
            "1",
            "1. {{< artist-wikilink \"The Sound\" >}} - Winning",
            "| 1 | {{<title \"Winning--The Sound\">}} | From The Lions Mouth (1981) | 4:18 | |");

        this.WriteContent(
            "artists",
            "t",
            "the-sound",
            "index.md",
            """
            ---
            title: "The Sound"
            ---
            """);
        this.WriteContent(
            "artists",
            "t",
            "the-sound-2",
            "index.md",
            """
            ---
            title: "The Sound"
            ---
            """);

        var command = new EnrichContentCommand(
            this.siteRoot,
            [],
            Path.Combine(this.root, "reports", "content-enrichment-report.md"),
            Guid.NewGuid().ToString("D"));

        var result = ContentEnrichmentGenerator.Enrich(command);

        result.AmbiguousItems.Should().BeGreaterThan(0);
        result.ArtistPagesCreated.Should().Be(0);
        File.ReadAllText(command.ReportPath).Should().Contain("Artist: The Sound matched");
    }

    [Fact]
    public void Enrich_UsesChangedPaths_ToLimitShows()
    {
        this.WriteShowFiles(
            "1",
            "1. {{< artist-wikilink \"Wire\" >}} - Outdoor Miner",
            "| 1 | {{<title \"Outdoor Miner--Wire\">}} | Chairs Missing (1978) | 1:45 | |");
        this.WriteShowFiles(
            "2",
            "1. {{< artist-wikilink \"Magazine\" >}} - Shot By Both Sides",
            "| 1 | {{<title \"Shot By Both Sides--Magazine\">}} | Real Life (1978) | 4:01 | |");

        var command = new EnrichContentCommand(
            this.siteRoot,
            ["src/content/shows/2/track-info.md"],
            Path.Combine(this.root, "reports", "content-enrichment-report.md"),
            Guid.NewGuid().ToString("D"));

        var result = ContentEnrichmentGenerator.Enrich(command);

        result.ShowsScanned.Should().Be(1);
        result.PagesCreated.Should().Be(3);
        this.ContentExists("artists", "m", "magazine", "index.md").Should().BeTrue();
        this.ContentExists("artists", "w", "wire", "index.md").Should().BeFalse();
    }

    [Theory]
    [InlineData("Nick Cave & The Bad Seeds", "nick cave and the bad seeds")]
    [InlineData("O'Hara", "ohara")]
    [InlineData("Cafe\u0301 Society", "cafe society")]
    [InlineData("  RED   RIGHT--HAND  ", "red right hand")]
    public void NormalizeKey_NormalizesLookupValues(string value, string expected)
    {
        ContentEnrichmentGenerator.NormalizeKey(value).Should().Be(expected);
    }

    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    private void WriteShowFiles(string showId, string playlist, string trackInfo)
    {
        var showDirectory = Path.Combine(this.siteRoot, "content", "shows", showId);
        Directory.CreateDirectory(showDirectory);
        File.WriteAllText(Path.Combine(showDirectory, "playlist.md"), playlist);
        File.WriteAllText(Path.Combine(showDirectory, "track-info.md"), trackInfo);
    }

    private void WriteContent(params string[] partsAndContent)
    {
        var content = partsAndContent[^1];
        var path = Path.Combine([this.siteRoot, "content", .. partsAndContent[..^1]]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private string ReadContent(params string[] parts)
    {
        return File.ReadAllText(Path.Combine([this.siteRoot, "content", .. parts]));
    }

    private bool ContentExists(params string[] parts)
    {
        return File.Exists(Path.Combine([this.siteRoot, "content", .. parts]));
    }
}
