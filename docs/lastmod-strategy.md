# Hugo lastmod strategy

Sundown Sessions uses Hugo Git metadata as the canonical source of page freshness.

The site enables Git metadata in `src/config/_default/hugo.toml`:

```toml
enableGitInfo = true
```

The front matter date resolver in `src/config/_default/frontmatter.toml` uses only Git for `lastmod`:

```toml
lastmod = [':git']
```

Content authors should not add `lastmod` front matter manually. Generated content should not include `lastmod` either. When a content file is meaningfully updated, commit the file and Hugo will derive `.Lastmod` from that file's Git history.

The deploy and validation workflows must keep using a full Git checkout (`fetch-depth: 0`) so Hugo can resolve the correct per-file history in CI.

Sitemap and structured data freshness are produced from Hugo's `.Lastmod` value. The Blowfish sitemap template emits `<lastmod>` when `.Lastmod` is available, and schema metadata uses the same page value.

Some pages render relationship-driven content, such as artist, release, and track backlinks to shows. Under this Git-only policy, those template-derived changes do not update a page's `lastmod` unless the page's own content file is also changed and committed.

Commit SHA storage in front matter, such as `lastmod_sha`, is intentionally out of scope. Git history remains the traceability mechanism.
