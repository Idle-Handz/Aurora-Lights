"""Prepare parity-baseline rehearsal cases from installed content without modifying it.

Usage:
  python prepare_baseline.py                 create buildtmp/parity-baseline-<stamp>/
  python prepare_baseline.py --from <dir>    create buildtmp/parity-rerun-<stamp>/ from an earlier
                                             root's frozen XML, database copy and characters
  python prepare_baseline.py --verify <dir>  re-hash installed inputs against <dir>/installed-before.json

Cases (each contains .aurora-rehearsal):
  installed  XML copy + consistent backup of the installed database, with every absolute
             path relocated into the case, plus copies of the top-level characters.
  fresh-a    XML copy only (build a database here).
  fresh-b    XML copy only (a second independent build, to measure determinism).
"""
import argparse
import datetime
import hashlib
import json
import pathlib
import shutil
import sqlite3
import sys
import uuid

LIVE_ROOT = pathlib.Path.home() / "Documents" / "5e Character Builder"
CONTENT = LIVE_ROOT / "custom"
DATABASE = CONTENT / "aurora-elements.sqlite"
REPO = pathlib.Path(__file__).resolve().parents[2]
FIXTURE_CHARACTERS = [REPO / "Aurora.Tests" / "Fixtures" / "Characters" / "prepared-paladin.dnd5e"]
LIVE_MARKERS = ["documents\\5e character builder", "documents/5e character builder"]


def digest(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def installed_inputs() -> dict:
    files = sorted(CONTENT.rglob("*.xml")) + [DATABASE] + sorted(LIVE_ROOT.glob("*.dnd5e"))
    return {str(p): digest(p) for p in files}


def text_columns(connection):
    tables = [r[0] for r in connection.execute(
        "select name from sqlite_master where type='table' and name not like 'sqlite_%'")]
    for table in tables:
        for column in connection.execute(f'pragma table_info("{table}")').fetchall():
            yield table, column[1]


def relocate(database: pathlib.Path, old_root: str, new_root: str) -> dict:
    connection = sqlite3.connect(database)
    changed = 0
    for table, column in list(text_columns(connection)):
        cursor = connection.execute(
            f'update "{table}" set "{column}" = replace("{column}", ?, ?) '
            f'where typeof("{column}") = \'text\' and instr("{column}", ?) > 0',
            (old_root, new_root, old_root))
        changed += cursor.rowcount
    connection.commit()
    remaining = {}
    for table, column in text_columns(connection):
        for marker in LIVE_MARKERS:
            count = connection.execute(
                f'select count(*) from "{table}" where typeof("{column}") = \'text\' '
                f'and instr(lower("{column}"), ?) > 0', (marker,)).fetchone()[0]
            if count:
                remaining[f"{table}.{column}"] = remaining.get(f"{table}.{column}", 0) + count
    connection.close()
    return {"relocatedValues": changed, "remainingLiveReferences": remaining}


def copy_xml(target_content: pathlib.Path, files):
    for path in files:
        target = target_content / path.relative_to(CONTENT)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)


def prepare() -> None:
    output = REPO / "buildtmp" / ("parity-baseline-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
                                   + "-" + uuid.uuid4().hex[:6])
    assert output.resolve().is_relative_to((REPO / "buildtmp").resolve())
    output.mkdir(parents=True)
    before = installed_inputs()
    (output / "installed-before.json").write_text(json.dumps(before, indent=2))

    xml_files = sorted(CONTENT.rglob("*.xml"))
    for case in ["installed", "fresh-a", "fresh-b"]:
        case_dir = output / case
        (case_dir / "custom").mkdir(parents=True)
        (case_dir / ".aurora-rehearsal").write_text("Disposable copy, created by prepare_baseline.py.\n")
        copy_xml(case_dir / "custom", xml_files)

    installed_db = output / "installed" / "custom" / DATABASE.name
    with sqlite3.connect(DATABASE.as_uri() + "?mode=ro", uri=True) as source:
        with sqlite3.connect(installed_db) as destination:
            source.backup(destination)
        metadata = dict(zip([c[0] for c in source.execute("select * from database_metadata").description],
                            source.execute("select * from database_metadata").fetchone()))
    relocation = relocate(installed_db, str(CONTENT), str(output / "installed" / "custom"))
    if relocation["remainingLiveReferences"]:
        raise RuntimeError("Installed-database copy still references live content: "
                           + json.dumps(relocation["remainingLiveReferences"]))

    characters = output / "installed" / "characters"
    characters.mkdir()
    character_sources = sorted(LIVE_ROOT.glob("*.dnd5e")) + [p for p in FIXTURE_CHARACTERS if p.exists()]
    for path in character_sources:
        shutil.copy2(path, characters / path.name)

    manifest = {
        "created": datetime.datetime.now().isoformat(timespec="seconds"),
        "liveContent": str(CONTENT), "xmlFiles": len(xml_files),
        "installedMetadata": metadata, "relocation": relocation,
        "characters": [p.name for p in character_sources],
        "cases": ["installed", "fresh-a", "fresh-b"],
    }
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2, default=str))
    print(json.dumps({"output": str(output), "xmlFiles": len(xml_files),
                      "characters": len(character_sources), "relocation": relocation,
                      "dataVersion": metadata.get("data_version")}, indent=2, default=str))


