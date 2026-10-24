"""Focused regression checks for asset release gates; no app build or device access."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from asset_inventory import verify_assets


class AssetInventoryTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.name = 'Aurora.App/Resources/Fonts/test.ttf'
        file = self.root / self.name
        file.parent.mkdir(parents=True)
        file.write_bytes(b'fixture-font')
        digest = hashlib.sha256(file.read_bytes()).hexdigest()
        self.components = {'components': [{'name': 'Test font', 'state': 'recorded',
            'license': 'OFL-1.1', 'binaryPath': self.name, 'binarySha256': digest}]}
        self.manifest = {'schemaVersion': 1, 'groups': [{'id': 'font', 'name': 'Test font',
            'state': 'recorded', 'license': 'OFL-1.1', 'component': 'Test font',
            'evidence': ['fixture-license']}], 'files': [{'path': self.name,
            'sha256': digest, 'group': 'font', 'distributions': ['windows', 'android', 'source']}]}

    def verify(self, distribution=None):
        folder = self.root / 'third-party'
        folder.mkdir(exist_ok=True)
        (folder / 'assets.json').write_text(json.dumps(self.manifest), encoding='utf-8')
        return verify_assets(self.components, self.root, distribution)

    def test_recorded_asset_matches_licensed_binary(self):
        self.assertEqual([], self.verify()[1])

    def test_changed_asset_requires_review(self):
        (self.root / self.name).write_bytes(b'changed-font')
        with self.assertRaisesRegex(ValueError, 'Asset changed'):
            self.verify()

    def test_new_media_outside_resource_folder_is_not_silently_bundled(self):
        extra = self.root / 'Aurora.Logic/Images/new.png'
        extra.parent.mkdir(parents=True)
        extra.write_bytes(b'new-art')
        with self.assertRaisesRegex(ValueError, 'Unrecorded: Aurora.Logic/Images/new.png'):
            self.verify()

    def test_removed_asset_requires_manifest_update(self):
        (self.root / self.name).unlink()
        with self.assertRaisesRegex(ValueError, 'missing: '):
            self.verify()

    def test_component_license_change_cannot_leave_stale_asset_approval(self):
        self.components['components'][0]['license'] = 'LicenseRef-Permission-Needed'
        with self.assertRaisesRegex(ValueError, 'license differs'):
            self.verify()

    def test_recorded_asset_cannot_bypass_pending_component(self):
        self.components['components'][0]['state'] = 'permission-pending'
        with self.assertRaisesRegex(ValueError, 'component is not recorded'):
            self.verify()

    def test_missing_evidence_is_rejected(self):
        self.manifest['groups'][0]['evidence'] = []
        with self.assertRaisesRegex(ValueError, 'no license or evidence'):
            self.verify()

    def test_stable_asset_cannot_be_relabelled_source_only(self):
        self.manifest['files'][0]['distributions'] = ['source']
        with self.assertRaisesRegex(ValueError, 'scope does not match packaging'):
            self.verify('windows')

    def test_source_only_permissions_are_still_required_for_source_distribution(self):
        name = 'Aurora.Lights/resources/old-art.jpg'
        file = self.root / name
        file.parent.mkdir(parents=True)
        file.write_bytes(b'old-art')
        self.manifest['groups'].append({'id': 'legacy', 'name': 'Legacy art',
            'state': 'permission-pending', 'license': 'Unresolved', 'evidence': []})
        self.manifest['files'].append({'path': name, 'sha256': hashlib.sha256(file.read_bytes()).hexdigest(),
            'group': 'legacy', 'distributions': ['source']})
        self.assertEqual([], self.verify('android')[1])
        self.assertEqual(['Legacy art'], self.verify('source')[1])

    def test_duplicate_asset_entries_are_rejected(self):
        self.manifest['files'].append(copy.deepcopy(self.manifest['files'][0]))
        with self.assertRaisesRegex(ValueError, 'Duplicate asset path'):
            self.verify()

    def add_data_asset(self, name, data):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        self.manifest['files'].append({'path': name, 'sha256': hashlib.sha256(data).hexdigest(),
            'group': 'data', 'distributions': ['windows', 'android', 'source']})
        self.manifest['groups'].append({'id': 'data', 'name': 'Original data', 'license': 'MIT',
            'state': 'recorded', 'evidence': ['fixture-scope-review']})
        return path

    def test_adapter_cannot_gain_book_prose_even_if_its_asset_hash_is_refreshed(self):
        self.add_data_asset('Aurora.App/Resources/Raw/StartingEquipment/fixture.xml',
            b'<aurora-starting-equipment><entry>Unreviewed descriptive prose</entry></aurora-starting-equipment>')
        with self.assertRaisesRegex(ValueError, 'contains descriptive text'):
            self.verify()

    def test_adapter_schema_cannot_silently_expand_to_descriptions(self):
        self.add_data_asset('Aurora.App/Resources/Raw/StartingEquipment/fixture.xml',
            b'<aurora-starting-equipment><description /></aurora-starting-equipment>')
        with self.assertRaisesRegex(ValueError, 'Unsupported starting-equipment content'):
            self.verify()

    def add_baseline(self):
        name = 'Aurora.Logic/Resources/Data/core/fixture.xml'
        path = self.add_data_asset(name, b'<elements><element id="ONE" name="Original" type="Support" /></elements>')
        baseline = {'resources': [{'path': name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'elementCount': 1}],
            'subjects': [{'id': 'ONE', 'name': 'Original', 'type': 'Support', 'resourcePath': name}]}
        folder = self.root / 'third-party'
        folder.mkdir(exist_ok=True)
        (folder / 'srd-baseline.json').write_text(json.dumps(baseline), encoding='utf-8')
        return path, baseline

    def test_asset_hash_refresh_does_not_bypass_separate_content_review(self):
        path, _ = self.add_baseline()
        path.write_bytes(b'<elements><element id="ONE" name="Changed" type="Support" /></elements>')
        self.manifest['files'][-1]['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
        with self.assertRaisesRegex(ValueError, 'SRD baseline changed'):
            self.verify()

    def test_content_hash_refresh_does_not_silently_change_reviewed_subjects(self):
        path, baseline = self.add_baseline()
        path.write_bytes(b'<elements><element id="TWO" name="Changed" type="Support" /></elements>')
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        self.manifest['files'][-1]['sha256'] = digest
        baseline['resources'][0]['sha256'] = digest
        (self.root / 'third-party/srd-baseline.json').write_text(json.dumps(baseline), encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'differs from reviewed subjects'):
            self.verify()


if __name__ == '__main__':
    unittest.main()
