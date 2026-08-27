#!/usr/bin/env python3
"""Validate release tracklists and write a repeatable catalogue audit report."""

from __future__ import annotations

import argparse
import re
import urllib.parse
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path

import yaml


DURATION_RE = re.compile(r"^(?:\d+:)?[0-5]?\d:[0-5]\d$")
MARKDOWN_TRACKLIST_RE = re.compile(
    r"(?ims)^##\s+Tracklist\s*$\s*(.*?)(?=^##\s+|\Z)"
)
MARKDOWN_TRACK_RE = re.compile(
    r"^\s*(\d+)[.)]\s+(.+?)(?:\s+\((\d+(?::\d{2}){1,2})\))?\s*$"
)


@dataclass
class Audit:
    path: Path
    title: str
    track_count: int = 0
    representation: str = "missing"
    source: str = ""
    edition: str = ""
    errors: list[str] = field(default_factory=list)


def split_front_matter(text: str) -> tuple[dict, str]:
    text = text.removeprefix("\ufeff")
    match = re.match(r"(?s)^---\s*\n(.*?)\n---\s*(?:\n|$)(.*)$", text)
    if not match:
        return {}, text
    data = yaml.safe_load(match.group(1)) or {}
    return data if isinstance(data, dict) else {}, match.group(2)


def duration_seconds(value: str) -> int:
    parts = [int(part) for part in value.split(":")]
    if len(parts) == 2:
        return parts[0] * 60 + parts[1]
    return parts[0] * 3600 + parts[1] * 60 + parts[2]


def structured_track_errors(data: dict, tracks: list) -> list[str]:
    errors: list[str] = []
    by_disc: dict[int, list[tuple[int, str]]] = {}
    calculated_duration = 0
    all_durations_known = True

    for index, track in enumerate(tracks, start=1):
        if not isinstance(track, dict):
            errors.append(f"track {index} is not structured data")
            continue
        title = str(track.get("title", "")).strip()
        disc = track.get("discNumber", 1)
        number = track.get("trackNumber")
        if not title:
            errors.append(f"track {index} has no title")
        if not isinstance(disc, int) or disc < 1:
            errors.append(f"track {index} has invalid discNumber {disc!r}")
            continue
        if not isinstance(number, int) or number < 1:
            errors.append(f"track {index} has invalid trackNumber {number!r}")
            continue
        by_disc.setdefault(disc, []).append((number, title))

        duration = str(track.get("duration", "")).strip()
        if not duration:
            all_durations_known = False
        elif not DURATION_RE.fullmatch(duration):
            errors.append(f"disc {disc} track {number} has invalid duration {duration!r}")
        else:
            calculated_duration += duration_seconds(duration)

    for disc, entries in sorted(by_disc.items()):
        numbers = [number for number, _ in entries]
        expected = list(range(1, len(entries) + 1))
        if numbers != expected:
            errors.append(
                f"disc {disc} track numbers are {numbers}; expected {expected}"
            )
        duplicate_numbers = sorted(
            number for number, count in Counter(numbers).items() if count > 1
        )
        if duplicate_numbers:
            errors.append(f"disc {disc} has duplicate track numbers {duplicate_numbers}")
        title_counts = Counter(title.casefold() for _, title in entries if title)
        duplicate_titles = sorted(
            title for title, count in title_counts.items() if count > 1
        )
        if duplicate_titles:
            errors.append(f"disc {disc} has duplicate track titles {duplicate_titles}")

    release_duration = str(data.get("duration", "")).strip()
    if release_duration:
        if not DURATION_RE.fullmatch(release_duration):
            errors.append(f"release has invalid duration {release_duration!r}")
        elif all_durations_known and duration_seconds(release_duration) != calculated_duration:
            errors.append(
                "release duration does not equal the sum of its track durations"
            )
    return errors


def audit_release(path: Path) -> Audit:
    data, body = split_front_matter(path.read_text(encoding="utf-8-sig"))
    audit = Audit(path=path, title=str(data.get("title", path.parent.name)))
    audit.source = str(data.get("tracklist_source", "")).strip()
    audit.edition = str(data.get("tracklist_edition", "")).strip()
    tracks = data.get("tracks")
    if isinstance(tracks, list) and tracks:
        audit.representation = "structured"
        audit.track_count = len(tracks)
        audit.errors.extend(structured_track_errors(data, tracks))
        return audit

    match = MARKDOWN_TRACKLIST_RE.search(body)
    if match:
        audit.representation = "markdown"
        parsed = [
            MARKDOWN_TRACK_RE.match(line)
            for line in match.group(1).splitlines()
            if line.strip()
        ]
        audit.track_count = sum(item is not None for item in parsed)
        if audit.track_count == 0:
            audit.errors.append("Tracklist heading has no numbered tracks")
        elif any(item is None for item in parsed):
            audit.errors.append("Tracklist contains unrecognised lines")
        return audit

    audit.errors.append("no tracklist is recorded")
    return audit


