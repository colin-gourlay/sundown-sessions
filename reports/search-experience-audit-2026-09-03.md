# Search Experience Audit

Audit date: 3 September 2026  
Audited revision: `f2749b91`  
Related issue: [#576](https://github.com/colin-gourlay/sundown-sessions/issues/576)

## Executive Summary

The current search is easy to find, quick to open and effective for exact
published Artist, Release and Track titles. Its relevance model generally puts
the destination implied by an exact query first without imposing an arbitrary
content-type hierarchy. The Blowfish and Fuse.js foundation should be retained.

Four evidenced shortcomings merit focused follow-up work:

1. Empty and no-result states do not help the visitor, and clearing a query
   renders all 1,539 index entries.
2. Strict punctuation matching means `Post Punk` misses the canonical
   `Post-Punk` genre result.
3. A displayed broadcast date is not a search field, so a natural date query
   such as `19 June 2024` misses the relevant Show.
4. The broad index includes duplicate taxonomy destinations, utility pages and
   four empty Artist placeholders.

These are bounded configuration or project-override problems. The audit found
no evidence that Sundown Sessions needs autocomplete, personalisation, a
recommendation engine or an external search service.

## Method

The review used a production Hugo build made with Hugo Extended 0.165.0 and the
vendored Blowfish 2.105.0 theme. The generated `index.json` was queried with the
same Fuse.js 7.5.0 library and options used by the browser. Source inspection
covered the project search partial, responsive header overrides, output
configuration, Blowfish index template and Blowfish search client.

Entry-point layouts were rendered in headless Google Chrome at 1,440 by 1,000,
768 by 1,024 and 390 by 844 pixels. The modal's responsive classes, scrolling
container and result-card structure were also inspected. Existing modal
semantics and focus-management defects are intentionally not reassessed here;
they remain within their dedicated accessibility work.

## Discoverability and Responsive Usability

Search is consistently exposed as a recognisable magnifying-glass button in
the right side of the header:

- the desktop header places it beside the appearance control and Listen Live;
- the tablet and mobile header places it beside the appearance and menu
  controls;
- both controls have a `Search` accessible name and a title which advertises
  the `/` keyboard shortcut.

At all three audited widths, search remains visible without competing with the
primary Shows, About, Contact and Listen Live journeys. This is the right level
of prominence for a complementary discovery tool.

The modal uses the available viewport, reduces outer padding on small screens,
caps line length at `max-w-3xl`, and keeps the results section independently
scrollable. Result links occupy the card width and have vertical and horizontal
padding, so titles, metadata and summaries remain readable and selectable on a
narrow screen. No search-specific responsive change is justified by the
evidence gathered.

## Current Implementation

### Project and theme responsibilities

The project enables search and the home JSON output through configuration. It
overrides Blowfish's `search.html` partial to provide the modal markup, while
retaining Blowfish's index template and JavaScript implementation. No vendored
theme file has been modified for Sundown-specific ranking.

On first opening the modal, the client fetches `/index.json` and builds an
in-memory Fuse index. Subsequent key-up events search that index and replace the
result list. Search is entirely client-side and requires JavaScript.

### Indexed fields and ranking

Each index entry can contain:

- title;
- resolved section title;
- Hugo summary;
- complete rendered plain-text content;
- formatted date, when present;
- relative permalink, optional external URL and Hugo content type.

Fuse searches only title, section, summary and content. Their weights are 0.8,
0.2, 0.6 and 0.4 respectively. Results are sorted by Fuse relevance with
location ignored and a strict threshold of 0.0. Date and type are returned for
presentation but do not contribute to matching. There is no explicit global
Artist, Show, Release or Track preference.

### Index scope

The audited production index is 791,029 bytes and contains 1,539 entries:

| Content type | Entries |
| --- | ---: |
| Releases | 500 |
| Artists | 401 |
| Genres | 247 |
| Labels | 218 |
| Tags | 109 |
| Producers | 19 |
| Years | 18 |
| Tracks | 9 |
| General pages | 6 |
| Shows, including the section page | 5 |
| Release types | 5 |
| Categories | 1 |
| Upcoming | 1 |

Hugo excludes the 19 draft pages from the production build. The index template
also honours `excludeFromSearch`, although no current content file uses it.
Because it otherwise ranges over all site pages, published taxonomy terms and
utility pages enter the index automatically. The production configuration
builds future-dated content, so a published future Upcoming page can also be
indexed by design.

Only 78 entries provide a non-empty summary, and 633 have no searchable body
content. Four published Artist placeholders have no title, summary or content:
Alphaville, Bruce Springsteen, The Korgis and The Prime Movers. They cannot
produce a useful result card.

### Result presentation

Every result card shows its title and section, conditionally shows a date, and
renders a summary when one exists. These cues are compact and sufficient to
distinguish an Artist, Release, Track or Show; artwork would add download and
visual cost without solving an observed choice problem.

The main limitation is data coverage rather than card design. Most indexed
items have no summary, and taxonomy results frequently have neither summary nor
body content. Clearer content-type wording may help in individual cases, but it
should be considered only after the index scope is corrected.

## Representative Queries

The result positions below reproduce the production client configuration.
Only useful leading results are listed; weak incidental matches are noted where
they affect the visitor journey.

| Query | Outcome | Assessment |
| --- | --- | --- |
| `Queens of the Stone Age` | Artist 1st, matching tag 2nd, Track 3rd | Canonical Artist is correctly first and associated content supports discovery, but the tag duplicates the Artist destination. |
| `Big Country` | Artist 1st, matching tag 2nd, Tony Butler 3rd, Releases 4th and 5th | Strong canonical result and useful relationships, with the same duplicate-tag issue. |
| `The Chameleons` | Artist 1st; one weak Release match | Correct for the content currently published. |
| `Redemption ZERO` | No results | No matching published destination exists in the audited archive. The result is reasonable, but the blank UI does not explain it. |
| `Post Punk` | Four weak Artist matches; no genre result | Poor: an ordinary punctuation variant misses the intended taxonomy. |
| `Post-Punk` | Genre 1st, tag 2nd, Post-Punk Revival 3rd and 4th | Relevant canonical genre is first, though taxonomy duplication is visible. |
| `Feet Don't Fail Me` | Track 1st and only result | Exact Track intent is handled correctly. |
| `Take Me Out` | Track 1st, Artist 2nd, Show 3rd | Excellent intent-sensitive order and useful onward discovery. |
| `Fat Cats Drown in Milk` | Track 1st, Show 2nd | Exact Track is prioritised correctly. |
| `Cabinet of Curiosities` | Release 1st, Artist 2nd, Show 3rd | Exact Release is prioritised with useful related destinations. |
| `Villains` | Release 1st, Track 2nd, Artist 3rd, Show 4th | Good Release-title ordering and discovery path. |
| `The Crossing` | Release 1st and only result | Correct exact Release result. |
| `Propaganda` | Track and Release joint-leading, Artist 3rd | Appropriate ambiguity: multiple exact titles are shown before related content. |
| `Show #3` | Show 1st, one weak Release match | Recognisable stored show identifier works. |
| `19 June 2024` | No results | Poor: this is the displayed date for Show #3, but date is not searched and the title contains an ordinal suffix. |

An empty search initially shows no entries because no query has run. After a
visitor types and then clears a query, Fuse receives an empty string and
returns the complete 1,539-entry corpus. A non-empty unmatched query renders an
empty list with no status or browsing guidance. Weak matches are permitted only
when every query character occurs in a field, but field-length normalisation
can still make their relevance opaque to visitors.

## Strengths to Preserve

- Search is globally available without displacing curated navigation.
- Exact Artist, Release and Track titles reliably put the likely destination
  first.
- Ordering follows query relevance rather than a fixed content hierarchy.
- Full page content makes associated Shows and Releases discoverable after many
  exact searches.
- Section, date and summary metadata keep cards compact and understandable when
  the underlying data is present.
- The responsive modal and scrollable result list work within small viewports.
- Drafts are not leaked into the production index.
- The local override boundary keeps the vendored theme maintainable.

## Prioritised Recommendations

| Priority | Recommendation | Visitor impact | Complexity | Follow-up |
| --- | --- | --- | --- | --- |
| 1 | Handle empty and no-result states explicitly. | High: prevents an unbounded list and gives unsuccessful visitors a way forward. | Low | [#920](https://github.com/colin-gourlay/sundown-sessions/issues/920) |
| 2 | Normalise common space and hyphen variants before strict matching. | Medium: fixes a demonstrated intended genre query without broad fuzzy matching. | Low | [#921](https://github.com/colin-gourlay/sundown-sessions/issues/921) |
| 3 | Add searchable show identifiers and displayed dates. | Medium: supports visitors who remember when or which show aired. | Low to medium | [#922](https://github.com/colin-gourlay/sundown-sessions/issues/922) |
| 4 | Explicitly narrow the index to useful destinations. | Medium: removes empty and duplicate choices and limits growth. | Medium | [#923](https://github.com/colin-gourlay/sundown-sessions/issues/923) |

The first three changes can be addressed with a small project-level JavaScript
override and/or index configuration. Index-scope work should retain genuinely
useful genre discovery and validate representative queries before excluding a
whole content kind. Rich cards, suggestions and alternative search platforms
remain unsupported by the evidence and are not recommended.
