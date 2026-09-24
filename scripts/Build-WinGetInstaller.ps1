[CmdletBinding()]
param(
    [ValidatePattern('^(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)$')]
    [string] $Version,

    [ValidateSet('x64', 'arm64', 'all')]
    [string[]] $Platforms,

    [switch] $StageOnly,

    [switch] $PackageOnly,

    [string] $OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($StageOnly -and $PackageOnly) {
    throw 'StageOnly and PackageOnly cannot be used together.'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'CommandPaletteLLM\CommandPaletteLLM.csproj'
$installerScript = Join-Path $repositoryRoot 'installer\CommandPaletteLLM.iss'
$sparseManifestSource = Join-Path $repositoryRoot 'installer\Package.Sparse.appxmanifest'
$fusionManifestSource = Join-Path $repositoryRoot 'CommandPaletteLLM\app.WinGet.manifest'

$identityPublisher = 'CN=CommandPaletteLLM Community Package'
[xml] $sparseManifestTemplate = Get-Content -Raw -LiteralPath $sparseManifestSource
[xml] $fusionManifest = Get-Content -Raw -LiteralPath $fusionManifestSource
$fusionNamespaces = [System.Xml.XmlNamespaceManager]::new($fusionManifest.NameTable)
$fusionNamespaces.AddNamespace('asm', 'urn:schemas-microsoft-com:asm.v1')
$fusionNamespaces.AddNamespace('msix', 'urn:schemas-microsoft-com:msix.v1')
$fusionIdentity = $fusionManifest.SelectSingleNode('/asm:assembly/msix:msix', $fusionNamespaces)
$sparseIdentity = $sparseManifestTemplate.Package.Identity
$sparseApplication = $sparseManifestTemplate.Package.Applications.Application

if ($null -eq $fusionIdentity -or
    $sparseIdentity.Publisher -ne $identityPublisher -or
    $fusionIdentity.publisher -ne $sparseIdentity.Publisher -or
    $fusionIdentity.packageName -ne $sparseIdentity.Name -or
    $fusionIdentity.applicationId -ne $sparseApplication.Id) {
    throw 'The executable and sparse package identities do not match the community package identity.'
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $releaseTag = & git -C $repositoryRoot describe --tags --abbrev=0 --match 'v[0-9]*.[0-9]*.[0-9]*' 2>$null
    if ($LASTEXITCODE -eq 0 -and $releaseTag -match '^v(?<version>(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*))$') {
        $Version = $Matches.version
        Write-Host "Using version $Version from the nearest release tag '$releaseTag' reachable from HEAD."
    }
    else {
        $versionFile = Join-Path $repositoryRoot 'Directory.Build.props'
        [xml] $buildProperties = Get-Content -Raw -LiteralPath $versionFile
        $projectVersion = @($buildProperties.Project.PropertyGroup.Version) |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -First 1

        if ($null -eq $projectVersion -or [string] $projectVersion -notmatch '^(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)$') {
            throw 'No reachable vMajor.Minor.Patch release tag was found and Directory.Build.props does not contain a valid fallback Version.'
        }

        $Version = [string] $projectVersion
        Write-Warning "No reachable vMajor.Minor.Patch release tag was found. Using fallback version $Version from Directory.Build.props."
    }
}

if ($null -eq $Platforms -or $Platforms.Count -eq 0) {
    $Platforms = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
        ([System.Runtime.InteropServices.Architecture]::X64) { @('x64') }
        ([System.Runtime.InteropServices.Architecture]::Arm64) { @('arm64') }
        default {
            throw "The native architecture '$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)' is not supported. Supply -Platforms x64, arm64, or all explicitly."
        }
    }
}
elseif ($Platforms -contains 'all') {
    if ($Platforms.Count -ne 1) {
        throw "Use '-Platforms all' by itself; it cannot be combined with individual platforms."
    }

    $Platforms = @('x64', 'arm64')
}

Write-Host "Building platform(s): $($Platforms -join ', ')."

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot 'artifacts\winget'
}

$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$publishRoot = Join-Path $OutputRoot 'publish'
$intermediateRoot = Join-Path $OutputRoot 'intermediate'
$installerOutput = Join-Path $OutputRoot 'installer'
$fileVersion = "$Version.0"

$components = $Version.Split('.') | ForEach-Object { [int] $_ }
if (@($components | Where-Object { $_ -gt 65535 }).Count -ne 0) {
    throw "Version '$Version' cannot be represented as a Windows file version."
}

