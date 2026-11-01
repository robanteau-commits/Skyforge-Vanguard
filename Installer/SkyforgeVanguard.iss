; Inno Setup script for the Windows installer (SkyforgeVanguardSetup-<version>.exe).
; Build it with Installer\build-installer.ps1, which passes the version from Unity's Player Settings.
; Packages Builds\Windows (the Unity player build) except Unity's debug-symbol folders.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#define AppName "Skyforge Vanguard"
#define AppExe "Skyforge Vanguard.exe"
#define BuildDir "..\Builds\Windows"

[Setup]
; Never change AppId: it's how Windows recognises a newer installer as an upgrade of this game.
AppId={{A0D5555E-66E8-4D1B-9B63-4D15EAB29431}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Rob Anteau
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\Builds\Installer
OutputBaseFilename=SkyforgeVanguardSetup-{#AppVersion}
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*_BackUpThisFolder_ButDontShipItWithYourGame,*_BurstDebugInformation_DoNotShip"

[InstallDelete]
; Clear out files a previous version shipped that this one no longer does.
Type: filesandordirs; Name: "{app}\Skyforge Vanguard_Data"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
