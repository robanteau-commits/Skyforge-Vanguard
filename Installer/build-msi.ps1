# Builds Builds\Installer\SkyforgeVanguard-<version>.msi from the current Unity build in Builds\Windows.
# Needs the WiX Toolset (once, in Command Prompt):  dotnet tool install --global wix --version 5.0.2
#   (and the .NET SDK for that: winget install Microsoft.DotNet.SDK.8)
# Run after making a fresh Windows build:  powershell -ExecutionPolicy Bypass -File Installer\build-msi.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = "$root\Builds\Windows"

if (-not (Test-Path "$build\Skyforge Vanguard.exe")) { throw "No game build found in Builds\Windows - build the game in Unity first (Skyforge > Build > Windows)." }

# Version from Unity's Player Settings (bundleVersion), e.g. 0.7.1. MSI versions must be numbers only.
$version = (Select-String -Path "$root\ProjectSettings\ProjectSettings.asset" -Pattern '^\s*bundleVersion:\s*(.+)$').Matches[0].Groups[1].Value.Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "bundleVersion '$version' isn't in the form 1.2.3, which an MSI needs." }

$wix = Get-Command wix -ErrorAction SilentlyContinue
if (-not $wix) {
    $wix = "$env:USERPROFILE\.dotnet\tools\wix.exe"
    if (-not (Test-Path $wix)) { throw "WiX not found. Install it with: dotnet tool install --global wix --version 5.0.2" }
}

New-Item -ItemType Directory -Force "$root\Builds\Installer" | Out-Null
$out = "$root\Builds\Installer\SkyforgeVanguard-$version.msi"
& $wix build "$PSScriptRoot\SkyforgeVanguard.wxs" -arch x64 -d "Version=$version" -d "BuildDir=$build" -o $out
if ($LASTEXITCODE -ne 0) { throw "WiX failed ($LASTEXITCODE)" }
Remove-Item "$root\Builds\Installer\SkyforgeVanguard-$version.wixpdb" -ErrorAction SilentlyContinue
Write-Host "MSI: $out"
