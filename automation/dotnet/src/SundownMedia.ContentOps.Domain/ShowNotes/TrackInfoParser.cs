// <copyright file="TrackInfoParser.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Domain.ShowNotes;

using System.Text.RegularExpressions;

public static partial class TrackInfoParser
{
    private static readonly Regex FeaturedGuestShortcodePattern =
        FeaturedGuestShortcodeRegex();

    private static readonly Regex FeaturedGuestWikilinkPattern =
        FeaturedGuestWikilinkRegex();

    private static readonly Regex TitleShortcodePattern =
        TitleShortcodeRegex();

    public static IReadOnlyList<string> ParseArtistNames(string trackInfoContent)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        var featuredArtists = new List<string>();

        foreach (Match match in FeaturedGuestShortcodePattern.Matches(trackInfoContent))
        {
            var artist = match.Groups["artist"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(artist) && seen.Add(artist))
            {
                featuredArtists.Add(artist);
            }
        }

        foreach (Match match in FeaturedGuestWikilinkPattern.Matches(trackInfoContent))
        {
            var artist = match.Groups["artist"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(artist) && seen.Add(artist))
            {
                featuredArtists.Add(artist);
            }
        }

        result.AddRange(featuredArtists);

        foreach (Match match in TitleShortcodePattern.Matches(trackInfoContent))
        {
            var artist = match.Groups["artist"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(artist) && seen.Add(artist))
            {
                result.Add(artist);
            }
        }

        return result.AsReadOnly();
    }

    [GeneratedRegex(
        @"\{\{<\s*track-info-featured-guest\s+""[^""]*--(?<artist>[^""]+)""\s*>\}\}",
        RegexOptions.Compiled)]
    private static partial Regex FeaturedGuestShortcodeRegex();

    [GeneratedRegex(
        @"\{\{<\s*featured-guest-wikilink\s+""(?<artist>[^""]+)""\s*>\}\}",
        RegexOptions.Compiled)]
    private static partial Regex FeaturedGuestWikilinkRegex();

    [GeneratedRegex(
        @"\{\{<\s*title\s+""[^""]*--(?<artist>[^""]+)""\s*>\}\}",
        RegexOptions.Compiled)]
    private static partial Regex TitleShortcodeRegex();
}
