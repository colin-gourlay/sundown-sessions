import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "scripts/audit-artist-biographies.py"
SPEC = importlib.util.spec_from_file_location("artist_biography_audit", SCRIPT)
audit_module = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
sys.modules[SPEC.name] = audit_module
SPEC.loader.exec_module(audit_module)


class ArtistBiographyAuditTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

    def write_artist(self, letter: str, slug: str, front_matter: str, body: str = "") -> Path:
        path = self.root / letter / slug / "index.md"
        path.parent.mkdir(parents=True, exist_ok=True)
        content = f"---\n{front_matter}---\n{body}"
        path.write_text(content, encoding="utf-8")
        return path

    # ── audit_file ────────────────────────────────────────────────────────────

    def test_flags_missing_last_reviewed(self):
        self.write_artist("a", "artist-a", "title: Artist A\n", "\n## About\n\nSome bio here.\n")
        import datetime as dt
        today = dt.date(2026, 8, 20)
        findings = [
            audit_module.audit_file(p, today, 365)
            for p in sorted(self.root.glob("*/*/index.md"))
        ]
        findings = [f for f in findings if f]
        self.assertEqual(1, len(findings))
        self.assertTrue(any("lastReviewed" in r for r in findings[0].reasons))

    def test_passes_fresh_last_reviewed(self):
        self.write_artist(
            "a", "artist-b",
            "title: Artist B\nlastReviewed: 2026-08-01\n",
            "\n## About\n\nSome bio here.\n",
        )
        import datetime as dt
        today = dt.date(2026, 8, 20)
        findings = [
            audit_module.audit_file(p, today, 365)
            for p in sorted(self.root.glob("*/*/index.md"))
        ]
        findings = [f for f in findings if f]
        self.assertEqual(0, len(findings))

    # ── write_report heading sanitisation ────────────────────────────────────

    def test_report_strips_trailing_punctuation_from_headings(self):
        """Artist names ending with . ! ? should not appear as-is in headings (MD026)."""
        import datetime as dt
        today = dt.date(2026, 8, 20)
        findings = [
            audit_module.Finding(
                path="src/content/artists/b/b-e-f/index.md",
                title="B.E.F.",
                reasons=["missing `lastReviewed` front matter"],
            ),
        ]
        output = self.root / "report.md"
        audit_module.write_report(findings, output, today, 365)
        text = output.read_text(encoding="utf-8")
        self.assertIn("### B.E.F", text)
        # The heading must NOT end with a period.
        for line in text.splitlines():
            if line.startswith("### "):
                self.assertFalse(
                    line.rstrip().endswith("."),
                    f"Heading has trailing punctuation: {line!r}",
                )

    def test_report_disambiguates_duplicate_artist_names(self):
        """Two artists with the same display name should get unique headings (MD024)."""
        import datetime as dt
        today = dt.date(2026, 8, 20)
        reason = "missing `lastReviewed` front matter"
        findings = [
            audit_module.Finding("src/.../echo-and-the-bunnymen/index.md", "Echo & the Bunnymen", [reason]),
            audit_module.Finding("src/.../echo-the-bunnymen/index.md", "Echo & the Bunnymen", [reason]),
        ]
        output = self.root / "report.md"
        audit_module.write_report(findings, output, today, 365)
        text = output.read_text(encoding="utf-8")
        headings = [line for line in text.splitlines() if line.startswith("### ")]
        self.assertEqual(2, len(headings))
        self.assertEqual(len(set(headings)), 2, "Duplicate headings found in report")
        # First occurrence is unsuffixed; second gets (2).
        self.assertIn("### Echo & the Bunnymen", headings)
        self.assertIn("### Echo & the Bunnymen (2)", headings)

    def test_report_preserves_artist_name_in_body(self):
        """The original artist name (with punctuation) should still appear in the body."""
        import datetime as dt
        today = dt.date(2026, 8, 20)
        findings = [
            audit_module.Finding(
                path="src/content/artists/t/the-shock/index.md",
                title="The Shock!",
                reasons=["missing `lastReviewed` front matter"],
            ),
        ]
        output = self.root / "report.md"
        audit_module.write_report(findings, output, today, 365)
        text = output.read_text(encoding="utf-8")
        self.assertIn("The Shock!", text)


if __name__ == "__main__":
    unittest.main()
