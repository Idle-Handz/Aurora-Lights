"""Copy installed XML and a consistent SQLite backup into isolated rehearsal cases."""
import datetime
import hashlib
import json
import pathlib
import shutil
import sqlite3
import uuid
import argparse

arguments = argparse.ArgumentParser()
arguments.add_argument("--exclude-draft", action="append", default=[],
    help="Filename of an explicitly retired draft (for example efa-class.xml).")
options = arguments.parse_args()

repo = pathlib.Path(__file__).resolve().parents[2]
live = pathlib.Path.home() / "Documents/5e Character Builder/custom"
alias_evidence = repo.parent / "5eApiTranslator/artifacts/append-full-validation/aliases/alias-evidence.json"
output = repo / "buildtmp" / ("content-rehearsal-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S") + "-" + uuid.uuid4().hex[:6])
assert output.resolve().is_relative_to((repo / "buildtmp").resolve())
output.mkdir(parents=True)
digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest().upper()
files = sorted(live.rglob("*.xml"))
before = {str(p): digest(p) for p in files}
database = live / "aurora-elements.sqlite"
before[str(database)] = digest(database)
(output / "installed-before.json").write_text(json.dumps(before, indent=2))
with sqlite3.connect(database.as_uri() + "?mode=ro", uri=True) as src:
    with sqlite3.connect(output / "installed-backup.sqlite") as dst:
        src.backup(dst)
    metadata = dict(zip([c[0] for c in src.execute("SELECT * FROM database_metadata").description],
                        src.execute("SELECT * FROM database_metadata").fetchone()))
plans = json.loads(alias_evidence.read_text(encoding="utf-8-sig"))
unknown = set(options.exclude_draft) - {pathlib.Path(p["localPath"]).name for p in plans}
if unknown:
    raise RuntimeError("Unknown excluded drafts: " + ", ".join(sorted(unknown)))
plans = [p for p in plans if pathlib.Path(p["localPath"]).name not in options.exclude_draft]
for plan in plans:
    for field, expected in [("localPath", "originalLocalHash"), ("sourcePath", "originalSourceHash"), ("outputPath", "outputHash")]:
        if digest(pathlib.Path(plan[field])) != plan[expected]:
            raise RuntimeError("Alias draft no longer matches its input: " + plan[field])
cases = ["installed-v" + str(metadata["data_version"]), "migration-v12", "fresh-v12", "legacy-v10"]
for case in cases:
    case_dir = output / case
    content = case_dir / "custom"
    content.mkdir(parents=True)
    (case_dir / ".aurora-rehearsal").write_text("Disposable copy, created by prepare.py.\n")
    for path in files:
        target = content / path.relative_to(live)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
    if case.startswith("installed") or case == "migration-v12":
        shutil.copy2(output / "installed-backup.sqlite", content / database.name)
    if case in ["migration-v12", "fresh-v12"]:
        for plan in plans:
            target = content / pathlib.Path(plan["localPath"]).relative_to(live)
            shutil.copy2(plan["outputPath"], target)
(output / "manifest.json").write_text(json.dumps({
    "live": str(live), "xmlFiles": len(files), "originalMetadata": metadata,
    "cases": cases, "aliasEvidence": str(alias_evidence),
    "excludedDrafts": options.exclude_draft,
    "aliases": [{k: p[k] for k in ["localPath", "outputPath", "outputHash", "aliases"]} for p in plans]
}, indent=2))
print(json.dumps({"output": str(output), "files": len(files), "metadata": metadata, "cases": cases}, indent=2))
