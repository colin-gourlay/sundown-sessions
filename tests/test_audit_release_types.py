import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "scripts/audit-release-types.py"
SPEC = importlib.util.spec_from_file_location("release_type_audit", SCRIPT)
audit_module = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
sys.modules[SPEC.name] = audit_module
SPEC.loader.exec_module(audit_module)


class ReleaseTypeAuditTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.types = self.root / "types.yaml"
        self.types.write_text("- Album\n- EP\n", encoding="utf-8")

    def write_release(self, name: str, metadata: str) -> None:
        path = self.root / name / "index.md"
        path.parent.mkdir(parents=True)
        path.write_text(f"---\n{metadata}---\n", encoding="utf-8")

    def test_accepts_canonical_type(self):
        self.write_release("valid", "title: Valid\nreleaseType: Album\n")
        releases, errors = audit_module.audit(self.root, self.types)
        self.assertEqual("valid", releases[0]["status"])
        self.assertEqual([], errors)

    def test_accepts_explicit_manual_review(self):
        self.write_release("review", "title: Review\nreleaseTypeReview: true\n")
        releases, errors = audit_module.audit(self.root, self.types)
        self.assertEqual("review", releases[0]["status"])
        self.assertEqual([], errors)

    def test_rejects_missing_invalid_and_legacy_types(self):
        self.write_release("missing", "title: Missing\n")
        self.write_release("invalid", "title: Invalid\nreleaseType: Record\n")
        self.write_release("legacy", "title: Legacy\nrelease_type: EP\n")
        _, errors = audit_module.audit(self.root, self.types)
        self.assertEqual(3, len(errors))
        self.assertTrue(any("missing or empty" in error for error in errors))
        self.assertTrue(any("unrecognised" in error for error in errors))
        self.assertTrue(any("canonical" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
