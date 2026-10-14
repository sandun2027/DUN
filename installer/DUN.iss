; Install engine for DUN. scripts/build-release.ps1 builds it with AppVersion and PublishDir (the
; self-contained `dotnet publish` output) and embeds it in the installer window (installer/shell),
; which runs it silently and reads the DUN_PROGRESS lines it writes to its log. Its uninstaller is
; what "Apps > Installed apps" uses.

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
OutputDir=..\dist\engine
OutputBaseFilename=DUN-Engine
SetupIconFile=..\assets\DUN.ico
UninstallDisplayIcon={app}\DUN.exe
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; The installer window stops a running DUN first; this catches anything left holding the files.
CloseApplications=force

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "zh"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
en.AutoStart=Start DUN when Windows starts
zh.AutoStart=开机时自动启动 DUN

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

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM DUN.exe /F"; Flags: runhidden; RunOnceId: "StopDUN"

[Code]
// Progress for the installer window, which tails the setup log. Logged only when the percentage changes.
var
  LastPercent: Integer;

procedure CurInstallProgressChanged(CurProgress, MaxProgress: Integer);
var
  Percent: Integer;
begin
  if MaxProgress < 100 then Exit;
  Percent := CurProgress div (MaxProgress div 100);
  if Percent > 100 then Percent := 100;
  if Percent <> LastPercent then
  begin
    LastPercent := Percent;
    Log('DUN_PROGRESS|' + IntToStr(Percent));
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssDone then Log('DUN_PROGRESS|100');
end;
