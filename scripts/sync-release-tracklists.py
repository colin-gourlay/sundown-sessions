#!/usr/bin/env python3
"""Synchronise every release tracklist from an edition-specific MusicBrainz release.

Responses are cached so an interrupted run is safe to resume. Files are only changed
after a release-group and release edition have both been selected.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import time
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
from collections import Counter
from dataclasses import dataclass
from datetime import date
from pathlib import Path
from typing import Any

import yaml


USER_AGENT = (
    "SundownSessionsTracklistAudit/1.0 "
    "(https://github.com/colin-gourlay/sundown-sessions)"
)
GROUP_ID_RE = re.compile(r"musicbrainz\.org/release-group/([0-9a-f-]{36})", re.I)
FRONT_MATTER_RE = re.compile(r"(?s)^(\ufeff?---\s*\n)(.*?)(\n---\s*(?:\n|$))(.*)$")
EDITION_WORDS = {
    "anniversary",
    "bonus",
    "deluxe",
    "expanded",
    "legacy",
    "remaster",
    "remastered",
    "reissue",
    "version",
}


@dataclass
class Result:
    path: Path
    artist: str
    title: str
    status: str
    detail: str = ""
    group_id: str = ""
    release_id: str = ""
    source: str = ""
    track_count: int = 0


class MusicBrainz:
    def __init__(self, cache: Path, delay: float = 1.05):
        self.cache = cache
        self.cache.mkdir(parents=True, exist_ok=True)
        self.delay = delay
        self.last_request = 0.0

    def get(self, endpoint: str, params: dict[str, str] | None = None) -> dict:
        query = urllib.parse.urlencode(params or {})
        url = f"https://musicbrainz.org/ws/2/{endpoint}"
        if query:
            url += f"?{query}"
        cache_file = self.cache / f"{hashlib.sha256(url.encode()).hexdigest()}.json"
        if cache_file.exists():
            return json.loads(cache_file.read_text(encoding="utf-8"))

        remaining = self.delay - (time.monotonic() - self.last_request)
        if remaining > 0:
            time.sleep(remaining)
        request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
        for attempt in range(5):
            try:
                with urllib.request.urlopen(request, timeout=30) as response:
                    payload = json.load(response)
                self.last_request = time.monotonic()
                cache_file.write_text(
                    json.dumps(payload, ensure_ascii=False), encoding="utf-8"
                )
                return payload
            except (urllib.error.URLError, TimeoutError):
                if attempt == 4:
                    raise
                time.sleep(2**attempt)
        raise RuntimeError("unreachable")


def normalise(value: Any) -> str:
    text = unicodedata.normalize("NFKD", str(value or "")).casefold()
    text = "".join(character for character in text if not unicodedata.combining(character))
    text = text.replace("&", " and ")
    return " ".join(re.sub(r"[^a-z0-9]+", " ", text).split())


def artist_credit(value: dict) -> str:
    return "".join(
        f"{part.get('name', '')}{part.get('joinphrase', '')}"
        for part in value.get("artist-credit", [])
    )


def parse_year(value: Any) -> str:
    match = re.search(r"\b(19|20)\d{2}\b", str(value or ""))
    return match.group(0) if match else ""


def existing_group_id(data: dict) -> str:
    links = data.get("links", {})
    values: list[Any] = []
    if isinstance(links, dict):
        values.extend(links.values())
    elif isinstance(links, list):
        values.extend(
            item.get("url") for item in links if isinstance(item, dict)
        )
    for value in values:
        match = GROUP_ID_RE.search(str(value))
        if match:
            return match.group(1)
    return ""


def canonical_release_title(value: str) -> str:
    value = re.sub(
        r"\s*\([^)]*(?:"
        + "|".join(sorted(EDITION_WORDS))
        + r"|(?:19|20)\d{2}|u\.?s\.?)\b[^)]*\)",
        "",
        value,
        flags=re.I,
    )
    return normalise(value)


def choose_group(candidates: list[dict], data: dict) -> tuple[dict | None, str]:
    if not candidates:
        return None, "found 0 exact release-group matches"
    wanted_year = parse_year(
        data.get("releaseDate") or data.get("release_date") or data.get("date")
    )
    wanted_type = normalise(
        data.get("releaseType") or data.get("release_type") or ""
    )
    if wanted_type:
        typed = [
            group
            for group in candidates
            if normalise(group.get("primary-type")) == wanted_type
        ]
        if typed:
            candidates = typed
    if wanted_year:
        dated = [
            group
            for group in candidates
            if str(group.get("first-release-date", "")).startswith(wanted_year)
        ]
        if dated:
            candidates = dated
    candidates.sort(
        key=lambda group: (
            -int(group.get("score", 0)),
            str(group.get("first-release-date") or "9999"),
            len(str(group.get("disambiguation", ""))),
            group["id"],
        )
    )
    chosen = candidates[0]
    return chosen, f"selected 1 of {len(candidates)} artist/title release-group matches"


def find_artist_id(client: MusicBrainz, artist: str) -> str:
    payload = client.get(
        "artist/", {"query": f'artist:"{artist}"', "fmt": "json", "limit": "10"}
    )
    wanted = normalise(artist)
    exact = [
        candidate
        for candidate in payload.get("artists", [])
        if normalise(candidate.get("name")) == wanted
        or any(
            normalise(alias.get("name")) == wanted
            for alias in candidate.get("aliases", [])
        )
    ]
    exact.sort(key=lambda candidate: (-int(candidate.get("score", 0)), candidate["id"]))
    return exact[0]["id"] if exact else ""


def find_group(
    client: MusicBrainz, data: dict, *, use_existing: bool = True
) -> tuple[str, str]:
    known = existing_group_id(data)
    if known and use_existing:
        return known, "existing MusicBrainz release-group link"

    title = str(data.get("title", "")).strip()
    artist = data.get("artist") or data.get("artists") or ""
    if isinstance(artist, list):
        artist = artist[0] if artist else ""
    search_title = re.sub(
        r"\s*\([^)]*(?:"
        + "|".join(sorted(EDITION_WORDS))
        + r"|(?:19|20)\d{2}|u\.?s\.?)\b[^)]*\)",
        "",
        title,
        flags=re.I,
    ).strip()
    artist_id = find_artist_id(client, str(artist))
    if not artist_id:
        return "", "artist was not found in MusicBrainz"
    escaped_title = search_title.replace('"', r'\"')
    query = f'releasegroup:"{escaped_title}" AND arid:{artist_id}'
    payload = client.get(
        "release-group/", {"query": query, "fmt": "json", "limit": "10"}
    )
    title_key, artist_key = canonical_release_title(title), normalise(artist)
    candidates = []
    for group in payload.get("release-groups", []):
        group_title = canonical_release_title(str(group.get("title", "")))
        group_artist = normalise(artist_credit(group))
        if group_title != title_key:
            continue
        if group_artist != artist_key:
            continue
        candidates.append(group)
    chosen, reason = choose_group(candidates, data)
    if not chosen:
        return "", reason
    return chosen["id"], f"exact artist/title MusicBrainz search; {reason}"


def edition_tokens(title: str) -> set[str]:
    words = set(normalise(title).split())
    return words & EDITION_WORDS


def choose_release(client: MusicBrainz, group_id: str, data: dict) -> tuple[dict | None, str]:
    payload = client.get(
        "release",
        {
            "release-group": group_id,
            "inc": "labels+media",
            "fmt": "json",
            "limit": "100",
        },
    )
    releases = [
        release
        for release in payload.get("releases", [])
        if release.get("status") in (None, "Official") and release.get("media")
    ]
    if not releases:
        return None, "release group has no official edition with media"

    title = str(data.get("title", ""))
    wanted_year = parse_year(
        data.get("releaseDate") or data.get("release_date") or data.get("date")
    )
    wanted_catalogue = normalise(data.get("catalogue_number"))
    wanted_edition = edition_tokens(title)

    def catalogue_numbers(release: dict) -> set[str]:
        return {
            normalise(item.get("catalog-number"))
            for item in release.get("label-info", [])
            if item.get("catalog-number")
        }

    if wanted_catalogue:
        matches = [
            release
            for release in releases
            if wanted_catalogue in catalogue_numbers(release)
        ]
        if len(matches) == 1:
            return matches[0], "catalogue-number match"
        if matches:
            releases = matches

    if wanted_edition:
        matches = [
            release
            for release in releases
            if wanted_edition
            <= edition_tokens(
                f"{release.get('title', '')} {release.get('disambiguation', '')}"
            )
        ]
        if matches:
            releases = matches

    if wanted_year:
        matches = [
            release for release in releases if str(release.get("date", "")).startswith(wanted_year)
        ]
        if matches:
            releases = matches

    def score(release: dict) -> tuple:
        release_date = str(release.get("date") or "9999-99-99")
        formats = {
            normalise(medium.get("format"))
            for medium in release.get("media", [])
        }
        format_rank = (
            0
            if "cd" in formats
            else 1
            if "digital media" in formats
            else 2
        )
        country_rank = 0 if release.get("country") == "GB" else 1
        bonus_rank = len(
            edition_tokens(
                f"{release.get('title', '')} {release.get('disambiguation', '')}"
            )
            - wanted_edition
        )
        return release_date, bonus_rank, format_rank, country_rank, release["id"]

    releases.sort(key=score)
    chosen = releases[0]
    reason = (
        f"earliest matching official edition ({chosen.get('date', 'undated')}, "
        f"{chosen.get('country', 'no country')})"
    )
    return chosen, reason


def load_release(client: MusicBrainz, release_id: str) -> dict:
    return client.get(
        f"release/{release_id}",
        {"inc": "recordings+labels+artist-credits", "fmt": "json"},
    )


def find_direct_release(client: MusicBrainz, data: dict) -> tuple[dict | None, str]:
    title = str(data.get("title", "")).strip()
    artist = data.get("artist") or data.get("artists") or ""
    if isinstance(artist, list):
        artist = artist[0] if artist else ""
    search_title = re.sub(
        r"\s*\([^)]*(?:"
        + "|".join(sorted(EDITION_WORDS))
        + r"|(?:19|20)\d{2}|u\.?s\.?)\b[^)]*\)",
        "",
        title,
        flags=re.I,
    ).strip()
    payload = client.get(
        "release/",
        {
            "query": (
                f"release:({normalise(search_title)}) "
                f"AND artist:({normalise(artist)})"
            ),
            "fmt": "json",
            "limit": "100",
        },
    )
    title_key = canonical_release_title(title)
    artist_key = normalise(artist)
    candidates = []
    close_candidates = []
    for release in payload.get("releases", []):
        if release.get("status") not in (None, "Official") or not release.get("media"):
            continue
        credited = normalise(artist_credit(release))
        if credited != artist_key and artist_key not in credited and credited not in artist_key:
            continue
        if canonical_release_title(str(release.get("title", ""))) == title_key:
            candidates.append(release)
        elif int(release.get("score", 0)) >= 90:
            close_candidates.append(release)
    selection_kind = "exact"
    if not candidates:
        candidates = close_candidates
        selection_kind = "high-confidence search"
    if not candidates:
        return None, "direct release search found no exact edition"

    wanted_year = parse_year(
        data.get("releaseDate") or data.get("release_date") or data.get("date")
    )
    wanted_edition = edition_tokens(title)
    if wanted_edition:
        edition_matches = [
            release
            for release in candidates
            if wanted_edition
            <= edition_tokens(
                f"{release.get('title', '')} {release.get('disambiguation', '')}"
            )
        ]
        if edition_matches:
            candidates = edition_matches
    if wanted_year:
        dated = [
            release
            for release in candidates
            if str(release.get("date", "")).startswith(wanted_year)
        ]
        if dated:
            candidates = dated
    candidates.sort(
        key=lambda release: (
            -int(release.get("score", 0)),
            str(release.get("date") or "9999-99-99"),
            0 if release.get("country") == "GB" else 1,
            release["id"],
        )
    )
    return candidates[0], (
        f"direct {selection_kind} artist/title edition search "
        f"({len(candidates)} candidates)"
    )


def format_duration(milliseconds: Any) -> str:
    if not isinstance(milliseconds, int) or milliseconds <= 0:
        return ""
    seconds = round(milliseconds / 1000)
    hours, remainder = divmod(seconds, 3600)
    minutes, seconds = divmod(remainder, 60)
    if hours:
        return f"{hours}:{minutes:02d}:{seconds:02d}"
    return f"{minutes}:{seconds:02d}"


def track_url_map(data: dict, body: str) -> dict[str, str]:
    urls: dict[str, str] = {}
    tracks = data.get("tracks")
    if isinstance(tracks, list):
        for track in tracks:
            if isinstance(track, dict) and track.get("title") and track.get("url"):
                urls[normalise(track["title"])] = str(track["url"])
    for title, url in re.findall(
        r"(?m)^\s*\d+[.)]\s+\[([^\]]+)\]\((/tracks/[^)]+)\)", body
    ):
        urls[normalise(title)] = url
    return urls


def release_tracks(release: dict, urls: dict[str, str]) -> list[dict]:
    media = release.get("media", [])
    include_disc = len(media) > 1
    tracks: list[dict] = []
    for disc_index, medium in enumerate(media, start=1):
        for index, item in enumerate(medium.get("tracks", []), start=1):
            title = str(item.get("title") or item.get("recording", {}).get("title") or "").strip()
            if not title:
                continue
            track: dict[str, Any] = {}
            if include_disc:
                track["discNumber"] = disc_index
            track["trackNumber"] = index
            track["title"] = title
            duration = format_duration(item.get("length") or item.get("recording", {}).get("length"))
            if duration:
                track["duration"] = duration
            if normalise(title) in urls:
                track["url"] = urls[normalise(title)]
            tracks.append(track)
    return tracks


def yaml_scalar(value: str) -> str:
    return json.dumps(value, ensure_ascii=False)


def tracks_yaml(tracks: list[dict]) -> list[str]:
    lines = ["tracks:"]
    for track in tracks:
        first = True
        for key in ("discNumber", "trackNumber", "title", "duration", "url"):
            if key not in track:
                continue
            value = track[key]
            encoded = str(value) if isinstance(value, int) else yaml_scalar(str(value))
            lines.append(f"  - {key}: {encoded}" if first else f"    {key}: {encoded}")
            first = False
    return lines


def replace_top_level(front: str, key: str, new_lines: list[str]) -> str:
    lines = front.splitlines()
    start = next(
        (index for index, line in enumerate(lines) if re.match(rf"^{re.escape(key)}\s*:", line)),
        None,
    )
    if start is None:
        insertion = len(lines)
        return "\n".join(lines[:insertion] + new_lines + lines[insertion:])
    end = start + 1
    while end < len(lines) and (not lines[end] or lines[end][0].isspace()):
        end += 1
    return "\n".join(lines[:start] + new_lines + lines[end:])


def strip_markdown_tracklist(body: str) -> str:
    return re.sub(
        r"(?ims)^##\s+Tracklist\s*$\s*.*?(?=^##\s+|\Z)",
        "",
        body,
    ).lstrip("\n")


def update_file(path: Path, data: dict, release: dict, source: str) -> int:
    original = path.read_text(encoding="utf-8-sig")
    match = FRONT_MATTER_RE.match(original)
    if not match:
        raise ValueError("missing YAML front matter")
    front, body = match.group(2), match.group(4)
    tracks = release_tracks(release, track_url_map(data, body))
    if not tracks:
        raise ValueError("selected edition has no tracks")
    all_have_duration = all(track.get("duration") for track in tracks)

    front = replace_top_level(front, "tracks", tracks_yaml(tracks))
    if all_have_duration:
        total_seconds = sum(
            sum(
                int(part) * (60**position)
                for position, part in enumerate(
                    reversed(str(track["duration"]).split(":"))
                )
            )
            for track in tracks
        )
        total_duration = (
            f"{total_seconds // 3600}:{(total_seconds % 3600) // 60:02d}:"
            f"{total_seconds % 60:02d}"
            if total_seconds >= 3600
            else f"{total_seconds // 60}:{total_seconds % 60:02d}"
        )
        front = replace_top_level(
            front, "duration", [f"duration: {yaml_scalar(total_duration)}"]
        )
    front = replace_top_level(
        front, "tracklist_source", [f"tracklist_source: {yaml_scalar(source)}"]
    )
    edition = " ".join(
        part
        for part in (
            str(release.get("date", "")),
            str(release.get("country", "")),
            str(release.get("disambiguation", "")),
        )
        if part
    )
    front = replace_top_level(
        front, "tracklist_edition", [f"tracklist_edition: {yaml_scalar(edition)}"]
    )
    updated = f"---\n{front}\n---\n{strip_markdown_tracklist(body)}"
    path.write_text(updated, encoding="utf-8")
    return len(tracks)


def write_report(results: list[Result], root: Path, report_path: Path) -> None:
    counts = Counter(result.status for result in results)
    lines = [
        "# Release Tracklist Synchronisation",
        "",
        f"Generated: {date.today().isoformat()}",
        "",
        "## Summary",
        "",
        f"- Releases reviewed: {len(results)}",
        f"- Releases updated from an edition-specific source: {counts['updated']}",
        f"- Releases unchanged: {counts['unchanged']}",
        f"- Releases requiring manual source resolution: {counts['manual-review']}",
        f"- Errors: {counts['error']}",
        f"- Tracks written: {sum(result.track_count for result in results if result.status == 'updated')}",
        "",
        "MusicBrainz release URLs below identify the exact edition used. Existing "
        "editorial content and matching Track-page links were preserved.",
        "",
        "## Updated",
        "",
    ]
    for result in results:
        if result.status == "updated":
            relative = result.path.relative_to(root)
            lines.append(
                f"- `{relative}` — {result.artist} — *{result.title}*: "
                f"{result.track_count} tracks; [{result.release_id}]({result.source}); "
                f"{result.detail}"
            )
    lines.extend(["", "## Manual review and errors", ""])
    unresolved = [result for result in results if result.status != "updated"]
    if not unresolved:
        lines.append("- None")
    for result in unresolved:
        relative = result.path.relative_to(root)
        lines.append(
            f"- `{relative}` — {result.artist} — *{result.title}*: "
            f"**{result.status}** — {result.detail}"
        )
    lines.append("")
    report_path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--cache", type=Path, default=Path(".cache/musicbrainz"))
    parser.add_argument("--limit", type=int)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--missing-only", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    cache = args.cache if args.cache.is_absolute() else root / args.cache
    client = MusicBrainz(cache)
    paths = sorted(
        path
        for path in (root / "src/content/releases").rglob("*.md")
        if path.name != "_index.md"
    )
    if args.missing_only:
        missing_paths = []
        for path in paths:
            match = FRONT_MATTER_RE.match(path.read_text(encoding="utf-8-sig"))
            data = yaml.safe_load(match.group(2)) if match else {}
            if not isinstance((data or {}).get("tracks"), list) or not data["tracks"]:
                missing_paths.append(path)
        paths = missing_paths
    if args.limit:
        paths = paths[: args.limit]
    results: list[Result] = []
    for number, path in enumerate(paths, start=1):
        original = path.read_text(encoding="utf-8-sig")
        match = FRONT_MATTER_RE.match(original)
        if not match:
            results.append(Result(path, "", path.stem, "error", "missing front matter"))
            continue
        data = yaml.safe_load(match.group(2)) or {}
        artist = data.get("artist") or data.get("artists") or ""
        if isinstance(artist, list):
            artist = ", ".join(map(str, artist))
        title = str(data.get("title", path.parent.name))
        print(f"[{number}/{len(paths)}] {artist} — {title}", flush=True)
        try:
            group_id, group_reason = find_group(client, data)
            if not group_id:
                release, edition_reason = find_direct_release(client, data)
                if release:
                    group_reason = "direct MusicBrainz release search"
                    group_id = str(release.get("release-group", {}).get("id", ""))
                else:
                    results.append(
                        Result(
                            path,
                            str(artist),
                            title,
                            "manual-review",
                            f"{group_reason}; {edition_reason}",
                        )
                    )
                    continue
            else:
                release, edition_reason = choose_release(client, group_id, data)
            if not release and existing_group_id(data):
                fallback_group, fallback_reason = find_group(
                    client, data, use_existing=False
                )
                if fallback_group and fallback_group != group_id:
                    fallback_release, fallback_edition_reason = choose_release(
                        client, fallback_group, data
                    )
                    if fallback_release:
                        group_id = fallback_group
                        group_reason = (
                            f"replaced unusable existing release-group; {fallback_reason}"
                        )
                        release = fallback_release
                        edition_reason = fallback_edition_reason
            if not release:
                direct_release, direct_reason = find_direct_release(client, data)
                if direct_release:
                    release = direct_release
                    group_reason = "direct MusicBrainz release search"
                    edition_reason = direct_reason
                    group_id = str(
                        direct_release.get("release-group", {}).get("id", group_id)
                    )
            if not release:
                results.append(
                    Result(
                        path,
                        str(artist),
                        title,
                        "manual-review",
                        edition_reason,
                        group_id,
                    )
                )
                continue
            detail = load_release(client, release["id"])
            source = f"https://musicbrainz.org/release/{release['id']}"
            tracks = release_tracks(detail, track_url_map(data, match.group(4)))
            if not tracks:
                results.append(
                    Result(
                        path,
                        str(artist),
                        title,
                        "manual-review",
                        "selected edition has no track data",
                        group_id,
                        release["id"],
                        source,
                    )
                )
                continue
            if not args.dry_run:
                count = update_file(path, data, detail, source)
            else:
                count = len(tracks)
            results.append(
                Result(
                    path,
                    str(artist),
                    title,
                    "updated",
                    f"{group_reason}; {edition_reason}",
                    group_id,
                    release["id"],
                    source,
                    count,
                )
            )
        except Exception as error:
            results.append(
                Result(path, str(artist), title, "error", f"{type(error).__name__}: {error}")
            )
    report = root / "reports/release-tracklist-synchronisation.md"
    write_report(results, root, report)
    print(Counter(result.status for result in results), flush=True)
    return 0 if all(result.status == "updated" for result in results) else 2


if __name__ == "__main__":
    raise SystemExit(main())
