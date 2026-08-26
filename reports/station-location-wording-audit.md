# Station and Location Wording Audit Report

Generated: 2026-08-26

This report documents the findings of a repository-wide scan for station and location wording across Sundown Sessions content. It covers East Coast FM references, schedule wording, and community-radio and location phrasing including Kirkcaldy, Haddington, and Scotland.

No immediate content changes are required. This report exists to ensure findings are not lost and to inform future standardisation decisions.

## Scope

Findings are divided into two scopes:

- **Canonical content** — curated pages under `src/content/` that form the live site (show index pages, listen-live pages, the about page, and similar). Changes here have direct impact on the published site.
- **Transcript artefacts** — raw or lightly processed text files (`transcript.md`, `transcript-with-timestamps`, `.txt`) that record historical audio. These often contain exact speech from broadcast and may legitimately differ from editorial copy.

---

## Search summary

| Query | Matches | Files |
|---|---|---|
| East Coast FM | 99 | 9 |
| East Coast FM + schedule | 97 | 11 |
| Kirkcaldy, Haddington, community | 50 | 14 |

Exact phrases checked with **no exact match found**:

- `A radio show broadcast on East Coast FM on Tuesday evenings from 7pm - 10pm (UK time)`
- `A community radio show broadcasting from Haddington, Scotland`

---

## Findings

### East Coast FM references — canonical content

| File | Notes |
|---|---|
| `src/content/shows/12/index.md` | East Coast FM reference in show metadata or body copy |
| `src/content/shows/13/index.md` | East Coast FM reference in show metadata or body copy |
| `src/content/shows/14/index.md` | East Coast FM reference in show metadata or body copy |
| `src/content/shows/15/index.md` | East Coast FM reference in show metadata or body copy |
| `src/content/shows/18/index.md` | East Coast FM reference in show metadata or body copy |
| `src/content/shows/19/index.md` | East Coast FM reference in show metadata or body copy |
| `src/content/shows/20/index.md` | East Coast FM reference in show metadata or body copy |
| `src/content/shows/15/track-info.md` | East Coast FM reference in track-guide content |
| `src/content/shows/9/featured-guest.md` | East Coast FM reference in featured-guest content |

### East Coast FM references — template behaviour

| File | Notes |
|---|---|
| `src/layouts/partials/show/hero.html` | Strips a leading phrase matching `live on east coast fm` from teaser text. This may mask upstream inconsistency in show descriptions. |

### Schedule and UK time wording

| File | Notes |
|---|---|
| `src/content/listen-live/index.md` | Contains `Tuesday 8pm–10pm` and `UK time` |
| `src/layouts/listen-live/single.html` | Contains `UK time` |

No direct match found for `Tuesday evenings from 7pm–10pm UK time`.

### Kirkcaldy references — transcript artefacts

| File | Notes |
|---|---|
| `src/content/shows/3/transcript` | Contains `Kirkcaldy's Community Radio` |
| `src/content/shows/3/transcript-with-timestamps` | Contains `Kirkcaldy's Community Radio` |
| `src/content/shows/2/transcript` | Contains Kirkcaldy mention |
| `src/content/shows/2/transcript-with-timestamps` | Contains Kirkcaldy mention |
| `src/content/shows/4/transcript` | Contains Kirkcaldy mention |
| `src/content/shows/4/transcript-with-timestamps` | Contains Kirkcaldy mention |
| `src/content/shows/39/39-show-transcript.md5` | Contains Kirkcaldy mention |

### Haddington and Scotland references

| File | Scope | Notes |
|---|---|---|
| `src/layouts/about/single.html` | Canonical (template) | JSON-LD structured data contains `Haddington, East Lothian, Scotland` |
| `src/content/shows/52/52.txt` | Transcript artefact | Haddington mention |
| `src/content/shows/54/transcript.txt` | Transcript artefact | Haddington mention |
| `src/content/shows/56/56.txt` | Transcript artefact | Haddington mention |

### Community radio station phrasing — transcript artefacts

| File | Notes |
|---|---|
| `src/content/shows/52/52.txt` | Community radio station phrasing tied to East Coast FM |
| `src/content/shows/54/transcript.txt` | Community radio station phrasing tied to East Coast FM |
| `src/content/shows/56/56.txt` | Community radio station phrasing tied to East Coast FM |

---

## Notes on source quality

A large proportion of matches come from transcript and transcript-derived artefacts. These files record historical audio text rather than curated editorial copy, and their wording may legitimately differ from the canonical site.

---

## Deferred follow-up options

- Define a canonical wording policy for station identity, location, and schedule text.
- Separate canonical pages from transcript artefacts in future audits.
- Decide whether the teaser cleanup in `src/layouts/partials/show/hero.html` should remain, be expanded, or be removed after content normalisation.
- Run a canonical-only audit and open a focused clean-up PR once a wording policy is agreed.
