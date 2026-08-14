#!/usr/bin/env python3
"""Audit Sundown Sessions artist biographies for editorial review candidates."""

from __future__ import annotations

import argparse
import datetime as dt
import re
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path

STALE_AFTER_DAYS = 365
DATE_RE = re.compile(r"^lastReviewed:\s*['\"]?([0-9]{4}-[0-9]{2}-[0-9]{2})['\"]?\s*$", re.MULTILINE)
TITLE_RE = re.compile(r"^title:\s*['\"]?(.+?)['\"]?\s*$", re.MULTILINE)
PLACEHOLDER_RE = re.compile(r"\b(None found, add one\?|TODO|TBC|to be confirmed)\b", re.IGNORECASE)
DATED_RELEASE_RE = re.compile(
    r"\b(latest|new|newest|recent|most recent|forthcoming|upcoming|currently|continue(?:s)? to|still)\b[^\n.]{0,120}\b(19|20)\d{2}\b",
    re.IGNORECASE,
)
OLD_YEAR_RE = re.compile(r"\b(19|20)\d{2}\b")
MD026_TRAILING_PUNCTUATION = ".,;:!。，；：！"


@dataclass(order=True)
class Finding:
    path: str
    title: str
    reasons: list[str] = field(default_factory=list)


def split_front_matter(text: str) -> tuple[str, str]:
    if not text.startswith("---\n"):
        return "", text
    end = text.find("\n---", 4)
    if end == -1:
        return "", text
    return text[4:end], text[end + 4 :]


def title_from_front_matter(front_matter: str, path: Path) -> str:
    match = TITLE_RE.search(front_matter)
    return match.group(1).strip('"\'') if match else path.parent.name.replace("-", " ").title()


def last_reviewed(front_matter: str) -> dt.date | None:
    match = DATE_RE.search(front_matter)
    if not match:
        return None
    try:
        return dt.date.fromisoformat(match.group(1))
    except ValueError:
        return None


def audit_file(path: Path, today: dt.date, stale_days: int) -> Finding | None:
    text = path.read_text(encoding="utf-8")
    front_matter, body = split_front_matter(text)
    title = title_from_front_matter(front_matter, path)
    reasons: list[str] = []

    reviewed = last_reviewed(front_matter)
    review_is_current = False
    if reviewed is None:
        reasons.append("missing `lastReviewed` front matter")
    elif reviewed > today:
        reasons.append(f"`lastReviewed` is in the future ({reviewed.isoformat()})")
    elif (today - reviewed).days > stale_days:
        reasons.append(f"`lastReviewed` is older than {stale_days} days ({reviewed.isoformat()})")
    else:
        review_is_current = True

    about_match = re.search(r"## About\s*(.*?)(?:\n## |\Z)", body, re.IGNORECASE | re.DOTALL)
    about = about_match.group(1).strip() if about_match else ""
    if not about:
        reasons.append("missing `## About` biography text")
    if PLACEHOLDER_RE.search(about):
        reasons.append("placeholder biography wording remains")

    # Freshly reviewed pages are treated as explicitly checked by an editor, so
    # dated-reference heuristics only apply to pages without a current review.
    if not review_is_current:
        if DATED_RELEASE_RE.search(about):
            reasons.append("possibly stale release or career-currentness wording")

        years = [int(match.group(0)) for match in OLD_YEAR_RE.finditer(about)]
        if years and max(years) <= today.year - 7 and len(about.split()) > 35:
            reasons.append(f"biography mentions no year after {max(years)}")

    if reasons:
        return Finding(str(path), title, reasons)
    return None


def report_heading(finding: Finding, title_counts: Counter[str]) -> str:
    title = finding.title
    if (
        title_counts[title.casefold()] > 1
        or (title and title[-1] in MD026_TRAILING_PUNCTUATION)
    ):
        return f"{title} — {Path(finding.path).parent.name}"
    return title


def write_report(findings: list[Finding], output: Path, today: dt.date, stale_days: int) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        "# Artist Biography Audit Report",
        "",
        f"Generated: {today.isoformat()}",
        "",
        "This report flags artist biographies that need a human editorial pass. It does not rewrite copy automatically.",
        "",
        "## Review rules",
        "",
        f"- Flag pages without `lastReviewed` front matter.",
        f"- Flag pages where `lastReviewed` is older than {stale_days} days.",
        "- Flag placeholder biography text.",
        "- Flag wording that may imply a release, tour or career summary is still current.",
        "- Flag longer biographies whose dated references appear to stop several years ago.",
        "",
        "## Summary",
        "",
        f"- Artist pages needing review: {len(findings)}",
        "",
    ]

    if findings:
        title_counts = Counter(finding.title.casefold() for finding in findings)
        lines.extend(["## Findings", ""])
        for finding in findings:
            lines.append(f"### {report_heading(finding, title_counts)}")
            lines.append("")
            lines.append(f"- File: `{finding.path}`")
            for reason in finding.reasons:
                lines.append(f"- Reason: {reason}")
            lines.append("")
    else:
        lines.extend(["## Findings", "", "No artist biography review candidates were found.", ""])

    output.write_text("\n".join(lines).rstrip() + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--content-root", default="src/content/artists", type=Path)
    parser.add_argument("--output", default="reports/artist-biography-audit.md", type=Path)
    parser.add_argument("--stale-days", default=STALE_AFTER_DAYS, type=int)
    parser.add_argument("--today", default=dt.date.today().isoformat())
    parser.add_argument("--fail-on-findings", action="store_true")
    args = parser.parse_args()

    today = dt.date.fromisoformat(args.today)
    findings = [
        finding
        for path in sorted(args.content_root.glob("*/*/index.md"))
        if (finding := audit_file(path, today, args.stale_days))
    ]
    write_report(findings, args.output, today, args.stale_days)
    print(f"Wrote {args.output} with {len(findings)} artist biography review candidate(s).")
    return 1 if args.fail_on_findings and findings else 0


if __name__ == "__main__":
    raise SystemExit(main())
