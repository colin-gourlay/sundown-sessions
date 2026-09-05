import json
import shutil
import subprocess
import tempfile
import unittest
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CACHE_DIRECTORY = ROOT / ".cache"


class SearchIndexTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if shutil.which("hugo") is None:
            raise unittest.SkipTest("Hugo is not installed")

        CACHE_DIRECTORY.mkdir(exist_ok=True)
        cls.temporary_directory = tempfile.TemporaryDirectory(
            prefix="search-index-test-", dir=CACHE_DIRECTORY
        )
        destination = Path(cls.temporary_directory.name)
        subprocess.run(
            [
                "hugo",
                "--source",
                str(ROOT / "src"),
                "--destination",
                str(destination),
                "--environment",
                "production",
                "--quiet",
            ],
            check=True,
            text=True,
        )
        cls.entries = json.loads(
            (destination / "index.json").read_text(encoding="utf-8")
        )

    @classmethod
    def tearDownClass(cls):
        if hasattr(cls, "temporary_directory"):
            cls.temporary_directory.cleanup()

    def test_only_intentional_content_kinds_are_indexed(self):
        indexed_types = Counter(entry["type"] for entry in self.entries)

        self.assertEqual(
            set(indexed_types), {"artists", "releases", "tracks", "shows", "genres"}
        )
        self.assertGreater(indexed_types["artists"], 0)
        self.assertGreater(indexed_types["releases"], 0)
        self.assertGreater(indexed_types["tracks"], 0)
        self.assertGreater(indexed_types["shows"], 0)
        self.assertGreater(indexed_types["genres"], 0)

    def test_every_entry_has_a_meaningful_identity(self):
        self.assertTrue(self.entries)
        self.assertTrue(all(entry["title"].strip() for entry in self.entries))

        permalinks = {entry["permalink"] for entry in self.entries}
        self.assertNotIn("/artists/a/alphaville/", permalinks)
        self.assertNotIn("/artists/b/bruce-springsteen/", permalinks)
        self.assertNotIn("/artists/t/the-korgis/", permalinks)
        self.assertNotIn("/artists/t/the-prime-movers/", permalinks)

    def test_taxonomies_and_utility_pages_are_deliberately_scoped(self):
        permalinks = {entry["permalink"] for entry in self.entries}

        self.assertIn("/genres/post-punk/", permalinks)
        self.assertNotIn("/genres/", permalinks)
        self.assertNotIn("/tags/queens-of-the-stone-age/", permalinks)
        self.assertNotIn("/tags/big-country/", permalinks)
        self.assertTrue(
            permalinks.isdisjoint(
                {
                    "/",
                    "/about/",
                    "/categories/",
                    "/contact/",
                    "/corrections/",
                    "/labels/",
                    "/listen-live/",
                    "/producers/",
                    "/release-types/",
                    "/search/",
                    "/tags/",
                    "/upcoming/",
                    "/years/",
                }
            )
        )

    def test_known_good_search_destinations_remain_indexed(self):
        titles = {entry["title"] for entry in self.entries}

        for title in (
            "Queens of the Stone Age",
            "Big Country",
            "Feet Don't Fail Me",
            "Take Me Out",
            "Cabinet of Curiosities",
            "Villains",
        ):
            with self.subTest(title=title):
                self.assertIn(title, titles)

        self.assertTrue(any(entry["type"] == "shows" for entry in self.entries))

    def test_draft_content_remains_excluded_from_production(self):
        permalinks = {entry["permalink"] for entry in self.entries}

        self.assertNotIn("/shows/13/", permalinks)


if __name__ == "__main__":
    unittest.main()
