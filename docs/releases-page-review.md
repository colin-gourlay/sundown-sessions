# Releases Page Review

This review covers the public Releases index at `/releases/` and individual release detail pages under `/releases/<letter>/<artist>/<release>/`. It is intended as an editorial, UX, accessibility and maintainability roadmap for the Blowfish-based release experience.

## Current Experience Summary

The Releases section already has strong foundations: it uses the shared Blowfish list layout, release cards, artwork, release metadata, artist links, track listings, featured shows and further-discovery modules. Individual release pages feel substantially richer than a simple archive because they combine editorial context with structured data and onward journeys into artists, shows and related releases.

The main opportunity is to make the release index work harder as a discovery page. Visitors should immediately understand what the archive contains, how it is ordered, what each card represents and how to continue exploring if they are looking for a specific artist, era, release type or purchasable record.

## Visible Elements Reviewed

### Releases Index

| Element | Assessment | Recommendation |
| --- | --- | --- |
| Hero section | The section currently relies on the standard list treatment and a concise title. This is consistent with Blowfish but understated for a discovery entry point. | Keep the native Blowfish section pattern, but add a stronger editorial introduction in content rather than a bespoke layout. |
| Strapline and intro copy | The original description was accurate but functional. It did not set expectations around albums, EPs, singles, show connections or purchase cues. | Expand the index copy to explain browsing, artwork, featured shows and purchase indicators. |
| Release cards | Cards are visually aligned with other content types and benefit from the shared card component. | Preserve the shared card implementation; avoid a custom release-only grid unless specific metadata requirements cannot be met through front matter or existing partials. |
| Artwork | Artwork gives the page strong visual rhythm. Mixed artwork dimensions can create varied cropping. | Prefer square or high-quality cover assets in release content and document artwork expectations for future release pages. |
| Artist visibility | Artist names exist in release front matter and on detail pages, but the card title can be ambiguous when a release title matches an artist name. | Consider a future release-card metadata line that surfaces `artist` before date or taxonomy metadata. |
| Release type indicators | Release type taxonomy exists, but indicators are not consistently prominent in the card experience. | Use release-type taxonomy consistently in front matter, then consider exposing a small native badge or taxonomy link on cards. |
| Release date presentation | The list uses the section's normal ordering and metadata patterns; release-specific dates may differ from page dates. | Audit release pages so `date` and `release_date` are intentionally aligned or clearly differentiated. |
| Sorting and grouping | The index inherits site list sorting. This is maintainable, but the page copy should clarify that the archive is browseable rather than a strict discography. | Keep default Blowfish ordering for now; revisit grouping by decade/year only if visitors struggle with discovery at scale. |
| Search and discoverability | Site search is available globally, but the index did not previously point visitors towards it. | Add index copy linking to `/search/` for artist, track and show lookups. |
| Pagination and empty states | Pagination and empty states are provided by the shared list template. | No custom work recommended unless analytics show release browsing drop-off. |
| Mobile and desktop layout | The shared card grid gives a reliable responsive baseline. | Continue using the shared grid and avoid release-specific layout forks. |
| Spacing | Spacing is consistent with other list pages. | Improve perceived hierarchy through content and headings rather than custom spacing overrides. |
| Accessibility | Cards use linked titles and lazy-loaded images; detail pages use semantic sections. Search/discovery routes could be clearer in copy. | Keep headings descriptive, avoid link text such as "click here", and ensure future badges are text-based rather than colour-only. |

### Release Detail Pages

| Element | Assessment | Recommendation |
| --- | --- | --- |
| Hero and artwork | The release hero provides strong page identity and makes good use of artwork. | Keep this as the primary visual anchor. |
| Metadata card | The metadata card helps visitors understand artist, label, dates, producers and related structured fields. | Continue expanding front matter completeness before adding new layout code. |
| Artist section | Artist links support exploration from a release into artist pages. | Maintain this relationship and ensure artist slug/front matter consistency. |
| About content | Editorial copy makes release pages useful beyond raw metadata. | Keep the generated `About the Album/EP/Single` heading behaviour and avoid duplicate markdown headings. |
| Track listings | Structured track listings create a useful music-discovery layer. | Prefer structured `tracks` data for new pages; only use markdown tracklists when structured data is unavailable. |
| Featured shows | Linking releases back to broadcasts is one of the strongest differentiators of the site. | Preserve and prioritise this module because it turns release pages into listening journeys. |
| Purchase links | Purchase availability is valuable, but users need consistent cues from index to detail pages. | Standardise purchase-link front matter and consider a future card-level "available to buy" badge. |
| Explore further and discover more | These modules provide natural onward journeys. | Keep them, but monitor repetition as the release archive grows. |
| Accessibility and maintainability | The page largely reuses partials and theme conventions. | Continue centralising release logic in partials rather than repeating markup in content files. |

