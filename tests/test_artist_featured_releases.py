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
        cls.echo_artist_page = (
            destination / "artists/e/echo-and-the-bunnymen/index.html"
        ).read_text(encoding="utf-8")
        cls.ist_ist_artist_page = (
            destination / "artists/i/ist-ist/index.html"
        ).read_text(encoding="utf-8")
        cls.ist_ist_architecture_page = (
            destination / "releases/i/ist-ist/architecture/index.html"
        ).read_text(encoding="utf-8")

    @classmethod
    def tearDownClass(cls):
        if hasattr(cls, "temporary_directory"):
            cls.temporary_directory.cleanup()

    def test_echo_and_the_bunnymen_only_lists_supported_featured_releases(self):
        self.assertIn(
            "Releases Featured on Sundown Sessions",
            self.echo_artist_page,
            "Expected the artist featured-release heading to remain unchanged.",
        )
        self.assertIn(
            'href="/releases/e/echo-the-bunnymen/the-best-of-echo-the-bunnymen/"',
            self.echo_artist_page,
            "Expected the supported Show 3 release relationship to remain visible.",
        )
        self.assertNotIn(
            'href="/releases/e/echo-the-bunnymen/porcupine/"',
            self.echo_artist_page,
            "Porcupine must not appear without a supported show relationship.",
        )

    def test_ist_ist_only_lists_releases_connected_to_published_shows(self):
        self.assertIn(
            'href="/releases/i/ist-ist/architecture/"',
            self.ist_ist_artist_page,
            "Expected Architecture to remain visible through published show plays.",
        )
        self.assertIn(
            'href="/releases/i/ist-ist/the-art-of-lying/"',
            self.ist_ist_artist_page,
            "Expected The Art of Lying to remain visible through Show 1.",
        )
        self.assertNotIn(
            'href="/releases/i/ist-ist/lost-my-shadow/"',
            self.ist_ist_artist_page,
            "Lost My Shadow must not appear while it is only tied to draft Show 20.",
        )

    def test_ist_ist_artist_history_matches_published_track_guides(self):
        for track in ("Black", "Fat Cats Drown in Milk", "You're Mine"):
            with self.subTest(track=track):
                self.assertIn(track, self.ist_ist_artist_page)

        for href in (
            'href="/shows/featuring-the-big-now/"',
            'href="/shows/featuring-the-receiving-end/"',
            'href="/shows/featuring-baby-bartok/"',
        ):
            with self.subTest(href=href):
                self.assertIn(href, self.ist_ist_artist_page)

        self.assertRegex(
            self.ist_ist_artist_page,
            r"<dt>\s*Featured on Sundown Sessions\s*</dt>\s*<dd>3 broadcasts</dd>",
        )
        self.assertRegex(
            self.ist_ist_artist_page,
            r"<dt>\s*Tracks played\s*</dt>\s*<dd>3</dd>",
        )
        self.assertIn("5 June 2024", self.ist_ist_artist_page)
        self.assertIn("7 August 2024", self.ist_ist_artist_page)
        self.assertNotIn("Lost My Shadow", self.ist_ist_artist_page)
        self.assertNotIn('href="/shows/featuring-the-twist/"', self.ist_ist_artist_page)
        self.assertNotIn('href="/shows/featuring-tbd/"', self.ist_ist_artist_page)

    def test_ist_ist_release_show_links_follow_published_track_guides(self):
        self.assertIn(
            'href="/shows/featuring-the-receiving-end/"',
            self.ist_ist_architecture_page,
        )
        self.assertIn(
            'href="/shows/featuring-baby-bartok/"',
            self.ist_ist_architecture_page,
        )
        self.assertNotIn(
            'href="/shows/featuring-the-twist/"',
            self.ist_ist_architecture_page,
        )


if __name__ == "__main__":
    unittest.main()
