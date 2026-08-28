# Copilot Instructions for Sundown Sessions

## Repository Context

This repository contains the Sundown Sessions Hugo website:

- `src/`: Hugo-based website content and templates for Sundown Sessions.

Keep changes scoped to the area requested.

## Language and Writing Standard

Use British English for all human-facing text in this repository.

This includes:

- Markdown documentation.
- Code comments.
- UI or user-facing copy.
- Release notes, changelog entries, and contributor guidance.

Prefer British spelling and phrasing, for example: "organisation", "behaviour", "optimise", "colour", and "licence" (noun).

## Engineering Guardrails

- Keep edits focused and minimal.
- Do not refactor unrelated code.
- Preserve existing architecture boundaries and naming conventions.
- Avoid editing generated output unless explicitly requested.

## Hugo Guidance (`src/`)

- Follow existing front matter and section patterns in `src/content/`.
- Reuse current shortcodes and template patterns in `src/layouts/`.
- Prefer content edits over structural template changes unless requested.
- Treat `src/public/` and `src/resources/_gen/` as build artefacts; edit only when the task explicitly asks for it.

## Editorial Voice

Sundown Sessions is a solo project. Use first-person singular (`I`, `me`, `my`) when writing as the presenter, curator or person behind the project. Use `Sundown Sessions`, `the show` or `the site` when describing the project itself. Do not use `we`, `us` or `our` in a way that implies Sundown Sessions is run by a team. Inclusive `we` is appropriate when it genuinely refers to the presenter and listeners/community together.

This applies to pages, microcopy, metadata, contact copy and all editorial prose.

## Safety and Quality

- Confirm assumptions from nearby code before making changes.
- Maintain clear, deterministic behaviour.
- If requirements are ambiguous, choose the smallest safe implementation and document assumptions in the response.

## Markdown Linting

All Markdown files are linted using markdownlint. The shared ruleset is in `.markdownlint.jsonc`. When authoring or editing Markdown, ensure content passes linting locally with:

```bash
npx markdownlint-cli2 "**/*.md" "!src/themes/**"
```

The `Lint Markdown` GitHub Actions workflow validates Markdown on pull requests.
