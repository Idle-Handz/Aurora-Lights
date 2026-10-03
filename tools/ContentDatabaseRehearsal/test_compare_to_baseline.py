"""Regression checks for comparison of warnings from isolated rehearsal runs."""
import contextlib
import io
import json
import pathlib
import tempfile
import unittest
from unittest.mock import patch

import compare_to_baseline


class WarningComparisonTests(unittest.TestCase):
    def compare(self, change=None):
        with tempfile.TemporaryDirectory() as temp:
            roots = [pathlib.Path(temp) / name for name in ("baseline", "rerun")]
            for root, guid in zip(roots, ("a" * 32, "b" * 32)):
                path = root / "fresh-b" / ("failure-fixture-" + guid) / "custom" / "core" / "zz-unreadable.xml"
                report = {"mode": "failure-check", "success": True, "warnings": [
                    {"Message": f"Content skipped (unreadable): {path}"}]}
                if change:
                    change(report, root == roots[1])
                results = root / "results"
                results.mkdir(parents=True)
                (results / "fresh-b-failure-check.json").write_text(json.dumps(report), encoding="utf-8")
            with patch("sys.argv", ["compare_to_baseline.py", *map(str, roots)]), \
                    contextlib.redirect_stdout(io.StringIO()), self.assertRaises(SystemExit) as exit:
                compare_to_baseline.main()
            return exit.exception.code

    def test_generated_fixture_path_does_not_fail_otherwise_identical_check(self):
        self.assertEqual(0, self.compare())

    def test_meaningful_warning_changes_still_fail(self):
        for before, after in (("zz-unreadable.xml", "other.xml"),
                              ("(unreadable)", "(invalid-root)")):
            with self.subTest(change=after):
                def change(report, rerun):
                    if rerun:
                        report["warnings"][0]["Message"] = report["warnings"][0]["Message"].replace(before, after)
                self.assertEqual(1, self.compare(change))

    def test_missing_warning_and_failed_check_still_fail(self):
        for field, value in (("warnings", []), ("success", False)):
            with self.subTest(field=field):
                def change(report, rerun):
                    if rerun:
                        report[field] = value
                self.assertEqual(1, self.compare(change))

    def test_fixture_like_text_outside_the_generated_path_is_not_normalized(self):
        def change(report, rerun):
            guid = ("b" if rerun else "a") * 32
            report["warnings"].append({"Message": "Missing definition failure-fixture-" + guid})
        self.assertEqual(1, self.compare(change))


if __name__ == "__main__":
    unittest.main()
