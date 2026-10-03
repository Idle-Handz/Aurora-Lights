"""Compare real local overrides with/without proposed metadata through bundled Translator.

Reads the supplied override directory; all copies, SQLite databases and logs go to
a new temporary directory. No production database or original XML is modified.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sqlite3
import subprocess
import tempfile
import xml.etree.ElementTree as ET


def snapshot(path):
    with sqlite3.connect(path.as_uri() + '?mode=ro', uri=True) as db:
        tables = [r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT IN ('database_metadata','import_state') ORDER BY name")]
        result = {}
        for table in tables:
            query = db.execute('SELECT * FROM "' + table.replace('"', '""') + '"')
            columns = [c[0] for c in query.description]
            result[table] = sorted(json.dumps({k: v for k, v in zip(columns, row) if k not in ('file_hash', 'created_utc')}, sort_keys=True) for row in query)
        return result


def run_import(executable, content, database, log):
    run = subprocess.run([str(executable), 'sqlite-import', str(content), str(database)], capture_output=True, text=True, timeout=180)
    log.write_text(run.stdout + '\n' + run.stderr, encoding='utf-8')
    if run.returncode:
        raise RuntimeError(f'Translator exited {run.returncode}; see {log}')
    if not database.is_file():
        raise RuntimeError(f'Translator did not create {database}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--overrides', type=Path, required=True)
    parser.add_argument('--translator', type=Path, required=True)
    args = parser.parse_args()
    work = Path(tempfile.mkdtemp(prefix='aurora-correction-cli-'))
    content = work / 'content'
    originals = {p.relative_to(args.overrides): p.read_bytes() for p in sorted(args.overrides.rglob('*.xml'))}
    if not originals:
        raise RuntimeError('No override XML found')
    for relative, raw in originals.items():
        target = content / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)
    baseline = work / 'baseline.sqlite'
    run_import(args.translator, content, baseline, work / 'baseline.log')

    namespace = 'urn:aurora-lights:corrections:1'
    for relative, raw in originals.items():
        text = raw.decode('utf-8-sig')
        first = ET.fromstring(text).find('element')
        block = ET.Element('{'+namespace+'}corrections', {'version': '1'})
        ET.register_namespace('al', namespace)
        correction = ET.SubElement(block, '{'+namespace+'}correction', {
            'key': 'compatibility-probe', 'operation': 'replace',
            'target-id': first.get('id'), 'state': 'review-pending'})
        ET.SubElement(correction, '{'+namespace+'}reason').text = 'Compatibility test only; original XML is unchanged.'
        ET.SubElement(correction, '{'+namespace+'}baseline', {'encoding': 'escaped-xml'}).text = ET.tostring(first, encoding='unicode')
        probe = ET.SubElement(correction, '{'+namespace+'}baseline-probe')
        ET.SubElement(probe, 'element', {'id': 'ID_METADATA_MUST_NOT_IMPORT', 'type': 'Item', 'name': 'Probe', 'source': 'Probe'})
        ET.SubElement(probe, 'append', {'id': 'ID_METADATA_MUST_NOT_APPEND'})
        assert text.count('</elements>') == 1
        annotated = text.replace('</elements>', ET.tostring(block, encoding='unicode') + '\n</elements>')
        (content / relative).write_text(annotated, encoding='utf-8')

    candidate = work / 'annotated.sqlite'
    run_import(args.translator, content, candidate, work / 'annotated.log')
    before, after = snapshot(baseline), snapshot(candidate)
    differences = [name for name in before.keys() | after.keys() if before.get(name) != after.get(name)]
    assert not differences, f'Changed content tables: {differences}'
    with sqlite3.connect(candidate.as_uri() + '?mode=ro', uri=True) as db:
        count, unique = db.execute('SELECT COUNT(*),COUNT(DISTINCT aurora_id) FROM elements').fetchone()
        metadata = dict(zip([c[0] for c in db.execute('SELECT * FROM database_metadata').description], db.execute('SELECT * FROM database_metadata').fetchone()))
        resolved = db.execute("SELECT g.target_aurora_id,e.aurora_id FROM grants g LEFT JOIN elements e ON e.element_id=g.target_element_id WHERE g.target_aurora_id IN ('ID_JONOMAN3000_ARCHETYPE_FEATURE_DEVOUT_ZEALOTS_DEVOTION_DEFENDER_OF_KIN','ID_JONOMAN3000_ARCHETYPE_FEATURE_DEVOUT_ZEALOTS_DEVOTION_SLAYER_OF_FOES','ID_RGTTYR_RACIAL_TRAIT_TATSUMI_RYUJIN_HEARTENING_BREATH') ORDER BY g.target_aurora_id").fetchall()
        assert len(resolved) == 3 and all(target == actual for target, actual in resolved), resolved
        assert db.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
        assert not db.execute('PRAGMA foreign_key_check').fetchall()
    assert all((args.overrides / rel).read_bytes() == raw for rel, raw in originals.items())
    report = {
        'work_directory': str(work), 'translator': str(args.translator),
        'translator_sha256': hashlib.sha256(args.translator.read_bytes()).hexdigest(),
        'files': [{'path': str(rel), 'sha256': hashlib.sha256(raw).hexdigest()} for rel, raw in originals.items()],
        'compared_tables': len(before), 'changed_content_tables': differences,
        'elements': count, 'distinct_ids': unique, 'repaired_grants': resolved,
        'database_metadata': metadata, 'original_files_unchanged': True,
        'excluded_comparison_fields': ['database_metadata', 'import_state', 'file_hash', 'created_utc'],
        'scope': 'Tolerance only; correction mirroring and lifecycle are not implemented.'}
    (work / 'report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
