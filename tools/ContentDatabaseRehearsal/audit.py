"""Read-only database audit and installed-content preservation check."""
import hashlib
import json
import pathlib
import sqlite3
import sys

root = pathlib.Path(sys.argv[1]).resolve()
if not (root / "manifest.json").exists():
    raise RuntimeError("Expected a rehearsal directory.")
digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest().upper()
before = json.loads((root / "installed-before.json").read_text())
live = pathlib.Path(json.loads((root / "manifest.json").read_text())["live"])
current_paths = {str(p) for p in live.rglob("*.xml")} | {str(live / "aurora-elements.sqlite")}
changes = sorted(set(before) ^ current_paths)
changes += [p for p, h in before.items() if pathlib.Path(p).exists() and digest(pathlib.Path(p)) != h]
report = {"installedFilesChecked": len(before), "installedChanges": changes, "databases": {}}
for database in sorted(root.glob("*/custom/aurora-elements.sqlite")):
    with sqlite3.connect(database.as_uri() + "?mode=ro", uri=True) as db:
        def rows(sql): return db.execute(sql).fetchall()
        tables = {r[0] for r in rows("SELECT name FROM sqlite_master WHERE type='table'")}
        entry = {"bytes": database.stat().st_size,
            "integrity": rows("PRAGMA integrity_check"),
            "foreignKeyErrors": rows("PRAGMA foreign_key_check"),
            "metadata": rows("SELECT * FROM database_metadata"),
            "elements": rows("SELECT COUNT(*),COUNT(DISTINCT aurora_id) FROM elements"),
            "duplicates": rows("SELECT aurora_id,COUNT(*) FROM elements GROUP BY aurora_id HAVING COUNT(*)>1"),
            "packages": rows("SELECT package_key,package_kind,is_enabled,precedence_rank FROM content_packages ORDER BY package_key"),
            "aliases": rows("SELECT aurora_id FROM elements WHERE instr(aurora_id,'_LOCAL_')>0 ORDER BY aurora_id")}
        if "content_prepared_elements" in tables:
            entry["prepared"] = rows("SELECT COUNT(*),COUNT(DISTINCT aurora_id) FROM content_prepared_elements")
            entry["appends"] = rows("SELECT status,COUNT(*) FROM content_append_operations GROUP BY status")
            entry["corrections"] = rows("SELECT state,COUNT(*) FROM local_corrections GROUP BY state")
            entry["disabledStored"] = rows("SELECT COUNT(*) FROM elements e JOIN source_files sf USING(source_file_id) JOIN content_packages cp USING(content_package_id) WHERE cp.is_enabled=0")
        report["databases"][database.parents[1].name] = entry
fresh = root / "fresh-v12/custom/aurora-elements.sqlite"
migrated = root / "migration-v12/custom/aurora-elements.sqlite"
if fresh.exists() and migrated.exists():
    with sqlite3.connect(fresh.as_uri() + "?mode=ro", uri=True) as a, sqlite3.connect(migrated.as_uri() + "?mode=ro", uri=True) as b:
        statements = {
            "preparedDefinitions": "SELECT aurora_id,base_xml,effective_xml FROM content_prepared_elements ORDER BY aurora_id",
            "appendOperations": "SELECT relative_path,ordinal,target_aurora_id,operation_xml,status FROM content_append_operations ORDER BY relative_path,ordinal",
            "globalCatalogIds": "SELECT aurora_id FROM resolved_elements_cache ORDER BY aurora_id"
        }
        report["freshMigrationParity"] = {name: a.execute(sql).fetchall() == b.execute(sql).fetchall() for name, sql in statements.items()}
        v10_migrated = root / "migration-from-v10/custom/aurora-elements.sqlite"
        if v10_migrated.exists():
            with sqlite3.connect(v10_migrated.as_uri() + "?mode=ro", uri=True) as c:
                report["v10MigrationParity"] = {name: a.execute(sql).fetchall() == c.execute(sql).fetchall() for name, sql in statements.items()}
(root / "audit.json").write_text(json.dumps(report, indent=2))
print(json.dumps({"installedChanges": changes, "cases": {
    k: {field: v[field] for field in ["bytes", "integrity", "foreignKeyErrors", "elements"]}
    for k, v in report["databases"].items()}}, indent=2))
if changes or any(v["integrity"] != [("ok",)] or v["foreignKeyErrors"] for v in report["databases"].values()) or not all(report.get("freshMigrationParity", {}).values()) or not all(report.get("v10MigrationParity", {}).values()):
    sys.exit(1)
