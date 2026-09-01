import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "scripts/audit-artist-links.py"
ROOT = Path(__file__).parents[1]
SPEC = importlib.util.spec_from_file_location("artist_link_audit", SCRIPT)
audit_module = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
sys.modules[SPEC.name] = audit_module
SPEC.loader.exec_module(audit_module)


class ArtistLinkAuditTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.content = self.root / "content"
        self.artists = self.content / "artists"
        self.exceptions = self.root / "artist-link-exceptions.json"
        self.write_exceptions([])

    def write(self, relative: str, text: str) -> Path:
        path = self.content / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def write_artist(self, slug: str, draft: bool = False) -> None:
        first_char = slug[0]
        self.write(
            f"artists/{first_char}/{slug}/index.md",
            f"---\ntitle: Artist\ndraft: {str(draft).lower()}\n---\n",
        )

    def write_exceptions(self, exceptions: list[dict]) -> None:
        self.exceptions.write_text(
            json.dumps({"documentation": "test", "exceptions": exceptions}),
            encoding="utf-8",
        )

    def audit(self):
        return audit_module.audit(self.content, self.artists, self.exceptions)

    def test_accepts_generated_and_explicit_canonical_slugs(self):
        self.write_artist("wire")
        self.write_artist("echo-and-the-bunnymen")
        self.write(
            "shows/1/playlist.md",
            '{{< artist-wikilink "Wire" >}}\n'
            '{{< artist-wikilink "Echo & the Bunnymen" '
            '"echo-and-the-bunnymen" >}}\n',
        )
        references, unresolved, errors = self.audit()
        self.assertEqual(2, len(references))
        self.assertEqual([], unresolved)
        self.assertEqual([], errors)

    def test_title_artist_override_resolves_canonically(self):
        self.write_artist("elvis-costello-and-the-attractions")
        self.write(
            "shows/3/track-info.md",
            '{{<title "Pump It Up--Elvis Costello & The Attractions----'
            'elvis-costello-and-the-attractions">}}\n',
        )
        _, unresolved, errors = self.audit()
        self.assertEqual([], unresolved)
        self.assertEqual([], errors)

    def test_reports_unresolved_published_reference(self):
        self.write(
            "shows/1/playlist.md",
            '{{< artist-wikilink "Missing Artist" >}}\n',
        )
        _, unresolved, errors = self.audit()
        self.assertEqual(["Missing Artist"], [item.artist for item in unresolved])
        self.assertEqual([], errors)

    def test_skips_references_in_draft_page_bundle(self):
        self.write("shows/1/index.md", "---\ndraft: true\n---\n")
        self.write(
            "shows/1/playlist.md",
            '{{< artist-wikilink "Missing Artist" >}}\n',
        )
        references, unresolved, errors = self.audit()
        self.assertEqual([], references)
        self.assertEqual([], unresolved)
        self.assertEqual([], errors)

    def test_narrow_exception_does_not_suppress_another_reference(self):
        first = self.write(
            "shows/1/playlist.md",
            '{{< artist-wikilink "Missing Artist" >}}\n',
        )
        second = self.write(
            "shows/2/playlist.md",
            '{{< artist-wikilink "Missing Artist" >}}\n',
        )
        self.write_exceptions(
            [
                {
                    "source": first.relative_to(self.content).as_posix(),
                    "line": 1,
                    "shortcode": "artist-wikilink",
                    "artist": "Missing Artist",
                    "artistSlug": "missing-artist",
                    "reason": "Confirmed intentional non-Artist credit pending review.",
                }
            ]
        )
        _, unresolved, errors = self.audit()
        self.assertEqual(1, len(unresolved))
        self.assertEqual(second.relative_to(self.content).as_posix(), unresolved[0].source)
        self.assertEqual([], errors)

    def test_rejects_stale_exception(self):
        self.write_exceptions(
            [
                {
                    "source": "shows/1/playlist.md",
                    "line": 1,
                    "shortcode": "artist-wikilink",
                    "artist": "Missing Artist",
                    "artistSlug": "missing-artist",
                    "reason": "Confirmed intentional non-Artist credit pending review.",
                }
            ]
        )
        _, _, errors = self.audit()
        self.assertTrue(any("stale" in error for error in errors))

    def test_unresolved_shortcodes_render_text_instead_of_fabricated_links(self):
        for name in ("artist-wikilink.html", "title.html"):
            template = (ROOT / "src/layouts/shortcodes" / name).read_text(
                encoding="utf-8"
            )
            with self.subTest(shortcode=name):
                self.assertIn('class="artist-link--unresolved"', template)
                self.assertNotIn("$artistPath | absURL", template)


if __name__ == "__main__":
    unittest.main()
