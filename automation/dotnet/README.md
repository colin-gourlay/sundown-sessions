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

The music root cannot be inside the preparation root. Preparation results use
root-relative source paths and stable folder/file names; absolute local paths
are not returned through MCP.

## Run the MCP server

From the repository root:

```bash
dotnet run \
  --project automation/dotnet/src/SundownSessions.Showrunner.Mcp
```

The stdio server exposes focused tools:

- `show_prepare` matches a plan, checks repeats, calculates timings and rebuilds
  the numbered folder only when preparation is fully resolved.
- `recording_resolve` records an explicit choice of a candidate returned by
  `show_prepare`.
- `repeat_exception_create` records an explicit repeat reason separately from
  preparation.
- `show_reconciliation_evidence` reads Mixxx playback evidence and compares it
  with the authoritative show plan, returning dropped/unexpected/order
  differences and uncertainty explicitly.
- `show_reconciliation_confirm` confirms an explicit operator-approved
  reconciliation and rejects unresolved ambiguity.

## Verify

```bash
dotnet test automation/dotnet/Showrunner.sln --configuration Release
```
