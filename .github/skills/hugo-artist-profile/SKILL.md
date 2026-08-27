---
name: hugo-artist-profile
description: "Use when drafting, refining, or correcting artist profile pages under src/content/artists using existing conventions, metadata patterns, and editorial tone."
argument-hint: "Artist name, target folder, and requested profile updates"
user-invocable: true
disable-model-invocation: false
---

# Hugo Artist Profile

## When to Use

- Creating a new artist page under `src/content/artists/**`.
- Refreshing biography, metadata, or media links for an existing artist.
- Aligning artist pages with repository style and structure.

## Do Not Use When

- The request is about show-page bundle structure under `src/content/shows/`.
- The task is a .NET feature or bug-fix in `automation/dotnet/`.
- The work is primarily release maintenance, changelog curation, or CI workflow updates.

## Procedure

1. Inspect similar artist entries in the same alphabet section.
2. Apply requested content updates while preserving front matter patterns.
3. Keep copy concise, factual, and suitable for publication.
4. Verify image references, links, and any related metadata.
5. Summarise changes and note unresolved factual gaps.

## Quality Checklist

- File location and slug conventions are preserved.
- Front matter matches adjacent artist patterns.
- Content is readable, accurate, and in British English.
- No unrelated changes were introduced.
