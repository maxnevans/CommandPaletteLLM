# Set up unsigned Command Palette WinGet publication

This repository follows Microsoft's [Command Palette WinGet publication guide](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension-winget) while publishing unsigned Inno Setup executables. A `vMajor.Minor.Patch` tag starts the WinGet workflow at the same time as the Microsoft Store workflow. The WinGet workflow:

1. tests the tagged commit;
2. publishes self-contained, unpackaged x64 and ARM64 applications;
3. creates unsigned, per-user Inno Setup installers;
4. verifies that exactly two unsigned installers were produced; and
5. attaches them to the matching GitHub Release.

No Azure account, code-signing certificate, signing secret, GitHub environment, or additional repository secret is required. GitHub's automatically provided `GITHUB_TOKEN` creates or updates the release.

The community package identifier is `maxnevans.CommandPaletteLLM`. It is independent of the Store product ID `9PK5TNWKQ00Q`; users must select the `winget` source to use the human-readable identifier.

## Understand the unsigned-installer tradeoff

The WinGet community repository requires MSIX packages to be signed, but this channel uses EXE-based Inno Setup installers. WinGet validates each EXE against the `InstallerSha256` in the manifest and performs security and installation scans during submission.

Unsigned EXE installers still have important user-facing limitations:

- Windows displays the publisher as **Unknown publisher**.
- Microsoft Defender SmartScreen can show **Windows protected your PC**. A user might need **More info** → **Run anyway** when installing outside WinGet.
- Smart App Control or organization policy can block the installer without offering an override.
- Unsigned files cannot carry publisher reputation between versions; each new file hash starts without reputation.

Keep the Microsoft Store package as the recommended installation path for users who do not want to run unsigned software. Microsoft signs the Store package after certification.

References:

- [WinGet installer manifest fields and SHA-256 validation](https://github.com/microsoft/winget-pkgs/blob/master/doc/manifest/schema/1.28.0/installer.md)
- [WinGet community repository validation](https://github.com/microsoft/winget-pkgs/blob/master/doc/Validation.md)
- [Microsoft Defender SmartScreen reputation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)

## Remove obsolete signing configuration

The WinGet workflow does not read any Artifact Signing settings. If an `artifact-signing` GitHub environment or the following values were created only for this workflow, they can be removed:

```text
AZURE_SUBSCRIPTION_ID
ARTIFACT_SIGNING_ENDPOINT
ARTIFACT_SIGNING_ACCOUNT_NAME
ARTIFACT_SIGNING_CERTIFICATE_PROFILE_NAME
```

Do not remove the existing Store publication values:

```text
AZURE_AD_TENANT_ID
AZURE_AD_APPLICATION_CLIENT_ID
AZURE_AD_APPLICATION_SECRET
SELLER_ID
STORE_PRODUCT_ID
```

Those remain necessary for the separate Microsoft Store workflow.

## Test the installers locally

Install [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) and [Inno Setup 6](https://jrsoftware.org/isinfo.php), then run from the repository root:

```powershell
.\scripts\Build-WinGetInstaller.ps1
```

For a local build, the script uses the nearest `vMajor.Minor.Patch` release tag reachable from the current `HEAD`. If the branch has no reachable release tag yet, it warns and falls back to `Version` in `Directory.Build.props`. With no `-Platforms` argument it builds only the current machine's native architecture.

To build both supported architectures locally, run:

```powershell
.\scripts\Build-WinGetInstaller.ps1 -Platforms all
```

The outputs are written to:

```text
artifacts\winget\installer\CommandPaletteLLM-Setup-<version>-<native-platform>.exe
```

Confirm that they are unsigned:

```powershell
Get-ChildItem .\artifacts\winget\installer\*.exe |
  Get-AuthenticodeSignature |
  Select-Object Path, Status
```

Both installer rows should report `NotSigned`.

The installer writes the application under `%LOCALAPPDATA%\Programs\CommandPaletteLLM` and registers the extension's COM local server under HKCU for the current user. Uninstalling removes that registration and the installed application files.

## Publish a GitHub Release

Create the next annotated tag with the release-tag script:

```powershell
# Patch release
.\scripts\New-ReleaseTag.ps1

# Minor release
.\scripts\New-ReleaseTag.ps1 -Minor

# Major release
.\scripts\New-ReleaseTag.ps1 -Major
```

Inspect the tagged commit and push the exact tag printed by the script, for example:

```powershell
git push origin v1.2.3
```

The Store and WinGet workflows start independently from the same tag. The WinGet workflow creates the GitHub Release if it does not exist, or uploads the installers to the existing release.

For `v1.2.3`, the immutable installer URLs are:

```text
https://github.com/maxnevans/CommandPaletteLLM/releases/download/v1.2.3/CommandPaletteLLM-Setup-1.2.3-x64.exe
https://github.com/maxnevans/CommandPaletteLLM/releases/download/v1.2.3/CommandPaletteLLM-Setup-1.2.3-arm64.exe
```

After the workflow completes:

1. Open the repository on GitHub.
2. Select **Actions** → **Publish WinGet installers**.
3. Open the run associated with the release tag and confirm every step is green.
4. Open **Releases** and select the release.
5. Confirm both architecture-specific installers are present.
6. Download them and recompute their hashes if preparing a manifest manually.

Never replace a release installer after its WinGet manifest is submitted. WinGet pins the exact SHA-256 value; publish a new version instead.

## Submit the first community WinGet manifest

Microsoft requires the first submission to be interactive. Install WingetCreate:

```powershell
winget install --id Microsoft.WingetCreate --source winget
wingetcreate --version
```

Run it against both GitHub Release asset URLs:

```powershell
wingetcreate new `
  "https://github.com/maxnevans/CommandPaletteLLM/releases/download/v1.2.3/CommandPaletteLLM-Setup-1.2.3-x64.exe" `
  "https://github.com/maxnevans/CommandPaletteLLM/releases/download/v1.2.3/CommandPaletteLLM-Setup-1.2.3-arm64.exe"
```

Review the generated manifests before submission. They must contain:

- `PackageIdentifier: maxnevans.CommandPaletteLLM`;
- `PackageVersion` matching the tag without the leading `v`;
- `License: Proprietary`;
- `InstallerType: inno`;
- `Scope: user`;
- `ElevationRequirement: elevationProhibited` because installation and COM registration are per-user;
- x64 and ARM64 installer entries with the correct architecture, URL, and SHA-256 hash; and
- the following entry in every locale manifest:

```yaml
Tags:
- windows-commandpalette-extension
```

Do not add `SignatureSha256`; it applies to the signature inside an MSIX and these installers are unsigned EXEs.

The project does not reference Windows App SDK, so do not add a `Microsoft.WindowsAppRuntime` package dependency. Microsoft's Command Palette guidance requires that dependency only when an extension uses Windows App SDK.

Let WingetCreate submit the pull request to `microsoft/winget-pkgs`, then follow validation and reviewer feedback. The service checks the SHA-256 hashes, scans the installers, performs silent installation and uninstall tests, and verifies the declared architecture and installer behavior.

After the pull request merges, verify:

```powershell
winget show --id maxnevans.CommandPaletteLLM --source winget
winget install --id maxnevans.CommandPaletteLLM --source winget
```

Run the installation from a non-elevated terminal because the package is per-user and its manifest prohibits elevation.

## Settings when switching channels

The Store package and unpackaged WinGet installer use different application-data locations. Export settings before switching installation channels, uninstall the old channel, install the new one, and import the backup. Avoid keeping both variants installed because both expose the same extension CLSID.
