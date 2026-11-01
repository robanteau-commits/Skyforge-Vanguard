#!/bin/bash
# Builds a player through the open Editor:  bash Tools/claude/build.sh [windows|web|android]
#   windows (default): Builds/Windows/Skyforge Vanguard.exe, then the installer if Inno Setup is installed
#   web:               Builds/Web (WebGL; Gzip with decompression fallback, runs on any host)
#   android:           Builds/Android/SkyforgeVanguard.apk (debug-signed unless SKYFORGE_KEYSTORE is set)
# Platform settings (Android id, IL2CPP, portrait, WebGL compression) are applied by
# Assets/Skyforge/Editor/SkyforgeBuild.cs before every build, however it's started.
set -u
case "${1:-windows}" in
  windows) TARGET=StandaloneWindows64; OUT="D:/Games/SkyforgeVanguard/Builds/Windows/Skyforge Vanguard.exe" ;;
  web)     TARGET=WebGL;               OUT="D:/Games/SkyforgeVanguard/Builds/Web" ;;
  android) TARGET=Android;             OUT="D:/Games/SkyforgeVanguard/Builds/Android/SkyforgeVanguard.apk" ;;
  *) echo "usage: build.sh [windows|web|android]"; exit 2 ;;
esac
U="${UNITY_CLI:-/c/Users/Rob/AppData/Local/Unity/bin/unity.exe}"
P="${UNITY_PROJECT:-D:\\Games\\SkyforgeVanguard}"

# Remember the previous build's end time: if the Editor refuses a new build (busy compiling, in play
# mode, already building), build_status keeps reporting the old one.
prev=$(timeout 30 "$U" command --project-path "$P" build_status --result-only 2>&1 | grep -m1 '"buildEndedAt"')
"$U" command --project-path "$P" build --target "$TARGET" \
  --outputPath "$OUT" --confirm true >/dev/null 2>&1
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

if [ "$TARGET" = StandaloneWindows64 ]; then
  powershell.exe -ExecutionPolicy Bypass -File "D:\\Games\\SkyforgeVanguard\\Installer\\build-installer.ps1" || echo "(installer skipped)"
  powershell.exe -ExecutionPolicy Bypass -File "D:\\Games\\SkyforgeVanguard\\Installer\\build-msi.ps1" || echo "(MSI skipped)"
fi
