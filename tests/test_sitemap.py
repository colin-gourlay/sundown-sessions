"""Validate the production build before uploading it to GitHub Pages.

Usage: python3 tests/test_sitemap.py src/public
"""

import sys
import unittest
import xml.etree.ElementTree as ET
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit


BASE_URL = "https://sundownsessions.co.uk/"
DESTINATION = Path("src/public")


class PageMetadata(HTMLParser):
    def __init__(self, html):
        super().__init__()
        self.canonicals = []
        self.redirect = False
        self.feed(html)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "link" and attrs.get("rel") == "canonical":
            self.canonicals.append(attrs.get("href"))
        if tag == "meta" and attrs.get("http-equiv", "").lower() == "refresh":
            self.redirect = True


class SitemapTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = ET.parse(DESTINATION / "sitemap.xml").getroot()
        cls.urls = [node.text for node in cls.root.findall("{*}url/{*}loc")]

    def test_valid_unique_canonical_urls_resolve_to_pages(self):
        self.assertEqual(
            self.root.tag, "{http://www.sitemaps.org/schemas/sitemap/0.9}urlset"
        )
        self.assertTrue(self.urls)
        self.assertEqual(len(self.urls), len(set(self.urls)))
        for url in self.urls:
            with self.subTest(url=url):
                self.assertTrue(url.startswith(BASE_URL))
                parsed = urlsplit(url)
                self.assertFalse(parsed.query or parsed.fragment)
                page = DESTINATION / unquote(parsed.path).lstrip("/") / "index.html"
                self.assertTrue(page.is_file(), f"Missing output: {page}")
                head = page.read_text(encoding="utf-8").split("</head>", 1)[0]
                metadata = PageMetadata(head)
                self.assertFalse(metadata.redirect)
                self.assertEqual(metadata.canonicals, [url])

    def test_public_sections_and_pages_are_discoverable(self):
        for path in (
            "", "about/", "contact/", "corrections/",
            "listen-live/", "search/", "upcoming/",
        ):
            self.assertIn(BASE_URL + path, self.urls)
        for section in ("shows", "artists", "releases", "tracks"):
            self.assertIn(f"{BASE_URL}{section}/", self.urls)
            self.assertTrue(any(
                url.startswith(f"{BASE_URL}{section}/")
                and url != f"{BASE_URL}{section}/"
                for url in self.urls
            ))

    def test_drafts_placeholders_and_internal_resources_are_absent(self):
        for path in (
            "shows/featuring-a-celebration-of-vertigo-records/",
            "shows/featuring-tbd/", "artists/s/skunk-anansie/",
            "upcoming/next/", "upcoming/twisted-nerve/",
            "artists/a/alphaville/", "artists/b/bruce-springsteen/",
            "artists/t/the-korgis/", "artists/t/the-prime-movers/",
            "shows/39/discussion-points/", "404.html",
        ):
            self.assertNotIn(BASE_URL + path, self.urls)

    def test_approximate_release_date_survives_production_build(self):
        page = DESTINATION / "releases/t/the-big-now/fast-cars-soul-music/index.html"
        self.assertIn("c.1989", page.read_text(encoding="utf-8"))

    def test_production_robots_allows_discovery(self):
        robots = (DESTINATION / "robots.txt").read_text(encoding="utf-8")
        self.assertIn("User-agent: *", robots)
        self.assertIn("Allow: /", robots)
        self.assertNotIn("Disallow: /", robots)
        self.assertIn(f"Sitemap: {BASE_URL}sitemap.xml", robots)


if __name__ == "__main__":
    if len(sys.argv) > 1:
        DESTINATION = Path(sys.argv.pop(1))
    unittest.main()
