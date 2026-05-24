# Hugo Theme Investigation and Recommendation

Status: **Investigation complete — Blowfish recommended**
Scope: Visual/UX modernisation of the Sundown Sessions Hugo website.
Constraint: Existing content must not be changed or lost.

This document records the investigation requested by the "Investigate Modern
Hugo Theme Alternatives Without Changing Existing Content" issue. It audits the
current site, compares candidate themes, summarises the prototype already
staged in this repository, and records the recommended direction so that any
follow-up migration work can proceed against an agreed baseline.

No site content, layouts, or production configuration are modified by this
document.

## 1. Current Site Audit

The audit reflects the repository at the time this investigation was recorded.
It is intentionally descriptive rather than prescriptive — the goal is to make
the content model explicit so a theme migration can preserve it.

### 1.1 Theme baseline

- Active theme: `sundown-sessions`, a project-local theme under
  [`src/themes/sundown-sessions/`](../src/themes/sundown-sessions/). It is a
  long-running fork of the Ananke theme, customised in place over time.
- Hugo configuration: [`src/config/_default/hugo.toml`](../src/config/_default/hugo.toml)
  pins `theme = 'sundown-sessions'`, `languageCode = 'en-GB'`,
  `timeZone = 'Europe/London'`, and `buildFuture = true`.
- Site parameters and social links:
  [`src/config/_default/params.toml`](../src/config/_default/params.toml),
  including a `custom_css` list that layers
  `css/tables.css`, `css/live-player.css`, `css/tiles.css`, and `css/pills.css`
  from [`src/static/css/`](../src/static/css/) on top of the theme bundle.
- CSS pipeline: the theme concatenates and minifies
  `src/themes/sundown-sessions/assets/css/*.css` via
  `layouts/partials/func/style/GetMainCSS.html` into `css/main.min.css`. There
  is no Sass/SCSS preprocessing.
- Deployment target: GitHub Pages, via
  [`.github/workflows/deploy-github-pages.yml`](../.github/workflows/deploy-github-pages.yml).

### 1.2 Content sections

Top-level sections under [`src/content/`](../src/content/):

| Section      | Purpose                                            |
| ------------ | -------------------------------------------------- |
| `_index.md`  | Homepage intro copy and cascading `featured_image` |
| `about/`     | About page                                         |
| `artists/`   | Artist profile pages, bucketed by first letter     |
| `contact/`   | Contact page                                       |
| `releases/`  | Album/release pages, bucketed by first letter      |
| `search/`    | Client-side search page                            |
| `shows/`     | Broadcast episodes, one folder per show number     |
| `tracks/`    | Track-level pages used by show track-info entries  |
| `upcoming/`  | Upcoming/announced shows                           |

Shows currently number in the thirties; artists and releases are organised
into alphabetised subdirectories (`a/`, `b/`, … `z/`, plus `1/` for numeric).
Each show typically contains an `index.md` plus sibling pages such as
`playlist.md`, `track-info.md`, `featured-guest.md`, `listen-again.md`,
`discussion-points.md`, and `transcript/` directories.

### 1.3 Front matter conventions

The site relies on a stable set of front matter fields. Any theme migration
must continue to honour these (or provide an explicit, documented mapping):

- Shows (`content/shows/<n>/index.md`): `title`, `slug`, `description`,
  `summary`, `keywords`, `featured_image`, `read_more_copy`,
  `show_reading_time`, `date`, `draft`.
- Show subpages: optional `_build.list: never` to exclude a page from
  homepage `.Site.RegularPages` listings while keeping the page output.
- Artists (`content/artists/<letter>/<slug>/index.md`): `title`,
  `featured_image`, `artist_page: true`, optional `genres`.
- Releases (`content/releases/<letter>/<artist>/<release>/index.md`):
  `title`, `artist`, `label`, `release_date`, `release_page: true`,
  `shows` (list of show numbers), optional `for_sale: true` to render an
  "Available to buy" badge.
- Section index files (`_index.md`): `title`, `description`, and `menu.main`
  entries that drive the primary navigation order.

`featured_image` accepts both repository-relative paths and absolute http(s)
URLs; the `GetFeaturedImage` partial passes absolute URLs through unchanged.

### 1.4 Custom layouts, partials, and shortcodes

Project-level overrides under [`src/layouts/`](../src/layouts/) provide
behaviour that any candidate theme must continue to support:

- Layouts: `index.html`, `_default/{baseof,list,single,tile,taxonomy}.html`,
  `artists/single.html`, `releases/single.html`, `tracks/single.html`,
  `upcoming/{list,tile}.html`, `posts/list.html`, `search/single.html`,
  `page/single.html`, `404.html`, plus `index.json` for the search index.
