// <copyright file="ContentEnrichmentGenerator.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ContentEnrichment
{
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    public static partial class ContentEnrichmentGenerator
    {
        private const string IndexFileName = "index.md";

        public static EnrichContentResult Enrich(EnrichContentCommand command)
        {
            var siteRoot = Path.GetFullPath(command.SiteRoot);
            var contentRoot = Path.Combine(siteRoot, "content");
            var showsRoot = Path.Combine(contentRoot, "shows");
            var reportPath = Path.GetFullPath(command.ReportPath);

            var showDirectories = GetShowDirectories(showsRoot, command.ChangedPaths).ToList();
            var existing = ExistingContentIndex.Build(contentRoot);
            var parsed = PlaylistParser.Parse(showDirectories);
            var report = new EnrichmentReport();

            var candidates = BuildCandidates(parsed.Entries);

            foreach (var candidate in candidates)
            {
                CreateArtistPage(contentRoot, existing, candidate, report);
                CreateTrackPage(contentRoot, existing, candidate, report);
            }

            foreach (var release in BuildReleaseCandidates(candidates))
            {
                CreateReleasePage(contentRoot, existing, release, report);
            }

            report.Write(reportPath, showDirectories.Count, parsed.PlaylistEntriesParsed, parsed.TrackInfoEntriesParsed);

            return new EnrichContentResult(
                reportPath,
                showDirectories.Count,
                parsed.PlaylistEntriesParsed,
                parsed.TrackInfoEntriesParsed,
                report.CreatedArtists.Count,
                report.CreatedReleases.Count,
                report.CreatedTracks.Count,
                report.SkippedArtists.Count,
                report.SkippedReleases.Count,
                report.SkippedTracks.Count,
                report.Ambiguous.Count,
                command.CorrelationId);
        }

        public static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormKD).ToLowerInvariant();
            normalized = normalized.Replace("&", " and ", StringComparison.Ordinal);
            normalized = ApostropheRegex().Replace(normalized, string.Empty);
            normalized = NonAlphaNumericRegex().Replace(normalized, " ");
            return WhitespaceRegex().Replace(normalized, " ").Trim();
        }

        public static string ToSlug(string value)
        {
            var key = NormalizeKey(value);
            if (string.IsNullOrWhiteSpace(key))
            {
                return "unknown";
            }

            return key.Replace(' ', '-');
        }

        private static IEnumerable<string> GetShowDirectories(string showsRoot, IReadOnlyList<string> changedPaths)
        {
            if (!Directory.Exists(showsRoot))
            {
                return [];
            }

            if (changedPaths.Count == 0)
            {
                return Directory
                    .EnumerateDirectories(showsRoot)
                    .Where(HasPlaylistData)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            }

            var root = Path.GetFullPath(Path.Combine(showsRoot, "..", ".."));
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawPath in changedPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                var fullPath = ResolveChangedPath(root, rawPath);
                var fileName = Path.GetFileName(fullPath);

                if (!IsShowDataFile(fileName))
                {
                    continue;
                }

                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory) &&
                    Directory.Exists(directory) &&
                    IsUnderDirectory(directory, showsRoot))
                {
                    directories.Add(directory);
                }
            }

            return directories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        }

        private static string ResolveChangedPath(string siteRoot, string rawPath)
        {
            var path = rawPath.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            var direct = Path.GetFullPath(Path.Combine(siteRoot, path));
            if (File.Exists(direct))
            {
                return direct;
            }

            var siteRootName = Path.GetFileName(siteRoot.TrimEnd(Path.DirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(siteRootName) &&
                path.StartsWith(siteRootName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(Path.Combine(siteRoot, path[(siteRootName.Length + 1)..]));
            }

            return direct;
        }

        private static bool HasPlaylistData(string directory)
        {
            return File.Exists(Path.Combine(directory, "playlist.md")) ||
                File.Exists(Path.Combine(directory, "track-info.md"));
        }

        private static bool IsShowDataFile(string fileName)
        {
            return string.Equals(fileName, "playlist.md", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "track-info.md", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUnderDirectory(string path, string root)
        {
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<ContentCandidate> BuildCandidates(IReadOnlyList<ParsedPlaylistEntry> entries)
        {
            var candidates = new Dictionary<string, ContentCandidate>(StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Artist) || string.IsNullOrWhiteSpace(entry.Track))
                {
                    continue;
                }

                var key = $"{NormalizeKey(entry.Artist)}|{NormalizeKey(entry.Track)}";
                if (!candidates.TryGetValue(key, out var candidate))
                {
                    candidate = new ContentCandidate(entry.Artist, entry.Track);
                    candidates.Add(key, candidate);
                }

                candidate.Shows.Add(entry.ShowId);

                if (!string.IsNullOrWhiteSpace(entry.Duration))
                {
                    candidate.Duration = entry.Duration;
                }

                if (!string.IsNullOrWhiteSpace(entry.ReleaseTitle))
                {
                    candidate.ReleaseTitle = entry.ReleaseTitle;
                }

                if (!string.IsNullOrWhiteSpace(entry.ReleaseYear))
                {
                    candidate.ReleaseYear = entry.ReleaseYear;
                }

                if (!string.IsNullOrWhiteSpace(entry.ReleaseArtist))
                {
                    candidate.ReleaseArtist = entry.ReleaseArtist;
                }
            }

            return candidates.Values
                .OrderBy(candidate => candidate.Artist, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Track, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IReadOnlyList<ReleaseCandidate> BuildReleaseCandidates(IReadOnlyList<ContentCandidate> candidates)
        {
            var releases = new Dictionary<string, ReleaseCandidate>(StringComparer.Ordinal);

            foreach (var candidate in candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate.ReleaseTitle)))
            {
                var releaseArtist = candidate.ReleaseArtist ?? candidate.Artist;
                var key = $"{NormalizeKey(releaseArtist)}|{NormalizeKey(candidate.ReleaseTitle!)}";
                if (!releases.TryGetValue(key, out var release))
                {
                    release = new ReleaseCandidate(releaseArtist, candidate.ReleaseTitle!);
                    releases.Add(key, release);
                }

                foreach (var show in candidate.Shows)
                {
                    release.Shows.Add(show);
                }

                if (!string.IsNullOrWhiteSpace(candidate.ReleaseYear))
                {
                    release.Year = candidate.ReleaseYear;
                }

                release.Tracks.Add(new ReleaseTrack(candidate.Track, candidate.Duration));
            }

            return releases.Values
                .OrderBy(candidate => candidate.Artist, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void CreateArtistPage(
            string contentRoot,
            ExistingContentIndex existing,
            ContentCandidate candidate,
            EnrichmentReport report)
        {
            var matches = existing.FindArtist(candidate.Artist);
            if (matches.Count > 1)
            {
                report.Ambiguous.Add($"Artist: {candidate.Artist} matched {string.Join(", ", matches)}");
                return;
            }

            if (matches.Count == 1)
            {
                report.SkippedArtists.Add(candidate.Artist);
                return;
            }

            var path = GetArtistPath(contentRoot, candidate.Artist);
            if (File.Exists(path))
            {
                report.SkippedArtists.Add(candidate.Artist);
                return;
            }

            WriteFile(path, PageBuilder.BuildArtist(candidate));
            existing.AddArtist(candidate.Artist, path);
            report.CreatedArtists.Add(RelativeToRepository(path));
        }

        private static void CreateReleasePage(
            string contentRoot,
            ExistingContentIndex existing,
            ReleaseCandidate candidate,
            EnrichmentReport report)
        {
            var matches = existing.FindRelease(candidate.Artist, candidate.Title);
            if (matches.Count > 1)
            {
                report.Ambiguous.Add($"Release: {candidate.Artist} - {candidate.Title} matched {string.Join(", ", matches)}");
                return;
            }

            if (matches.Count == 1)
            {
                report.SkippedReleases.Add($"{candidate.Artist} - {candidate.Title}");
                return;
            }

            var path = GetReleasePath(contentRoot, candidate.Artist, candidate.Title);
            if (File.Exists(path))
            {
                report.SkippedReleases.Add($"{candidate.Artist} - {candidate.Title}");
                return;
            }

            WriteFile(path, PageBuilder.BuildRelease(candidate));
            existing.AddRelease(candidate.Artist, candidate.Title, path);
            report.CreatedReleases.Add(RelativeToRepository(path));
        }

        private static void CreateTrackPage(
            string contentRoot,
            ExistingContentIndex existing,
            ContentCandidate candidate,
            EnrichmentReport report)
        {
            var matches = existing.FindTrack(candidate.Artist, candidate.Track);
            if (matches.Count > 1)
            {
                report.Ambiguous.Add($"Track: {candidate.Artist} - {candidate.Track} matched {string.Join(", ", matches)}");
                return;
            }

            if (matches.Count == 1)
            {
                report.SkippedTracks.Add($"{candidate.Artist} - {candidate.Track}");
                return;
            }

            var path = GetTrackPath(contentRoot, candidate.Artist, candidate.Track);
            if (File.Exists(path))
            {
                report.SkippedTracks.Add($"{candidate.Artist} - {candidate.Track}");
                return;
            }

            WriteFile(path, PageBuilder.BuildTrack(candidate));
            existing.AddTrack(candidate.Artist, candidate.Track, path);
            report.CreatedTracks.Add(RelativeToRepository(path));
        }

        private static string GetArtistPath(string contentRoot, string artist)
        {
            var artistSlug = ToSlug(artist);
            return Path.Combine(contentRoot, "artists", GetFirstBucket(artistSlug), artistSlug, IndexFileName);
        }

        private static string GetReleasePath(string contentRoot, string artist, string release)
        {
            var artistSlug = ToSlug(artist);
            var releaseSlug = ToSlug(release);
            return Path.Combine(contentRoot, "releases", GetFirstBucket(artistSlug), artistSlug, releaseSlug, IndexFileName);
        }

        private static string GetTrackPath(string contentRoot, string artist, string track)
        {
            var artistSlug = ToSlug(artist);
            var trackSlug = ToSlug(track);
            return Path.Combine(contentRoot, "tracks", GetFirstBucket(artistSlug), artistSlug, trackSlug, IndexFileName);
        }

        private static string GetFirstBucket(string slug)
        {
            var first = slug.Length > 0 ? slug[0] : '0';
            return char.IsAsciiLetterOrDigit(first) ? first.ToString() : "0";
        }

        private static void WriteFile(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, Encoding.UTF8);
        }

        private static string RelativeToRepository(string path)
        {
            return Path.GetRelativePath(Directory.GetCurrentDirectory(), path).Replace('\\', '/');
        }

        [GeneratedRegex("[\u2018\u2019\u201A\u201B']")]
        private static partial Regex ApostropheRegex();

        [GeneratedRegex("[^a-z0-9]+")]
        private static partial Regex NonAlphaNumericRegex();

        [GeneratedRegex("\\s+")]
        private static partial Regex WhitespaceRegex();

        private sealed class ParsedPlaylistResult
        {
            public ParsedPlaylistResult(IReadOnlyList<ParsedPlaylistEntry> entries, int playlistEntriesParsed, int trackInfoEntriesParsed)
            {
                this.Entries = entries;
                this.PlaylistEntriesParsed = playlistEntriesParsed;
                this.TrackInfoEntriesParsed = trackInfoEntriesParsed;
            }

            public IReadOnlyList<ParsedPlaylistEntry> Entries { get; }

            public int PlaylistEntriesParsed { get; }

            public int TrackInfoEntriesParsed { get; }
        }

        private sealed class ParsedPlaylistEntry
        {
            public ParsedPlaylistEntry(string showId, string artist, string track)
            {
                this.ShowId = showId;
                this.Artist = artist;
                this.Track = track;
            }

            public string ShowId { get; }

            public string Artist { get; }

            public string Track { get; }

            public string? ReleaseTitle { get; set; }

            public string? ReleaseArtist { get; set; }

            public string? ReleaseYear { get; set; }

            public string? Duration { get; set; }
        }

        private sealed class ContentCandidate
        {
            public ContentCandidate(string artist, string track)
            {
                this.Artist = artist;
                this.Track = track;
            }

            public string Artist { get; }

            public string Track { get; }

            public SortedSet<string> Shows { get; } = new(StringComparer.OrdinalIgnoreCase);

            public string? ReleaseTitle { get; set; }

            public string? ReleaseArtist { get; set; }

            public string? ReleaseYear { get; set; }

            public string? Duration { get; set; }
        }

        private sealed class ReleaseCandidate
        {
            public ReleaseCandidate(string artist, string title)
            {
                this.Artist = artist;
                this.Title = title;
            }

            public string Artist { get; }

            public string Title { get; }

            public string? Year { get; set; }

            public SortedSet<string> Shows { get; } = new(StringComparer.OrdinalIgnoreCase);

            public List<ReleaseTrack> Tracks { get; } = [];
        }

        private sealed record ReleaseTrack(string Title, string? Duration);

        private static partial class PlaylistParser
        {
            public static ParsedPlaylistResult Parse(IReadOnlyList<string> showDirectories)
            {
                var entries = new Dictionary<string, ParsedPlaylistEntry>(StringComparer.Ordinal);
                var playlistEntriesParsed = 0;
                var trackInfoEntriesParsed = 0;

                foreach (var showDirectory in showDirectories)
                {
                    var showId = Path.GetFileName(showDirectory) ?? string.Empty;

                    foreach (var entry in ParsePlaylist(Path.Combine(showDirectory, "playlist.md"), showId))
                    {
                        playlistEntriesParsed++;
                        entries.TryAdd(GetEntryKey(entry), entry);
                    }

                    foreach (var entry in ParseTrackInfo(Path.Combine(showDirectory, "track-info.md"), showId))
                    {
                        trackInfoEntriesParsed++;
                        entries[GetEntryKey(entry)] = entry;
                    }
                }

                return new ParsedPlaylistResult(entries.Values.ToList(), playlistEntriesParsed, trackInfoEntriesParsed);
            }

            private static IEnumerable<ParsedPlaylistEntry> ParsePlaylist(string path, string showId)
            {
                if (!File.Exists(path))
                {
                    yield break;
                }

                foreach (var line in File.ReadLines(path))
                {
                    var match = PlaylistLineRegex().Match(line);
                    if (!match.Success)
                    {
                        continue;
                    }

                    var title = CleanMarkdown(match.Groups["track"].Value);
                    var artist = ExtractArtist(match.Groups["artist"].Value);
                    if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
                    {
                        yield return new ParsedPlaylistEntry(showId, artist, title);
                    }
                }
            }

            private static IEnumerable<ParsedPlaylistEntry> ParseTrackInfo(string path, string showId)
            {
                if (!File.Exists(path))
                {
                    yield break;
                }

                foreach (var line in File.ReadLines(path))
                {
                    if (!TrackInfoRowRegex().IsMatch(line))
                    {
                        continue;
                    }

                    var cells = SplitMarkdownTableRow(line);
                    if (cells.Count < 4 || cells[1].Contains("track-info-featured-guest", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var titleArtist = ParseTitleArtist(cells[1]);
                    if (string.IsNullOrWhiteSpace(titleArtist.Track) || string.IsNullOrWhiteSpace(titleArtist.Artist))
                    {
                        continue;
                    }

                    var entry = new ParsedPlaylistEntry(showId, titleArtist.Artist, titleArtist.Track)
                    {
                        Duration = CleanMarkdown(cells[3]),
                    };

                    var release = ParseRelease(cells[2], titleArtist.Artist);
                    if (release is not null)
                    {
                        entry.ReleaseTitle = release.Title;
                        entry.ReleaseArtist = release.Artist;
                        entry.ReleaseYear = release.Year;
                    }

                    yield return entry;
                }
            }

            private static string GetEntryKey(ParsedPlaylistEntry entry)
            {
                return $"{entry.ShowId}|{NormalizeKey(entry.Artist)}|{NormalizeKey(entry.Track)}";
            }

            private static IReadOnlyList<string> SplitMarkdownTableRow(string line)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith('|') || !trimmed.EndsWith('|'))
                {
                    return [];
                }

                return trimmed[1..^1].Split('|').Select(cell => cell.Trim()).ToList();
            }

            private static TitleArtist ParseTitleArtist(string cell)
            {
                var shortcode = TitleShortcodeRegex().Match(cell);
                if (shortcode.Success)
                {
                    var parts = shortcode.Groups["value"].Value.Split("--", StringSplitOptions.TrimEntries);
                    if (parts.Length >= 2)
                    {
                        return new TitleArtist(parts[0], parts[1]);
                    }
                }

                return new TitleArtist(CleanMarkdown(cell), string.Empty);
            }

            private static ReleaseInfo? ParseRelease(string cell, string fallbackArtist)
            {
                if (string.IsNullOrWhiteSpace(cell))
                {
                    return null;
                }

                var releaseShortcode = ReleaseShortcodeRegex().Match(cell);
                if (releaseShortcode.Success)
                {
                    var parts = releaseShortcode.Groups["value"].Value.Split("--", StringSplitOptions.TrimEntries);
                    if (parts.Length >= 2)
                    {
                        var title = StripYear(parts[0], out var year);
                        return new ReleaseInfo(title, parts[1], year);
                    }
                }

                var clean = CleanMarkdown(cell);
                if (string.IsNullOrWhiteSpace(clean) ||
                    UnknownReleaseRegex().IsMatch(clean))
                {
                    return null;
                }

                var releaseTitle = StripYear(clean, out var releaseYear);
                return string.IsNullOrWhiteSpace(releaseTitle)
                    ? null
                    : new ReleaseInfo(releaseTitle, fallbackArtist, releaseYear);
            }

            private static string StripYear(string value, out string? year)
            {
                year = null;
                var match = YearSuffixRegex().Match(value);
                if (match.Success)
                {
                    year = match.Groups["year"].Value;
                    return match.Groups["title"].Value.Trim();
                }

                return value.Trim();
            }

            private static string ExtractArtist(string value)
            {
                var artistShortcode = ArtistShortcodeRegex().Match(value);
                return artistShortcode.Success
                    ? artistShortcode.Groups["artist"].Value.Trim()
                    : CleanMarkdown(value);
            }

            private static string CleanMarkdown(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return string.Empty;
                }

                var clean = ShortcodeRegex().Replace(value, string.Empty);
                clean = MarkdownLinkRegex().Replace(clean, "${text}");
                clean = MarkdownStyleRegex().Replace(clean, string.Empty);
                return WhitespaceRegex().Replace(clean, " ").Trim();
            }

            [GeneratedRegex("^\\s*\\d+[.)]\\s*(?<artist>.+?)\\s+-\\s+(?<track>.+?)\\s*$")]
            private static partial Regex PlaylistLineRegex();

            [GeneratedRegex("^\\s*\\|\\s*\\d+\\s*\\|")]
            private static partial Regex TrackInfoRowRegex();

            [GeneratedRegex("\\{\\{<\\s*artist-wikilink\\s+\"(?<artist>[^\"]+)\"\\s*>\\}\\}", RegexOptions.IgnoreCase)]
            private static partial Regex ArtistShortcodeRegex();

            [GeneratedRegex("\\{\\{<\\s*title\\s+\"(?<value>[^\"]+)\"\\s*>\\}\\}", RegexOptions.IgnoreCase)]
            private static partial Regex TitleShortcodeRegex();

            [GeneratedRegex("\\{\\{<\\s*release\\s+\"(?<value>[^\"]+)\"\\s*>\\}\\}", RegexOptions.IgnoreCase)]
            private static partial Regex ReleaseShortcodeRegex();

            [GeneratedRegex("^(single|n/?a|unknown|non[- ]album|tbc|-+)$", RegexOptions.IgnoreCase)]
            private static partial Regex UnknownReleaseRegex();

            [GeneratedRegex("^(?<title>.*?)\\s*\\((?<year>\\d{4})\\)\\s*$")]
            private static partial Regex YearSuffixRegex();

            [GeneratedRegex("\\{\\{<[^>]+>\\}\\}")]
            private static partial Regex ShortcodeRegex();

            [GeneratedRegex("\\[(?<text>[^\\]]+)\\]\\([^)]+\\)")]
            private static partial Regex MarkdownLinkRegex();

            [GeneratedRegex("\\*\\*|__|_")]
            private static partial Regex MarkdownStyleRegex();

            private sealed record TitleArtist(string Track, string Artist);

            private sealed record ReleaseInfo(string Title, string Artist, string? Year);
        }

        private sealed class ExistingContentIndex
        {
            private readonly Dictionary<string, HashSet<string>> artists = new(StringComparer.Ordinal);
            private readonly Dictionary<string, HashSet<string>> releases = new(StringComparer.Ordinal);
            private readonly Dictionary<string, HashSet<string>> tracks = new(StringComparer.Ordinal);

            public static ExistingContentIndex Build(string contentRoot)
            {
                var index = new ExistingContentIndex();
                index.AddExistingArtists(Path.Combine(contentRoot, "artists"));
                index.AddExistingReleases(Path.Combine(contentRoot, "releases"));
                index.AddExistingTracks(Path.Combine(contentRoot, "tracks"));
                return index;
            }

            public IReadOnlyList<string> FindArtist(string artist)
            {
                return this.Find(this.artists, NormalizeKey(artist));
            }

            public IReadOnlyList<string> FindRelease(string artist, string release)
            {
                return this.Find(this.releases, PairKey(artist, release));
            }

            public IReadOnlyList<string> FindTrack(string artist, string track)
            {
                return this.Find(this.tracks, PairKey(artist, track));
            }

            public void AddArtist(string artist, string path)
            {
                this.Add(this.artists, NormalizeKey(artist), path);
                this.Add(this.artists, NormalizeKey(ToSlug(artist)), path);
            }

            public void AddRelease(string artist, string release, string path)
            {
                this.Add(this.releases, PairKey(artist, release), path);
                this.Add(this.releases, PairKey(ToSlug(artist), ToSlug(release)), path);
            }

            public void AddTrack(string artist, string track, string path)
            {
                this.Add(this.tracks, PairKey(artist, track), path);
                this.Add(this.tracks, PairKey(ToSlug(artist), ToSlug(track)), path);
            }

            private static string PairKey(string first, string second)
            {
                return $"{NormalizeKey(first)}|{NormalizeKey(second)}";
            }

            private void AddExistingArtists(string root)
            {
                foreach (var file in EnumerateIndexFiles(root))
                {
                    var frontMatter = FrontMatterReader.Read(file);
                    var title = frontMatter.GetValueOrDefault("title");
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        this.AddArtist(title, file);
                    }

                    this.Add(this.artists, NormalizeKey(Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty), file);
                }
            }

            private void AddExistingReleases(string root)
            {
                foreach (var file in EnumerateIndexFiles(root))
                {
                    var frontMatter = FrontMatterReader.Read(file);
                    var title = frontMatter.GetValueOrDefault("title");
                    var artist = frontMatter.GetValueOrDefault("artist");
                    if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
                    {
                        this.AddRelease(artist, title, file);
                    }

                    var releaseSlug = Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty;
                    var artistSlug = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(file) ?? string.Empty)) ?? string.Empty;
                    this.Add(this.releases, PairKey(artistSlug, releaseSlug), file);
                }
            }

            private void AddExistingTracks(string root)
            {
                foreach (var file in EnumerateIndexFiles(root))
                {
                    var frontMatter = FrontMatterReader.Read(file);
                    var title = frontMatter.GetValueOrDefault("title");
                    var artist = frontMatter.GetValueOrDefault("artist");
                    if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
                    {
                        this.AddTrack(artist, title, file);
                    }

                    var trackSlug = Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty;
                    var artistSlug = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(file) ?? string.Empty)) ?? string.Empty;
                    this.Add(this.tracks, PairKey(artistSlug, trackSlug), file);
                }
            }

            private static IEnumerable<string> EnumerateIndexFiles(string root)
            {
                return Directory.Exists(root)
                    ? Directory.EnumerateFiles(root, IndexFileName, SearchOption.AllDirectories)
                    : [];
            }

            private IReadOnlyList<string> Find(Dictionary<string, HashSet<string>> index, string key)
            {
                return index.TryGetValue(key, out var paths)
                    ? paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Select(RelativeToRepository).ToList()
                    : [];
            }

            private void Add(Dictionary<string, HashSet<string>> index, string key, string path)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return;
                }

                if (!index.TryGetValue(key, out var paths))
                {
                    paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    index.Add(key, paths);
                }

                paths.Add(path);
            }
        }

        private static class FrontMatterReader
        {
            public static IReadOnlyDictionary<string, string> Read(string path)
            {
                var lines = File.ReadLines(path).ToList();
                if (lines.Count < 3 || !string.Equals(lines[0].Trim(), "---", StringComparison.Ordinal))
                {
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in lines.Skip(1))
                {
                    if (string.Equals(line.Trim(), "---", StringComparison.Ordinal))
                    {
                        break;
                    }

                    var separator = line.IndexOf(':', StringComparison.Ordinal);
                    if (separator <= 0)
                    {
                        continue;
                    }

                    var key = line[..separator].Trim();
                    var value = line[(separator + 1)..].Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                    {
                        values[key] = value;
                    }
                }

                return values;
            }
        }

        private static class PageBuilder
        {
            public static string BuildArtist(ContentCandidate candidate)
            {
                var builder = new StringBuilder();
                builder.AppendLine("---");
                AddYamlScalar(builder, "title", candidate.Artist);
                builder.AppendLine("artist_page: true");
                builder.AppendLine("draft: true");
                builder.AppendLine("---");
                builder.AppendLine();
                builder.AppendLine("## About");
                builder.AppendLine();
                builder.AppendLine($"{candidate.Artist} has been featured on Sundown Sessions. This draft page was generated from show playlist data and needs editorial review.");
                return builder.ToString();
            }

            public static string BuildRelease(ReleaseCandidate candidate)
            {
                var builder = new StringBuilder();
                builder.AppendLine("---");
                AddYamlScalar(builder, "title", candidate.Title);
                AddYamlScalar(builder, "artist", candidate.Artist);
                AddYamlScalar(builder, "releaseDate", candidate.Year);
                AddYamlList(builder, "featuredInShows", candidate.Shows);
                AddYamlList(builder, "shows", candidate.Shows);
                builder.AppendLine("tracks:");
                foreach (var track in candidate.Tracks.DistinctBy(track => NormalizeKey(track.Title)))
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"  - title: {YamlQuote(track.Title)}");
                    if (!string.IsNullOrWhiteSpace(track.Duration))
                    {
                        builder.AppendLine(CultureInfo.InvariantCulture, $"    duration: {YamlQuote(track.Duration)}");
                    }
                }

                builder.AppendLine("draft: true");
                builder.AppendLine("---");
                builder.AppendLine();
                builder.AppendLine("## About");
                builder.AppendLine();
                builder.AppendLine($"{candidate.Title} is a release by {candidate.Artist}. This draft page was generated from Sundown Sessions playlist data and needs editorial review.");
                builder.AppendLine();
                builder.AppendLine("## Tracks Featured on Sundown Sessions");
                builder.AppendLine();
                foreach (var track in candidate.Tracks.DistinctBy(track => NormalizeKey(track.Title)))
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"- {track.Title}");
                }

                return builder.ToString();
            }

            public static string BuildTrack(ContentCandidate candidate)
            {
                var builder = new StringBuilder();
                builder.AppendLine("---");
                AddYamlScalar(builder, "title", candidate.Track);
                AddYamlScalar(builder, "artist", candidate.Artist);
                AddYamlScalar(builder, "release", candidate.ReleaseTitle);
                if (!string.IsNullOrWhiteSpace(candidate.ReleaseTitle))
                {
                    AddYamlScalar(builder, "release_slug", ToSlug(candidate.ReleaseTitle));
                }

                AddYamlList(builder, "featuredInShows", candidate.Shows);
                builder.AppendLine("track_page: true");
                builder.AppendLine("draft: true");
                builder.AppendLine("---");
                builder.AppendLine();
                builder.AppendLine("## About");
                builder.AppendLine();
                builder.AppendLine($"{candidate.Track} by {candidate.Artist} has been featured on Sundown Sessions. This draft page was generated from show playlist data and needs editorial review.");
                return builder.ToString();
            }

            private static void AddYamlScalar(StringBuilder builder, string key, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"{key}: {YamlQuote(value)}");
                }
            }

            private static void AddYamlList(StringBuilder builder, string key, IEnumerable<string> values)
            {
                var clean = values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (clean.Count == 0)
                {
                    return;
                }

                builder.AppendLine(CultureInfo.InvariantCulture, $"{key}:");
                foreach (var value in clean)
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"  - {YamlQuote(value)}");
                }
            }

            private static string YamlQuote(string value)
            {
                return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
            }
        }

        private sealed class EnrichmentReport
        {
            public List<string> CreatedArtists { get; } = [];

            public List<string> CreatedReleases { get; } = [];

            public List<string> CreatedTracks { get; } = [];

            public List<string> SkippedArtists { get; } = [];

            public List<string> SkippedReleases { get; } = [];

            public List<string> SkippedTracks { get; } = [];

            public List<string> Ambiguous { get; } = [];

            public void Write(string path, int showsScanned, int playlistEntriesParsed, int trackInfoEntriesParsed)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, this.Build(showsScanned, playlistEntriesParsed, trackInfoEntriesParsed), Encoding.UTF8);
            }

            private string Build(int showsScanned, int playlistEntriesParsed, int trackInfoEntriesParsed)
            {
                var builder = new StringBuilder();
                builder.AppendLine("# Content Enrichment Report");
                builder.AppendLine();
                builder.AppendLine(CultureInfo.InvariantCulture, $"- Shows scanned: {showsScanned}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"- Playlist entries parsed: {playlistEntriesParsed}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"- Track info entries parsed: {trackInfoEntriesParsed}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"- Artist pages created: {this.CreatedArtists.Count}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"- Release pages created: {this.CreatedReleases.Count}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"- Track pages created: {this.CreatedTracks.Count}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"- Existing artists skipped: {this.SkippedArtists.Count}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"- Existing releases skipped: {this.SkippedReleases.Count}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"- Existing tracks skipped: {this.SkippedTracks.Count}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"- Ambiguous items: {this.Ambiguous.Count}");
                builder.AppendLine();
                AddSection(builder, "Created Artists", this.CreatedArtists);
                AddSection(builder, "Created Releases", this.CreatedReleases);
                AddSection(builder, "Created Tracks", this.CreatedTracks);
                AddSection(builder, "Manual Review Required", this.Ambiguous);
                return builder.ToString();
            }

            private static void AddSection(StringBuilder builder, string title, IReadOnlyList<string> items)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"## {title}");
                builder.AppendLine();
                if (items.Count == 0)
                {
                    builder.AppendLine("- None");
                }
                else
                {
                    foreach (var item in items.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
                    {
                        builder.AppendLine(CultureInfo.InvariantCulture, $"- {item}");
                    }
                }

                builder.AppendLine();
            }
        }
    }
}
