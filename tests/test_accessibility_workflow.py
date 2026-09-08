import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github/workflows/accessibility.yml"
MANUAL_TESTING = ROOT / "docs/manual-accessibility-testing.md"


class AccessibilityWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = WORKFLOW.read_text(encoding="utf-8")
        cls.manual_testing = MANUAL_TESTING.read_text(encoding="utf-8")

    def test_required_check_name_is_stable(self):
        self.assertRegex(
            self.workflow,
            r"(?m)^  pa11y:\n    name: Accessibility / Pa11y$",
        )
        self.assertIn("**Accessibility / Pa11y**", self.manual_testing)

    def test_pull_request_workflow_is_not_path_filtered(self):
        pull_request_event = re.search(
            r"(?ms)^  pull_request:\n(?P<body>.*?)(?=^  push:)",
            self.workflow,
        )

        self.assertIsNotNone(pull_request_event)
        self.assertNotIn("paths:", pull_request_event.group("body"))

    def test_path_gate_matches_documented_required_scope(self):
        expected_scope = [
            "src/**",
            "docs/accessibility.md",
            ".pa11yci.json",
            ".github/workflows/accessibility.yml",
        ]

        for path in expected_scope:
            with self.subTest(path=path):
                self.assertIn(path, self.manual_testing)

        expected_patterns = [
            r"src/",
            r"docs/accessibility\.md",
            r"\.pa11yci\.json",
            r"\.github/workflows/accessibility\.yml",
        ]

        for pattern in expected_patterns:
            with self.subTest(pattern=pattern):
                self.assertIn(pattern, self.workflow)

    def test_unrelated_pull_requests_complete_required_check(self):
        self.assertIn("run_pa11y=false", self.workflow)
        self.assertIn("skipping pa11y", self.workflow)
        self.assertIn("remaining pending", self.manual_testing)


if __name__ == "__main__":
    unittest.main()