- Partials: site header, page header, live player, social share/follow,
  featured-image resolution, and a small `func/` helper layer for
  socials/styles.
- Shortcodes: `title`, `release`, `release-label`, `for-sale`,
  `featured-guest-wikilink`, `artist-wikilink`,
  `track-info-featured-guest`, `include_content`, `include_playlist`, and
  `new-tab-link`. Several of these are invoked from within `RenderString`
  contexts, which requires `trim " "` rather than `strings.TrimSpace`.

### 1.5 Taxonomies, menus, and permalinks

- Primary navigation is built from `menu.main` entries declared on each
  section's `_index.md` (`Shows`, `Releases`, `Artists`, etc.).
- URLs follow Hugo defaults plus per-page `slug` overrides on shows. There
  are no custom `permalinks` configured, so existing URLs are tied to the
  current section/file layout. **This is the highest-risk surface for any
  theme migration** — see Section 4.
- No custom taxonomies beyond what Hugo provides by default; cross-linking
  between shows, releases, and artists is handled via shortcodes that
  resolve pages by path or by title, not via Hugo taxonomy terms.

### 1.6 Static media

- [`src/static/images/`](../src/static/images/) holds shared branding
  (logo, banner, social preview).
- Per-show, per-artist, and per-release imagery is co-located with the
  content under the relevant `content/<section>/.../` folder, referenced
  via `featured_image` or directly by Markdown.
- A bespoke Twitter/X icon override lives at
  `src/assets/ananke/socials/twitter.svg` and is used by both the footer
  follow links and per-page share links.

## 2. Candidate Themes Considered

The shortlist below was filtered to themes that are actively maintained,
GitHub-Pages-friendly (no server-side rendering or paid asset CDNs
required), and a plausible fit for an editorial music site.

| Theme                | Strengths                                              | Concerns                                                                                                |
| -------------------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------- |
| **Blowfish**         | Modern Tailwind-based design, dark/light auto-switch, strong typography, taxonomy/series support, active maintenance, good docs; "simple"/"page"/"list" content types map cleanly to shows/artists/releases. | Tailwind asset pipeline introduces a build dependency; some bespoke shortcodes will need re-implementing. |
| Hugo Bear Blog       | Extremely lightweight, fast, clean reading experience. | Minimal layout vocabulary — not enough surface area for tile grids, release pages, or rich show pages.   |
| PaperMod             | Popular, mature, dark mode, good defaults.             | Blog-first; tile/grid presentation for shows and releases would need significant custom layout work.     |
| Hugo Book / Doks     | Clean documentation-style typography.                  | Documentation-shaped IA; doesn't fit an editorial/broadcast catalogue.                                   |
| Congo                | Direct Blowfish predecessor with similar strengths.    | Superseded by Blowfish from the same author; no reason to pick the older project.                        |
| Stay with Ananke fork| Zero migration cost.                                   | Does not address the "feels dated" problem the issue raises.                                             |

### Assessment criteria

Each candidate was assessed against the rubric in the issue:

- Visual polish and modern typography
- Strong responsive design and mobile behaviour
- Clear primary navigation and content presentation
- Accessible colour contrast (WCAG AA minimum)
- Performance (small payload, no blocking JS)
- Documentation quality and community activity
- Compatibility with the existing content model (Section 1.3)
- GitHub Pages deployability with no extra runtime
- Ease of customisation without forking the upstream theme

Blowfish scored highest across visual polish, responsiveness, accessibility,
and content-model compatibility, and is the only candidate with a
content-type vocabulary that maps directly onto shows/artists/releases
without restructuring existing pages.

## 3. Prototype: Blowfish Parallel Build

A prototype has already been staged in this repository so that reviewers can
preview Blowfish without disturbing the live build:

- Theme vendored as a Git submodule under
  [`src/themes/blowfish/`](../src/themes/blowfish/), pinned via
  [`.gitmodules`](../.gitmodules) (no tracking branch — bumps happen via an
  explicit submodule pointer commit).
- Parallel Hugo configuration under
  [`src/config/blowfish/hugo.toml`](../src/config/blowfish/hugo.toml) and
  [`src/config/blowfish/params.toml`](../src/config/blowfish/params.toml).
  Blowfish defaults are kept where they don't conflict; the few overrides
  needed for the existing build (`buildFuture`, `summaryLength`,
  `enableEmoji`, `pagination.pagerSize`, `outputs.home`) are pinned
  explicitly.
- The default build remains on the `sundown-sessions` theme via
  [`src/config/_default/hugo.toml`](../src/config/_default/hugo.toml), so
  production output is unaffected by the prototype.

To preview the Blowfish prototype locally:

