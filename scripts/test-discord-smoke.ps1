<#
.SYNOPSIS
  Smoke-test Discord Rich Presence WITHOUT needing SolidWorks.
  Proves your App ID + sw_logo asset work before you do the full add-in test.

  Requires: .NET SDK installed + Discord desktop app running.

.USAGE
  # Discord must be running, Activity Privacy -> Activity Status ON
  powershell -ExecutionPolicy Bypass -File scripts/test-discord-smoke.ps1
  # Custom App ID:
  powershell -ExecutionPolicy Bypass -File scripts/test-discord-smoke.ps1 -AppId 123456...

  Expected: your Discord profile shows "Test: SolidWorks RPC works! / Smoke test running" for 30s.
#>
param(
    [string]$AppId = "1531258607457271939",
    [int]$Seconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Check dotnet SDK
try { dotnet --version | Out-Null } catch {
    Write-Error ".NET SDK not found. Install from https://dotnet.microsoft.com/download then retry."
}

$tmp = Join-Path $env:TEMP "SwRpcSmokeTest"
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
dotnet new console -o $tmp -f net8.0 --no-restore | Out-Null
dotnet add $tmp package DiscordRichPresence --version 1.6.1.70 | Out-Null

$program = @"
using System;
using System.Threading;
using DiscordRPC;
class Program {
    static void Main() {
        var client = new DiscordRpcClient("$AppId");
        client.OnReady += (s,e) => Console.WriteLine($"[Discord] Connected as {e.User.Username}");
        client.OnError += (s,e) => Console.WriteLine($"[Discord] Error: {e.Message}");
        try { client.Initialize(); } catch (Exception ex) { Console.WriteLine($"Init failed: {ex.Message}"); return; }
        client.SetPresence(new RichPresence {
            Details = "Test: SolidWorks RPC works!",
            State = "Smoke test running",
            Timestamps = Timestamps.Now,
            Assets = new Assets { LargeImageKey = "sw_logo", LargeImageText = "SolidWorks" }
        });
        Console.WriteLine("Presence set for $Seconds seconds - check your Discord profile now!");
        Console.WriteLine("If no image: upload sw_logo art asset in Discord portal (see docs/art-assets.md).");
        Thread.Sleep($Seconds * 1000);
        client.Dispose();
        Console.WriteLine("Done.");
    }
}
"@

Set-Content -Path (Join-Path $tmp "Program.cs") -Value $program
Write-Host "Running Discord smoke test (AppId $AppId) ..." -ForegroundColor Cyan
dotnet run --project $tmp
