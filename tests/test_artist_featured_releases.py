import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CACHE_DIRECTORY = ROOT / ".cache"


class ArtistFeaturedReleasesTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if shutil.which("hugo") is None:
            raise unittest.SkipTest("Hugo is not installed")

        CACHE_DIRECTORY.mkdir(exist_ok=True)
        cls.temporary_directory = tempfile.TemporaryDirectory(
            prefix="artist-featured-releases-test-", dir=CACHE_DIRECTORY
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
        cls.artist_page = (
            destination / "artists/e/echo-and-the-bunnymen/index.html"
        ).read_text(encoding="utf-8")

    @classmethod
    def tearDownClass(cls):
        if hasattr(cls, "temporary_directory"):
            cls.temporary_directory.cleanup()

    def test_echo_and_the_bunnymen_only_lists_supported_featured_releases(self):
        self.assertIn("Releases Featured on Sundown Sessions", self.artist_page)
        self.assertIn(
            'href="/releases/e/echo-the-bunnymen/the-best-of-echo-the-bunnymen/"',
            self.artist_page,
        )
        self.assertNotIn(
            'href="/releases/e/echo-the-bunnymen/porcupine/"',
            self.artist_page,
        )


if __name__ == "__main__":
    unittest.main()