def build_report(audits: list[Audit], root: Path) -> str:
    counts = Counter(audit.representation for audit in audits)
    invalid = [audit for audit in audits if audit.errors]
    duplicate_warnings = [
        audit
        for audit in invalid
        if all("duplicate track titles" in error for error in audit.errors)
    ]
    actionable = [audit for audit in invalid if audit not in duplicate_warnings]
    sourced = [audit for audit in audits if audit.source]
    source_hosts = Counter(
        urllib.parse.urlparse(audit.source).netloc or "Sundown Sessions"
        for audit in sourced
    )
    multi_disc = 0
    for audit in audits:
        data, _ = split_front_matter(audit.path.read_text(encoding="utf-8-sig"))
        tracks = data.get("tracks", [])
        if isinstance(tracks, list) and any(
            isinstance(track, dict) and track.get("discNumber", 1) != 1
            for track in tracks
        ):
            multi_disc += 1
    lines = [
        "# Release Tracklist Audit",
        "",
        "Generated by `scripts/audit-release-tracklists.py`. This report is safe to "
        "regenerate and reports repository data only; it does not claim external "
        "verification where no source has been recorded.",
        "",
        "## Summary",
        "",
        f"- Releases reviewed: {len(audits)}",
        f"- Tracks catalogued: {sum(audit.track_count for audit in audits)}",
        f"- Structured tracklists: {counts['structured']}",
        f"- Legacy Markdown tracklists: {counts['markdown']}",
        f"- Releases without tracklists: {counts['missing']}",
        f"- Releases with recorded source provenance: {len(sourced)}",
        f"- Multi-disc releases represented: {multi_disc}",
        f"- Releases passing structural validation: {len(audits) - len(actionable)}",
        f"- Releases requiring correction: {len(actionable)}",
        f"- Releases with intentional duplicate-title warnings: {len(duplicate_warnings)}",
        "",
        "## Sources used",
        "",
    ]
    lines.extend(
        f"- {host}: {count} release{'s' if count != 1 else ''}"
        for host, count in sorted(source_hosts.items())
    )
    lines.extend([
        "",
        "## Corrections in issue #766",
        "",
        "- Queens of the Stone Age — *Villains*: corrected the original nine-track "
        "album order and all durations; moved the list into structured front matter "
        "and preserved the existing Track-page link.",
        "",
        "Source: [MusicBrainz release 68d808e1-abb9-4da2-80a3-5b1b0634006b]"
        "(https://musicbrainz.org/release/68d808e1-abb9-4da2-80a3-5b1b0634006b).",
        "",
        "## Validation findings",
        "",
        "Duplicate titles are retained when they occur in the selected edition, for "
        "example reprises, alternate versions, interludes, or multi-disc compilations.",
        "",
    ])
    for audit in invalid:
        relative = audit.path.relative_to(root)
        lines.append(f"- `{relative}` — {'; '.join(audit.errors)}")
    if not invalid:
        lines.append("- None")
    lines.append("")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument(
        "--report", type=Path, default=Path("reports/release-tracklist-audit.md")
    )
    parser.add_argument(
        "--check", action="store_true", help="fail if the generated report is stale"
    )
    args = parser.parse_args()
    root = args.root.resolve()
    releases = root / "src/content/releases"
    paths = sorted(
        path for path in releases.rglob("*.md") if path.name != "_index.md"
    )
    audits = [audit_release(path) for path in paths]
    report = build_report(audits, root)
    report_path = args.report if args.report.is_absolute() else root / args.report
    if args.check:
        if not report_path.exists() or report_path.read_text(encoding="utf-8") != report:
            print(f"stale audit report: {report_path}")
            return 1
    else:
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(report, encoding="utf-8")
    findings = sum(bool(audit.errors) for audit in audits)
    print(f"reviewed {len(audits)} releases; {findings} validation findings")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
