"""Verify bundled asset provenance without opening installed app data or using the network."""
from __future__ import annotations

import hashlib
import json
import os
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RESOURCE_ROOTS = (
    'Aurora.Reflections/Resources', 'Aurora.Documents/Resources',
    'Aurora.Logic/Resources', 'Aurora.Legacy/resources',
    'docs/branding',
)
MEDIA_EXTENSIONS = {
    '.png', '.jpg', '.jpeg', '.ico', '.svg', '.gif', '.webp', '.pdf',
    '.ttf', '.otf', '.woff', '.woff2', '.mp3', '.wav', '.mp4',
}
PRODUCTION_PROJECTS = (
    'Aurora.Reflections', 'Aurora.Components', 'Aurora.Documents', 'Aurora.Logic',
    'Aurora.Presentation', 'Aurora.DataIntegration', 'Aurora.PdfImport',
    'Builder.Core', 'Builder.Data',
)


def discovered_assets(root: Path) -> set[str]:
    paths = set()
    for name in RESOURCE_ROOTS:
        for path in (root / name).rglob('*'):
            if path.is_file() and path.suffix.lower() not in {'.cs', '.xaml'}:
                paths.add(path.relative_to(root).as_posix())
    # Also catch media introduced outside the existing resource directories.
    for name in PRODUCTION_PROJECTS:
        for directory, children, files in os.walk(root / name):
            children[:] = [child for child in children if child not in {'bin', 'obj', 'node_modules'}]
            for filename in files:
                path = Path(directory) / filename
                if path.suffix.lower() in MEDIA_EXTENSIONS:
                    paths.add(path.relative_to(root).as_posix())
    return paths