```powershell
Set-Location src
hugo server --config config/_default/hugo.toml,config/blowfish/hugo.toml
```

Because the prototype layers a second config file on top of `_default`, all
existing content under `src/content/` continues to be served verbatim. No
front matter, no Markdown body, and no image has been altered to support the
prototype — satisfying the issue's central constraint.

### Content preservation evidence

- The prototype uses the same `src/content/` tree as the live theme; no
  files are duplicated or rewritten.
- Section index files (`_index.md`) retain their `menu.main` entries, so
  navigation continues to be sourced from content rather than theme config.
- `featured_image` fields, including absolute URLs, are resolved by the
  existing project-level `GetFeaturedImage` partial, which Blowfish does not
  override.
- Custom CSS files in `src/static/css/` remain in place. Where Blowfish
  supplies equivalent styling (tile grids, pill badges, table layout), the
  custom CSS becomes redundant during cutover, but no file needs to be
  deleted as part of this investigation.

## 4. Risks and Required Changes for a Future Migration

These items are recorded for the eventual migration PR(s). They are **not**
addressed by this investigation.

- **URL stability.** The current site has no explicit `permalinks` block, so
  URLs are determined by section/file layout plus per-page `slug`s. A
  migration must either preserve the existing URL shapes or ship redirects
  (e.g. via a `static/_redirects`-style file or HTML meta refresh aliases)
  before cutover, to protect external links and search ranking.
- **Shortcodes.** The project shortcodes listed in Section 1.4 must be
  carried across as project-level overrides. Several rely on `RenderString`
  contexts where `strings.*` is unavailable; that convention must be
  preserved.
- **Custom partials.** `GetFeaturedImage`, `GetMainCSS`, `GetRegisteredServices`,
  `live-player`, `social-share`, and `social-follow` need either to remain as
  project-level partials or to be re-implemented against Blowfish equivalents.
- **Social configuration.** Blowfish uses a `socialIcons` mapping; the
  existing site uses `[[socials]]` entries (with `[[ananke_socials]]` as a
  legacy fallback). A migration must map the existing entries into the
  Blowfish vocabulary without losing the bespoke Twitter/X icon override.
- **CSS pipeline.** Blowfish ships its own asset pipeline (Tailwind). The
  custom CSS layered via `params.toml.custom_css` should be audited
  per-rule: keep what Blowfish doesn't already provide, retire the rest.
- **Search.** The site has its own `layouts/index.json` and `search/single.html`.
  Blowfish has built-in Fuse-based search; the migration should choose one
  and remove the other to avoid duplicate indices.
- **Homepage upcoming-shows block.** The current homepage renders an
  "Upcoming Shows" grid only when at least one upcoming page exists. This
  behaviour is implemented in `src/layouts/index.html` and must be ported as
  a project-level override of the Blowfish home layout.
- **Featured-image semantics.** The override that lets `featured_image`
  accept absolute http(s) URLs unchanged must be preserved.
- **`_build.list: never` usage.** Some show subpages opt out of homepage
  listings via `_build.list`. Any replacement homepage layout must continue
  to honour the same flag.

## 5. Recommendation and Decision

**Recommend adopting Blowfish as the new site theme**, vendored as a Git
submodule (already in place) and customised through project-level layout
overrides where the existing site's behaviour is not native to Blowfish.

Rationale:

- Best alignment with the rubric in Section 2.
- Content-model compatibility: every existing front matter field can be
  honoured without rewriting content.
- The prototype already runs against the unchanged content tree, providing
  concrete evidence that the constraint "existing content must not be
  changed or lost" can be met.
- Active upstream maintenance and good documentation reduce long-term
  maintenance cost compared to the in-repo Ananke fork.

Pros:

- Modern typography, responsive layout, and accessible default palette.
- Dark/light auto-switching out of the box.
- Built-in support for taxonomies, series, and content types that fit shows,
  artists, and releases.
- Reduces the maintenance surface currently carried by the
  `sundown-sessions` theme fork.

Cons:

- Tailwind-based asset pipeline is a new build dependency.
- Project-level overrides will be required for several bespoke shortcodes
  and partials.
- A one-time URL/redirect review is required before cutover.

Migration effort: moderate. The work breaks down naturally into staged PRs —
parity scaffolding, params/socials mapping, content-template port, homepage
slice, and cutover — which is the approach already implied by the prototype
vendored under `src/config/blowfish/` and the migration note in the
project [`README.md`](../README.md).

### Decision record

- **Decision:** adopt Blowfish, migrate in staged PRs.
- **Status:** investigation complete; implementation tracked separately.
- **Constraint carried forward:** existing content must remain byte-for-byte
  unchanged through the migration; any content-shaped change required by
  Blowfish must be raised as its own decision before being applied.
