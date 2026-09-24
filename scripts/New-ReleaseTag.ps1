[CmdletBinding(DefaultParameterSetName = 'Patch')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Minor')]
    [switch] $Minor,

    [Parameter(Mandatory, ParameterSetName = 'Major')]
    [switch] $Major
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-ReleaseVersion {
    param(
        [Parameter(Mandatory)]
        [string] $Value,

        [Parameter(Mandatory)]
        [string] $Source
    )

    if ($Value -notmatch '^(?:v)?(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)$') {
        throw "$Source contains '$Value', which is not a Major.Minor.Patch version."
    }

    $components = @(
        [int] $Matches.major
        [int] $Matches.minor
        [int] $Matches.patch
    )

    if ($components.Where({ $_ -gt 65535 }).Count -ne 0) {
        throw "$Source contains '$Value', but MSIX version components cannot exceed 65535."
    }

    [pscustomobject]@{
        Major = $components[0]
        Minor = $components[1]
        Patch = $components[2]
    }
}

function Compare-ReleaseVersion {
    param(
        [Parameter(Mandatory)]
        [pscustomobject] $Left,

        [Parameter(Mandatory)]
        [pscustomobject] $Right
    )

    foreach ($property in 'Major', 'Minor', 'Patch') {
        if ($Left.$property -gt $Right.$property) {
            return 1
        }

        if ($Left.$property -lt $Right.$property) {
            return -1
        }
    }

    return 0
}

$repositoryRoot = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw 'Run this script from inside the CommandPaletteLLM Git repository.'
}

Push-Location $repositoryRoot
try {
    $versionFile = Join-Path $repositoryRoot 'Directory.Build.props'
    if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
        throw "Could not find '$versionFile'."
    }

    [xml] $buildProperties = Get-Content -Raw -LiteralPath $versionFile
    $projectVersionValue = @($buildProperties.Project.PropertyGroup.Version) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1

    if ($null -eq $projectVersionValue) {
        throw "Directory.Build.props does not define a Version property."
    }

    $currentVersion = ConvertTo-ReleaseVersion -Value ([string] $projectVersionValue) -Source 'Directory.Build.props'

    $tags = @(& git tag --list)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to read Git tags.'
    }

    foreach ($tag in $tags) {
        if ($tag -notmatch '^v(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)$') {
            continue
        }

        $tagVersion = ConvertTo-ReleaseVersion -Value $tag -Source "Git tag '$tag'"
        if ((Compare-ReleaseVersion -Left $tagVersion -Right $currentVersion) -gt 0) {
            $currentVersion = $tagVersion
        }
    }

    switch ($PSCmdlet.ParameterSetName) {
        'Major' {
            $nextVersion = [pscustomobject]@{
                Major = $currentVersion.Major + 1
                Minor = 0
                Patch = 0
            }
        }
        'Minor' {
            $nextVersion = [pscustomobject]@{
                Major = $currentVersion.Major
                Minor = $currentVersion.Minor + 1
                Patch = 0
            }
        }
        default {
            $nextVersion = [pscustomobject]@{
                Major = $currentVersion.Major
                Minor = $currentVersion.Minor
                Patch = $currentVersion.Patch + 1
            }
        }
    }

    if (@($nextVersion.Major, $nextVersion.Minor, $nextVersion.Patch).Where({ $_ -gt 65535 }).Count -ne 0) {
        throw 'The next version cannot be represented as an MSIX version because a component exceeds 65535.'
    }

    $nextTag = 'v{0}.{1}.{2}' -f $nextVersion.Major, $nextVersion.Minor, $nextVersion.Patch
    & git show-ref --verify --quiet "refs/tags/$nextTag"
    if ($LASTEXITCODE -eq 0) {
        throw "Tag '$nextTag' already exists."
    }

    & git tag --annotate $nextTag --message "Release $nextTag"
    if ($LASTEXITCODE -ne 0) {
        throw "Git could not create tag '$nextTag'."
    }

    $commit = (& git rev-parse --short HEAD)
    Write-Host "Created $nextTag on commit $commit."
    Write-Host 'The tag remains local. When ready to publish it, run:'
    Write-Host "  git push origin $nextTag"
}
finally {
    Pop-Location
}
