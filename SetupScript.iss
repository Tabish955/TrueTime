; Inno Setup Script for TrueTime Professional
; Fully rebranded, high-trust production installer

[Setup]
AppId={{D3F65A8C-8891-4AE1-827F-F3A06B491295}
AppName=TrueTime Professional
AppVersion=1.0.0
AppPublisher=TrueTime Software
AppPublisherURL=https://github.com/TrueTime
AppSupportURL=https://github.com/TrueTime
AppUpdatesURL=https://github.com/TrueTime
DefaultDirName={autopf}\TrueTime
DefaultGroupName=TrueTime Professional
OutputBaseFilename=TrueTimeSetup
OutputDir=.\Output
Compression=lzma
SolidCompression=no
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\app.ico
WizardStyle=modern
CloseApplications=yes

; Complete Version Information
VersionInfoVersion=1.0.0.0
VersionInfoCompany=TrueTime Software
VersionInfoDescription=TrueTime Professional Setup
VersionInfoCopyright=Copyright (C) 2026 TrueTime Software
VersionInfoProductName=TrueTime Professional
VersionInfoProductVersion=1.0.0.0

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\Service\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "publish\GUI\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\TrueTime Professional\TrueTime"; Filename: "{app}\TrueTime.Tray.exe"; IconFilename: "{app}\app.ico"
Name: "{autoprograms}\TrueTime Professional\Uninstall TrueTime"; Filename: "{uninstallexe}"
Name: "{autodesktop}\TrueTime"; Filename: "{app}\TrueTime.Tray.exe"; IconFilename: "{app}\app.ico"; Tasks: desktopicon
Name: "{autostartup}\TrueTime"; Filename: "{app}\TrueTime.Tray.exe"; IconFilename: "{app}\app.ico"

[Registry]
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TrueTimeTray"; ValueData: """{app}\TrueTime.Tray.exe"""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TrueTimeTray"; ValueData: """{app}\TrueTime.Tray.exe"""; Flags: uninsdeletevalue

[Run]
; Register and configure TrueTimeService cleanly via native Win32 Service Manager
Filename: "{app}\TrueTimeService.exe"; Parameters: "--install"; Flags: runhidden

; Launch the GUI tray monitor
Filename: "{app}\TrueTime.Tray.exe"; Description: "Launch TrueTime System Tray Monitor"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    ExecAsOriginalUser(ExpandConstant('{app}\TrueTime.Tray.exe'), '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
  end;
end;

[UninstallRun]
; Gracefully stop and remove TrueTimeService natively
Filename: "{app}\TrueTimeService.exe"; Parameters: "--uninstall"; Flags: runhidden; RunOnceId: "UninstallTrueTimeService"

[UninstallDelete]
Type: files; Name: "{app}\*.log"
Type: files; Name: "{app}\*.tmp"
Type: filesandordirs; Name: "{app}\logs"
