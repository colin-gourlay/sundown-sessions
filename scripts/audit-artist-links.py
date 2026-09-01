#!/usr/bin/env python3
"""Validate Artist relationships emitted by published Hugo shortcodes."""

from __future__ import annotations

import argparse
import json
import re
import shlex
import unicodedata
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).parents[1]
DEFAULT_CONTENT = ROOT / "src/content"
DEFAULT_ARTISTS = DEFAULT_CONTENT / "artists"
DEFAULT_EXCEPTIONS = ROOT / "config/artist-link-exceptions.json"
SHORTCODE = re.compile(
    r"\{\{<\s*(artist-wikilink|title)\s+((?:(?!>}}).)*?)\s*>}}",
    re.DOTALL,
)
FRONT_MATTER = re.compile(r"(?s)^---\s*\n(.*?)\n---(?:\s*\n|$)")


@dataclass(frozen=True)
class ArtistReference:
    source: str
    line: int
    shortcode: str
    artist: str
    artist_slug: str

    @property
    def exception_key(self) -> tuple[object, ...]:
        return (self.source, self.line, self.shortcode, self.artist, self.artist_slug)


def urlize(value: str) -> str:
    """Mirror Hugo's URL form for the Artist names used by this repository."""
    value = unicodedata.normalize("NFKD", value)
    value = value.encode("ascii", "ignore").decode("ascii").lower()
    value = re.sub(r"['’]", "", value)
    return re.sub(r"-+", "-", re.sub(r"[^a-z0-9]+", "-", value)).strip("-")


def front_matter(path: Path) -> str:
    if not path.is_file():
        return ""
    match = FRONT_MATTER.match(path.read_text(encoding="utf-8-sig"))
    return match.group(1) if match else ""


def is_draft(path: Path, content_root: Path) -> bool:
    candidates = [path]
    current = path.parent
    while current == content_root or content_root in current.parents:
        candidates.extend((current / "index.md", current / "_index.md"))
        if current == content_root:
            break
        current = current.parent
    return any(
        re.search(r"(?mi)^draft\s*:\s*true\s*$", front_matter(candidate))
        for candidate in candidates
    )


def source_name(path: Path, content_root: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.relative_to(content_root).as_posix()


def parse_reference(
    shortcode: str, argument_text: str, source: str, line: int
) -> ArtistReference:
    values = shlex.split(argument_text)
    if not values:
        raise ValueError("shortcode has no arguments")

    if shortcode == "artist-wikilink":
        artist = values[0].strip()
        slug = values[1].strip() if len(values) >= 2 else artist
    else:
        parts = values[0].split("--")
        if len(parts) < 2 or not parts[1].strip():
            raise ValueError("title argument must contain track--artist")
        artist = parts[1].strip()
        slug = parts[3].strip() if len(parts) >= 4 and parts[3].strip() else artist

    if not artist:
        raise ValueError("Artist name is empty")
    return ArtistReference(source, line, shortcode, artist, urlize(slug))


def published_references(content_root: Path) -> tuple[list[ArtistReference], list[str]]:
    references: list[ArtistReference] = []
    errors: list[str] = []
    for path in sorted(content_root.rglob("*.md")):
        if is_draft(path, content_root):
            continue
        text = path.read_text(encoding="utf-8-sig")
        source = source_name(path, content_root)
        for match in SHORTCODE.finditer(text):
            line = text.count("\n", 0, match.start()) + 1
            try:
                references.append(
                    parse_reference(match.group(1), match.group(2), source, line)
                )
            except (ValueError, IndexError) as error:
                errors.append(f"{source}:{line}: {error}")
    return references, errors


def published_artist_paths(artists_root: Path, content_root: Path) -> set[str]:
    paths: set[str] = set()
    for path in artists_root.glob("*/*/index.md"):
        if not is_draft(path, content_root):
            paths.add(path.parent.relative_to(artists_root).as_posix())
    return paths


def load_exceptions(path: Path) -> tuple[dict[tuple[object, ...], str], list[str]]:
    errors: list[str] = []
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        return {}, [f"{path}: cannot load Artist-link exceptions: {error}"]

    items = document.get("exceptions") if isinstance(document, dict) else None
    if not isinstance(items, list):
        return {}, [f"{path}: 'exceptions' must be a list"]

    exceptions: dict[tuple[object, ...], str] = {}
    required = {"source", "line", "shortcode", "artist", "artistSlug", "reason"}
    for index, item in enumerate(items, 1):
        if not isinstance(item, dict) or set(item) != required:
            errors.append(
                f"{path}: exception {index} must contain exactly: "
                + ", ".join(sorted(required))
            )
            continue
        reason = str(item["reason"]).strip()
        key = (
            item["source"], item["line"], item["shortcode"],
            item["artist"], item["artistSlug"],
        )
        if not isinstance(item["line"], int) or item["line"] < 1:
            errors.append(f"{path}: exception {index} has an invalid line number")
        elif item["shortcode"] not in {"artist-wikilink", "title"}:
            errors.append(f"{path}: exception {index} has an invalid shortcode")
        elif len(reason) < 20:
            errors.append(f"{path}: exception {index} needs a specific reason")
        elif key in exceptions:
            errors.append(f"{path}: exception {index} duplicates an earlier entry")
        else:
            exceptions[key] = reason
    return exceptions, errors


def audit(
    content_root: Path, artists_root: Path, exceptions_path: Path
) -> tuple[list[ArtistReference], list[ArtistReference], list[str]]:
    references, errors = published_references(content_root)
    artist_paths = published_artist_paths(artists_root, content_root)
    exceptions, exception_errors = load_exceptions(exceptions_path)
    errors.extend(exception_errors)
    unresolved: list[ArtistReference] = []
    used_exceptions: set[tuple[object, ...]] = set()

    for reference in references:
        first_char = reference.artist[0].lower()
        artist_path = f"{first_char}/{reference.artist_slug}"
        if artist_path in artist_paths:
            continue
        if reference.exception_key in exceptions:
            used_exceptions.add(reference.exception_key)
        else:
            unresolved.append(reference)

    for key in exceptions.keys() - used_exceptions:
        errors.append(
            f"{exceptions_path}: stale or non-matching Artist-link exception: "
            f"{key[0]}:{key[1]} ({key[2]} {key[3]!r} -> {key[4]})"
        )
    return references, unresolved, errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--content", type=Path, default=DEFAULT_CONTENT)
    parser.add_argument("--artists", type=Path, default=DEFAULT_ARTISTS)
    parser.add_argument("--exceptions", type=Path, default=DEFAULT_EXCEPTIONS)
    args = parser.parse_args()

    references, unresolved, errors = audit(
        args.content, args.artists, args.exceptions
    )
    for reference in unresolved:
        errors.append(
            f"{reference.source}:{reference.line}: unresolved {reference.shortcode} "
            f"Artist {reference.artist!r} (derived slug: {reference.artist_slug!r}); "
            "add the canonical slug override, or document a narrow exception"
        )
    for error in errors:
        print(f"ERROR: {error}")
    print(
        f"Audited {len(references)} published Artist shortcode relationships; "
        f"{len(unresolved)} unresolved."
    )
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
