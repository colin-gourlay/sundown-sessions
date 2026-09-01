import html.parser
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
HUGO_CONFIG = ROOT / "src/config/_default/hugo.toml"
PAGINATION_PARTIAL = ROOT / "src/layouts/partials/pagination.html"
CACHE_DIRECTORY = ROOT / ".cache"


class PaginationParser(html.parser.HTMLParser):
    def __init__(self):
        super().__init__()
        self.items = []
        self.links = []
        self.nav_labels = []

    def handle_starttag(self, tag, attrs):
        attributes = dict(attrs)
        if tag == "a":
            self.links.append(attributes)
            if "item" in attributes.get("class", "").split():
                self.items.append(attributes["href"])
        elif tag == "nav":
            self.nav_labels.append(attributes.get("aria-label"))


class PaginationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if shutil.which("hugo") is None:
            raise unittest.SkipTest("Hugo is not installed")

        CACHE_DIRECTORY.mkdir(exist_ok=True)
        config = HUGO_CONFIG.read_text(encoding="utf-8")
        match = re.search(r"(?m)^\s*pagerSize\s*=\s*(\d+)\s*$", config)
        if match is None:
            raise AssertionError("The global Hugo pagerSize setting is missing")
        cls.pager_size = int(match.group(1))

    def build_fixture(self, item_count):
        temporary_directory = tempfile.TemporaryDirectory(
            prefix="pagination-test-", dir=CACHE_DIRECTORY
        )
        site = Path(temporary_directory.name)
        (site / "content/items").mkdir(parents=True)
        (site / "layouts/_default").mkdir(parents=True)
        (site / "layouts/partials").mkdir(parents=True)

        (site / "hugo.toml").write_text(
            f'baseURL = "https://example.invalid/"\n[pagination]\n  pagerSize = {self.pager_size}\n',
            encoding="utf-8",
        )
        (site / "content/items/_index.md").write_text(
            "---\ntitle: Items\n---\n", encoding="utf-8"
        )
        for number in range(1, item_count + 1):
            (site / f"content/items/item-{number:03d}.md").write_text(
                f"---\ntitle: Item {number:03d}\n---\n", encoding="utf-8"
            )

        (site / "layouts/_default/list.html").write_text(
            """<!doctype html><html lang=\"en\"><body><main>
{{ $paginator := .Paginate .Pages.ByTitle }}
{{ range $paginator.Pages }}<a class=\"item\" href=\"{{ .RelPermalink }}\">{{ .Title }}</a>{{ end }}
{{ partial \"pagination.html\" . }}
</main></body></html>
""",
            encoding="utf-8",
        )
        shutil.copyfile(PAGINATION_PARTIAL, site / "layouts/partials/pagination.html")

        subprocess.run(
            ["hugo", "--source", str(site), "--quiet"],
            check=True,
            text=True,
        )
        return temporary_directory, site / "public"

    def parse(self, path):
        parser = PaginationParser()
        parser.feed(path.read_text(encoding="utf-8"))
        return parser

    def test_collections_at_or_below_boundary_do_not_paginate(self):
        for item_count in (self.pager_size - 1, self.pager_size):
            with self.subTest(item_count=item_count):
                temporary_directory, public = self.build_fixture(item_count)
                with temporary_directory:
                    first_page = self.parse(public / "items/index.html")
                    self.assertEqual(len(first_page.items), item_count)
                    self.assertEqual(first_page.nav_labels, [])
                    self.assertFalse((public / "items/page/2/index.html").exists())

    def test_collection_above_boundary_is_complete_and_accessible(self):
        temporary_directory, public = self.build_fixture(self.pager_size + 1)
        with temporary_directory:
            first_page = self.parse(public / "items/index.html")
            second_page = self.parse(public / "items/page/2/index.html")

            self.assertEqual(len(first_page.items), self.pager_size)
            self.assertEqual(len(second_page.items), 1)
            self.assertEqual(
                first_page.items + second_page.items,
                [f"/items/item-{number:03d}/" for number in range(1, self.pager_size + 2)],
            )
            self.assertEqual(first_page.nav_labels, ["Pagination"])
            self.assertEqual(second_page.nav_labels, ["Pagination"])

            first_current = [
                link
                for link in first_page.links
                if link.get("aria-current") == "page"
            ]
            second_current = [
                link
                for link in second_page.links
                if link.get("aria-current") == "page"
            ]
            self.assertEqual(first_current[0].get("aria-label"), "Page 1, current page")
            self.assertEqual(second_current[0].get("aria-label"), "Page 2, current page")
            self.assertTrue(
                any(
                    link.get("rel") == "next"
                    and link.get("aria-label") == "Next page"
                    for link in first_page.links
                )
            )
            self.assertTrue(
                any(
                    link.get("rel") == "prev"
                    and link.get("aria-label") == "Previous page"
                    for link in second_page.links
                )
            )


if __name__ == "__main__":
    unittest.main()
