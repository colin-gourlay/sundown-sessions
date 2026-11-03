# Showrunner publication workflow

This workflow prepares a reviewable Hugo change from authoritative Showrunner
state. It can be followed by any MCP-capable agent with separate repository and
GitHub access; it does not depend on a particular agent host.

Showrunner supplies factual broadcast data. The repository supplies the current
publication model. Neither side is a substitute for the other, and the agent
must not deploy the result.

## Preconditions

- Use a clean, short-lived branch based on the current `main` branch.
- Confirm that the target show has been reconciled and finalised.
- Make the Showrunner MCP server available to the agent alongside read/write
  access to this repository and, when a pull request is required, GitHub.
- Do not give the Showrunner MCP server generic filesystem or GitHub tools.

## Procedure

1. Inspect the current repository before editing. At minimum, inspect a recent
   show bundle, its playlist include, related artist and release pages, the
   repository instructions, and the validation scripts. Do not copy assumptions
   from the obsolete C# Markdown generator or from an earlier publication run.
2. Call `show_publication_export` with exactly one show identifier. Prefer the
   stable show ID or slug; a date is acceptable only when it identifies one
   show unambiguously.
3. Stop if `isFinalised` is false. Treat `finalisedAtUtc` and `finalPlaylist` as
   the finalisation boundary and do not substitute the planned running order.
4. Review every returned track before editing. Missing or ambiguous facts needed
   by the repository's current model must be reported for operator resolution;
   do not infer an artist, release, identifier, relationship or duration.
   Nullable fields remain null (and may be omitted by the MCP serialiser) rather
   than being guessed.
5. Map the facts to the conventions found in step 1. Treat Spotify and other
   returned publication-safe identifiers equally. The export uses an explicit
   public-source allowlist and deliberately omits local files, backlog workflow
   references, and unknown future integration sources. Adding a newly supported
   public identifier requires an application change and privacy review; it must
   not become public merely because it was stored on a recording.
6. Make the smallest deterministic change. Preserve existing show notes,
   biographies, commentary, reviews, images, promotional copy and unrelated
   front matter. Do not create editorial prose to make a factual update appear
   complete.
7. Inspect the diff specifically for deleted or rewritten editorial content.
   If the repository conventions conflict, have changed unexpectedly, or no
   longer represent the exported facts safely, stop and describe the schema
   drift instead of writing an obsolete shape.
8. Call the export again and compare it with the facts used for the edit. Review
   a second application of the same transformation: it should produce no
   additional content changes.
9. Run the checks below. Leave the resulting commit or pull request for normal
   review; do not merge or deploy it automatically.

## Validation

Run the production site build from the repository root:

```bash
hugo --source src --environment production
```

Run the content audits relevant to the files changed. For artist, release and
track relationships, run all current audits:

```bash
python3 scripts/audit-artist-biographies.py
python3 scripts/audit-release-metadata.py
python3 scripts/audit-release-tracklists.py
python3 scripts/audit-release-types.py
```

If the Showrunner implementation or contract changed, also run:

```bash
dotnet test automation/dotnet/Showrunner.sln --configuration Release
```

Generated `src/public/`, `src/resources/_gen/` and report changes are build
artefacts unless a separate task explicitly asks to commit them.

## Review hand-off

The pull request or accompanying issue note should identify:

- the Showrunner show ID, slug and finalisation timestamp used;
- the factual files and fields changed;
- any missing or ambiguous metadata left unresolved;
- how existing editorial content was preserved;
- the validation commands and results;
- any repository convention drift encountered.
