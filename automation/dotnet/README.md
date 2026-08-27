# Sundown Showrunner

Showrunner keeps authoritative show state in SQLite and exposes deterministic
preparation capabilities through a local stdio MCP server. It does not expose
an HTTP listener or arbitrary filesystem operations.

## Configuration

Configure the server with environment variables:

- `SUNDOWN_SHOWRUNNER_DB_PATH`: optional SQLite path. The default is the
  platform-local application data directory.
- `SUNDOWN_SHOWRUNNER_MUSIC_ROOT`: required root of the read-only FLAC library.
- `SUNDOWN_SHOWRUNNER_PREPARATION_ROOT`: required root for rebuilt broadcast
  folders.
- `SUNDOWN_SHOWRUNNER_SHOW_DURATION_MINUTES`: optional positive programme
  duration used to calculate remaining time or overrun.
- `SUNDOWN_SHOWRUNNER_MIXXX_DB_PATH`: optional path to Mixxx's SQLite
  database for playback evidence. Defaults to `~/.mixxx/mixxxdb.sqlite`.

The Mixxx adapter opens that database read-only and understands Mixxx history
playlists (`Playlists.hidden = 2` with ordered `PlaylistTracks`). Evidence is
limited to a history session matching the Showrunner show date. If no session
or more than one session matches, the result remains explicitly incomplete and
includes session summaries rather than combining or guessing.

The music root cannot be inside the preparation root. Preparation results use
root-relative source paths and stable folder/file names; absolute local paths
are not returned through MCP.

## Spotify integration placement

No Spotify Web API or host-provided Spotify integration is exposed to the MCP
agent. This is an intentional implementation-time restriction, not a missing
generic proxy: as of 24 August 2026, the
[Spotify Developer Terms](https://developer.spotify.com/terms) and
[Developer Policy](https://developer.spotify.com/policy) prohibit using the
Spotify Platform or Spotify Content to train or otherwise ingest Spotify
Content into a machine-learning or AI model. The current
[playlist-items endpoint](https://developer.spotify.com/documentation/web-api/reference/get-playlists-items)
repeats that restriction. Returning playlist order or metadata to an MCP agent
would cross that boundary.

Showrunner therefore provides only integration-neutral deterministic
capabilities: an operator can explicitly associate an external identifier,
intentionally refresh a plan from a reviewed ordered recording list, obtain
authoritative repeat history, and retrieve finalised played/dropped identifiers
for manual housekeeping. It does not scrape Spotify, accept a Spotify access
token, or call the technically available February 2026 `/items` read/write
endpoints. Spotify remains available as the operator's sequencing UI, and
Spotify failure or policy changes cannot affect authoritative Showrunner
history.

If Spotify later changes the restriction or grants written permission for this
workflow, reassess whether the chosen agent host has a safe integration before
adding a thin replaceable adapter. Until then, playlist reading, backlog
removal and show-playlist correction remain explicit manual steps.

## Todoist integration placement

Todoist remains an external capture workspace. Showrunner does not accept
Todoist credentials or proxy its API: the MCP-capable agent host reads and
completes tasks through its own replaceable integration. Showrunner receives
only structured, operator-resolved recording data and an integration-neutral
external source reference.

`backlog_candidate_import` persists the recording (when new), source reference
and backlog item in one transaction. The caller must provide exactly one
resolved identity: an existing `recordingId`, or `newRecording` only after an
ambiguity-safe `recording_history` lookup finds no match. The source/value pair
is the retry key, so an identical retry returns the same recording and backlog
item with `isNoOp: true`; it cannot silently reassign the task to a different
recording.

After finalisation, `show_reconciliation_finalise` returns source references
separately for recordings that aired and recordings that were planned but
dropped. The agent host can therefore complete only aired Todoist tasks and
leave dropped candidates available. A Todoist outage cannot affect Showrunner
finalisation, and the stable finalisation retry payload supports later
housekeeping retries.

## Run the MCP server

From the repository root:

```bash
dotnet run \
  --project automation/dotnet/src/SundownSessions.Showrunner.Mcp
```

The stdio server exposes focused tools:

- `show_get` finds a show by its authoritative identifier, slug, or broadcast
  date and surfaces multiple shows on the same date as ambiguous.
- `show_prepare` matches a plan, checks repeats, calculates timings and rebuilds
  the numbered folder only when preparation is fully resolved.
- `recording_resolve` records an explicit choice of a candidate returned by
  `show_prepare`.
- `recording_external_identifier_add` associates a Spotify or other external
  identifier with an existing authoritative recording.
- `backlog_candidate_import` atomically imports an externally captured,
  identity-resolved candidate and is safely retryable by source reference.
- `backlog_item_list` returns the authoritative backlog in stable chronological
  order.
- `show_plan_refresh` intentionally refreshes the mutable planned order and
  returns authoritative repeat history. It is blocked once reconciliation has
  started.
- `repeat_exception_create` records an explicit repeat reason separately from
  preparation.
- `show_reconciliation_evidence` reads Mixxx playback evidence and compares it
  with the authoritative show plan, returning dropped/unexpected/order
  differences and uncertainty explicitly.
- `show_reconciliation_confirm` confirms an explicit operator-approved
  playback order and rejects unresolved ambiguity. It stores reconciliation
  state for later finalisation; it does not create permanent broadcast history.
- `show_reconciliation_finalise` persistently finalises an
  operator-confirmed reconciliation into permanent broadcast history.
- `show_publication_export` returns the finalised, ordered factual show state
  needed by a repository-aware publication agent. It excludes operator notes,
  local-file references, Todoist task references and other workflow state.
- `recording_history` returns structured permanent broadcast history for an
  exact recording identifier, exact external identifier, or an ambiguity-safe
  title/artist lookup.

Permanent broadcast history has one write path: confirm the operator-approved
playback order, then call `show_reconciliation_finalise`. The older application
reconciliation-save operation stores draft state only and cannot bypass this
boundary. A finalisation retry returns the same played/dropped recording and
external-identifier summary, allowing interrupted external housekeeping to be
resumed safely. Manual Spotify housekeeping never creates broadcast history,
and Spotify availability cannot roll back or alter authoritative history.

The host-neutral publication procedure and its editorial, schema-drift and
validation safeguards are documented in
[Showrunner publication workflow](../../docs/showrunner-publication-workflow.md).

## Verify

```bash
dotnet test automation/dotnet/Showrunner.sln --configuration Release
```
