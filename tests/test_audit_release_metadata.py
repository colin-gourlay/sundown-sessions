import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "scripts/audit-release-metadata.py"
SPEC = importlib.util.spec_from_file_location("release_metadata_audit", SCRIPT)
audit_module = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
sys.modules[SPEC.name] = audit_module
SPEC.loader.exec_module(audit_module)


class ReleaseMetadataAuditTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

    def write_release(self, name: str, metadata: str) -> None:
        path = self.root / name / "index.md"
        path.parent.mkdir(parents=True)
        path.write_text(f"---\n{metadata}---\n", encoding="utf-8")

    def test_accepts_canonical_taxonomy_metadata(self):
        self.write_release(
            "valid",
            "title: Valid\nlabels: [Label]\nproducers: [Producer]\n"
            "genres: [Rock]\ntags: [rock]\n",
        )
        releases, errors = audit_module.audit(self.root)
        self.assertEqual([], errors)
        self.assertEqual(
            {"labels": "linkable", "producers": "linkable",
             "genres": "linkable", "tags": "linkable"},
            releases[0]["statuses"],
        )

    def test_distinguishes_legacy_empty_missing_and_unavailable(self):
        self.write_release(
            "review",
            "title: Review\nlabel: Legacy Label\nproducers: []\n"
            "releaseMetadataUnavailable: [tags]\n",
        )
        releases, _ = audit_module.audit(self.root)
        self.assertEqual(
            {"labels": "legacy", "producers": "empty",
             "genres": "missing", "tags": "unavailable"},
            releases[0]["statuses"],
        )

    def test_rejects_unknown_unavailable_field(self):
        self.write_release(
            "invalid",
            "title: Invalid\nreleaseMetadataUnavailable: [moods]\n",
        )
        _, errors = audit_module.audit(self.root)
        self.assertEqual(1, len(errors))
        self.assertIn("unknown", errors[0])


if __name__ == "__main__":
    unittest.main()
