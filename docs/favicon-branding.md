# Favicon branding

The favicon set is derived from the main Sundown Sessions logo at
`src/static/images/sundown-sessions-logo.jpg`. It uses the compact headphones,
sunset, and radio mast mark rather than the full wordmark so the asset remains
recognisable at browser-tab sizes.

The generated static assets are:

- `favicon.ico` with 16x16, 32x32, and 48x48 entries
- `favicon-16x16.png`
- `favicon-32x32.png`
- `favicon-48x48.png`
- `favicon-192x192.png`
- `favicon-512x512.png`
- `apple-touch-icon.png`
- `android-chrome-192x192.png`
- `android-chrome-512x512.png`
- `site.webmanifest`

The Hugo integration lives in `src/layouts/partials/favicons.html`, which
overrides Blowfish's default favicon links.

## White-label configuration

Favicon paths are configured in `src/config/_default/params.toml`:

```toml
[favicon]
  ico = "favicon.ico"
  icon16 = "favicon-16x16.png"
  icon32 = "favicon-32x32.png"
  icon48 = "favicon-48x48.png"
  appleTouchIcon = "apple-touch-icon.png"
  manifest = "site.webmanifest"
  themeColor = "#081a3a"
```

Future tenant or station builds can override this block with tenant-specific
assets and a tenant-specific web manifest while reusing the same template.
