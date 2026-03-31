using FluentAssertions;
using SundownMedia.ContentOps.Domain.ShowNotes;

namespace SundownMedia.ContentOps.Domain.Tests;

public sealed class TrackInfoParserTests
{
    [Fact]
    public void ParseArtistNames_ReturnsEmpty_WhenContentHasNoShortcodes()
    {
        var content = "---\n---\n| # | Title | Album | Duration | Notes |\n|:-:|:------|-------|:--------:|-------|";

        var result = TrackInfoParser.ParseArtistNames(content);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseArtistNames_ExtractsTitleArtists_InTrackOrder()
    {
        var content = """
            ---
            ---
            | 1 | {{<title "Take Me Out--Franz Ferdinand">}} | Album | 3:57 | |
            | 2 | {{<title "Propaganda--Sparks">}} | Album | 0:23 | |
            | 3 | {{<title "Cuddly Toy--Roachford">}} | Album | 3:48 | |
            """;

        var result = TrackInfoParser.ParseArtistNames(content);

        result.Should().Equal("Franz Ferdinand", "Sparks", "Roachford");
    }

    [Fact]
    public void ParseArtistNames_PutsFeaturedGuestFirst_FromTrackInfoFeaturedGuestShortcode()
    {
        var content = """
            ---
            ---
            | 1 | {{<title "Take Me Out--Franz Ferdinand">}} | Album | 3:57 | |
            | 2 | {{<track-info-featured-guest "Fast Cars, Soul Music--The Big Now">}} | Demo | 4:15 | |
            | 3 | {{<title "Propaganda--Sparks">}} | Album | 0:23 | |
            """;

        var result = TrackInfoParser.ParseArtistNames(content);

        result.Should().Equal("The Big Now", "Franz Ferdinand", "Sparks");
    }

    [Fact]
    public void ParseArtistNames_PutsFeaturedGuestFirst_FromFeaturedGuestWikilinkShortcode()
    {
        var content = """
            ---
            ---
            | 1 | {{<title "Smile Away--Paul McCartney">}} | Album | 3:52 | |
            | 2 | Interview with {{< featured-guest-wikilink "Kenny Armour from ANDYSMANCLUB">}} | | | |
            | 3 | {{<title "Pump It Up--Elvis Costello & The Attractions">}} | Album | 3:16 | |
            """;

        var result = TrackInfoParser.ParseArtistNames(content);

        result.Should().Equal("Kenny Armour from ANDYSMANCLUB", "Paul McCartney", "Elvis Costello & The Attractions");
    }

    [Fact]
    public void ParseArtistNames_DeduplicatesArtists_PreservingFirstOccurrence()
    {
        var content = """
            ---
            ---
            | 1 | {{<title "Normal Boy--Goodbye Mr Mackenzie">}} | Album | | |
            | 2 | {{<title "Hard--Goodbye Mr Mackenzie">}} | Album | | |
            | 3 | {{<title "Propaganda--Sparks">}} | Album | 0:23 | |
            """;

        var result = TrackInfoParser.ParseArtistNames(content);

        result.Should().Equal("Goodbye Mr Mackenzie", "Sparks");
    }

    [Fact]
    public void ParseArtistNames_DeduplicatesFeaturedGuestAgainstTitleArtists()
    {
        var content = """
            ---
            ---
            | 1 | {{<title "Track--IST IST">}} | Album | | |
            | 2 | {{<track-info-featured-guest "Song--IST IST">}} | Demo | | |
            """;

        var result = TrackInfoParser.ParseArtistNames(content);

        result.Should().Equal("IST IST");
    }

    [Fact]
    public void ParseArtistNames_HandlesTitleShortcodeWithSpaceAfterAngleBracket()
    {
        var content = "| 1 | {{< title \"Track One--Artist One\">}} | Album | | |";

        var result = TrackInfoParser.ParseArtistNames(content);

        result.Should().Equal("Artist One");
    }

    [Fact]
    public void ParseArtistNames_TrimsWhitespaceFromArtistNames()
    {
        var content = "| 1 | {{<title \"Track--  Artist With Spaces  \">}} | Album | | |";

        var result = TrackInfoParser.ParseArtistNames(content);

        result.Should().Equal("Artist With Spaces");
    }
}
