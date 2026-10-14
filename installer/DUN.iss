; Inno Setup script for DUN. Built by scripts/build-release.ps1, which passes
; AppVersion and PublishDir (the self-contained `dotnet publish` output).

#define AppName "DUN"
#ifndef AppVersion
  #define AppVersion "0.2.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\dist\publish"
#endif

[Setup]
AppId={{8F3A6C2E-5B1D-4E7A-9C44-2D6E1B7F0A93}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Sandun
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Installs for the current user without admin rights; the user can choose all users instead.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=DUN-Setup-{#AppVersion}
SetupIconFile=..\assets\DUN.ico
UninstallDisplayIcon={app}\DUN.exe
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; Matches the single-instance mutex in Program.cs, so setup asks to close a running copy.
AppMutex=Local\DUN.SingleInstance
CloseApplications=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "zh"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
en.AutoStart=Start DUN when Windows starts
zh.AutoStart=开机时自动启动 DUN
en.LaunchNow=Launch DUN
zh.LaunchNow=立即运行 DUN

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "{cm:AutoStart}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\DUN.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\DUN.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DUN"; ValueData: """{app}\DUN.exe"""; Flags: uninsdeletevalue; Tasks: autostart
; The app can also turn autostart on itself; always remove that entry on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "DUN"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\DUN.exe"; Description: "{cm:LaunchNow}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM DUN.exe /F"; Flags: runhidden; RunOnceId: "StopDUN"
