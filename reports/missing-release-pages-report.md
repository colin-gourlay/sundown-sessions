# Missing Release Pages Report

- Shows scanned: 35
- Tracks processed: 403
- Playlist entries parsed: 134
- Releases identified: 325
- Release pages created: 249
- Existing releases skipped: 73
- Potential duplicates: 3
- Metadata requiring manual review: 73
- Errors: 0

## Created

- 249 release pages were generated under `src/content/releases`.
- 176 generated pages include MusicBrainz metadata and links.
- 73 generated pages were created from local show metadata only and should be manually reviewed when richer release metadata is needed.

## Potential Duplicates

- Suede - Dog Man Star (30th Anniversary Edition): `src/content/releases/s/suede/dog-man-star-30th-anniversary-edition/index.md`, `src/content/releases/s/suede/dog-man-star/index.md`
- The Move - Shazam: `src/content/releases/t/the-move/shazam/index.md`, `src/content/releases/t/the-move/shazam-2007-remaster/index.md`
- The Move - Shazam (2007 Remaster): `src/content/releases/t/the-move/shazam-2007-remaster/index.md`, `src/content/releases/t/the-move/shazam/index.md`

## Manual Review

- 73 generated pages do not have a confident MusicBrainz match.
- Find them with:

```powershell
$files = git ls-files --others --exclude-standard 'src/content/releases/**/index.md'
foreach ($file in $files) {
  if ((Get-Content -Raw $file) -notmatch 'musicbrainz.org') { $file }
}
```

## Errors

- None
