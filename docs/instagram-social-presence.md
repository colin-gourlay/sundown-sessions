# Instagram Presence and Footer Social Links

Issue #510 is code-complete in this repository for the website-owned parts of the Instagram launch: footer social links are rendered from Blowfish-compatible author configuration, Instagram is included in that channel list, and the footer partial provides accessible labels for every configured social link.

## Current website implementation

- Footer social links are driven by `src/config/_default/params.toml` under `params.author.links`.
- The active Instagram profile URL is `https://www.instagram.com/sundownsessionsshow`.
- Instagram appears alongside the other configured Sundown Sessions channels: Facebook, TikTok, Mixcloud, Pinterest, X, Mastodon, and LinkedIn.
- The footer uses a dedicated social-links navigation landmark and labels each icon link as `Follow Sundown Sessions on {channel}`.

## Instagram profile launch checklist

These items require access to Instagram and cannot be completed purely in code. Complete them before treating the Instagram presence as fully launched:

1. Confirm that `@sundownsessionsshow` is the official long-term handle.
2. Use the approved Sundown Sessions logo or station branding as the profile picture.
3. Add a concise bio that explains the station, music-discovery focus, and listening route.
4. Link the profile to `https://sundownsessions.co.uk` or the most appropriate live listening landing page.
5. Add contact details where they are suitable for public use.
6. Publish initial launch content before broadly promoting the account.

## Recommended profile bio

```text
Independent online radio
Musical discovery & great presenters
Scotland
Listen live: sundownsessions.co.uk
```

## Initial content plan

The account should not be promoted while empty. Publish a small starter grid first:

1. Welcome post introducing Sundown Sessions and its listening promise.
2. Listen Live post explaining how to hear the station.
3. Presenter introduction posts for core hosts.
4. Behind-the-scenes post showing the people or process behind broadcasts.
5. Show highlight post pointing listeners to a recent or upcoming programme.
6. Guest feature post for a recent interview or featured artist.
7. Schedule post for upcoming broadcasts.

## Sustainable content pillars

Use these recurring pillars to avoid ad-hoc posting:

- **Presenter content:** introductions, favourite albums, listening notes, and studio moments.
- **Show promotion:** upcoming guests, themed episodes, special broadcasts, and listen-again reminders.
- **Music discovery:** album recommendations, artist spotlights, track context, and playlist-style posts.
- **Community:** listener prompts, polls, tagged listening setups, questions, and competitions.
- **Station updates:** schedule changes, milestones, new site features, and important announcements.

## Reels strategy

Reels should be treated as a discovery channel rather than a duplicate of static posts. Prioritise short, repeatable formats:

- Upcoming show teasers.
- Presenter soundbites.
- Guest highlights.
- Studio or preparation moments.
- Music recommendation clips.
- Quick listen-again reminders.

## White-label considerations

The current implementation keeps social profiles in site configuration rather than hard-coded templates. For a future SaaS or white-label version, each hosted station should own its social-channel map, for example:

```toml
[params.author]
  links = [
    { facebook = "https://www.facebook.com/example-station" },
    { instagram = "https://www.instagram.com/example-station" },
    { mixcloud = "https://www.mixcloud.com/example-station" },
  ]
```

This preserves station-specific handles and launch readiness while continuing to use the shared Blowfish-compatible footer rendering.
