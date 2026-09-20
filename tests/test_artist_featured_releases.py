import html
import re
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
        cls.destination = destination
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
        cls.ist_ist_youre_mine_page = (
            destination / "tracks/i/ist-ist/youre-mine/index.html"
        ).read_text(encoding="utf-8")
        cls.teskey_artist_page = (
            destination / "artists/t/the-teskey-brothers/index.html"
        ).read_text(encoding="utf-8")
        cls.teskey_release_page = (
            destination / "releases/t/the-teskey-brothers/run-home-slow/index.html"
        ).read_text(encoding="utf-8")

    @classmethod
    def tearDownClass(cls):
        if hasattr(cls, "temporary_directory"):
            cls.temporary_directory.cleanup()

    def test_detroit_cobras_month_precision_release_date(self):
        artist = (
            self.destination / "artists/t/the-detroit-cobras/index.html"
        ).read_text()
        section = re.search(
            r'<section[^>]*aria-labelledby="artist-featured-releases-heading"[\s\S]*?</section>', artist
        ).group()
        self.assertIn("April 2001", section)
        self.assertNotIn("2001-04", section)
        self.assertNotIn("1 April 2001", section)

    def test_detroit_cobras_canonical_tracks_and_complete_published_history(self):
        artist_path = "/artists/t/the-detroit-cobras/"
        artist = (self.destination / artist_path.lstrip("/") / "index.html").read_text()
        section = re.search(
            r'<section class="artist-featured-tracks\b[\s\S]*?</section>', artist
        ).group()
        rows = re.findall(
            r'<article class="artist-featured-track">[\s\S]*?</article>', section
        )
        self.assertEqual(len(rows), 2)
        cases = (
            ("Cry On", "cry-on", "life-love-and-leaving",
             1, "featuring-the-big-now", "2024-06-05", "5 June 2024"),
            ("Shout Bama Lama", "shout-bama-lama", "life-love-and-leaving",
             4, "featuring-kenny-armour-from-andysmanclub", "2024-06-26", "26 June 2024"),
        )
        for title, slug, release, number, show, date, display_date in cases:
            with self.subTest(track=title):
                track_path = f"/tracks/t/the-detroit-cobras/{slug}/"
                release_path = f"/releases/t/the-detroit-cobras/{release}/"
                show_path = f"/shows/{show}/"
                row = next(row for row in rows if f'href="{track_path}"' in row)
                self.assertIn(title, html.unescape(row))
                self.assertIn(f'href="{show_path}"', row)
                self.assertIn(f"Sundown Sessions #{number}", row)
                self.assertIn(f'datetime="{date}"', row)
                self.assertIn(display_date, row)
                self.assertIn("View Broadcast", row)
                self.assertIn(f'href="{release_path}"', artist)

                track = (self.destination / track_path.lstrip("/") / "index.html").read_text()
                for href in (artist_path, release_path):
                    self.assertIn(f'href="{href}"', track)
                history = re.search(
                    r'<section[^>]*aria-labelledby="track-featured-shows-heading"[\s\S]*?</section>',
                    track,
                ).group()
                self.assertEqual(history.count('class="release-featured-shows__link"'), 1)
                self.assertIn(f'href="{show_path}"', history)
                self.assertIn(f"Sundown Sessions #{number}", history)
                self.assertIn(f'datetime="{date}"', history)
                self.assertIn(display_date, history)

    def test_detroit_cobras_track_content_has_no_duplicate_identities(self):
        matches = {"Cry On": [], "Shout Bama Lama": []}
        for path in (ROOT / "src/content/tracks").rglob("*.md"):
            content = path.read_text(encoding="utf-8")
            if not content.startswith("---\n"):
                continue
            frontmatter = content.split("---", 2)[1]
            fields = {}
            for key in ("title", "artist"):
                match = re.search(rf"^{key}:\s*(.+)$", frontmatter, re.MULTILINE)
                if match:
                    fields[key] = match.group(1).strip().strip("\"'")
            title = fields.get("title")
            if fields.get("artist") == "The Detroit Cobras" and title in matches:
                matches[title].append(path.relative_to(ROOT / "src/content/tracks").as_posix())
        self.assertEqual(matches, {
            "Cry On": ["t/the-detroit-cobras/cry-on/index.md"],
            "Shout Bama Lama": ["t/the-detroit-cobras/shout-bama-lama/index.md"],
        })

    def test_detroit_cobras_artist_statistics_and_release_relationship(self):
        artist = (
            self.destination / "artists/t/the-detroit-cobras/index.html"
        ).read_text()
        for label, value in (
            ("Featured on Sundown Sessions", "2 broadcasts"),
            ("First featured", "5 June 2024"),
            ("Tracks played", "2"),
        ):
            self.assertRegex(artist, rf"<dt>{label}</dt>\s*<dd>{value}</dd>")
        last_featured = re.search(
            r"<dt>Last featured</dt>\s*<dd>([\s\S]*?)</dd>", artist
        ).group(1)
        self.assertIn("26 June 2024", last_featured)
        self.assertIn(
            'href="/shows/featuring-kenny-armour-from-andysmanclub/"', last_featured
        )
        releases = re.search(
            r'<section[^>]*aria-labelledby="artist-featured-releases-heading"[\s\S]*?</section>',
            artist,
        ).group()
        self.assertEqual(releases.count('class="release-discover-card"'), 1)
        self.assertIn(
            'href="/releases/t/the-detroit-cobras/life-love-and-leaving/"', releases
        )
        release = (
            self.destination / "releases/t/the-detroit-cobras/life-love-and-leaving/index.html"
        ).read_text()
        for slug in ("cry-on", "shout-bama-lama"):
            self.assertIn(f'href="/tracks/t/the-detroit-cobras/{slug}/"', release)

    def test_elo_canonical_tracks_and_complete_published_history(self):
        artist_path = "/artists/e/electric-light-orchestra/"
        artist = (self.destination / artist_path.lstrip("/") / "index.html").read_text()
        section = re.search(
            r'<section class="artist-featured-tracks\b[\s\S]*?</section>', artist
        ).group()
        rows = re.findall(
            r'<article class="artist-featured-track">[\s\S]*?</article>', section
        )
        self.assertEqual(len(rows), 4)
        cases = (
            ("Four Little Diamonds", "four-little-diamonds", "secret-messages",
             1, "featuring-the-big-now", "2024-06-05", "5 June 2024"),
            ("Here Is The News", "here-is-the-news", "time",
             2, "featuring-the-receiving-end", "2024-06-12", "12 June 2024"),
            ("Don't Bring Me Down", "dont-bring-me-down", "discovery",
             9, "featuring-colin-gourlay-from-andysmanclub", "2024-08-14", "14 August 2024"),
            ("Mr. Blue Sky", "mr.-blue-sky", "out-of-the-blue",
             11, "featuring-a-celebration-of-elektra-records", "2024-08-28", "28 August 2024"),
        )
        for title, slug, release, number, show, date, display_date in cases:
            with self.subTest(track=title):
                track_path = f"/tracks/e/electric-light-orchestra/{slug}/"
                release_path = f"/releases/e/electric-light-orchestra/{release}/"
                show_path = f"/shows/{show}/"
                row = next(row for row in rows if f'href="{track_path}"' in row)
                self.assertIn(title, html.unescape(row))
                self.assertIn(f'href="{show_path}"', row)
                self.assertIn(f"Sundown Sessions #{number}", row)
                self.assertIn(f'datetime="{date}"', row)
                self.assertIn(display_date, row)
                self.assertIn("View Broadcast", row)
                self.assertIn(f'href="{release_path}"', artist)

                track = (self.destination / track_path.lstrip("/") / "index.html").read_text()
                for href in (artist_path, release_path):
                    self.assertIn(f'href="{href}"', track)
                history = re.search(
                    r'<section[^>]*aria-labelledby="track-featured-shows-heading"[\s\S]*?</section>',
                    track,
                ).group()
                self.assertEqual(history.count('class="release-featured-shows__link"'), 1)
                self.assertIn(f'href="{show_path}"', history)
                self.assertIn(f"Sundown Sessions #{number}", history)
                self.assertIn(f'datetime="{date}"', history)
                self.assertIn(display_date, history)

    def test_elo_artist_statistics_and_broadcast_navigation(self):
        artist = (
            self.destination / "artists/e/electric-light-orchestra/index.html"
        ).read_text()
        for label, value in (
            ("Featured on Sundown Sessions", "4 broadcasts"),
            ("First featured", "5 June 2024"),
            ("Tracks played", "4"),
        ):
            self.assertRegex(artist, rf"<dt>{label}</dt>\s*<dd>{value}</dd>")
        last_featured = re.search(
            r"<dt>Last featured</dt>\s*<dd>([\s\S]*?)</dd>", artist
        ).group(1)
        self.assertIn("28 August 2024", last_featured)
        self.assertIn(
            'href="/shows/featuring-a-celebration-of-elektra-records/"', last_featured
        )
        self.assertRegex(
            artist,
            r'<a href="#artist-featured-shows-heading" class="artist-header-cta">'
            r"\s*Explore Featured Broadcasts",
        )
        self.assertIn('id="artist-featured-shows-heading"', artist)

        releases = re.search(
            r'<section[^>]*aria-labelledby="artist-featured-releases-heading"[\s\S]*?</section>',
            artist,
        ).group()
        cards = re.findall(
            r'<article class="release-discover-card">[\s\S]*?</article>', releases
        )
        self.assertEqual(len(cards), 4)
        for slug, date in (
            ("secret-messages", "24 June 1983"),
            ("time", "1981"),
            ("discovery", "21 May 1979"),
            ("out-of-the-blue", "28 October 1977"),
        ):
            with self.subTest(release=slug):
                card = next(card for card in cards if f"/{slug}/" in card)
                self.assertIn(f'class="release-discover-card__meta">{date}</span>', card)
                self.assertRegex(card, r'<img[^>]+alt="[^"]+ artwork"')

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
        normalised_artist_page = self.ist_ist_artist_page.replace("&#39;", "'").replace(
            "’", "'"
        )
        for track in ("Black", "Fat Cats Drown in Milk", "You're Mine"):
            with self.subTest(track=track):
                self.assertIn(track, normalised_artist_page)

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
        self.assertRegex(
            self.ist_ist_artist_page,
            r"<dt>\s*First featured\s*</dt>\s*<dd>\s*0?5\s+June\s+2024\s*</dd>",
        )
        last_featured_pattern = (
            r"<dt>\s*Last featured\s*</dt>\s*<dd>[\s\S]*"
            r'href="/shows/featuring-baby-bartok/"[\s\S]*'
            r"0?7\s+August\s+2024[\s\S]*</dd>"
        )
        self.assertRegex(
            self.ist_ist_artist_page,
            last_featured_pattern,
        )
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

    def test_ist_ist_show_two_track_has_canonical_relationships(self):
        for page in (self.ist_ist_artist_page, self.ist_ist_architecture_page):
            self.assertIn('href="/tracks/i/ist-ist/youre-mine/"', page)
        for href in (
            "/artists/i/ist-ist/",
            "/releases/i/ist-ist/architecture/",
            "/shows/featuring-the-receiving-end/",
            "https://ististmusic.bandcamp.com/track/youre-mine-5",
        ):
            self.assertIn(f'href="{href}"', self.ist_ist_youre_mine_page)
        self.assertIn('datetime="2024-06-12"', self.ist_ist_youre_mine_page)
        self.assertIn("2:41", self.ist_ist_youre_mine_page)
        self.assertIn('href="/tracks/i/ist-ist/black/"', self.ist_ist_artist_page)

    def test_teskey_brothers_release_and_track_history_follow_show_one(self):
        self.assertIn(
            'href="/releases/t/the-teskey-brothers/run-home-slow/"',
            self.teskey_artist_page,
            "Expected Run Home Slow to appear through the published Show 1 play.",
        )
        self.assertIn(
            '<span class="artist-featured-track__title">Rain</span>',
            self.teskey_artist_page,
        )
        self.assertIn(
            '<span class="artist-featured-track__release">from Run Home Slow (2019)</span>',
            self.teskey_artist_page,
        )
        self.assertIn(
            'href="/shows/featuring-the-big-now/"',
            self.teskey_artist_page,
        )
        self.assertIn(
            '<time datetime="2024-06-05">5 June 2024</time>',
            self.teskey_artist_page,
        )
        self.assertIn(
            'href="/shows/featuring-the-big-now/"',
            self.teskey_release_page,
            "Expected the Run Home Slow release page to link to Show 1.",
        )
        self.assertNotIn(
            'href="/46"',
            self.teskey_release_page,
            "Run Home Slow must not render an unsupported Show 46 relationship.",
        )


if __name__ == "__main__":
    unittest.main()
