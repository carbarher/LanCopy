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
Name: "firewall"; Description: "Permitir LanCopy en el Firewall de Windows (puerto 8742)"; GroupDescription: "Red:"

[Files]
; El publish self-contained genera un unico LanCopy.exe
Source: "..\publish\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Por si el publish no fuese single-file, incluir el resto del contenido
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Regla de firewall (TCP 8742) para que la app sea accesible en la LAN
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""LanCopy"" dir=in action=allow protocol=TCP localport=8742"; Flags: runhidden; Tasks: firewall
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""LanCopy"""; Flags: runhidden