if (-not $PackageOnly) {
    $sdkBinRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $sdkVersionDirectories = Get-ChildItem -LiteralPath $sdkBinRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [version] $_.Name } -Descending

    $makeAppxCommand = Get-Command makeappx.exe -ErrorAction SilentlyContinue
    $makeAppx = if ($null -ne $makeAppxCommand) {
        $makeAppxCommand.Source
    }
    else {
        $sdkVersionDirectories |
            ForEach-Object { Join-Path $_.FullName 'x64\makeappx.exe' } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            Select-Object -First 1
    }

    if ([string]::IsNullOrWhiteSpace($makeAppx)) {
        throw 'MakeAppx.exe was not found. Install the Windows SDK MSIX tools.'
    }

    $signToolCommand = Get-Command signtool.exe -ErrorAction SilentlyContinue
    $signTool = if ($null -ne $signToolCommand) {
        $signToolCommand.Source
    }
    else {
        $sdkVersionDirectories |
            ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            Select-Object -First 1
    }

    if ([string]::IsNullOrWhiteSpace($signTool)) {
        throw 'SignTool.exe was not found. Install the Windows SDK signing tools.'
    }

    $buildCertificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $identityPublisher `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable `
        -NotAfter (Get-Date).AddYears(10)

    try {
        foreach ($platform in $Platforms) {
            $runtimeIdentifier = "win-$platform"
            $publishDirectory = Join-Path $publishRoot $runtimeIdentifier
            $sparseDirectory = Join-Path $intermediateRoot "sparse-$platform"

            if (Test-Path -LiteralPath $publishDirectory) {
                Remove-Item -LiteralPath $publishDirectory -Recurse -Force
            }

            if (Test-Path -LiteralPath $sparseDirectory) {
                Remove-Item -LiteralPath $sparseDirectory -Recurse -Force
            }

            New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
            New-Item -ItemType Directory -Path $sparseDirectory -Force | Out-Null

            $publishArguments = @(
                'publish'
                $projectPath
                '--configuration', 'Release'
                '--runtime', $runtimeIdentifier
                '--self-contained', 'true'
                '--output', $publishDirectory
                "-p:Platform=$platform"
                '-p:WinGetPackage=true'
                "-p:Version=$Version"
                "-p:AssemblyVersion=$fileVersion"
                "-p:FileVersion=$fileVersion"
            )

            & dotnet @publishArguments
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet publish failed for $platform with exit code $LASTEXITCODE."
            }

            $applicationPath = Join-Path $publishDirectory 'CommandPaletteLLM.exe'
            if (-not (Test-Path -LiteralPath $applicationPath -PathType Leaf)) {
                throw "The unpackaged application was not created at '$applicationPath'."
            }

            [xml] $sparseManifest = Get-Content -Raw -LiteralPath $sparseManifestSource
            $sparseManifest.Package.Identity.Version = $fileVersion
            $sparseManifestPath = Join-Path $sparseDirectory 'AppxManifest.xml'
            $sparseManifest.Save($sparseManifestPath)

            $identityPackage = Join-Path $publishDirectory 'CommandPaletteLLM.identity.msix'
            & $makeAppx pack /o /d $sparseDirectory /nv /p $identityPackage
            if ($LASTEXITCODE -ne 0) {
                throw "MakeAppx failed for $platform with exit code $LASTEXITCODE."
            }

            $identityCertificate = Join-Path $publishDirectory 'CommandPaletteLLM.identity.cer'
            Export-Certificate -Cert $buildCertificate -FilePath $identityCertificate -Force | Out-Null

            & $signTool sign /sha1 $buildCertificate.Thumbprint /s My /fd SHA256 $identityPackage
            if ($LASTEXITCODE -ne 0) {
                throw "SignTool failed for $platform with exit code $LASTEXITCODE."
            }
        }
    }
    finally {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($buildCertificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }
}

if (-not $StageOnly) {
    $innoSetupCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    $innoSetup = $innoSetupCandidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1

    if ($null -eq $innoSetup) {
        throw 'Inno Setup 6 was not found. Install it before compiling the WinGet installers.'
    }

    New-Item -ItemType Directory -Path $installerOutput -Force | Out-Null

    foreach ($platform in $Platforms) {
        $sourceDirectory = Join-Path $publishRoot "win-$platform"
        $applicationPath = Join-Path $sourceDirectory 'CommandPaletteLLM.exe'
        $identityPackage = Join-Path $sourceDirectory 'CommandPaletteLLM.identity.msix'
        $identityCertificate = Join-Path $sourceDirectory 'CommandPaletteLLM.identity.cer'
        if (-not (Test-Path -LiteralPath $applicationPath -PathType Leaf)) {
            throw "Run the staging build first; '$sourceDirectory' does not contain CommandPaletteLLM.exe."
        }
        if (-not (Test-Path -LiteralPath $identityPackage -PathType Leaf)) {
            throw "Run the staging build first; '$identityPackage' does not exist."
        }
        if (-not (Test-Path -LiteralPath $identityCertificate -PathType Leaf)) {
            throw "Run the staging build first; '$identityCertificate' does not exist."
        }

        & $innoSetup `
            "/DAppVersion=$Version" `
            "/DPlatform=$platform" `
            "/DSourceDir=$sourceDirectory" `
            "/DIdentityPackage=$identityPackage" `
            "/DIdentityCertificate=$identityCertificate" `
            "/DOutputDir=$installerOutput" `
            $installerScript

        if ($LASTEXITCODE -ne 0) {
            throw "Inno Setup failed for $platform with exit code $LASTEXITCODE."
        }

        $expectedInstaller = Join-Path $installerOutput "CommandPaletteLLM-Setup-$Version-$platform.exe"
        if (-not (Test-Path -LiteralPath $expectedInstaller -PathType Leaf)) {
            throw "The installer was not created at '$expectedInstaller'."
        }
    }
}
