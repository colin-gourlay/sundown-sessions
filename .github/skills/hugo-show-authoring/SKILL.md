---
name: hugo-show-authoring
description: "Use when creating or updating Sundown Sessions show pages, episode bundles, section ordering, and shortcode composition under src/content/shows."
argument-hint: "Show number, required edits, and publishing constraints"
user-invocable: true
disable-model-invocation: false
---

# Hugo Show Authoring

## When to Use

- Creating a new show entry under `src/content/shows/`.
- Updating an existing show page and related section includes.
- Standardising show front matter and publish-ready page structure.

## Do Not Use When

- The work is primarily .NET implementation in `automation/dotnet/`.
- The request is only about artist profile updates under `src/content/artists/`.
- The task is release-note or changelog drafting without show content changes.

## Procedure

1. Inspect a nearby show bundle and mirror its file structure and conventions.
2. Update front matter fields, ensuring date, slug, summary, and metadata consistency.
3. Preserve section ordering and shortcode usage, especially `include_content` blocks.
4. Keep edits scoped to the requested show and directly related content files.
5. Validate for formatting consistency and editorial clarity.

## Quality Checklist

- Front matter is complete and coherent.
- Section headings and separators follow established style.
- Shortcode paths resolve to existing content.
- New human-facing text uses British English.
