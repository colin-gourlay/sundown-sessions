# Production sitemap

Hugo generates `/sitemap.xml` during the normal build using Blowfish's existing
sitemap template. `src/config/_default/params.toml` excludes taxonomy and term
pages; published pages and section indexes remain discoverable. Hugo excludes
drafts and does not add alias redirects or leaf-bundle resources as sitemap pages.
The intentional public utility pages (including search and upcoming shows) remain
included. Future-dated content follows the existing `buildFuture = true` policy.

GitHub Pages must build with the `production` environment. This loads the canonical
`https://sundownsessions.co.uk/` configuration and makes Blowfish's existing
`robots.txt` template allow crawling and advertise the sitemap. The deployment
checks the generated sitemap before uploading the Pages artifact:

```bash
hugo --source src --environment production --minify
python3 tests/test_sitemap.py src/public
```

Use Hugo's `sitemap.disable: true` front matter for published pages that should
not be listed. Four empty artist placeholders use this setting; remove it when
those profiles receive editorial content. This does not change publication state.
Do not maintain a separate URL list or custom sitemap template.

## Production audit, 18 September 2026

The deployed sitemap returned successfully and parsed as sitemap XML. All 931
entries used the canonical HTTPS domain, were unique, and returned HTTP 200 to
HEAD requests without HTTP redirects. The sitemap included shows, artists,
releases, tracks, their section indexes, and intentional standalone pages.
Four empty artist placeholders were the inappropriate entries identified.
All 12 draft pages were absent, as were alias redirects and internal resources.

The deployed `robots.txt` advertised the correct sitemap but contained
`Disallow: /`. The deployment's former `githubPages` environment caused Blowfish
to treat the build as non-production. The production environment fixes this using
the existing theme template.

The audit of the deployed site describes the pre-merge deployment. After merging,
check that the deployed `robots.txt` contains `Allow: /` and that the four empty
artist placeholders are absent from `/sitemap.xml`. Deployment is triggered by
merging the source changes into `main`.

The production-equivalent build also exposed a pre-existing date formatter error:
`c.1989` on The Big Now's *Fast Cars, Soul Music* release could not be parsed as
a date. The formatter now preserves approximate years, allowing deployment to
complete. The generated sitemap has 930 entries: the 931 deployed entries minus
the four placeholders, plus that release and two recently added IST IST tracks
(`black` and `youre-mine`). These three new pages returned 404 in the pre-merge
production audit and require the successful deployment of this fix.
