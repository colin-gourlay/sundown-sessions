import unittest
from pathlib import Path


ROOT = Path(__file__).parents[1]
ARTIST_PAGE = ROOT / "src/content/artists/b/blue-on-shock/index.md"
SHOW_THREE_TRACK_INFO = ROOT / "src/content/shows/3/track-info.md"


class BlueOnShockArtistPageTests(unittest.TestCase):
    def test_biography_separates_documented_recordings_from_rehearsal_cassette(self):
        content = ARTIST_PAGE.read_text(encoding="utf-8")
        self.assertIn("Fife and Kirkcaldy music scene", content)
        self.assertIn("local music-history archive Kirkcaldy Bands", content)
        self.assertIn("Abbotshall Hotel photograph from 1983", content)
        self.assertIn("documented 1989 7-inch recording", content)
        self.assertIn("Sound Café Studios around 1991", content)
        self.assertIn('"It Ain\'t Easy" released through that archival project', content)
        self.assertIn("private chrome rehearsal cassette", content)
        self.assertIn(
            "That cassette source is kept separate from the documented 1989 7-inch recording "
            "and The Lost Café Sessions material",
            content,
        )

    def test_explore_further_uses_verified_archival_project_link(self):
        content = ARTIST_PAGE.read_text(encoding="utf-8")
        self.assertIn("## Explore Further", content)
        self.assertIn(
            '{{< new-tab-link "Archive: '
            '[The Lost Café Sessions](https://www.facebook.com/TheLostCafeSessions)" >}}',
            content,
        )
        self.assertNotIn("TODO", content)
        self.assertNotIn("None found", content)

    def test_show_three_keeps_private_cassette_track_relationships(self):
        content = SHOW_THREE_TRACK_INFO.read_text(encoding="utf-8")
        for title in (
            "Love Is A Venture--Blue On Shock",
            "Brand New Chevy--Blue On Shock",
            "The Fighting's Never Won--Blue On Shock",
        ):
            with self.subTest(title=title):
                self.assertIn(title, content)
        self.assertEqual(3, content.count("Private Chrome Cassette (for rehearsals)"))


if __name__ == "__main__":
    unittest.main()
