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
$installerOutput = Join-Path $OutputRoot 'installer'

$components = $Version.Split('.') | ForEach-Object { [int] $_ }
if (@($components | Where-Object { $_ -gt 65535 }).Count -ne 0) {
    throw "Version '$Version' cannot be represented as a Windows file version."
}

$fileVersion = "$Version.0"

if (-not $PackageOnly) {
    foreach ($platform in $Platforms) {
        $runtimeIdentifier = "win-$platform"
        $publishDirectory = Join-Path $publishRoot $runtimeIdentifier

        if (Test-Path -LiteralPath $publishDirectory) {
            Remove-Item -LiteralPath $publishDirectory -Recurse -Force
        }

        New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

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
        if (-not (Test-Path -LiteralPath (Join-Path $sourceDirectory 'CommandPaletteLLM.exe') -PathType Leaf)) {
            throw "Run the staging build first; '$sourceDirectory' does not contain CommandPaletteLLM.exe."
        }

        & $innoSetup `
            "/DAppVersion=$Version" `
            "/DPlatform=$platform" `
            "/DSourceDir=$sourceDirectory" `
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
