# Release Front Matter

Release pages support compact music metadata for discovery, taxonomy pages and the Releases index. Prefer the plural fields for taxonomy-backed values, while the release templates still accept the older singular fields used by existing content.

```yaml
title: Villains
artist: Queens of the Stone Age
artist_slug: queens-of-the-stone-age

releaseType: Album
release-types:
  - Album

releaseDate: 2017-08-25
release_date: "2017"
years:
  - "2017"

labels:
  - Matador Records

genres:
  - Alternative Rock
  - Stoner Rock

producers:
  - Mark Ronson

tags:
  - alternative rock
  - queens of the stone age

shows:
  - "12"

for_sale: true
links:
  - title: Buy from Bandcamp
    url: https://example.com/release
```

Existing alternatives are also supported for rendering:

- `genre` or `genres`
- `label` or `labels`
- `producer` or `producers`
- `releaseType` or `release_type`
- `releaseDate`, `release_date`, or `date`
- `artist` or `artists`

`releaseType` is required for new Release pages. The recognised values live in
`src/data/release-types.yaml`, so the vocabulary can be extended deliberately.
Use `releaseTypeReview: true` only for an existing release whose type has been
explicitly reviewed but cannot yet be verified; never use a placeholder such as
`Unknown`. Run `python3 scripts/audit-release-types.py` after changing release
metadata and commit the regenerated audit report.

Hugo taxonomy pages are generated from taxonomy fields that exist in front matter. Use `genres`, `labels`, `producers`, `tags`, `years`, and `release-types` when a release should be grouped on taxonomy pages. Keep `releaseType` and `releaseDate` as the human-friendly canonical release fields; add `release-types` and `years` alongside them when native Hugo taxonomy indexing is wanted.

## Artwork Expectations

Release artwork is used in the release hero, release cards, featured-release rails and related discovery modules. Consistent artwork keeps the archive visually balanced without requiring release-specific layout overrides.

- Prefer square cover artwork at **1200 × 1200 px** or larger where licensing and source quality allow.
- Use clear front-cover crops with no extra borders, mockups or perspective effects.
- Keep local image filenames descriptive, for example `cover.jpg`, `cover.png` or `villains-cover.jpg`.
- Avoid very small, blurry or heavily compressed artwork because card crops and hero backgrounds make quality issues more visible.
- Set or provide artwork consistently before relying on richer release-card metadata such as release type, year or purchase availability.

## Discovery and Purchase Cues

The Releases index now surfaces release types, years, labels, featured release pathways and available-to-buy cues. To keep those sections useful:

- Add `releaseType` plus `release-types` for releases that should appear under type facets.
- Add `releaseDate` plus `years` for releases that should appear under year facets.
- Add `labels` for releases that should appear under label facets.
- Add `for_sale: true` only when the release has a reliable buying path or explicit purchase context.
- Keep `shows` current so release cards and detail pages can connect records back to the broadcasts that featured them.
