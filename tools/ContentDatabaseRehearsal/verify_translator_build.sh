#!/usr/bin/env bash
# Publish a Translator build, run the database parity suite through the app's refresh
# path with it, and compare every result against a baseline root.
# Usage: verify_translator_build.sh <baseline-root> <label> [translator-csproj]
# Exit code 0 means databases, projections and checks all match the baseline.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
baseline="$(cd "$1" && pwd)"
label="$2"
proj="${3:-$repo/../5eApiTranslator/5eApiTranslator/AuroraTranslator.csproj}"
work="$repo/buildtmp/translator-builds/$label"
win() { cygpath -w "$1"; }

rm -rf "$work" && mkdir -p "$work"
dotnet publish "$proj" -c Release -r win-x64 --self-contained false -o "$work/translator" --nologo -v quiet || exit 1
dotnet build "$here/ContentDatabaseRehearsal.csproj" -v quiet > /dev/null || exit 1
cp -r "$here/bin/Debug/net10.0" "$work/harness"
rm -rf "$work/harness/BundledTools/AuroraTranslator"
cp -r "$work/translator" "$work/harness/BundledTools/AuroraTranslator"

rerun_win="$(python "$(win "$here/prepare_baseline.py")" --from "$(win "$baseline")" \
  | python -c 'import json,sys; print(json.load(sys.stdin)["output"])')" || exit 1
rerun="$(cygpath -u "$rerun_win")"
HARNESS_EXE="$work/harness/ContentDatabaseRehearsal.exe" bash "$here/run_parity_suite.sh" "$rerun" db

status=0
for c in fresh-a fresh-b; do
  if python "$(win "$here/compare_databases.py")" "$(win "$baseline/results/$c.sqlite")" "$(win "$rerun/results/$c.sqlite")" \
      --root-a "$(win "$baseline/$c/custom")" --root-b "$(win "$rerun/$c/custom")" \
      --ignore "*.created_utc" --ignore "database_metadata.built_utc" \
      --out "$(win "$rerun/results/compare-$c.json")" > /dev/null; then
    echo "$c database identical to baseline: yes"
  else
    echo "$c database identical to baseline: NO (see results/compare-$c.json)"; status=1
  fi
done

python - "$(win "$baseline/results")" "$(win "$rerun/results")" <<'EOF' || status=1
import json, os, sys
base, rerun = sys.argv[1], sys.argv[2]
load = lambda root, name: json.load(open(os.path.join(root, name), encoding="utf-8"))
ok = True
for name in ["fresh-a-projection.json", "installed-projection.json"]:
    same = load(base, name) == load(rerun, name)
    print(f"{name} identical to baseline: {'yes' if same else 'NO'}")
    ok &= same
for name in ["installed-scan.json", "fresh-a-refresh.json", "fresh-a-parity.json",
             "fresh-a-fallback-check.json", "fresh-b-failure-check.json"]:
    a, b = load(base, name), load(rerun, name)
    same = a["success"] == b["success"] and \
        sorted(w["Message"] for w in a["warnings"]) == sorted(w["Message"] for w in b["warnings"])
    print(f"{name}: success={b['success']}, matches baseline: {'yes' if same else 'NO'}")
    ok &= same
sys.exit(0 if ok else 1)
EOF
echo "rerun root: $rerun_win"
exit $status
