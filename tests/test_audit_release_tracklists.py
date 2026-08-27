import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "scripts/audit-release-tracklists.py"
SPEC = importlib.util.spec_from_file_location("release_audit", SCRIPT)
audit_module = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
sys.modules[SPEC.name] = audit_module
SPEC.loader.exec_module(audit_module)


class ReleaseTracklistAuditTests(unittest.TestCase):
    def write_release(self, content: str) -> Path:
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        path = Path(temporary.name) / "index.md"
        path.write_text(content, encoding="utf-8")
        return path

    def test_accepts_contiguous_multi_disc_tracklists(self):
        path = self.write_release(
            """---
title: Example
duration: "0:03:00"
tracks:
  - {discNumber: 1, trackNumber: 1, title: One, duration: "1:00"}
  - {discNumber: 2, trackNumber: 1, title: Two, duration: "2:00"}
---
"""
        )
        result = audit_module.audit_release(path)
        self.assertEqual("structured", result.representation)
        self.assertEqual([], result.errors)

    def test_reports_number_duration_title_and_total_errors(self):
        path = self.write_release(
            """---
title: Broken
duration: "1:00"
tracks:
  - {trackNumber: 2, title: Repeat, duration: "0:30"}
  - {trackNumber: 2, title: repeat, duration: "3:99"}
---
"""
        )
        errors = audit_module.audit_release(path).errors
        self.assertTrue(any("expected" in error for error in errors))
        self.assertTrue(any("duplicate track numbers" in error for error in errors))
        self.assertTrue(any("duplicate track titles" in error for error in errors))
        self.assertTrue(any("invalid duration" in error for error in errors))

    def test_reports_missing_tracklist(self):
        path = self.write_release("---\ntitle: Empty\n---\n")
        result = audit_module.audit_release(path)
        self.assertEqual("missing", result.representation)
        self.assertEqual(["no tracklist is recorded"], result.errors)


if __name__ == "__main__":
    unittest.main()
