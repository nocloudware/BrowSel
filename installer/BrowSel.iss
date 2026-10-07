; BrowSel installer.
;
; Installs per user, into a fixed folder the app owns: the self updater overwrites that folder in
; place, so it must be writable without asking for administrator rights. Same reason it does not go
; to Program Files.

#define AppName "BrowSel"
#define AppVersion "1.1.2"
#define AppExeName "BrowSel.exe"

[Setup]
AppId={{7B1C4E90-4D2A-4F63-9C1E-2A8F5B3D7E11}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=nocloudware
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=BrowSelSetup-{#AppVersion}
SetupIconFile=..\BrowSel.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
DisableDirPage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
; Everything the publish step produced. Assets\ carries the flag images and the compiled XAML
; files are required, without them the app does not start.
Source: "..\out-rel\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Same keys the app writes itself. They are here so the protocol is registered even when the
; post install step below is skipped.
Root: HKCU; Subkey: "Software\Classes\BrowSelURL"; ValueType: string; ValueName: ""; ValueData: "URL:BrowSelURL"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\BrowSelURL"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\BrowSelURL\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\BrowSelURL\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\BrowSel\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"
Root: HKCU; Subkey: "Software\BrowSel\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Pick the browser and profile for each link"
Root: HKCU; Subkey: "Software\BrowSel\Capabilities"; ValueType: string; ValueName: "AppPath"; ValueData: "{app}\{#AppExeName}"
Root: HKCU; Subkey: "Software\BrowSel\Capabilities\URLAssociations"; ValueType: string; ValueName: "http"; ValueData: "BrowSelURL"
Root: HKCU; Subkey: "Software\BrowSel\Capabilities\URLAssociations"; ValueType: string; ValueName: "https"; ValueData: "BrowSelURL"
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#AppName}"; ValueData: "Software\BrowSel\Capabilities"; Flags: uninsdeletevalue
; Removes the whole BrowSel key on uninstall, settings included, same as the app's --unregister.
; Without this the empty capability keys stay behind and Windows keeps offering BrowSel as a
; handler for links that are no longer there.
Root: HKCU; Subkey: "Software\BrowSel"; Flags: uninsdeletekey

[Run]
; The app registers itself again and opens the Windows page where the default is chosen. Being the
; default is never automatic on Windows, it always has to be confirmed there.
Filename: "{app}\{#AppExeName}"; Parameters: "--register"; Flags: postinstall skipifsilent; Description: "Open Windows Settings to make BrowSel the default browser"