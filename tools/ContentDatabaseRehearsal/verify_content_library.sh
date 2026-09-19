#!/usr/bin/env bash
# Build the rehearsal harness against an Aurora.Content build, run the parity suite with it,
# and compare every result against a baseline root.
# Usage: verify_content_library.sh <baseline-root> <label> [db|characters|all]
# AURORA_CONTENT_SOURCE picks the Translator checkout to build from (default: ../5eApiTranslator
# beside this repo). Set it to "pinned" to use the vendored package pinned in AuroraContent.props.
# Exit code 0 means everything compared matches the baseline.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
baseline="$(cd "$1" && pwd)"
label="$2"
part="${3:-db}"
source="${AURORA_CONTENT_SOURCE:-$repo/../5eApiTranslator}"
work="$repo/buildtmp/content-library-builds/$label"
win() { cygpath -w "$1"; }

switch=()
[ "$source" = pinned ] || switch=(-p:UseLocalContentLibrary=true "-p:AuroraContentSource=$(win "$(cd "$source" && pwd)")")
rm -rf "$work" && mkdir -p "$work"
dotnet build "$here/ContentDatabaseRehearsal.csproj" "${switch[@]}" -o "$work/harness" -v quiet --nologo > "$work/build.log" 2>&1 \
  || { echo "harness build failed (see $work/build.log)"; exit 1; }

rerun_win="$(python "$(win "$here/prepare_baseline.py")" --from "$(win "$baseline")" \
  | python -c 'import json,sys; print(json.load(sys.stdin)["output"])')" || exit 1
rerun="$(cygpath -u "$rerun_win")"
HARNESS_EXE="$work/harness/ContentDatabaseRehearsal.exe" bash "$here/run_parity_suite.sh" "$rerun" "$part" > "$work/suite.log" 2>&1
python "$(win "$here/compare_to_baseline.py")" "$(win "$baseline")" "$(win "$rerun")"
status=$?
echo "rerun root: $rerun_win"
exit $status
