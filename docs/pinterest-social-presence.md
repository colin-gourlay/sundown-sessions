# Pinterest Presence and Footer Social Links

Issue #487 is partially code-complete in this repository: footer social links are rendered from `params.author.links`, Pinterest is included in that configuration, article sharing includes Pinterest, and the head partial supports Pinterest domain verification when a token is supplied.

## Current site configuration

- Footer social links are driven by `src/config/_default/params.toml` under `params.author.links`.
- The active Pinterest profile URL is `https://www.pinterest.co.uk/sundownsessionsshow/`.
- Pinterest sharing is enabled for articles through the Blowfish `article.sharingLinks` setting.
- Pinterest domain verification can be activated by setting `params.verification.pinterest`; the head partial will emit the required `p:domain_verify` meta tag.

## Footer channel coverage

The footer currently exposes the confirmed Sundown Sessions channels that have configured URLs:

- Facebook
- Instagram
- TikTok
- Mixcloud
- Pinterest
- X / Twitter
- Mastodon
- LinkedIn

Bluesky was listed in the original issue as a desired footer channel, but no verified official Sundown Sessions Bluesky profile URL is currently present in the repository. It should be added to `params.author.links` only after the official account URL is confirmed.

## Operational Pinterest launch checklist

These items cannot be completed purely in code, but should be completed before considering the public Pinterest presence fully launched:

1. Confirm whether the Pinterest account is a Business account.
2. Confirm the official profile name and handle.
3. Configure the approved Sundown Sessions profile image.
4. Configure banner artwork using approved station imagery.
5. Add a completed bio that reflects the station proposition.
6. Complete or record the outcome of website verification for the production domain.
7. Create starter boards before broader promotion.
8. Publish initial pins before broader promotion.

Suggested starter boards from issue #487 remain appropriate:

- Listen Again Highlights
- Presenters
- Behind the Mic
- Music Discovery
- Sundown Sessions
- Guest Interviews
- Competitions & Promotions
- Merchandise

## White-label considerations

The current implementation is already configuration-driven through `params.author.links`, which keeps footer social channels outside the theme templates and avoids hard-coded theme changes.

For a future SaaS or white-label version, each hosted station should provide its own social channel map, for example:

```toml
[author]
  links = [
    { facebook = "https://www.facebook.com/example-station" },
    { instagram = "https://www.instagram.com/example-station" },
    { pinterest = "https://www.pinterest.com/example-station" },
  ]

[verification]
  pinterest = "station-specific-domain-verification-token"
```

This preserves per-station control of social profiles and domain verification while continuing to use the shared footer rendering.
