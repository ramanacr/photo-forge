; PhotoForge Inno Setup Script
#define MyAppName "PhotoForge"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "PhotoForge Team"
#define MyAppURL "https://github.com/ramanacr/photo-forge"
#define MyAppExeName "PhotoForge.Desktop.exe"

[Setup]
AppId={{D64923C4-F3F8-4A1B-9477-E217F20D9A4C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=PhotoForge-Inno-Setup-v{#MyAppVersion}
Compression=lzma
SolidCompression=yes
SetupIconFile=..\..\apps\PhotoForge.Desktop\app.ico
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "registershell"; Description: "Register Windows Explorer Context Menu verbs"; GroupDescription: "Explorer Integration:"

[Files]
Source: "..\dist\PhotoForge-Windows-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\PhotoForge.Cli.exe"; Parameters: "--register-shell"; Flags: runhidden; Tasks: registershell

[UninstallRun]
Filename: "{app}\PhotoForge.Cli.exe"; Parameters: "--unregister-shell"; Flags: runhidden


