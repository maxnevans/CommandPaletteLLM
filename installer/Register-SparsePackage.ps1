[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $PackagePath,

    [Parameter(Mandatory)]
    [string] $CertificatePath,

    [Parameter(Mandatory)]
    [string] $ExternalLocation,

    [Parameter(Mandatory)]
    [string] $SuccessMarker,

    [Parameter(Mandatory)]
    [string] $ErrorFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Remove-Item -LiteralPath $SuccessMarker -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $ErrorFile -Force -ErrorAction SilentlyContinue

$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($CertificatePath)
$trustedCertificatePath = "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)"
$certificateWasPresent = Test-Path -LiteralPath $trustedCertificatePath

try {
    if (-not $certificateWasPresent) {
        Import-Certificate `
            -FilePath $CertificatePath `
            -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
    }

    Get-AppxPackage -Name 'maxnevans.CommandPaletteLLM.WinGet' |
        Remove-AppxPackage -ErrorAction Stop

    Add-AppxPackage `
        -Path $PackagePath `
        -ExternalLocation $ExternalLocation `
        -ForceApplicationShutdown `
        -ErrorAction Stop

    Get-ChildItem -Path 'Cert:\LocalMachine\TrustedPeople' |
        Where-Object {
            $_.Subject -eq 'CN=CommandPaletteLLM Community Package' -and
            $_.Thumbprint -ne $certificate.Thumbprint
        } |
        Remove-Item -Force

    New-Item -ItemType File -Path $SuccessMarker -Force | Out-Null
}
catch {
    if (-not $certificateWasPresent) {
        Remove-Item -LiteralPath $trustedCertificatePath -Force -ErrorAction SilentlyContinue
    }

    $details = $_.Exception.ToString()
    if ($null -ne $_.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) {
        $details += [Environment]::NewLine + $_.ErrorDetails.Message
    }

    [System.IO.File]::WriteAllText(
        $ErrorFile,
        $details,
        [System.Text.UTF8Encoding]::new($false))
    exit 1
}