## Implementation Status

The recommendations below have been implemented in the site experience: the Releases index now has editorial discovery copy, a featured release rail, search signposting, browse facets for release type/year/label, an available-to-buy section, release cards with artist/year/type/purchase cues, and updated release front-matter guidance for artwork and taxonomy consistency.

## Prioritised Recommendations

### Quick Wins

1. **Strengthen the Releases index introduction** — Implemented
   - **Rationale:** The index should explain that releases are part of a curated listening archive, not just a content dump.
   - **Visitor benefit:** Visitors can immediately understand what they can browse and why release pages are useful.
   - **Complexity:** Low.

2. **Signpost search from the Releases index** — Implemented
   - **Rationale:** Visitors looking for a specific artist, track, label or show need an obvious next step.
   - **Visitor benefit:** Faster discovery without adding custom filtering UI.
   - **Complexity:** Low.

3. **Clarify the meaning of purchase indicators** — Implemented
   - **Rationale:** Purchase links are useful, but they should be presented as a helpful cue rather than a hidden detail.
   - **Visitor benefit:** Visitors can quickly identify releases that can be bought or explored further.
   - **Complexity:** Low to Medium, depending on whether the cue remains detail-page only or appears on cards.

4. **Audit release front matter completeness** — Implemented through front-matter guidance and index fallbacks
   - **Rationale:** The Blowfish-native approach works best when front matter is consistent.
   - **Visitor benefit:** More reliable artist, date, label, release type and show-link presentation.
   - **Complexity:** Low, but can be time-consuming across the archive.

### Nice-to-Have Improvements

1. **Expose artist names more clearly on release cards** — Implemented
   - **Rationale:** Some release titles are not self-explanatory without artist context.
   - **Visitor benefit:** Faster scanning and better recognition in the grid.
   - **Complexity:** Medium.

2. **Add lightweight release-type badges to cards** — Implemented
   - **Rationale:** Album, EP and single distinctions support browsing and editorial context.
   - **Visitor benefit:** Visitors can identify the format before opening a page.
   - **Complexity:** Medium.

3. **Create editorial groupings for featured or recently discussed releases** — Implemented
   - **Rationale:** A pure archive grid can feel flat as the collection grows.
   - **Visitor benefit:** Better entry points for casual discovery.
   - **Complexity:** Medium.

4. **Document release artwork expectations** — Implemented
   - **Rationale:** Consistent artwork improves visual quality without layout customisation.
   - **Visitor benefit:** Cleaner cards and hero sections.
   - **Complexity:** Low.

### Longer-Term Enhancements

1. **Add release-specific faceted discovery** — Implemented
   - **Rationale:** As the archive grows, visitors may want to browse by artist, decade, type, label or availability.
   - **Visitor benefit:** More powerful exploration without relying entirely on search.
   - **Complexity:** High.

2. **Introduce a curated featured-release rail** — Implemented
   - **Rationale:** Editorially selected releases could make the index feel more magazine-like.
   - **Visitor benefit:** Stronger discovery for first-time visitors.
   - **Complexity:** Medium to High.

3. **Review analytics for release-to-show journeys** — Implemented as a documented monitoring requirement
   - **Rationale:** Featured-show links are a core differentiator; analytics can show whether visitors use them.
   - **Visitor benefit:** Future improvements can focus on the most valuable paths.
   - **Complexity:** Medium.

4. **Consider card-level purchase availability** — Implemented
   - **Rationale:** If buying records is an important user goal, availability should be visible before click-through.
   - **Visitor benefit:** Faster recognition of purchasable releases.
   - **Complexity:** Medium to High, depending on data consistency.

## Implementation Notes

- Prefer Blowfish configuration, front matter and shared partials before creating release-only layouts.
- Treat front matter consistency as the main dependency for richer index cards.
- Preserve the current detail-page modules because they already support music discovery, accessibility and long-term maintainability.
- Avoid colour-only status cues for release types or purchase availability; labels should remain readable text.
