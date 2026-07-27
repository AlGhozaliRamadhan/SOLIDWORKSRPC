<#
.SYNOPSIS
  Unregisters SolidworksDiscordRPC add-in (regasm /u).
  Must be run as Administrator.
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dll      = Join-Path $repoRoot "src\SolidworksDiscordRPC\bin\$Configuration\net48\SolidworksDiscordRPC.dll"

if (-not (Test-Path $dll)) {
    Write-Warning "DLL not found at $dll — will still try to remove registry keys via fallback GUID lookup."
}

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be run as Administrator."
}

$regasm = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\regasm.exe"

if (Test-Path $dll) {
    Write-Host "Unregistering $dll ..." -ForegroundColor Cyan
    & $regasm /u "$dll"
}

# Fallback: remove the registry keys directly by well-known GUID in case regasm /u missed
$guid = "2F6A6B2E-9F10-4B7B-9C7A-E3D9B9B46B1D"
$paths = @(
    "HKLM:\SOFTWARE\SolidWorks\Addins\{$guid}",
    "HKLM:\SOFTWARE\SolidWorks\AddInsStartup\{$guid}"
)
foreach ($p in $paths) {
    if (Test-Path $p) {
        Write-Host "Removing $p" -ForegroundColor DarkGray
        Remove-Item -Path $p -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`nAdd-in unregistered." -ForegroundColor Green
