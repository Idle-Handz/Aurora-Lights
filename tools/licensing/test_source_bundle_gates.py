"""A resolved asset list must not bypass other source-distribution obligations."""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import build_source_bundle


class SourceBundleGateTests(unittest.TestCase):
    def assert_blocked(self, components, expected):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'third-party').mkdir()
            (root / 'third-party/components.json').write_text(json.dumps(components), encoding='utf-8')
            output = root / 'must-not-exist.zip'
            with patch.object(build_source_bundle, 'ROOT', root), \
                 patch.object(build_source_bundle, 'verify_assets', return_value=({}, [])), \
                 patch('sys.argv', ['build_source_bundle', '--require-resolved', '--output', str(output)]):
                with self.assertRaisesRegex(ValueError, expected):
                    build_source_bundle.main()
            self.assertFalse(output.exists())

    def test_unresolved_component_blocks_source_even_when_assets_are_resolved(self):
        self.assert_blocked({'components': [{'name': 'Pending library', 'state': 'pending'}]}, 'Pending library')

    def test_open_release_issue_blocks_source_even_when_components_are_recorded(self):
        self.assert_blocked({'components': [], 'releaseIssues': [{'id': 'unreviewed-source', 'status': 'open'}]}, 'unreviewed-source')


if __name__ == '__main__':
    unittest.main()
