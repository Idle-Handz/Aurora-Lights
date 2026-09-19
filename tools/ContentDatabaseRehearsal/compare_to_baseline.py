"""Compare a parity rerun root against a baseline root.

Usage: python compare_to_baseline.py <baseline-root> <rerun-root>

Checks everything present in both roots' results folders: fresh databases (row by row,
ignoring build timestamps), loaded projections, rehearsal checks (success and warning text),
and every character (load results, captured state, choices, inventory and diagnostics).
Exit code 0 means every compared item matches.
"""
import json
import pathlib
import subprocess
import sys

CHECKS = ["installed-scan.json", "fresh-a-refresh.json", "fresh-a-parity.json",
          "fresh-a-fallback-check.json", "fresh-b-failure-check.json"]


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def messages(report):
    return sorted(w["Message"] for w in report.get("warnings", []))


def normalize(value, roots):
    if isinstance(value, str):
        for root in roots:
            value = value.replace(root, "<root>")
        return value
    if isinstance(value, list):
        return [normalize(v, roots) for v in value]
    if isinstance(value, dict):
        return {k: normalize(v, roots) for k, v in value.items()}
    return value


def first_difference(a, b, path=""):
    if type(a) is not type(b):
        return path or "<root>"
    if isinstance(a, dict):
        for key in sorted(set(a) | set(b)):
            if key not in a or key not in b:
                return f"{path}.{key}"
            found = first_difference(a[key], b[key], f"{path}.{key}")
            if found:
                return found
        return None
    if isinstance(a, list):
        if len(a) != len(b):
            return f"{path}[len {len(a)} vs {len(b)}]"
        for i, (x, y) in enumerate(zip(a, b)):
            found = first_difference(x, y, f"{path}[{i}]")
            if found:
                return found
        return None
    return None if a == b else path or "<root>"


def main():
    baseline, rerun = (pathlib.Path(p).resolve() for p in sys.argv[1:3])
    base_results, new_results = baseline / "results", rerun / "results"
    roots = [str(baseline), str(rerun)]
    ok = True

    for case in ["fresh-a", "fresh-b"]:
        a, b = base_results / f"{case}.sqlite", new_results / f"{case}.sqlite"
        if not (a.exists() and b.exists()):
            continue
        same = subprocess.run([sys.executable, str(pathlib.Path(__file__).with_name("compare_databases.py")),
                               str(a), str(b), "--root-a", str(baseline / case / "custom"),
                               "--root-b", str(rerun / case / "custom"), "--ignore", "*.created_utc",
                               "--ignore", "database_metadata.built_utc",
                               "--out", str(new_results / f"compare-{case}.json")],
                              capture_output=True).returncode == 0
        print(f"database {case}: {'match' if same else 'DIFFERENT (see results/compare-' + case + '.json)'}")
        ok &= same

    for name in ["fresh-a-projection.json", "installed-projection.json"]:
        a, b = base_results / name, new_results / name
        if a.exists() and b.exists():
            same = load(a) == load(b)
            print(f"projection {name}: {'match' if same else 'DIFFERENT'}")
            ok &= same

    for name in CHECKS:
        a, b = base_results / name, new_results / name
        if a.exists() and b.exists():
            x, y = load(a), load(b)
            same = x["success"] == y["success"] and messages(x) == messages(y)
            print(f"check {name}: success={y['success']} {'match' if same else 'DIFFERENT'}")
            ok &= same

    base_chars = {p.name: p for p in (base_results / "characters").glob("*.json")}
    new_chars = {p.name: p for p in (new_results / "characters").glob("*.json")}
    if base_chars and new_chars:
        differing = []
        for name in sorted(base_chars):
            if name not in new_chars:
                differing.append((name, "missing from rerun"))
                continue
            x, y = load(base_chars[name]), load(new_chars[name])
            a = normalize({"result": x.get("result"), "warnings": messages(x)}, roots)
            b = normalize({"result": y.get("result"), "warnings": messages(y)}, roots)
            where = first_difference(a, b)
            if where:
                differing.append((name, where))
        extra = sorted(set(new_chars) - set(base_chars))
        print(f"characters: {len(base_chars) - len(differing)}/{len(base_chars)} match baseline"
              + (f", {len(extra)} extra in rerun" if extra else ""))
        for name, where in differing:
            print(f"  DIFFERENT {name}: first difference at {where}")
        ok &= not differing

    print("OVERALL:", "match" if ok else "DIFFERENT")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
