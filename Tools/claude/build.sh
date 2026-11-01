#!/bin/bash
# Builds the Windows player through the open Editor.
#   Output: Builds/Windows/Skyforge Vanguard.exe
set -u
U="${UNITY_CLI:-/c/Users/Rob/AppData/Local/Unity/bin/unity.exe}"
P="${UNITY_PROJECT:-D:\\Games\\SkyforgeVanguard}"

# Remember the previous build's end time: if the Editor refuses a new build (busy compiling, in play
# mode, already building), build_status keeps reporting the old one.
prev=$(timeout 30 "$U" command --project-path "$P" build_status --result-only 2>&1 | grep -m1 '"buildEndedAt"')
"$U" command --project-path "$P" build --target StandaloneWindows64 \
  --outputPath "D:/Games/SkyforgeVanguard/Builds/Windows/Skyforge Vanguard.exe" --confirm true >/dev/null 2>&1
sleep 10
for i in $(seq 1 100); do
  r=$(timeout 30 "$U" command --project-path "$P" build_status --result-only 2>&1)
  echo "$r" | grep -m1 '"status"' | grep -qi 'running\|queued\|building' || break
  sleep 15
done
echo "$r" | grep -m3 '"result"\|"totalErrors"\|"buildEndedAt"'
echo "$r" | grep -q '"Succeeded"' || { echo "Player build failed."; exit 1; }
now=$(echo "$r" | grep -m1 '"buildEndedAt"')
if [ -n "$prev" ] && [ "$now" = "$prev" ]; then echo "No new player build ran (status is the previous build's)."; exit 1; fi
