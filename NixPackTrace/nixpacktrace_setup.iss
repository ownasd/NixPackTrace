[Setup]
AppName=NixPackTrace
AppVersion=1.1
AppPublisher=Nix Enterprise
DefaultDirName={autopf}\NixPackTrace
DefaultGroupName=NixPackTrace
OutputDir=..\Publish
OutputBaseFilename=NixPackTrace_Setup
SetupIconFile=app.ico
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\NixPackTrace"; Filename: "{app}\NixPackTrace.exe"; IconFilename: "{app}\NixPackTrace.exe"
Name: "{commondesktop}\NixPackTrace"; Filename: "{app}\NixPackTrace.exe"; Tasks: desktopicon; IconFilename: "{app}\NixPackTrace.exe"

[Run]
Filename: "{app}\NixPackTrace.exe"; Description: "{cm:LaunchProgram,NixPackTrace}"; Flags: nowait postinstall skipifsilent

