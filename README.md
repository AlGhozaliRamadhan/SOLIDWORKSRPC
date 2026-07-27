# SOLIDWORKS Design - Discord Rich Presence

A native SOLIDWORKS add-in that displays the active document on your Discord profile through Rich Presence.

## Requirements

- SOLIDWORKS 2010+ (tested on 2026)
- .NET SDK 8+ — `winget install Microsoft.DotNet.SDK.8`
- Discord desktop app running with Activity Status enabled

## Setup

The build auto-discovers your SOLIDWORKS installation. No manual path configuration needed.

It checks the registry, common install locations, and versioned folders (2024–2026). On first successful build it writes `SolidWorks.local.props` for future builds. If auto-discovery fails, copy `SolidWorks.local.props.example` to `SolidWorks.local.props` and set the path manually.

## Build

```
dotnet build src/SolidworksDiscordRPC/SolidworksDiscordRPC.csproj -c Debug
```

Or open `SolidworksDiscordRPC.sln` in Visual Studio 2022+.

## Install

### One-click (recommended)

Right-click `install.bat` and select **Run as Administrator**.

This builds the project, copies interop DLLs, and runs `regasm /codebase`.

### PowerShell

Run as Administrator from repo root:

```powershell
# Build and register
powershell -ExecutionPolicy Bypass -File scripts/install.ps1 -Build

# Register only (after building in VS)
powershell -ExecutionPolicy Bypass -File scripts/install.ps1

# Release build
powershell -ExecutionPolicy Bypass -File scripts/install.ps1 -Build -Configuration Release

# Unregister
powershell -ExecutionPolicy Bypass -File scripts/uninstall.ps1
```

### Manual regasm

Run as Administrator:

```
"%windir%\Microsoft.NET\Framework64\v4.0.30319\regasm.exe" /codebase ^
  "src\SolidworksDiscordRPC\bin\Debug\net48\SolidworksDiscordRPC.dll"
```

## Quick Start

For those who just want it running:

```
git clone https://github.com/AlGhozaliRamadhan/SOLIDWORKSRPC.git
cd SOLIDWORKSRPC
```

Then right-click `install.bat` and select **Run as Administrator**. Open SOLIDWORKS and your Discord status will update automatically.

## Verify

1. Open SOLIDWORKS and Discord.
2. Go to Tools > Add-Ins and confirm the add-in is listed and checked.
3. Open any Part/Assembly/Drawing. Discord status should update within 2 seconds.
4. Close all documents. Status should show "No document open / Idle".

Smoke test (no SOLIDWORKS needed):

```
powershell -ExecutionPolicy Bypass -File scripts/test-discord-smoke.ps1
```
