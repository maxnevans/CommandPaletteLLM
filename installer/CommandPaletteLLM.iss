#ifndef AppVersion
  #error AppVersion must be supplied with /DAppVersion=Major.Minor.Patch
#endif

#ifndef Platform
  #error Platform must be supplied with /DPlatform=x64 or /DPlatform=arm64
#endif

#ifndef SourceDir
  #error SourceDir must be supplied with /DSourceDir=path
#endif

#ifndef OutputDir
  #error OutputDir must be supplied with /DOutputDir=path
#endif

#define AppName "Command Palette LLM"
#define AppExeName "CommandPaletteLLM.exe"

[Setup]
AppId={{B22C475A-BDEE-4FFA-AF3D-915D4E86C56A}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=maxnevans
AppPublisherURL=https://github.com/maxnevans/CommandPaletteLLM
AppSupportURL=https://github.com/maxnevans/CommandPaletteLLM/issues
AppUpdatesURL=https://github.com/maxnevans/CommandPaletteLLM/releases
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=maxnevans
VersionInfoDescription={#AppName} installer
VersionInfoProductName={#AppName}
DefaultDirName={localappdata}\Programs\CommandPaletteLLM
DisableDirPage=auto
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=CommandPaletteLLM-Setup-{#AppVersion}-{#Platform}
SetupIconFile=..\CommandPaletteLLM\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
MinVersion=10.0.22000
CloseApplications=force
RestartApplications=no
WizardStyle=modern

#if Platform == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
Root: HKCU; Subkey: "Software\Classes\CLSID\{{B22C475A-BDEE-4FFA-AF3D-915D4E86C56A}"; ValueType: string; ValueName: ""; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CLSID\{{B22C475A-BDEE-4FFA-AF3D-915D4E86C56A}\LocalServer32"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" -RegisterProcessAsComServer"; Flags: uninsdeletekey
