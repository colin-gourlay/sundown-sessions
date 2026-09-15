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


class ArtistBiographyReportTests(unittest.TestCase):
    def report(self, titles):
        findings = [
            audit_module.Finding(f"artists/{index}/index.md", title, ["Needs review"])
            for index, title in enumerate(titles)
        ]
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "report.md"
            audit_module.write_report(findings, output, dt.date(2026, 9, 15), 365)
            return output.read_text(encoding="utf-8")

    def headings(self, report):
        return [line[4:] for line in report.splitlines() if line.startswith("### ")]

    def test_punctuation_matches_lint_configuration_and_preserves_names(self):
        titles = ["The Shock!", "B.E.F.", "R.E.M.", "Who?", "Artist,;:!¿?  ", "!!!"]
        report = self.report(titles)
        self.assertEqual(
            ["The Shock", "B.E.F.", "R.E.M.", "Who", "Artist", "Artist (2)"],
            self.headings(report),
        )
        for title in titles:
            self.assertIn(f"- Artist: {title}\n", report)
        self.assertIn("- File: `artists/0/index.md`\n- Reason: Needs review", report)

    def test_duplicate_and_sanitised_names_have_unique_headings(self):
        report = self.report(["Echo & the Bunnymen", "Echo & the Bunnymen", "Echo & the Bunnymen!"])
        self.assertEqual(
            ["Echo & the Bunnymen", "Echo & the Bunnymen (2)", "Echo & the Bunnymen (3)"],
            self.headings(report),
        )

    def test_generated_suffixes_do_not_collide_with_natural_names(self):
        for titles in (["Artist", "Artist", "Artist (2)"], ["Artist (2)", "Artist", "Artist"]):
            with self.subTest(titles=titles):
                headings = self.headings(self.report(titles))
                self.assertEqual(len(headings), len(set(headings)))
                self.assertEqual({"Artist", "Artist (2)", "Artist (3)"}, set(headings))

    def test_empty_report(self):
        report = self.report([])
        self.assertEqual([], self.headings(report))
        self.assertIn("No artist biography review candidates were found.", report)


if __name__ == "__main__":
    unittest.main()
