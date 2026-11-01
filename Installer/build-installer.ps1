# Builds Builds\Installer\SkyforgeVanguardSetup-<version>.exe from the current Unity build in Builds\Windows.
# Run after making a fresh Windows build:  powershell -ExecutionPolicy Bypass -File Installer\build-installer.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

if (-not (Test-Path "$root\Builds\Windows\Skyforge Vanguard.exe")) { throw "No game build found in Builds\Windows - build the game in Unity first (Skyforge > Build > Windows)." }

# Version from Unity's Player Settings (bundleVersion).
$version = (Select-String -Path "$root\ProjectSettings\ProjectSettings.asset" -Pattern '^\s*bundleVersion:\s*(.+)$').Matches[0].Groups[1].Value.Trim()

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup" }

& $iscc "/DAppVersion=$version" "$PSScriptRoot\SkyforgeVanguard.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed ($LASTEXITCODE)" }
Write-Host "Installer: $root\Builds\Installer\SkyforgeVanguardSetup-$version.exe"
