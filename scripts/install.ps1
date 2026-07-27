<#
.SYNOPSIS
  Builds (optional) and registers SolidworksDiscordRPC add-in.
  Auto-discovers where SolidWorks is installed so it works on any PC.
  Must be run as Administrator (writes to HKLM).
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Build,
    [switch]$NoAutoDiscover
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$propsFile = Join-Path $repoRoot "SolidWorks.local.props"
$csproj = Join-Path $repoRoot "src\SolidworksDiscordRPC\SolidworksDiscordRPC.csproj"
$dllDir = Join-Path $repoRoot "src\SolidworksDiscordRPC\bin\$Configuration\net48"
$dll = Join-Path $dllDir "SolidworksDiscordRPC.dll"

function Find-SolidWorksInterop {
    $candidates = @(
        "D:\SOLIDWORKS Corp\SOLIDWORKS\api\redist",
        "C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist",
        "C:\Program Files\SOLIDWORKS\api\redist",
        "E:\SOLIDWORKS Corp\SOLIDWORKS\api\redist",
        "C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS (2024)\api\redist",
        "C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS (2025)\api\redist"
    )
    try {
        $sw = Get-Process -Name "SLDWORKS" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($sw -and $sw.Path) {
            $candidate = Join-Path (Split-Path $sw.Path -Parent) "api\redist"
            if (Test-Path (Join-Path $candidate "SolidWorks.Interop.sldworks.dll")) {
                $candidates = @($candidate) + ($candidates | Where-Object { $_ -ne $candidate })
            }
        }
    } catch {}
    try {
        foreach ($regPath in @("HKLM:\SOFTWARE\SolidWorks\SOLIDWORKS", "HKLM:\SOFTWARE\WOW6432Node\SolidWorks\SOLIDWORKS")) {
            if (Test-Path $regPath) {
                $ip = Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue
                foreach ($p in $ip.PSObject.Properties) {
                    if ($p.Value -is [string] -and $p.Value -like "*.exe") {
                        $dir = Split-Path $p.Value -Parent
                        $c = Join-Path $dir "api\redist"
                        if (Test-Path (Join-Path $c "SolidWorks.Interop.sldworks.dll")) {
                            $candidates = @($c) + ($candidates | Where-Object { $_ -ne $c })
                        }
                    }
                }
            }
        }
    } catch {}

    foreach ($path in $candidates) {
        if ((Test-Path (Join-Path $path "SolidWorks.Interop.sldworks.dll")) -and
            (Test-Path (Join-Path $path "SolidWorks.Interop.swpublished.dll")) -and
            (Test-Path (Join-Path $path "SolidWorks.Interop.swconst.dll"))) {
            return $path
        }
    }
    return $null
}

# Auto-create props if missing
if (-not (Test-Path $propsFile)) {
    if (-not $NoAutoDiscover) {
        Write-Host "Auto-discovering SolidWorks install..." -ForegroundColor Cyan
        $found = Find-SolidWorksInterop
        if ($found) {
            Write-Host "Found at: $found" -ForegroundColor Green
            $xml = "<Project>`r`n  <PropertyGroup>`r`n    <SolidWorksInteropPath>$found</SolidWorksInteropPath>`r`n  </PropertyGroup>`r`n</Project>`r`n"
            Set-Content -Path $propsFile -Value $xml -Encoding UTF8
        } else {
            Write-Warning "Could not auto-find SolidWorks. Build will use stubs. Copy .props.example -> .props and edit manually for full build."
        }
    }
}

if ($Build) {
    Write-Host "Building $csproj ($Configuration)..." -ForegroundColor Cyan
    dotnet build $csproj -c $Configuration
}

if (-not (Test-Path $dll)) {
    Write-Host "DLL not found, building now..." -ForegroundColor Yellow
    dotnet build $csproj -c $Configuration
}

if (-not (Test-Path $dll)) { Write-Error "DLL still not found: $dll" }

# Check admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Must run as Administrator. Right-click PowerShell -> Run as Administrator, then run this again.`nOr right-click INSTALL_ADMIN.bat -> Run as Administrator."
}

# For regasm to resolve SolidWorks interop types, copy interop DLLs next to our DLL
try {
    $interopPath = $null
    if (Test-Path $propsFile) {
        [xml]$props = Get-Content $propsFile
        $interopPath = $props.Project.PropertyGroup.SolidWorksInteropPath
    }
    if (-not $interopPath) { $interopPath = Find-SolidWorksInterop }
    if ($interopPath -and (Test-Path $interopPath)) {
        Write-Host "Copying interop DLLs from $interopPath for registration..." -ForegroundColor DarkGray
        foreach ($name in @("SolidWorks.Interop.sldworks.dll","SolidWorks.Interop.swpublished.dll","SolidWorks.Interop.swconst.dll")) {
            $src = Join-Path $interopPath $name
            $dst = Join-Path $dllDir $name
            if ((Test-Path $src) -and (-not (Test-Path $dst))) {
                Copy-Item $src $dst -Force
            }
        }
    }
} catch { Write-Warning "Could not copy interop DLLs (non-fatal): $_" }

$regasm = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
if (-not (Test-Path $regasm)) { Write-Error "regasm.exe not found. Need .NET Framework 4.x installed." }

Write-Host "Registering $dll ..." -ForegroundColor Cyan
& $regasm /codebase "$dll"
if ($LASTEXITCODE -ne 0) {
    Write-Host "First regasm failed (code $LASTEXITCODE), trying with /tlb..." -ForegroundColor Yellow
    & $regasm /codebase "$dll" /tlb
    if ($LASTEXITCODE -ne 0) {
        Write-Error "regasm failed. Try closing SolidWorks first, then run again as Admin. Exit code $LASTEXITCODE"
    }
}

Write-Host ""
Write-Host "Add-in registered!" -ForegroundColor Green
Write-Host "  1. Restart/open SolidWorks -> Tools -> Add-Ins -> check 'SolidWorks Discord RPC'" -ForegroundColor Yellow
Write-Host "  2. Discord desktop must be running (Activity Status ON in Discord settings)" -ForegroundColor Yellow
Write-Host "  3. Open a Part -> Discord status should show filename within ~2s" -ForegroundColor Yellow
