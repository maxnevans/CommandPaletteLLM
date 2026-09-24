#ifndef AppVersion
  #error AppVersion must be supplied with /DAppVersion=Major.Minor.Patch
#endif

#ifndef Platform
  #error Platform must be supplied with /DPlatform=x64 or /DPlatform=arm64
#endif

#ifndef SourceDir
  #error SourceDir must be supplied with /DSourceDir=path
#endif

#ifndef IdentityPackage
  #error IdentityPackage must be supplied with /DIdentityPackage=path
#endif

#ifndef IdentityCertificate
  #error IdentityCertificate must be supplied with /DIdentityCertificate=path
#endif

#ifndef OutputDir
  #error OutputDir must be supplied with /DOutputDir=path
#endif

#define AppName "Command Palette LLM"
#define AppExeName "CommandPaletteLLM.exe"
#define IdentityPackageName "CommandPaletteLLM.identity.msix"
#define IdentityCertificateName "CommandPaletteLLM.identity.cer"

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
DefaultDirName={autopf}\CommandPaletteLLM
DisableDirPage=auto
DisableProgramGroupPage=yes
PrivilegesRequired=admin
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
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "CommandPaletteLLM.identity.msix,CommandPaletteLLM.identity.cer"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#IdentityPackage}"; DestDir: "{app}"; DestName: "{#IdentityPackageName}"; Flags: ignoreversion
Source: "{#IdentityCertificate}"; DestDir: "{app}"; DestName: "{#IdentityCertificateName}"; Flags: ignoreversion
Source: "Register-SparsePackage.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Unregister-SparsePackage.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Register-SparsePackage.ps1"" -PackagePath ""{app}\{#IdentityPackageName}"" -CertificatePath ""{app}\{#IdentityCertificateName}"" -ExternalLocation ""{app}"" -SuccessMarker ""{tmp}\CommandPaletteLLM.sparse.success"" -ErrorFile ""{tmp}\CommandPaletteLLM.sparse.error"""; StatusMsg: "Registering Command Palette extension..."; Flags: runhidden waituntilterminated; BeforeInstall: RemoveSparseRegistrationMarkers; AfterInstall: VerifySparseRegistration

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Unregister-SparsePackage.ps1"""; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterSparsePackage"

[Code]
procedure RemoveSparseRegistrationMarkers;
begin
  DeleteFile(ExpandConstant('{tmp}\CommandPaletteLLM.sparse.success'));
  DeleteFile(ExpandConstant('{tmp}\CommandPaletteLLM.sparse.error'));
end;

procedure VerifySparseRegistration;
var
  ErrorBytes: AnsiString;
  ErrorDetails: String;
begin
  if not FileExists(ExpandConstant('{tmp}\CommandPaletteLLM.sparse.success')) then
  begin
    if LoadStringFromFile(ExpandConstant('{tmp}\CommandPaletteLLM.sparse.error'), ErrorBytes) then
    begin
      ErrorDetails := UTF8Decode(ErrorBytes);
      RaiseException('Windows could not register the Command Palette extension identity:' + #13#10 + #13#10 + ErrorDetails)
    end
    else
      RaiseException('Windows could not register the Command Palette extension identity.');
  end;

  DeleteFile(ExpandConstant('{tmp}\CommandPaletteLLM.sparse.success'));
  DeleteFile(ExpandConstant('{tmp}\CommandPaletteLLM.sparse.error'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  PowerShellArguments: String;
begin
  Result := '';
  PowerShellArguments := '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass ' +
    '-Command "$package = Get-AppxPackage -Name ''maxnevans.CommandPaletteLLM'' -ErrorAction SilentlyContinue; ' +
    'if ($null -ne $package) { exit 10 }"';

  if not Exec(
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    PowerShellArguments,
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) then
  begin
    Result := 'Setup could not check for an existing Microsoft Store installation.';
    Exit;
  end;

  if ResultCode = 10 then
  begin
    Result := 'The Microsoft Store version of Command Palette LLM is already installed.' + #13#10 + #13#10 +
      'Open its settings and export your configuration, uninstall the Store version, and then run this installer again. ' +
      'The Store and community versions cannot be installed together.';
  end
  else if ResultCode <> 0 then
    Result := Format('Setup could not check for an existing Microsoft Store installation (PowerShell exit code %d).', [ResultCode]);
end;
