# Mastodon Presence and Footer Social Links

Issue #483 is code-complete in this repository: footer social links are rendered from Blowfish-compatible `params.author.links`, the Mastodon profile is included in that configuration, and the footer partial renders accessible social links from the configured channel list.

## Current site configuration

- Footer social links are driven by `src/config/_default/params.toml` under `params.author.links`.
- The active Mastodon profile URL is `https://mastodon.scot/@sundown_sessions`.
- The footer partial reads those configured links and renders each item with a platform-specific accessible label.
- The implementation is configuration-driven and does not require modifying the Blowfish theme submodule.

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

YouTube and Bluesky were listed in issue #483 as desired footer channels, but no verified official Sundown Sessions URLs for those services are currently present in the repository. They should be added to `params.author.links` only after the official account URLs are confirmed.

## Operational Mastodon launch checklist

These items cannot be completed purely in code, but should be completed before considering the public Mastodon presence fully launched:

1. Confirm `mastodon.scot` remains the selected instance for the station.
2. Confirm `@sundown_sessions@mastodon.scot` is the official handle.
3. Configure the approved Sundown Sessions profile image.
4. Configure header artwork using approved station imagery.
5. Add a completed bio that reflects the station proposition.
6. Add the production website URL.
7. Complete or record the outcome of Mastodon profile verification for the production domain.
8. Publish and pin an introductory post where supported.
9. Publish starter posts for listening information, show promotion, and presenter or station introductions before broader promotion.

## White-label considerations

The current implementation is already configuration-driven through `params.author.links`, which keeps footer social channels outside the theme templates and avoids hard-coded theme changes.

For a future SaaS or white-label version, each hosted station should provide its own social channel map, for example:

```toml
[author]
  links = [
    { facebook = "https://www.facebook.com/example-station" },
    { instagram = "https://www.instagram.com/example-station" },
    { mastodon = "https://mastodon.social/@example_station" },
  ]
```

This preserves per-station control of social profiles while continuing to use the shared footer rendering.
