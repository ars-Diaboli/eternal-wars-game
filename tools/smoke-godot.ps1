#!/usr/bin/env pwsh
# Smoke test of the Godot apps: runs each app with scripted input (AutoInput) under Godot's Movie Maker,
# saves the frames as PNGs for a visual check and fails if Godot or C# reports an error.
#
#   ./tools/smoke-godot.ps1 -Godot "C:\path\Godot_v4.7.2-stable_mono_win64_console.exe"
#
# Needs a GPU/display (Movie Maker renders for real). Builds the solution first. Frames go to .shots/<scenario>/.
# Click coordinates assume the default window sizes in each project.godot.

param(
    [string]$Godot = $env:GODOT,
    [string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) '.shots')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Godot -or -not (Test-Path $Godot)) {
    throw 'Pass -Godot <path to the Godot .NET console executable> or set $env:GODOT.'
}

& (Join-Path $PSScriptRoot 'link-shared-assets.ps1') | Out-Null
dotnet build (Join-Path $root 'EternalWars.sln') --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$exchangeMap = Join-Path $PSScriptRoot 'smoke/exchange.ewmap.json'
$battleMap = Join-Path $PSScriptRoot 'smoke/battle.ewmap.json'
$townsMap = Join-Path $PSScriptRoot 'smoke/towns.ewmap.json'
$scenarios = @(
    @{ Name = 'harness-guard-battle'; App = 'EternalWars.Harness2D'; Frames = 45
       Input = '5:click:133,263;12:click:133,263;20:click:103,140;28:click:30,140;35:key:Q' },
    @{ Name = 'game-route-and-move'; App = 'EternalWars.Game'; Frames = 60
       Input = '3:click:800,569;10:click:960,520;25:click:960,520' },
    @{ Name = 'game-exchange-and-gate'; App = 'EternalWars.Game'; Frames = 60; Map = $exchangeMap
       Input = '3:click:800,569;10:key:Right;16:click:258,209;20:click:258,151;24:click:194,256;30:click:960,557;36:click:960,557' },
    # Step next to a savage monster: the 3D battle scene opens; wait, defend, a click on the field, quick combat, leave.
    @{ Name = 'game-battle-3d'; App = 'EternalWars.Game'; Frames = 80; Map = $battleMap
       Input = '3:click:800,569;10:key:Right;30:key:W;40:key:D;48:click:960,600;58:key:Q;68:key:Enter' },
    # The town screen (ADR-050): T opens the first own town over the map, Escape closes it, T again, Escape.
    @{ Name = 'game-town-screen'; App = 'EternalWars.Game'; Frames = 50; Map = $townsMap
       Input = '3:click:800,569;12:key:T;24:key:Escape;30:key:T;40:key:Escape' },
    @{ Name = 'editor-tools'; App = 'EternalWars.MapEditor'; Frames = 40
       Input = '3:click:103,24;6:click:700,600;9:click:740,600;12:click:160,24;15:click:800,500;18:click:231,24;21:click:900,450;24:click:492,24;27:click:900,450;30:click:1305,24' }
)

$failed = 0
foreach ($scenario in $scenarios) {
    $dir = Join-Path $Out $scenario.Name
    New-Item -ItemType Directory -Force $dir | Out-Null
    $userArgs = @("--autoinput=$($scenario.Input)")
    if ($scenario.Map) { $userArgs += "--map=$($scenario.Map)" }

    $log = & $Godot --path (Join-Path $root "src/Apps/$($scenario.App)") --write-movie (Join-Path $dir 'frame.png') `
        --fixed-fps 10 --quit-after $scenario.Frames -- @userArgs 2>&1 | Out-String
    $errors = $log -split "`n" | Where-Object { $_ -match 'ERROR|SCRIPT ERROR|Unhandled exception|Exception:' }
    if ($LASTEXITCODE -ne 0 -or $errors) {
        $failed++
        Write-Host "FAIL  $($scenario.Name)"
        $errors | ForEach-Object { Write-Host "      $_" }
    }
    else {
        Write-Host "ok    $($scenario.Name)  ($dir)"
    }
}

if ($failed -gt 0) { throw "$failed smoke scenario(s) failed." }
