# Blowfish imagery audit and mapping

## Audit summary

- Image assets found under `src/content`: **931**
- Content markdown files reviewed: **765**
- Pages with `featured_image` set: **419**
- `featured_image` values using legacy content-relative paths (for example `artists/a/acdc/acdc.jpg`): **399**

## What was lost after migration

Blowfish partials were reading `featureimage` (without underscore) and trying to resolve local values through `resources.Get`, which does not resolve most page-bundle images referenced in the existing content front matter.

This meant many existing images were present in the repository but not rendered in cards, hero headers, taxonomy cards, and social preview metadata.

## Mapping from legacy usage to Blowfish-native usage

| Existing image asset | Previous usage | Blowfish usage after this change |
|---|---|---|
| `featured_image: artists/.../name.jpg` on artist/show/release pages | Legacy front matter for list/header visuals | Used as Blowfish card/hero source by supporting `featured_image` and resolving against page resources (exact path, then basename fallback) |
| Page bundle images that do not include `feature`/`cover`/`thumbnail` in filename | Inline/content identity images | Used for Blowfish cards and hero via front matter path resolution fallback |
| Bundle image selected for page previews | Social preview imagery | Used for `twitter:image` and `og:image` in the head partial when `featured_image`/`featureimage` is provided |

## Implementation scope

Local overrides were added only for the Blowfish partials that render these surfaces:

- `article-link` card/simple/related/shortcode cards
- hero variants (`basic`, `big`, `background`, `thumbAndBackground`)
- taxonomy term cards
- head social image metadata

These overrides preserve Blowfish defaults while restoring existing repository imagery.
