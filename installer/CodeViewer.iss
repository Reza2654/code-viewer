; Inno Setup Script for Code Viewer
; Builds a professional Windows 64-bit installer with Windows 11 Context Menu integration.

#define MyAppName "Code Viewer"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Code Viewer Open Source Community"
#define MyAppURL "https://github.com"
#define MyAppExeName "CodeViewer.exe"

[Setup]
AppId={{D37F2C0B-9799-4A9B-B1D2-8A73DC88235A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\CodeViewer
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist
OutputBaseFilename=CodeViewer-v1.0-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "win11contextmenu"; Description: "Integrate with Windows 11 right-click context menu"; GroupDescription: "Explorer Integration:"; Flags: checkedonce

[Files]
; Copy all published dist files recursively
Source: "..\dist\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.zip,*.exe,*.iss,checksums.sha256"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Classic Context Menu Fallback
Root: HKCU; Subkey: "Software\Classes\*\shell\OpenWithCodeViewer"; ValueType: string; ValueData: "Open with Code Viewer"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\OpenWithCodeViewer"; ValueName: "Icon"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"""
Root: HKCU; Subkey: "Software\Classes\*\shell\OpenWithCodeViewer\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Windows 11 Modern Context Menu COM Registration
Root: HKCU; Subkey: "Software\Classes\CLSID\{{A47366B9-1784-4E4C-971B-38A99C74E231}"; ValueType: string; ValueData: "Code Viewer Shell Extension"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CLSID\{{A47366B9-1784-4E4C-971B-38A99C74E231}\InprocServer32"; ValueType: string; ValueData: "{app}\CodeViewer.ShellExtension.comhost.dll"
Root: HKCU; Subkey: "Software\Classes\CLSID\{{A47366B9-1784-4E4C-971B-38A99C74E231}\InprocServer32"; ValueName: "ThreadingModel"; ValueType: string; ValueData: "Apartment"

[Run]
; Run Windows 11 Sparse Package registration if task selected
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File ""{app}\scripts\register-windows11-context-menu.ps1"""; Tasks: win11contextmenu; StatusMsg: "Registering Windows 11 Modern Context Menu..."; Flags: runhidden
; Option to launch app after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Unregister Windows 11 context menu prior to file deletion
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File ""{app}\scripts\unregister-windows11-context-menu.ps1"""; Flags: runhidden; RunOnceId: "UnregisterCodeViewerWin11ContextMenu"
