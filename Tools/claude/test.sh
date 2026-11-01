#!/bin/bash
# Runs the EditMode tests inside the open Unity Editor (the `unity test` CLI only works with the
# Editor closed, and `eval` has a 5 s main-thread limit, so tests run on a worker thread and we poll).
#   Tools/claude/test.sh                      all test classes
#   Tools/claude/test.sh '"TestA","TestB"'    just these SimulationTests methods
# Recompiles first and waits for compilation to finish. With the Editor closed it falls back to the
# `unity test` CLI (slower: it opens the project in batch mode).
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
U="${UNITY_CLI:-/c/Users/Rob/AppData/Local/Unity/bin/unity.exe}"
P="${UNITY_PROJECT:-D:\\Games\\SkyforgeVanguard}"
TMP="$(cygpath -m "$HERE/../../Logs")/claude_test_$$.cs"
cmd() { timeout 60 "$U" command --project-path "$P" "$@" 2>&1; }

if ! cmd eval --result-only -- --code 'return "up";' | grep -q '"up"'; then
  echo "Editor not running: using the unity test CLI."
  XML="$(cygpath -m "$HERE/../../Logs")/claude_tests.xml"
  filter=(); [ $# -ge 1 ] && filter=(--filter "$(echo "$1" | tr -d '"' | tr ',' ';')")
  timeout 1700 "$U" test "$P" --mode EditMode "${filter[@]}" --output "$XML" > /dev/null 2>&1
  python - "$XML" <<'PY2'
import sys, re
s = open(sys.argv[1], encoding='utf-8', errors='ignore').read()
m = re.search(r'<test-run[^>]*total="(\d+)"[^>]*passed="(\d+)"[^>]*failed="(\d+)"', s)
print('total/passed/failed:', m.groups() if m else 'no summary (compile error? see Logs)')
for m in re.finditer(r'<test-case [^>]*name="([^"]*)"[^>]*result="Failed"', s): print('FAIL', m.group(1))
PY2
  exit 0
fi

timeout 100 "$U" command --project-path "$P" recompile --result-only >/dev/null 2>&1; sleep 5
for i in $(seq 1 60); do
  cmd eval --result-only -- --code 'return UnityEditor.EditorApplication.isCompiling ? "busy" : "idle";' | grep -q '"result": "idle"' && break
  sleep 5
done

poll() {
  local r
  for i in $(seq 1 120); do
    r=$(cmd eval --result-only -- --code 'return ((string)System.AppDomain.CurrentDomain.GetData("testres")).Replace("\\","/");' | grep '"result"')
    echo "$r" | grep -q '"running"' || break
    sleep 10
  done
  echo "$r" | sed 's/^ *"result": //'
}

if [ $# -ge 1 ]; then
  sed "s/NAMES/$1/g" "$HERE/one_bg.cs" > "$TMP"
  cmd eval_file --file "$TMP" >/dev/null; poll
else
  for c in SimulationTests StageTests; do
    sed "s/CLASSNAME/$c/g" "$HERE/runtests_bg.cs" > "$TMP"
    cmd eval_file --file "$TMP" >/dev/null; poll
  done
fi
rm -f "$TMP"
