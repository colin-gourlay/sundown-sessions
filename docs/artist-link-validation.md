# Artist Link Validation

Published `artist-wikilink` and `title` shortcode relationships must resolve to
an Artist page. Run the same audit used by CI with:

```sh
python3 scripts/audit-artist-links.py
```

Fix a naming mismatch by supplying the canonical Artist slug. It is the second
argument to `artist-wikilink`, and the fourth `--`-separated field in `title`:

```go-html-template
{{< artist-wikilink "Echo & the Bunnymen" "echo-and-the-bunnymen" >}}
{{< title "People Are Strange--Echo & the Bunnymen----echo-and-the-bunnymen" >}}
```

If editorial review confirms that an unresolved published relationship is
intentional, add one entry to `config/artist-link-exceptions.json`:

```json
{
  "source": "src/content/shows/1/playlist.md",
  "line": 3,
  "shortcode": "artist-wikilink",
  "artist": "Example Credit",
  "artistSlug": "example-credit",
  "reason": "Specific editorial reason this credit has no Artist page."
}
```

An exception is tied to one source line and relationship, and unresolved text
remains unlinked. The audit rejects malformed, stale, duplicate, and overly
general exceptions. Draft page bundles are outside the published-content audit.
