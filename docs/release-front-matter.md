# Release Front Matter

Release pages support compact music metadata for discovery and taxonomy pages.
Prefer the plural fields for taxonomy-backed values, while the release
templates still accept the older singular fields used by existing content.

```yaml
genres:
  - Alternative Rock
  - Stoner Rock

labels:
  - Matador Records

releaseType: Album
releaseDate: 2017-08-25

producers:
  - Mark Ronson

tags:
  - alternative rock
  - queens of the stone age
```

Existing alternatives are also supported for rendering:

- `genre` or `genres`
- `label` or `labels`
- `producer` or `producers`
- `releaseType` or `release_type`
- `releaseDate`, `release_date`, or `date`

Hugo taxonomy pages are generated from taxonomy fields that exist in front
matter. Use `genres`, `labels`, `producers`, `tags`, `years`, and
`release-types` when a release should be grouped on taxonomy pages. Keep
`releaseType` and `releaseDate` as the human-friendly canonical release fields;
add `release-types` and `years` alongside them when native Hugo taxonomy
indexing is wanted.
