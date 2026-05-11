# Sundown Sessions Theme

A custom, standalone Hugo theme for the [Sundown Sessions](https://github.com/colin-gourlay/sundown-sessions) radio show website.

This theme replaces the former Ananke-based approach with a modern, self-contained design that:

- Uses **CSS custom properties** for a consistent, maintainable design system
- Includes **Tachyons** utility classes for backward compatibility with existing layouts
- Delivers a **responsive navigation** with an accessible mobile hamburger menu
- Provides **modern SVG social icons** for follow and share links
- Has no external dependencies — all assets are self-hosted

## Structure

```
assets/
  css/
    tachyons.css    – Tachyons v4.9.1 utility classes (layout compat layer)
    code.css        – Code block styles
    pagination.css  – Hugo pagination styles
    social.css      – Social icon base styles
    theme.css       – Modern design tokens and component overrides
  socials/
    *.svg           – Social platform SVG icons
layouts/
  partials/
    site-navigation.html   – Sticky responsive navbar
    site-footer.html       – Modern footer
    social-follow.html     – Social follow icons
    func/
      socials/             – Social service data helpers
      style/               – CSS bundling helpers
```

## Configuration

Social links are configured via `[[socials]]` entries in `params.toml`:

```toml
[[socials]]
name = "twitter"
url  = "https://x.com/yourhandle"

[[socials]]
name = "instagram"
url  = "https://www.instagram.com/yourprofile"
```

The legacy `[[ananke_socials]]` key is also supported as a fallback.

## Customisation

Add custom CSS by registering files in `params.toml`:

```toml
custom_css = ["css/my-overrides.css"]
```

Files are looked up in `assets/` first, then served as static links from `static/`.
