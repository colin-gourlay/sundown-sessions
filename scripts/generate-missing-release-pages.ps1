param(
    [string]$SiteRoot = "src",
    [switch]$Enrich,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

function ConvertTo-NormalisedKey {
    param([AllowNull()][string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }

    $normalised = $Value.Normalize([Text.NormalizationForm]::FormKD).ToLowerInvariant()
    $normalised = $normalised -replace "&", " and "
    $normalised = $normalised -replace "[\u2018\u2019\u201A\u201B']", ""
    $normalised = $normalised -replace "[^a-z0-9]+", " "
    $normalised = $normalised -replace "\s+", " "
    return $normalised.Trim()
}

function ConvertTo-Slug {
    param([string]$Value)
    $key = ConvertTo-NormalisedKey $Value
    $slug = $key -replace "\s+", "-"
    if ([string]::IsNullOrWhiteSpace($slug)) { return "unknown" }
    return $slug
}

function ConvertTo-ReleaseTitleMatchKey {
    param([AllowNull()][string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }

    $title = $Value
    $title = $title -replace "\s*\((?i:[^)]*(remaster|remastered|deluxe|expanded|anniversary|bonus|version|original album mix|legacy edition)[^)]*)\)", ""
    $title = $title -replace "\s+(?i:remaster(ed)?|deluxe edition|expanded edition|bonus remix version|legacy edition|original album mix)$", ""
    $title = $title -replace "\s+-\s+(?i:remaster(ed)?|deluxe edition|expanded edition|bonus remix version|legacy edition|original album mix)$", ""
    return ConvertTo-NormalisedKey $title
}

function Get-FrontMatter {
    param([string]$Path)
    $text = Get-Content -Raw -LiteralPath $Path
    if ($text -notmatch "(?s)^---\r?\n(.*?)\r?\n---") {
        return @{}
    }

    $frontMatter = $Matches[1]
    $values = @{}
    $currentKey = $null
    foreach ($line in ($frontMatter -split "\r?\n")) {
        if ($line -match "^\s*#") { continue }
        if ($line -match "^([A-Za-z0-9_-]+):\s*(.*)$") {
            $currentKey = $Matches[1]
            $raw = $Matches[2].Trim()
            if ($raw -eq "") {
                $values[$currentKey] = @()
            } else {
                $values[$currentKey] = ($raw -replace '^["'']|["'']$', "")
            }
            continue
        }
        if ($currentKey -and $line -match "^\s*-\s*(.+?)\s*$") {
            $item = $Matches[1] -replace '^["'']|["'']$', ""
            if ($values[$currentKey] -isnot [System.Collections.IList]) {
                $values[$currentKey] = @($values[$currentKey])
            }
            $values[$currentKey] += $item
        }
    }

    return $values
}

function Add-ReleaseKey {
    param(
        [hashtable]$Index,
        [string]$Key,
        [string]$Path
    )
    if ([string]::IsNullOrWhiteSpace($Key)) { return }
    if (-not $Index.ContainsKey($Key)) {
        $Index[$Key] = New-Object System.Collections.Generic.List[string]
    }
    $Index[$Key].Add($Path)
}

function Get-ExistingReleaseIndex {
    param([string]$ReleasesRoot)
    $index = @{}
    $entries = New-Object System.Collections.Generic.List[object]

    if (-not (Test-Path -LiteralPath $ReleasesRoot)) {
        return [pscustomobject]@{ Index = $index; Entries = $entries }
    }

    foreach ($file in Get-ChildItem -LiteralPath $ReleasesRoot -Recurse -Filter "index.md") {
        $frontMatter = Get-FrontMatter $file.FullName
        $title = [string]($frontMatter["title"])
        $artist = [string]($frontMatter["artist"])
        if ([string]::IsNullOrWhiteSpace($artist) -and $frontMatter["artists"]) {
            $artist = [string]($frontMatter["artists"] | Select-Object -First 1)
        }

        $releaseSlug = Split-Path -Leaf (Split-Path -Parent $file.FullName)
        $artistSlug = Split-Path -Leaf (Split-Path -Parent (Split-Path -Parent $file.FullName))
        $relativePath = Resolve-Path -LiteralPath $file.FullName -Relative

        foreach ($key in @(
            "pair:$(ConvertTo-NormalisedKey $artist)|$(ConvertTo-NormalisedKey $title)",
            "canonical-pair:$(ConvertTo-NormalisedKey $artist)|$(ConvertTo-ReleaseTitleMatchKey $title)",
            "pair:$(ConvertTo-NormalisedKey $artistSlug)|$(ConvertTo-NormalisedKey $releaseSlug)"
        )) {
            Add-ReleaseKey $index $key $relativePath
        }

        foreach ($aliasField in @("aliases", "alias")) {
            if ($frontMatter[$aliasField]) {
                foreach ($alias in @($frontMatter[$aliasField])) {
                    Add-ReleaseKey $index "pair:$(ConvertTo-NormalisedKey $artist)|$(ConvertTo-NormalisedKey $alias)" $relativePath
                    Add-ReleaseKey $index "canonical-pair:$(ConvertTo-NormalisedKey $artist)|$(ConvertTo-ReleaseTitleMatchKey $alias)" $relativePath
                }
            }
        }

        $entries.Add([pscustomobject]@{
            Title = $title
            Artist = $artist
            ReleaseSlug = $releaseSlug
            ArtistSlug = $artistSlug
            Path = $relativePath
        })
    }

    return [pscustomobject]@{ Index = $index; Entries = $entries }
}

function Split-MarkdownTableRow {
    param([string]$Line)
    $trimmed = $Line.Trim()
    if (-not ($trimmed.StartsWith("|") -and $trimmed.EndsWith("|"))) { return @() }
    $inner = $trimmed.Substring(1, $trimmed.Length - 2)
    return @($inner -split "\|").ForEach({ $_.Trim() })
}

function Remove-MarkdownMarkup {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    $clean = $Value -replace "\{\{<[^>]+>\}\}", ""
    $clean = $clean -replace "\[([^\]]+)\]\([^)]+\)", '$1'
    $clean = $clean -replace "\*\*|__|_", ""
    return ($clean -replace "\s+", " ").Trim()
}

function Get-TitleArtistFromCell {
    param([string]$Cell)
    if ($Cell -match '\{\{<\s*title\s+"([^"]+)"\s*>\}\}') {
        $parts = $Matches[1] -split "--"
        if ($parts.Count -ge 2) {
            return [pscustomobject]@{
                Track = $parts[0].Trim()
                Artist = $parts[1].Trim()
            }
        }
    }

    return [pscustomobject]@{
        Track = (Remove-MarkdownMarkup $Cell)
        Artist = ""
    }
}

function Get-ReleaseFromAlbumCell {
    param(
        [string]$Cell,
        [string]$FallbackArtist
    )

    if ([string]::IsNullOrWhiteSpace($Cell)) { return $null }
    if ($Cell -match "\[RECORD LABEL\]|\[.*?\]|\bTBC\b|^\s*-+\s*$") { return $null }

    if ($Cell -match '\{\{<\s*release\s+"([^"]+)"\s*>\}\}') {
        $parts = $Matches[1] -split "--"
        if ($parts.Count -ge 2) {
            $titleWithYear = $parts[0].Trim()
            $year = ""
            $title = $titleWithYear
            if ($titleWithYear -match "^(.*?)\s*\((\d{4})\)\s*$") {
                $title = $Matches[1].Trim()
                $year = $Matches[2]
            }

            return [pscustomobject]@{
                Title = $title
                Artist = $parts[1].Trim()
                Slug = if ($parts.Count -ge 3) { ConvertTo-Slug $parts[2] } else { ConvertTo-Slug $title }
                Year = $year
                Source = "release-shortcode"
            }
        }
    }

    $clean = Remove-MarkdownMarkup $Cell
    if ([string]::IsNullOrWhiteSpace($clean)) { return $null }

    $year = ""
    $title = $clean
    if ($clean -match "^(.*?)\s*\((\d{4})\)\s*$") {
        $title = $Matches[1].Trim()
        $year = $Matches[2]
    }

    if ($title -match "^(single|n/?a|unknown|non[- ]album)$") { return $null }
    if ([string]::IsNullOrWhiteSpace($FallbackArtist)) { return $null }

    return [pscustomobject]@{
        Title = $title
        Artist = $FallbackArtist
        Slug = ConvertTo-Slug $title
        Year = $year
        Source = "track-info-album-cell"
    }
}

function Get-ShowPlaylistEntries {
    param([string]$ShowsRoot)
    $entries = New-Object System.Collections.Generic.List[object]
    $playlistFiles = Get-ChildItem -LiteralPath $ShowsRoot -Recurse -File |
        Where-Object { $_.Name -match "^playlist(\s|\(|\.|$)" -or $_.Name -eq "playlist" }

    foreach ($file in $playlistFiles) {
        $showId = Split-Path -Leaf $file.DirectoryName
        foreach ($line in Get-Content -LiteralPath $file.FullName) {
            if ($line -match "^\s*\d+[\.\)]\s*(.+?)\s+-\s+(.+?)\s+-\s+\d{1,2}:\d{2}") {
                $entries.Add([pscustomobject]@{
                    Show = $showId
                    Artist = $Matches[1].Trim()
                    Track = $Matches[2].Trim()
                    Source = $file.FullName
                })
            }
        }
    }

    return $entries
}

function Get-ReleaseCandidates {
    param([string]$ShowsRoot)
    $candidates = @{}
    $tracksProcessed = 0

    foreach ($file in Get-ChildItem -LiteralPath $ShowsRoot -Recurse -Filter "track-info.md") {
        $showId = Split-Path -Leaf $file.DirectoryName
        foreach ($line in Get-Content -LiteralPath $file.FullName) {
            if ($line -notmatch "^\s*\|\s*\d+\s*\|") { continue }
            $cells = Split-MarkdownTableRow $line
            if ($cells.Count -lt 4) { continue }

            $trackInfo = Get-TitleArtistFromCell $cells[1]
            $release = Get-ReleaseFromAlbumCell $cells[2] $trackInfo.Artist
            $tracksProcessed++
            if (-not $release) { continue }

            $artistKey = ConvertTo-NormalisedKey $release.Artist
            $titleKey = ConvertTo-NormalisedKey $release.Title
            $candidateKey = "$artistKey|$titleKey"

            if (-not $candidates.ContainsKey($candidateKey)) {
                $candidates[$candidateKey] = [pscustomobject]@{
                    Title = $release.Title
                    Artist = $release.Artist
                    Slug = $release.Slug
                    Year = $release.Year
                    Sources = New-Object System.Collections.Generic.HashSet[string]
                    Shows = New-Object System.Collections.Generic.HashSet[string]
                    FeaturedTracks = New-Object System.Collections.Generic.HashSet[string]
                }
            }

            [void]$candidates[$candidateKey].Sources.Add($release.Source)
            [void]$candidates[$candidateKey].Shows.Add($showId)
            if ($trackInfo.Track) {
                [void]$candidates[$candidateKey].FeaturedTracks.Add($trackInfo.Track)
            }
            if (-not $candidates[$candidateKey].Year -and $release.Year) {
                $candidates[$candidateKey].Year = $release.Year
            }
        }
    }

    return [pscustomobject]@{
        Candidates = $candidates.Values
        TracksProcessed = $tracksProcessed
    }
}

function Find-ExistingRelease {
    param(
        [hashtable]$Index,
        [object]$Candidate
    )
    $keys = @(
        "pair:$(ConvertTo-NormalisedKey $Candidate.Artist)|$(ConvertTo-NormalisedKey $Candidate.Title)",
        "canonical-pair:$(ConvertTo-NormalisedKey $Candidate.Artist)|$(ConvertTo-ReleaseTitleMatchKey $Candidate.Title)",
        "pair:$(ConvertTo-NormalisedKey (ConvertTo-Slug $Candidate.Artist))|$(ConvertTo-NormalisedKey $Candidate.Slug)"
    )

    $matches = New-Object System.Collections.Generic.HashSet[string]
    foreach ($key in $keys) {
        if ($Index.ContainsKey($key)) {
            foreach ($path in $Index[$key]) {
                [void]$matches.Add($path)
            }
        }
    }

    return @($matches)
}

function Get-MusicBrainzJson {
    param([string]$Uri)
    Start-Sleep -Milliseconds 1100
    return Invoke-RestMethod -Uri $Uri -Headers @{
        "User-Agent" = "SundownSessionsReleaseGenerator/1.0 (https://github.com/colin-gourlay/sundown-sessions)"
    }
}

function Get-MusicBrainzMetadata {
    param([object]$Candidate)
    $query = [uri]::EscapeDataString(("release:`"{0}`" AND artist:`"{1}`"" -f $Candidate.Title, $Candidate.Artist))
    $rgUri = "https://musicbrainz.org/ws/2/release-group/?query=$query&fmt=json&limit=5"
    $groups = Get-MusicBrainzJson $rgUri
    $candidateTitleKey = ConvertTo-NormalisedKey $Candidate.Title
    $candidateArtistKey = ConvertTo-NormalisedKey $Candidate.Artist
    $group = @($groups.'release-groups') |
        Where-Object {
            (ConvertTo-NormalisedKey $_.title) -eq $candidateTitleKey -and
            (ConvertTo-NormalisedKey (@($_.'artist-credit' | ForEach-Object { $_.name }) -join " ")) -match [regex]::Escape($candidateArtistKey)
        } |
        Select-Object -First 1

    if (-not $group) { return $null }

    $releaseUri = "https://musicbrainz.org/ws/2/release?release-group=$($group.id)&inc=media+recordings+labels+artist-credits&fmt=json&limit=10"
    $releaseResult = Get-MusicBrainzJson $releaseUri
    $release = @($releaseResult.releases) |
        Sort-Object @{ Expression = { if ($_.status -eq "Official") { 0 } else { 1 } } }, @{ Expression = { if ($_.date) { $_.date } else { "9999" } } } |
        Select-Object -First 1

    $tracks = @()
    $durationMs = 0
    if ($release -and $release.media) {
        foreach ($medium in @($release.media)) {
            foreach ($track in @($medium.tracks)) {
                $title = [string]$track.title
                if ([string]::IsNullOrWhiteSpace($title)) { continue }
                $duration = ""
                if ($track.length) {
                    $durationMs += [int64]$track.length
                    $ts = [TimeSpan]::FromMilliseconds([int64]$track.length)
                    $duration = "{0:mm\:ss}" -f $ts
                    if ($ts.TotalHours -ge 1) { $duration = "{0:h\:mm\:ss}" -f $ts }
                }
                $tracks += [pscustomobject]@{
                    Title = $title
                    Number = [int]$track.position
                    Duration = $duration
                }
            }
        }
    }

    $labels = @()
    if ($release -and $release.'label-info') {
        foreach ($labelInfo in @($release.'label-info')) {
            if ($labelInfo.label.name) { $labels += [string]$labelInfo.label.name }
        }
    }

    $genres = @()
    if ($group.tags) {
        $genres = @($group.tags | Sort-Object count -Descending | Select-Object -First 5 | ForEach-Object { $_.name })
    }

    $duration = ""
    if ($durationMs -gt 0) {
        $ts = [TimeSpan]::FromMilliseconds($durationMs)
        $duration = "{0:h\:mm\:ss}" -f $ts
    }

    return [pscustomobject]@{
        ReleaseDate = if ($group.'first-release-date') { [string]$group.'first-release-date' } elseif ($release.date) { [string]$release.date } else { "" }
        ReleaseType = [string]$group.'primary-type'
        Labels = @($labels | Where-Object { $_ } | Select-Object -Unique)
        Genres = @($genres | Where-Object { $_ } | Select-Object -Unique)
        Tracks = $tracks
        Duration = $duration
        Links = @{ MusicBrainz = "https://musicbrainz.org/release-group/$($group.id)" }
    }
}

function ConvertTo-YamlString {
    param([string]$Value)
    $escaped = $Value -replace '"', '\"'
    return '"' + $escaped + '"'
}

function Add-YamlScalar {
    param([System.Text.StringBuilder]$Builder, [string]$Key, [AllowNull()][string]$Value)
    if (-not [string]::IsNullOrWhiteSpace($Value)) {
        [void]$Builder.AppendLine("$($Key): $(ConvertTo-YamlString $Value)")
    }
}

function Add-YamlList {
    param([System.Text.StringBuilder]$Builder, [string]$Key, [array]$Values)
    $clean = @($Values | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } | Select-Object -Unique)
    if ($clean.Count -eq 0) { return }
    [void]$Builder.AppendLine("$($Key):")
    foreach ($value in $clean) {
        [void]$Builder.AppendLine("  - $(ConvertTo-YamlString ([string]$value))")
    }
}

function New-ReleasePageContent {
    param(
        [object]$Candidate,
        [AllowNull()][object]$Metadata
    )

    $releaseDate = ""
    if ($Metadata -and $Metadata.ReleaseDate) { $releaseDate = $Metadata.ReleaseDate }
    elseif ($Candidate.Year) { $releaseDate = [string]$Candidate.Year }

    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.AppendLine("---")
    Add-YamlScalar $builder "title" $Candidate.Title
    Add-YamlScalar $builder "artist" $Candidate.Artist
    Add-YamlScalar $builder "releaseDate" $releaseDate
    if ($Metadata) {
        Add-YamlScalar $builder "releaseType" $Metadata.ReleaseType
        Add-YamlList $builder "genres" $Metadata.Genres
        Add-YamlList $builder "labels" $Metadata.Labels
        Add-YamlScalar $builder "duration" $Metadata.Duration
    }
    $sortedShows = @($Candidate.Shows) | Sort-Object { [int]($_ -replace "\D", "") }, { $_ }
    Add-YamlList $builder "featuredInShows" $sortedShows
    Add-YamlList $builder "shows" $sortedShows

    if ($Metadata -and $Metadata.Tracks.Count -gt 0) {
        [void]$builder.AppendLine("tracks:")
        foreach ($track in $Metadata.Tracks) {
            [void]$builder.AppendLine("  - title: $(ConvertTo-YamlString $track.Title)")
            if ($track.Number) { [void]$builder.AppendLine("    trackNumber: $($track.Number)") }
            if ($track.Duration) { [void]$builder.AppendLine("    duration: $(ConvertTo-YamlString $track.Duration)") }
        }
    }

    if ($Metadata -and $Metadata.Links.Count -gt 0) {
        [void]$builder.AppendLine("links:")
        foreach ($key in ($Metadata.Links.Keys | Sort-Object)) {
            [void]$builder.AppendLine("  $($key): $(ConvertTo-YamlString $Metadata.Links[$key])")
        }
    }

    Add-YamlScalar $builder "lastmod" (Get-Date -Format "yyyy-MM-dd")
    [void]$builder.AppendLine("---")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## About")
    [void]$builder.AppendLine()

    $showCount = @($Candidate.Shows).Count
    $trackNames = @($Candidate.FeaturedTracks) | Sort-Object
    $trackText = ""
    if ($trackNames.Count -gt 0) {
        $trackText = " Featured tracks include " + (($trackNames | Select-Object -First 3) -join ", ") + "."
    }

    $dateText = if ($releaseDate) { " released in $releaseDate" } else { "" }
    [void]$builder.AppendLine(("{0} is a release by {1}{2}. It has been featured on {3} Sundown Sessions show{4}.{5}" -f $Candidate.Title, $Candidate.Artist, $dateText, $showCount, $(if ($showCount -eq 1) { "" } else { "s" }), $trackText))
    [void]$builder.AppendLine()

    if (-not ($Metadata -and $Metadata.Tracks.Count -gt 0) -and $trackNames.Count -gt 0) {
        [void]$builder.AppendLine("## Tracks Featured on Sundown Sessions")
        [void]$builder.AppendLine()
        foreach ($track in $trackNames) {
            [void]$builder.AppendLine("- $track")
        }
        [void]$builder.AppendLine()
    }

    return $builder.ToString()
}

$sitePath = Join-Path (Get-Location) $SiteRoot
$showsRoot = Join-Path $sitePath "content/shows"
$releasesRoot = Join-Path $sitePath "content/releases"
$reportPath = Join-Path (Get-Location) "reports/missing-release-pages-report.md"

$existing = Get-ExistingReleaseIndex $releasesRoot
$playlistEntries = Get-ShowPlaylistEntries $showsRoot
$candidateResult = Get-ReleaseCandidates $showsRoot

$created = New-Object System.Collections.Generic.List[string]
$skipped = New-Object System.Collections.Generic.List[string]
$duplicates = New-Object System.Collections.Generic.List[string]
$manualReview = New-Object System.Collections.Generic.List[string]
$errors = New-Object System.Collections.Generic.List[string]

foreach ($candidate in ($candidateResult.Candidates | Sort-Object Artist, Title)) {
    $matches = Find-ExistingRelease $existing.Index $candidate
    if ($matches.Count -gt 1) {
        $duplicates.Add("$($candidate.Artist) - $($candidate.Title): $($matches -join ', ')")
        continue
    }
    if ($matches.Count -eq 1) {
        $skipped.Add("$($candidate.Artist) - $($candidate.Title)")
        continue
    }

    $metadata = $null
    if ($Enrich) {
        try {
            $metadata = Get-MusicBrainzMetadata $candidate
            if (-not $metadata) {
                $manualReview.Add("$($candidate.Artist) - $($candidate.Title): MusicBrainz match not found")
            }
        } catch {
            $manualReview.Add("$($candidate.Artist) - $($candidate.Title): MusicBrainz lookup failed")
            $errors.Add("$($candidate.Artist) - $($candidate.Title): $($_.Exception.Message)")
        }
    } else {
        $manualReview.Add("$($candidate.Artist) - $($candidate.Title): not externally enriched")
    }

    $artistSlug = ConvertTo-Slug $candidate.Artist
    $releaseSlug = ConvertTo-Slug $candidate.Slug
    $first = $artistSlug.Substring(0, 1)
    if ($first -notmatch "[a-z0-9]") { $first = "0" }
    $directory = Join-Path $releasesRoot (Join-Path $first (Join-Path $artistSlug $releaseSlug))
    $path = Join-Path $directory "index.md"

    if (Test-Path -LiteralPath $path) {
        $skipped.Add("$($candidate.Artist) - $($candidate.Title)")
        continue
    }

    $content = New-ReleasePageContent $candidate $metadata
    if (-not $WhatIf) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
        Set-Content -LiteralPath $path -Value $content -Encoding UTF8
    }
    $createdPath = Resolve-Path -LiteralPath $path -Relative -ErrorAction SilentlyContinue
    if (-not $createdPath) { $createdPath = $path }
    $created.Add([string]$createdPath)
}

if (-not $WhatIf) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $reportPath) | Out-Null
}

$report = [System.Text.StringBuilder]::new()
[void]$report.AppendLine("# Missing Release Pages Report")
[void]$report.AppendLine()
[void]$report.AppendLine("- Shows scanned: $((Get-ChildItem -LiteralPath $showsRoot -Directory).Count)")
[void]$report.AppendLine("- Tracks processed: $($candidateResult.TracksProcessed)")
[void]$report.AppendLine("- Playlist entries parsed: $($playlistEntries.Count)")
[void]$report.AppendLine("- Releases identified: $($candidateResult.Candidates.Count)")
[void]$report.AppendLine("- Release pages created: $($created.Count)")
[void]$report.AppendLine("- Existing releases skipped: $($skipped.Count)")
[void]$report.AppendLine("- Potential duplicates: $($duplicates.Count)")
[void]$report.AppendLine("- Metadata requiring manual review: $($manualReview.Count)")
[void]$report.AppendLine("- Errors: $($errors.Count)")
[void]$report.AppendLine()

foreach ($section in @(
    @{ Title = "Created"; Items = $created },
    @{ Title = "Potential Duplicates"; Items = $duplicates },
    @{ Title = "Manual Review"; Items = $manualReview },
    @{ Title = "Errors"; Items = $errors }
)) {
    [void]$report.AppendLine("## $($section.Title)")
    [void]$report.AppendLine()
    if ($section.Items.Count -eq 0) {
        [void]$report.AppendLine("- None")
    } else {
        foreach ($item in $section.Items) {
            [void]$report.AppendLine("- $item")
        }
    }
    [void]$report.AppendLine()
}

if (-not $WhatIf) {
    Set-Content -LiteralPath $reportPath -Value $report.ToString() -Encoding UTF8
}

Write-Output $report.ToString()