def verify_assets(components: dict, root: Path = ROOT, distribution: str | None = None) -> tuple[dict, list[str]]:
    manifest = json.loads((root / 'third-party/assets.json').read_text(encoding='utf-8'))
    if manifest.get('schemaVersion') != 1:
        raise ValueError('Unsupported asset inventory schema')
    groups = {group['id']: group for group in manifest['groups']}
    if len(groups) != len(manifest['groups']):
        raise ValueError('Duplicate asset group')
    records = {entry['path']: entry for entry in manifest['files']}
    if len(records) != len(manifest['files']):
        raise ValueError('Duplicate asset path')
    actual = discovered_assets(root)
    if actual != records.keys():
        raise ValueError('Update asset inventory. Unrecorded: ' + ', '.join(sorted(actual - records.keys()))
                         + '; missing: ' + ', '.join(sorted(records.keys() - actual)))
    known = {component['name']: component for component in components['components']}
    used = set()
    for name, entry in records.items():
        path = (root / name).resolve()
        if not path.is_relative_to(root.resolve()) or Path(name).is_absolute():
            raise ValueError('Asset path escapes repository: ' + name)
        if hashlib.sha256(path.read_bytes()).hexdigest() != entry['sha256']:
            raise ValueError('Asset changed; review provenance and update hash: ' + name)
        if entry['group'] not in groups:
            raise ValueError('Unknown asset group: ' + name)
        scopes = entry.get('distributions', [])
        if not scopes or not set(scopes) <= {'windows', 'android', 'source'}:
            raise ValueError('Invalid asset distribution scope: ' + name)
        # All monitored assets are in the source bundle; the legacy default
        # portrait is additionally linked into the stable native application.
        required = {'source'} if name.startswith(('Aurora.Legacy/', 'docs/')) else {'windows', 'android', 'source'}
        if name == 'Aurora.Legacy/resources/default-portrait.png':
            required = {'windows', 'android', 'source'}
        if set(scopes) != required:
            raise ValueError('Asset distribution scope does not match packaging: ' + name)
        if distribution is None or distribution in scopes:
            used.add(entry['group'])
        group = groups[entry['group']]
        if group.get('state') == 'recorded':
            if not group.get('license') or not group.get('evidence'):
                raise ValueError('Recorded asset group has no license or evidence: ' + group['id'])
            if group.get('component'):
                component = known.get(group['component'])
                if not component or component.get('state') != 'recorded':
                    raise ValueError('Asset license component is not recorded: ' + group['id'])
                if component.get('license') != group['license']:
                    raise ValueError('Asset license differs from component: ' + name)
                if component.get('binaryPath') != name or component.get('binarySha256') != entry['sha256']:
                    raise ValueError('Asset differs from licensed component binary: ' + name)
    pending = [groups[key]['name'] for key in sorted(used) if groups[key].get('state') != 'recorded']
    # Keep the reviewed SRD subject boundary independent of a generic asset-hash
    # refresh. Adding a spell requires an explicit source-page inventory update.
    content_path = root / 'third-party/srd-spells.json'
    if content_path.exists() or (root / 'Aurora.Logic/Resources/Data/elements/system_reference_document/spells/spells.xml').exists():
        content = json.loads(content_path.read_text(encoding='utf-8'))
        resource = (root / content['resourcePath']).resolve()
        if not resource.is_relative_to(root.resolve()):
            raise ValueError('SRD resource path escapes repository')
        if hashlib.sha256(resource.read_bytes()).hexdigest() != content['resourceSha256']:
            raise ValueError('SRD spell resource changed; review content provenance')
        subjects = content['subjects']
        expected = {subject['id']: subject['name'] for subject in subjects}
        elements = ET.fromstring(resource.read_bytes()).findall('element')
        actual_spells = {element.get('id'): element.get('name') for element in elements}
        if (len(subjects) != 319 or len(expected) != 319 or len(elements) != 319
                or actual_spells != expected or any(element.get('type') != 'Spell' for element in elements)):
            raise ValueError('Bundled spells differ from the reviewed SRD 5.1 subject list')
        if any(not 114 <= subject.get('srdPage', 0) <= 193 for subject in subjects):
            raise ValueError('SRD spell subject has no valid source page')
    baseline_path = root / 'third-party/srd-baseline.json'
    data_root = root / 'Aurora.Logic/Resources/Data'
    if baseline_path.exists() or (data_root / 'core').exists():
        baseline = json.loads(baseline_path.read_text(encoding='utf-8'))
        resources = {item['path']: item for item in baseline['resources']}
        actual_resources = {path.relative_to(root).as_posix() for path in data_root.rglob('*.xml')}
        if len(resources) != len(baseline['resources']) or resources.keys() != actual_resources:
            raise ValueError('SRD baseline resource coverage changed; review content provenance')
        actual_subjects = []
        for name, record in resources.items():
            path = (root / name).resolve()
            if not path.is_relative_to(data_root.resolve()):
                raise ValueError('SRD baseline resource escapes content directory')
            if hashlib.sha256(path.read_bytes()).hexdigest() != record['sha256']:
                raise ValueError('SRD baseline changed; review content provenance: ' + name)
            elements = ET.fromstring(path.read_bytes()).findall('element')
            if len(elements) != record['elementCount']:
                raise ValueError('SRD baseline element count changed: ' + name)
            actual_subjects.extend({'id': e.get('id'), 'name': e.get('name'), 'type': e.get('type'),
                                    'resourcePath': name} for e in elements)
        key = lambda subject: (subject['resourcePath'], subject['id'])
        if sorted(actual_subjects, key=key) != sorted(baseline['subjects'], key=key):
            raise ValueError('SRD baseline differs from reviewed subjects')
        if len({e['id'] for e in actual_subjects}) != len(actual_subjects):
            raise ValueError('Duplicate SRD baseline identifier')
    # The MIT classification of these adapters relies on their being original
    # machine-readable integration data, rather than copied book descriptions.
    allowed_tags = {'aurora-starting-equipment', 'entry', 'starting-equipment',
                    'choice', 'option', 'item', 'gold', 'gold-alternative', 'coins'}
    for path in (root / 'Aurora.Reflections/Resources/Raw/StartingEquipment').rglob('*.xml'):
        for node in ET.fromstring(path.read_bytes()).iter():
            if node.tag not in allowed_tags:
                raise ValueError('Unsupported starting-equipment content; review provenance: ' + str(path))
            if (node.text or '').strip() or (node.tail or '').strip():
                raise ValueError('Starting-equipment adapter contains descriptive text: ' + str(path))
    return manifest, pending
