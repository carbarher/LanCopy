; Inno Setup script para LanCopy
; Compilar con: ISCC.exe /DMyAppVersion=1.0.0 installer\LanCopy.iss
; (en CI el tag vX.Y.Z se pasa como MyAppVersion)

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "LanCopy"
#define MyAppPublisher "LanCopy"
#define MyAppExeName "LanCopy.exe"

[Setup]
AppId={{8F3C2A91-6B4D-4E27-9C3A-7D1E5F0A2B44}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=LanCopy-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "firewall"; Description: "Allow LanCopy in Windows Firewall (TCP 8742 and UDP discovery 8743)"; GroupDescription: "Network:"

[Files]
; El publish self-contained genera un unico LanCopy.exe
Source: "..\publish\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Por si el publish no fuese single-file, incluir el resto del contenido
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Reglas de firewall para que la app sea accesible y descubrible en redes privadas.
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""LanCopy TCP 8742"" dir=in action=allow program=""{app}\{#MyAppExeName}"" protocol=TCP localport=8742 profile=private"; Flags: runhidden; Tasks: firewall
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""LanCopy UDP Discovery 8743"" dir=in action=allow program=""{app}\{#MyAppExeName}"" protocol=UDP localport=8743 profile=private"; Flags: runhidden; Tasks: firewall
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""LanCopy TCP 8742"""; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""LanCopy UDP Discovery 8743"""; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""LanCopy"""; Flags: runhidden
