import importlib.util
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).parents[1]
TRACKLIST_SCRIPT = ROOT / "scripts/audit-release-tracklists.py"
SPEC = importlib.util.spec_from_file_location("release_tracklist_audit_jacco", TRACKLIST_SCRIPT)
audit_module = importlib.util.module_from_spec(SPEC)
assert SPEC.loader
sys.modules[SPEC.name] = audit_module
SPEC.loader.exec_module(audit_module)

RELEASE_PATH = ROOT / "src/content/releases/j/jacco-gardner/cabinet-of-curiosities/index.md"
SHOW_TRACK_GUIDES = [
    ROOT / "src/content/shows/1/track-info.md",
    ROOT / "src/content/shows/10/track-info.md",
    ROOT / "src/content/shows/13/track-info.md",
    ROOT / "src/content/shows/18/ti.txt",
]
RELEASE_SHORTCODE = '{{<release "Cabinet of Curiosities (2013)--Jacco Gardner--cabinet-of-curiosities">}}'


class JaccoGardnerCabinetOfCuriositiesTests(unittest.TestCase):
    def test_release_tracklist_is_single_disc_and_valid(self):
        audit = audit_module.audit_release(RELEASE_PATH)
        self.assertEqual("structured", audit.representation)
        self.assertEqual(12, audit.track_count)
        self.assertEqual([], audit.errors)

        data, _ = audit_module.split_front_matter(RELEASE_PATH.read_text(encoding="utf-8"))
        self.assertEqual("41:38", data["duration"])
        self.assertEqual([1], sorted({track.get("discNumber", 1) for track in data["tracks"]}))

    def test_all_known_track_guide_references_use_internal_release_shortcode(self):
        for path in SHOW_TRACK_GUIDES:
            with self.subTest(path=path):
                content = path.read_text(encoding="utf-8")
                self.assertIn(RELEASE_SHORTCODE, content)
                self.assertNotIn("| Cabinet of Curiosities (2013) |", content)


if __name__ == "__main__":
    unittest.main()
