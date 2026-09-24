[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Get-AppxPackage -Name 'maxnevans.CommandPaletteLLM.WinGet' |
    Remove-AppxPackage -ErrorAction Stop

Get-ChildItem -Path 'Cert:\LocalMachine\TrustedPeople' |
    Where-Object { $_.Subject -eq 'CN=CommandPaletteLLM Community Package' } |
    Remove-Item -Force
