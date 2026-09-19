#!/usr/bin/env bash
# Run the parity rehearsal suite against a root created by prepare_baseline.py.
# Usage: run_parity_suite.sh <baseline-root> [db|characters|all]
# Results (JSON, logs, database copies, summary.txt) are collected in <root>/results.
# Set HARNESS_EXE to run a copy of the harness whose BundledTools holds a different Translator.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$1" && pwd)"
part="${2:-all}"
exe="${HARNESS_EXE:-$here/bin/Debug/net10.0/ContentDatabaseRehearsal.exe}"
results="$root/results"
mkdir -p "$results/characters"
win() { cygpath -w "$1"; }

run() {  # run <mode> <case> [extra argument]
  local mode=$1 case=$2 extra=${3:-}
  local tag="$case-$mode${extra:+-$extra}" start=$SECONDS code
  if [ -n "$extra" ]; then
    timeout 1200 "$exe" "$mode" "$(win "$root/$case")" "$extra" > /dev/null 2>&1; code=$?
  else
    timeout 1200 "$exe" "$mode" "$(win "$root/$case")" > /dev/null 2>&1; code=$?
  fi
  cp "$root/$case/$mode-result.json" "$results/$tag.json" 2>/dev/null
  cp "$root/$case/$mode-app.log" "$results/$tag.log" 2>/dev/null
  echo "$tag exit=$code seconds=$((SECONDS - start))" | tee -a "$results/summary.txt"
}

if [ "$part" = db ] || [ "$part" = all ]; then
  run scan installed
  run refresh fresh-a
  cp "$root/fresh-a/custom/aurora-elements.sqlite" "$results/fresh-a.sqlite"
  run refresh fresh-b
  cp "$root/fresh-b/custom/aurora-elements.sqlite" "$results/fresh-b.sqlite"
  run scan fresh-a
  run dump installed; mv "$root/installed/projection-dump.json" "$results/installed-projection.json"
  run dump fresh-a;   mv "$root/fresh-a/projection-dump.json" "$results/fresh-a-projection.json"
  run parity fresh-a
  run fallback-check fresh-a
  run failure-check fresh-b
fi

if [ "$part" = characters ] || [ "$part" = all ]; then
  for file in "$root/installed/characters"/*.dnd5e; do
    name="$(basename "$file")"
    start=$SECONDS
    timeout 300 "$exe" characters "$(win "$root/installed")" "$name" > /dev/null 2>&1; code=$?
    mv "$root/installed/characters-result.json" "$results/characters/$name.json" 2>/dev/null
    mv "$root/installed/characters-app.log" "$results/characters/$name.log" 2>/dev/null
    echo "character $name exit=$code seconds=$((SECONDS - start))" | tee -a "$results/summary.txt"
  done
fi
