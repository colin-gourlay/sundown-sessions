import unittest
from pathlib import Path


ROOT = Path(__file__).parents[1]
ARTIST_PAGE = ROOT / "src/content/artists/s/sparks/index.md"
SHOW_ONE_PLAYLIST = ROOT / "src/content/shows/1/playlist.md"
SHOW_ONE_TRACK_INFO = ROOT / "src/content/shows/1/track-info.md"
SHOW_THREE_PLAYLIST = ROOT / "src/content/shows/3/playlist.md"
SHOW_THREE_TRACK_INFO = ROOT / "src/content/shows/3/track-info.md"


class SparksArtistPageTests(unittest.TestCase):
    def assert_track_guide_row(self, content, title_shortcode, release_shortcode):
        self.assertTrue(
            any(
                title_shortcode in line and release_shortcode in line
                for line in content.splitlines()
            ),
            (
                f"Expected {title_shortcode!r} to reference "
                f"{release_shortcode!r} in the same track-guide row."
            ),
        )

    def test_explore_further_prioritises_official_destination(self):
        content = ARTIST_PAGE.read_text(encoding="utf-8")
        self.assertIn("## Explore Further", content)

        official = (
            '{{< new-tab-link "Website: '
            '[Official Sparks website](https://allsparks.com/)" >}}'
        )
        social_links = (
            '{{< new-tab-link "Facebook: '
            '[Sparks on Facebook](https://www.facebook.com/sparksofficial)" >}}',
            '{{< new-tab-link "Instagram: '
            '[Sparks on Instagram](https://www.instagram.com/sparks_official/)" >}}',
            '{{< new-tab-link "X: '
            '[Sparks on X](https://twitter.com/sparksofficial)" >}}',
        )

        self.assertIn(official, content)
        for link in social_links:
            with self.subTest(link=link):
                self.assertIn(link, content)
                self.assertLess(content.index(official), content.index(link))

        self.assertNotIn(
            '{{< new-tab-link "[Facebook](https://www.facebook.com/sparksofficial)" >}}',
            content,
        )

    def test_show_one_and_three_sparks_track_relationships(self):
        show_one_playlist = SHOW_ONE_PLAYLIST.read_text(encoding="utf-8")
        show_one_track_info = SHOW_ONE_TRACK_INFO.read_text(encoding="utf-8")
        show_three_playlist = SHOW_THREE_PLAYLIST.read_text(encoding="utf-8")
        show_three_track_info = SHOW_THREE_TRACK_INFO.read_text(encoding="utf-8")

        show_one_tracks = (
            (
                "Propaganda",
                '{{<title "Propaganda--Sparks">}}',
                '{{<release "Propaganda (1974)--Sparks--propaganda">}}',
            ),
            (
                "At Home, At Work, At Play",
                '{{<title "At Home, At Work, At Play--Sparks">}}',
                '{{<release "Propaganda (1974)--Sparks--propaganda">}}',
            ),
        )

        for playlist_title, title_shortcode, release_shortcode in show_one_tracks:
            with self.subTest(show=1, title=playlist_title):
                self.assertIn(
                    '{{< artist-wikilink "Sparks" >}} - ' + playlist_title,
                    show_one_playlist,
                )
                self.assert_track_guide_row(
                    show_one_track_info,
                    title_shortcode,
                    release_shortcode,
                )

        self.assertIn(
            '{{< artist-wikilink "sparks" >}} - beat the clock',
            show_three_playlist.lower(),
        )
        self.assert_track_guide_row(
            show_three_track_info,
            '{{<title "Beat the Clock--Sparks">}}',
            '{{<release "No. 1 In Heaven (1979)--Sparks--no-1-in-heaven">}}',
        )


if __name__ == "__main__":
    unittest.main()
