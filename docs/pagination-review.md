# Pagination Review

This review records the cross-site pagination decision for issue #502. It covers
the production content and layouts present on 1 September 2026, after the
Blowfish migration and the later Artist and Releases index work.

## Decision

Retain the shared Hugo `pagerSize = 100` setting and Hugo's native paginator.
The current threshold gives the large Releases archive five manageable pages
without fragmenting the small Shows and Tracks collections. There is no evidence
to justify different thresholds for individual sections.

Keep the Artists index as the single deliberate exception. Its A–Z navigation
links to headings in one alphabetically sorted result set, so splitting it into
arbitrary 100-item pages would make some letters appear available while their
targets were on another page. That behaviour was established by the later
Artists index review and should not be undone as part of a cross-site pagination
change.

The review found one concrete deficiency in Blowfish's pagination controls: the
arrow links had no accessible names, the current page was communicated only by
colour, and the list had no navigation landmark. A project-level partial now
retains Blowfish's paginator logic while adding those semantics. The controls
also wrap at narrow widths and have 44 by 44 CSS-pixel targets.

## Current Collection Inventory

Counts below include published leaf pages in the production build. Draft content
does not participate in pagination.

| Surface | Published items | Implementation | Effective behaviour |
| --- | ---: | --- | --- |
| Shows | 4 | `layouts/shows/list.html` sorts dated pages before calling `.Paginate` | One page; no controls are rendered below the threshold |
| Artists | 400 | `layouts/artists/list.html` sorts all artists by title without `.Paginate` | One intentional A–Z index; all letter targets remain available |
| Releases | 499 | `layouts/releases/list.html` sorts `.RegularPagesRecursive` by title before calling `.Paginate` | Five pages at 100, 100, 100, 100 and 99 items |
| Tracks | 8 | The shared default list calls `.Paginate` | One page; no controls are rendered below the threshold |
| Rock genre term | 185 | The shared default list calls `.Paginate` | Two pages at 100 and 85 items |

The Rock genre is the only taxonomy term that currently exceeds the threshold.
The shared default list also covers all other taxonomy and ordinary list pages,
so the same threshold and controls apply when another index grows beyond 100
items. Curated homepage modules and individual Show, Artist, Release and Track
pages do not use archive pagination.

## Behaviour and Accessibility Assessment

- Pagination is applied after each relevant collection has been sorted. A page
  boundary therefore changes only where the ordered set is split; it does not
  restart or independently sort each page.
- Hugo generates crawlable `/page/2/` and later URLs. Previous and next links use
  `rel="prev"` and `rel="next"`, and each numbered page remains an ordinary link.
- The partial renders nothing when `TotalPages` is one, including collections
  exactly at the configured boundary.
- The pagination landmark is labelled `Pagination`. Previous and next controls
  have text alternatives, and `aria-current="page"` identifies the active page
  to assistive technology. Bold, underlined text also identifies it without
  relying on background colour.
- Controls remain native links in document order, so they are keyboard operable.
  The site's shared `:focus-visible` rule supplies a three-pixel focus outline.
- The list wraps rather than introducing horizontal scrolling, and each link has
  a minimum 44 by 44 CSS-pixel activation area.
- The project override changes only the pagination partial. No vendored Blowfish
  file is modified.

## Regression Coverage

`tests/test_pagination.py` builds small Hugo fixtures using the configured page
size and the production pagination partial. It verifies collections immediately
below and exactly at the boundary, then verifies a collection one item above the
boundary. The checks cover generated page URLs, item completeness and ordering,
absence of duplicates, previous and next relationships, the navigation label,
and the current-page state.

The normal production Hugo build remains the integration check for the real
catalogue. The accessibility workflow includes the Releases index, where
pagination is currently active, in its automated WCAG 2 AA scan.

## Revisit Triggers

Reassess the threshold only when catalogue growth or measured visitor behaviour
shows a problem, such as slow archive rendering, unusually high exits on the
first Releases page, or a materially larger Shows or Tracks collection. Review
the Artists exception separately if its single-page performance becomes a
problem; preserving useful alphabetic navigation would be a requirement of that
work rather than a reason to apply arbitrary pagination.
