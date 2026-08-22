import datetime as dt
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


class ArtistBiographyAuditReportTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.output = Path(temporary.name) / "artist-biography-audit.md"

    def headings_for(self, findings):
        audit_module.write_report(findings, self.output, dt.date(2026, 8, 14), 365)
        return [
            line
            for line in self.output.read_text(encoding="utf-8").splitlines()
            if line.startswith("### ")
        ]

    def finding(self, slug, title):
        return audit_module.Finding(
            f"src/content/artists/a/{slug}/index.md",
            title,
            ["test reason"],
        )

    def test_duplicate_titles_get_folder_slug_suffixes_case_insensitively(self):
        findings = [
            self.finding("first-version", "Duplicate Artist"),
            self.finding("second-version", "duplicate artist"),
        ]

        self.assertEqual(
            [
                "### Duplicate Artist — first-version",
                "### duplicate artist — second-version",
            ],
            self.headings_for(findings),
        )

    def test_title_ending_in_md026_punctuation_gets_folder_slug_suffix(self):
        findings = [self.finding("initials", "B.E.F.")]

        self.assertEqual(["### B.E.F. — initials"], self.headings_for(findings))

    def test_normal_title_is_unchanged(self):
        findings = [self.finding("normal-artist", "Normal Artist")]

        self.assertEqual(["### Normal Artist"], self.headings_for(findings))


if __name__ == "__main__":
    unittest.main()
