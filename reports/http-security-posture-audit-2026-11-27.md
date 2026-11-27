# HTTP Security Posture Audit

Audit date: 27 November 2026  
Audited revision: `684400b` (site content equivalent to `main` at `7add9b0`)  
Related issue: [#1004](https://github.com/colin-gourlay/sundown-sessions/issues/1004)

## Executive Summary

Sundown Sessions is a static Hugo site deployed to GitHub Pages behind the
Fastly content delivery network. The build itself has a healthy browser
security posture: every stylesheet and script the site ships is same-origin,
fingerprinted and carries a Subresource Integrity (SRI) hash, there are no
external script or stylesheet loads, no inline event handlers, no
`javascript:` URLs, and no third-party analytics currently execute because the
provider identifiers are still placeholders. The production build was also
checked for accidental publication of source maps, configuration, build
statistics, backups or development files, and none were found.

The dominant limitation is structural rather than a defect in the repository:
GitHub Pages does not let a project set arbitrary HTTP response headers, so the
site currently serves **no** `Content-Security-Policy`, `Strict-Transport-Security`,
`X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` or
`X-Frame-Options` beyond whatever GitHub Pages adds by default. The security
headers defined in `src/config/_default/server.toml` apply only to the local
`hugo server` and have no effect on the deployed site.

The findings below separate two confirmed issues (no production security
headers; a widely dynamic inline analytics script that blocks a simple hashed
CSP) from several hardening opportunities, and route the substantive remediation
into focused follow-up issues with acceptance criteria. No change that could
alter the live site's behaviour is made in this pull request; this is an
inventory and assessment.

## Method and Scope

The assessment combined three sources of evidence:

- **Source inspection** of the Hugo configuration (`src/config`), layouts and
  partials (`src/layouts`), front-end assets (`src/assets`), static files
  (`src/static`) and the deployment workflow
  (`.github/workflows/deploy-github-pages.yml`).
- **A production build** reproduced from the workflow using Hugo Extended
  0.164.0, Dart Sass 1.77.8 and the vendored Blowfish 2.105.0 theme submodule:

  ```bash
  hugo --source src --minify --baseURL "https://sundownsessions.co.uk/"
  ```

  The generated `src/public` tree (2,566 pages, 804 non-page files, 19 static
  files) was then inventoried for externally reachable and unintended assets.
- **Rendered-output inspection** of representative pages (home, About,
  Listen Live, Contact, Corrections and a release page) to enumerate inline
  scripts, inline styles and runtime network destinations.

The audit environment has no network route to the production host, so live
response headers and redirect behaviour could **not** be captured here. Where a
value depends on the deployed response rather than the repository, this report
states GitHub's documented behaviour and marks the item for live confirmation.
The verification commands in the final section should be run from a networked
environment and the result recorded, consistent with the practice already
documented in [`docs/security-txt.md`](../docs/security-txt.md).

Throughout, controls are classified as **in-repository** (expressible from the
Hugo build, i.e. HTML `<meta>` elements) or **platform** (response headers that
only the host can set).

## Hosting Model and GitHub Pages Constraints

The repository contains no `_headers`, `netlify.toml`, `vercel.json` or
`_redirects` file, confirming GitHub Pages as the sole delivery mechanism. The
deployment uploads `src/public` as a Pages artefact and publishes it unchanged.

GitHub Pages imposes the following constraints that shape every recommendation
in this report:

- **Custom response headers cannot be configured.** GitHub Pages ignores
  `_headers`-style files and offers no per-response header settings. Any header
  that is not sent by GitHub Pages automatically cannot be added from this
  repository.
- **Only `<meta>`-deliverable policies are in-repository controls.** From a
  static build, the only enforceable browser policies are
  `Content-Security-Policy` (via `<meta http-equiv>`) and `Referrer-Policy`
  (via `<meta name="referrer">`).
- **Meta-delivered CSP has reduced capability.** When a CSP is delivered through
  a `<meta>` element, the `frame-ancestors`, `report-uri`/`report-to` and
  `sandbox` directives are ignored by browsers. Clickjacking protection and
  violation reporting therefore cannot be achieved from the repository.
- **`X-Content-Type-Options`, `Strict-Transport-Security`, `Permissions-Policy`
  and `X-Frame-Options` are header-only** and cannot be set from a static build
  on GitHub Pages at all.

Delivering any of the header-only controls would require fronting GitHub Pages
with a platform that can rewrite response headers (for example a CDN such as
Cloudflare), which is a hosting change rather than a repository change.

## HTTPS and Redirect Behaviour

The production configuration sets `baseURL = 'https://sundownsessions.co.uk/'`
(`src/config/production/hugo.toml`), and every page emits an absolute HTTPS
`<link rel="canonical">`, so internally generated links are HTTPS. GitHub Pages
provides TLS for the custom domain and, when the repository's **Enforce HTTPS**
Pages setting is enabled, redirects `http://` requests to `https://`.

- In-repository: the build already commits to HTTPS canonicals and an HTTPS
  base URL.
- Platform: the HTTP→HTTPS redirect and certificate are supplied by GitHub
  Pages. Confirm that **Enforce HTTPS** is enabled and that a plain-HTTP request
  returns a redirect to HTTPS (see verification section).

## Production Response-Header Inventory

The table records the expected production state. Rows marked *confirm live*
depend on the deployed response and must be validated against a real request;
the audit environment could not reach the host.

| Header | Expected production state | In-repository control? | Notes |
| --- | --- | --- | --- |
| `Content-Security-Policy` | Absent | Partial (via `<meta>`) | No CSP is emitted by the build; see assessment below. |
| `Strict-Transport-Security` | Absent unless added by GitHub Pages (*confirm live*) | No | GitHub Pages does not reliably send HSTS for custom domains. |
| `X-Content-Type-Options` | Absent (*confirm live*) | No | Cannot be set from a static build. |
| `Referrer-Policy` | Absent (*confirm live*) | Yes (via `<meta>`) | Not currently emitted; a meta referrer policy is feasible. |
| `Permissions-Policy` | Absent | No | No `<meta>` equivalent; header-only. |
| `X-Frame-Options` | Absent (*confirm live*) | No | Header-only; meta `frame-ancestors` is ignored. |
| `Cross-Origin-Opener-Policy` | Absent | No | Header-only. |
| `Cross-Origin-Resource-Policy` | Absent | No | Header-only. |

The security headers in `src/config/_default/server.toml`
(`Content-Security-Policy = 'script-src localhost:1313'`, `Referrer-Policy`,
`X-Content-Type-Options`, `X-Frame-Options`, `X-XSS-Protection`) apply only to
Hugo's development server. They are **not** present in the built output and have
no bearing on production. The development CSP (`script-src localhost:1313`) is a
local convenience and is not a model for a production policy.

## Content Security Policy

### Resources the Site Actually Needs

A CSP should be derived from the resources the site genuinely loads. The build
and rendered pages show the following.

- **Scripts (same-origin, SRI-protected).** `appearance.js`, a concatenated
  `main.bundle.js` (modal focus, mobile navigation, scroll-to-top, the inlined
  Fuse.js search library, search, KaTeX render shim, print support, email
  obfuscation and global navigation shortcuts), the `medium-zoom` library and,
  on the Listen Live page, the player, now-playing and schedule scripts. Every
  one is served from the site origin with a `sha512` integrity attribute.
- **Styles (same-origin).** A single concatenated, minified, SRI-protected
  `main.bundle.css`. There are no external stylesheets and no inline `<style>`
  blocks.
- **Inline scripts.** Three kinds appear in rendered pages: `application/ld+json`
  structured-data blocks (data, exempt from `script-src`), a per-page analytics
  dispatcher (`window.sundownAnalytics…`), and a static `medium-zoom`
  initialiser.
- **Inline style attributes.** The theme emits inline `style=` attributes
  extensively (over 2,200 occurrences across the build), which a CSP would have
  to accommodate.
- **Runtime connections (Listen Live).** `fetch()` to
  `https://betelgeuse.nucast.co.uk:2020/json/stream/8062` (`connect-src`), the
  audio stream at `https://betelgeuse.nucast.co.uk:8062/stream` (`media-src`),
  and dynamically inserted artwork `<img>` elements whose URLs come from that
  JSON and may point at arbitrary hosts (`img-src`).
- **Form submission (Contact and Corrections).** A `fetch()` POST to
  `https://formspree.io/f/xzbnwyez` (`connect-src` and `form-action`).
- **Gated third parties (not currently loaded).** Google Analytics
  (`www.googletagmanager.com`), Microsoft Clarity (`www.clarity.ms`) and Google
  AdSense (`pagead2.googlesyndication.com`) are referenced by templates but only
  render when their identifiers are configured. All three are placeholders, so
  none load today. Any CSP must be updated in lockstep if they are enabled.

No external fonts, external scripts, external stylesheets, `<iframe>` embeds,
inline event handlers (`on*=`) or `javascript:` URLs were found, which keeps the
required policy comparatively narrow.

### Feasibility

A meta-delivered CSP is **possible but not yet simple**, for two concrete
reasons observed in the build.

1. **The inline analytics dispatcher varies per page.** Its content embeds the
   page title, type and URL, so its hash differs on every page (three sampled
   pages produced three different SHA-256 digests). A single static
   `script-src 'sha256-…'` cannot cover it, and GitHub Pages cannot issue a
   per-request nonce because the site is static. To adopt a strict CSP this
   script should first be externalised into the bundle, reading its per-page
   values from data attributes. The `medium-zoom` initialiser, by contrast, is
   byte-identical on every page and a single hash would cover it.
2. **Inline style attributes force `style-src 'unsafe-inline'`.** Hashes do not
   apply to style *attributes*, so the theme's inline styles mean a realistic
   policy keeps `style-src 'unsafe-inline'` (or `style-src-attr 'unsafe-inline'`)
   until the theme output changes.

With those caveats, a representative starting policy (to be trialled in
report-only mode, and only after the analytics script is externalised) is:

```text
default-src 'self';
script-src 'self';
style-src 'self' 'unsafe-inline';
img-src 'self' data: https:;
media-src 'self' https://betelgeuse.nucast.co.uk:8062;
connect-src 'self' https://betelgeuse.nucast.co.uk:2020 https://formspree.io;
form-action 'self' https://formspree.io;
font-src 'self';
object-src 'none';
base-uri 'self'
```

`frame-ancestors` is deliberately omitted because it is ignored in a
meta-delivered CSP; clickjacking protection remains a platform concern. The
`img-src https:` allowance reflects the arbitrary artwork hosts returned by the
NuCast now-playing feed; if that feed is constrained to known hosts, the policy
can be tightened accordingly.

## Assessment of Individual Headers

- **HSTS (`Strict-Transport-Security`).** Platform-only. GitHub Pages does not
  reliably emit HSTS for custom domains, and it cannot be set from the build.
  Confirm the live value; if HSTS is required, it needs a header-capable front.
- **`X-Content-Type-Options: nosniff`.** Platform-only; no `<meta>` equivalent.
  The risk is partly mitigated because all executable assets are same-origin and
  SRI-pinned, but the header cannot be added from this repository.
- **`Referrer-Policy`.** The only header in this group with an in-repository
  path: `<meta name="referrer" content="strict-origin-when-cross-origin">`. It
  is not currently emitted and is a low-risk, self-contained improvement.
- **`Permissions-Policy`.** Platform-only; there is no `<meta>` form. Without it
  the browser applies permissive defaults, though the site requests no sensitive
  capabilities (no geolocation, camera, microphone or payment APIs).
- **Clickjacking (`X-Frame-Options` / CSP `frame-ancestors`).** Neither is
  achievable on GitHub Pages: the header form is platform-only and the CSP form
  is ignored in a meta policy. This is an explicit platform limitation.

## Third-Party and Dependency Exposure

### Runtime Third Parties

- **NuCast streaming (`betelgeuse.nucast.co.uk`).** Supplies the live audio
  stream and the now-playing JSON on the Listen Live page. The now-playing
  response drives dynamically inserted artwork images; the client already
  validates that image URLs use `http(s)`, but the hosts are outside the site's
  control, which is the main reason `img-src` cannot be tightly pinned.
- **Formspree (`formspree.io`).** Handles Contact and Corrections submissions
  because GitHub Pages cannot process forms. This is an intentional, documented
  dependency (`src/config/_default/params.toml`,
  `docs/correction-submissions.md`).
- **Gated analytics/advertising.** Google Analytics, Microsoft Clarity and
  Google AdSense are wired into templates but inert while their identifiers
  remain placeholders. Enabling any of them introduces third-party script
  execution and must be paired with a CSP update.

### Client-Side Dependencies

All front-end libraries are vendored and self-hosted rather than loaded from a
CDN at runtime, and each shipped bundle carries an SRI hash, which materially
reduces client-side supply-chain risk:

| Dependency | Version | Delivery |
| --- | --- | --- |
| Hugo Extended | 0.164.0 | Build toolchain (pinned in workflow) |
| Dart Sass | 1.77.8 | Build toolchain (pinned in workflow) |
| Blowfish theme | 2.105.0 | Vendored git submodule (pinned tag) |
| Fuse.js | 7.5.0 | Bundled into `main.bundle.js`, SRI |
| medium-zoom | 1.1.0 | Self-hosted, SRI |

There is no `package.json` or `package-lock.json` at the repository root, so the
production site has no npm runtime dependencies; the workflow's `npm ci` step is
a deliberate no-op. GitHub Actions dependencies are pinned by commit SHA and
maintained by Dependabot. The theme submodule and the bundled libraries are not
covered by Dependabot and should be reviewed manually for security releases.

## Deployment Artefact Exposure

The built `src/public` tree was inventoried for accidental or sensitive
publication. The result is clean:

- **No source maps** (`*.map`) are produced for CSS or JavaScript.
- **No configuration, secrets or build metadata** are published: no `*.toml`,
  `*.yaml`/`*.yml`, `*.lock`, `*.env`, `*.ini`, `*.bak`, `*.orig`, `.DS_Store`
  or `package*.json` files appear in the output.
- **`hugo_stats.json` is generated but not published.** The production build
  writes it to `src/hugo_stats.json` (gitignored); it is absent from
  `src/public`.
- **Intended public files are present and correct:** `.well-known/security.txt`
  (preserved by the workflow's integrity check and `include-hidden-files`),
  `robots.txt` (allow-all with a sitemap reference), `sitemap.xml`,
  `site.webmanifest`, favicons and the search `index.json`.

One behavioural note, not a security issue: image zoom is effectively enabled
(the `medium-zoom` library and its inline initialiser ship on every page). This
is expected given the current template evaluation and is only relevant here
because that inline initialiser is one of the inline scripts a future CSP must
account for.

## Findings Summary

Confirmed issues:

| # | Finding | Severity | Nature |
| --- | --- | --- | --- |
| C1 | No production security response headers (CSP, Referrer-Policy and others are absent from the deployed site). | Medium | Mostly platform-limited; Referrer-Policy and CSP are partially addressable in-repository. |
| C2 | The inline analytics dispatcher varies per page, preventing a simple hashed CSP and requiring externalisation first. | Low | In-repository. |

Hardening opportunities:

| # | Opportunity | Severity | Nature |
| --- | --- | --- | --- |
| H1 | Add a `Referrer-Policy` via `<meta name="referrer">`. | Low | In-repository. |
| H2 | Introduce a report-only, then enforcing, meta CSP once C2 is resolved. | Low | In-repository. |
| H3 | Reconcile or annotate the dev-only `server.toml` headers so they are not mistaken for production controls. | Informational | In-repository. |
| H4 | Evaluate a header-capable front (e.g. CDN) for HSTS, `X-Content-Type-Options`, `Permissions-Policy` and clickjacking protection. | Low | Platform. |

No high-severity or critical finding was identified. The static, self-hosted,
SRI-pinned asset model is the site's main strength and should be preserved.

## Recommended Follow-Up Issues

The audit deliberately makes no behaviour-changing edit. The substantive
remediation is captured below as discrete issues to be filed, each with
acceptance criteria.

### F1 — Externalise the inline analytics dispatcher

- Move the per-page analytics logic out of the inline `<script>` in
  `layouts/partials/analytics-events.html` into a bundled, SRI-protected script.
- Pass per-page values (title, page type, URL, view event) via `data-`
  attributes rather than templated inline JavaScript.
- Acceptance: the production build contains no executable inline `<script>`
  other than static, hashable blocks; analytics event behaviour is unchanged on
  artist, release and track pages.

### F2 — Add a `Referrer-Policy` meta tag

- Emit `<meta name="referrer" content="strict-origin-when-cross-origin">` in the
  document head.
- Acceptance: the header equivalent is present on every page in the build; no
  change to outbound share links or canonical behaviour.

### F3 — Introduce a production Content Security Policy

- Depends on F1. Add a `<meta http-equiv="Content-Security-Policy">` derived
  from the resources inventoried here (self, NuCast connect/media, Formspree
  connect/form-action, gated analytics hosts only when enabled).
- Trial in report-only mode first and confirm zero violations across home,
  About, Listen Live, Contact, Corrections, search and a release page.
- Acceptance: an enforcing CSP ships with no console violations on the tested
  pages; the policy is documented alongside the analytics and forms
  configuration; `frame-ancestors` is noted as unachievable via meta.

### F4 — Reconcile development-server headers with production reality

- Annotate `src/config/_default/server.toml` (or relevant documentation) to make
  explicit that those headers apply only to `hugo server`, and align the
  development CSP with the eventual production policy where practical.
- Acceptance: documentation or comments prevent the dev headers being mistaken
  for a production control.

### F5 — Evaluate a header-capable delivery front

- Assess whether HSTS, `X-Content-Type-Options`, `Permissions-Policy` and
  clickjacking protection are required, and if so, the cost/benefit of placing a
  header-capable CDN in front of GitHub Pages.
- Acceptance: a recorded decision (adopt or accept the platform limitation) with
  rationale.

### F6 — Capture and record the live production header baseline

- Run the verification commands below from a networked environment and record
  the actual production headers and redirect behaviour in the issue or this
  report.
- Acceptance: a dated capture of real response headers exists, resolving every
  *confirm live* row in the inventory above.

## Verification (Run From a Networked Environment)

The audit environment could not reach the production host. Capture the live
posture as follows and record the result:

```bash
# Response headers and redirect behaviour for the site root.
curl --fail --silent --show-error --location --proto '=https' \
  --dump-header /tmp/sundown-root-headers.txt \
  --output /dev/null \
  https://sundownsessions.co.uk/
cat /tmp/sundown-root-headers.txt

# Confirm plain HTTP redirects to HTTPS.
curl --silent --show-error --output /dev/null --write-out '%{http_code} %{redirect_url}\n' \
  http://sundownsessions.co.uk/
```

Check for the presence and values of `Strict-Transport-Security`,
`X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`,
`Content-Security-Policy` and `X-Frame-Options`, and verify the HTTP request
returns a redirect (301/302) to the HTTPS origin. A local build alone cannot
establish production behaviour.
