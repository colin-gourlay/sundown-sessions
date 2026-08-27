from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


class CorrectionFlowTests(unittest.TestCase):
    def test_form_has_a_configured_static_submission_service(self):
        params = (ROOT / "src/config/_default/params.toml").read_text()
        shortcode = (ROOT / "src/layouts/shortcodes/form-correction.html").read_text()

        self.assertIn('correctionAction = "https://formspree.io/f/', params)
        self.assertIn(".Site.Params.forms.correctionAction", shortcode)

    def test_form_contains_triage_and_abuse_protection_fields(self):
        shortcode = (ROOT / "src/layouts/shortcodes/form-correction.html").read_text()

        self.assertIn('name="tags" value="correction,content,triage"', shortcode)
        self.assertIn('name="_gotcha"', shortcode)
        self.assertIn('maxlength="2000"', shortcode)

    def test_footer_passes_the_affected_page_to_the_form(self):
        footer = (ROOT / "src/layouts/partials/footer.html").read_text()

        self.assertIn('(eq .Identifier "corrections")', footer)
        self.assertIn('$currentPage.RelPermalink | urlquery', footer)


if __name__ == "__main__":
    unittest.main()
