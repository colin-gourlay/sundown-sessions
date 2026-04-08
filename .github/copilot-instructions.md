# Copilot Instructions for Sundown Sessions

## Repository Context

This repository contains two primary workstreams:

- `src/`: Hugo-based website content and templates for Sundown Sessions.
- `automation/dotnet/`: ContentOps automation platform built with .NET using clean architecture boundaries.

Keep changes scoped to the area requested. Do not blend website concerns and automation concerns unless the task explicitly requires both.

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
- Add or update tests when changing behaviour in `automation/dotnet/`.
- Avoid editing generated output unless explicitly requested.

## Hugo Guidance (`src/`)

- Follow existing front matter and section patterns in `src/content/`.
- Reuse current shortcodes and template patterns in `src/layouts/`.
- Prefer content edits over structural template changes unless requested.
- Treat `src/public/` and `src/resources/_gen/` as build artefacts; edit only when the task explicitly asks for it.

## ContentOps Guidance (`automation/dotnet/`)

- Keep domain rules in Domain, orchestration in Application, technical details in Infrastructure, and startup wiring in CLI.
- Follow one-type-per-file conventions.
- Prefer explicit result and validation flows over exception-driven control flow for expected failures.
- Respect integration boundaries for Spotify and Lidarr libraries.

## Safety and Quality

- Confirm assumptions from nearby code before making changes.
- Maintain clear, deterministic behaviour.
- If requirements are ambiguous, choose the smallest safe implementation and document assumptions in the response.