def prepare_from(source: pathlib.Path) -> None:
    """Recreate cases from an earlier root so every run compares the same frozen inputs."""
    source = source.resolve()
    output = REPO / "buildtmp" / ("parity-rerun-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
                                   + "-" + uuid.uuid4().hex[:6])
    assert output.resolve().is_relative_to((REPO / "buildtmp").resolve())
    frozen = source / "fresh-a" / "custom"
    xml_files = sorted(frozen.rglob("*.xml"))
    for case in ["installed", "fresh-a", "fresh-b"]:
        case_dir = output / case
        (case_dir / "custom").mkdir(parents=True)
        (case_dir / ".aurora-rehearsal").write_text("Disposable copy, created by prepare_baseline.py --from.\n")
        for path in xml_files:
            target = case_dir / "custom" / path.relative_to(frozen)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    installed_db = output / "installed" / "custom" / DATABASE.name
    shutil.copy2(source / "installed" / "custom" / DATABASE.name, installed_db)
    relocation = relocate(installed_db, str(source / "installed" / "custom"), str(output / "installed" / "custom"))
    leftover = {}
    connection = sqlite3.connect(installed_db)
    for table, column in text_columns(connection):
        count = connection.execute(
            f'select count(*) from "{table}" where typeof("{column}") = \'text\' and instr("{column}", ?) > 0',
            (str(source),)).fetchone()[0]
        if count:
            leftover[f"{table}.{column}"] = count
    connection.close()
    if relocation["remainingLiveReferences"] or leftover:
        raise RuntimeError("Database copy still references its source: "
                           + json.dumps({**relocation["remainingLiveReferences"], **leftover}))
    shutil.copytree(source / "installed" / "characters", output / "installed" / "characters")
    (output / "manifest.json").write_text(json.dumps({
        "created": datetime.datetime.now().isoformat(timespec="seconds"), "sourceRoot": str(source),
        "xmlFiles": len(xml_files), "relocation": relocation,
        "characters": sorted(p.name for p in (output / "installed" / "characters").glob("*.dnd5e")),
        "cases": ["installed", "fresh-a", "fresh-b"]}, indent=2))
    print(json.dumps({"output": str(output), "sourceRoot": str(source), "xmlFiles": len(xml_files),
                      "relocation": relocation}, indent=2))


def verify(root: pathlib.Path) -> None:
    before = json.loads((root / "installed-before.json").read_text())
    after = installed_inputs()
    changed = sorted(p for p in before if after.get(p) != before[p])
    added = sorted(p for p in after if p not in before)
    result = {"unchanged": not changed and not added, "changed": changed, "added": added,
              "checked": len(before)}
    (root / "installed-after-verification.json").write_text(json.dumps(result, indent=2))
    print(json.dumps(result, indent=2))
    sys.exit(0 if result["unchanged"] else 1)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--verify", type=pathlib.Path)
    parser.add_argument("--from", dest="source", type=pathlib.Path)
    options = parser.parse_args()
    if options.verify:
        verify(options.verify)
    elif options.source:
        prepare_from(options.source)
    else:
        prepare()
