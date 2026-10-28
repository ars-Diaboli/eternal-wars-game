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
       Input = '3:key:Enter;10:click:960,520;25:click:960,520' },
    @{ Name = 'game-exchange-and-gate'; App = 'EternalWars.Game'; Frames = 60; Map = $exchangeMap
       Input = '3:key:Enter;10:key:Right;16:click:258,209;20:click:258,151;24:click:194,256;30:click:960,557;36:click:960,557' },
    # Step next to a savage monster: the 3D battle scene opens; wait, defend, a click on the field, quick combat, leave.
    @{ Name = 'game-battle-3d'; App = 'EternalWars.Game'; Frames = 80; Map = $battleMap
       Input = '3:key:Enter;10:key:Right;30:key:W;40:key:D;48:click:960,600;58:key:Q;68:key:Enter' },
    # The town screen (ADR-050): T opens the first own town over the map, Escape closes it, T again, Escape.
    @{ Name = 'game-town-screen'; App = 'EternalWars.Game'; Frames = 50; Map = $townsMap
       Input = '3:key:Enter;12:key:T;24:key:Escape;30:key:T;40:key:Escape' },
    # The hero screen (ADR-050): select a hero, H opens it, Escape closes it, then the same through the hero list's Info button.
    @{ Name = 'game-hero-screen'; App = 'EternalWars.Game'; Frames = 50
       Input = '3:key:Enter;12:key:H;24:key:Escape;30:key:H;40:key:Escape' },
    # The kingdom overview (ADR-084): K opens it on the towns map, Escape closes it, K again, Escape.
    @{ Name = 'game-kingdom'; App = 'EternalWars.Game'; Frames = 50; Map = $townsMap
       Input = '3:key:Enter;12:key:K;24:key:Escape;30:key:K;40:key:Escape' },
    # A week of turns through "End turn" (two human seats, 14 clicks): the new week's notice and its event next to the date (ADR-084).
    @{ Name = 'game-weeks'; App = 'EternalWars.Game'; Frames = 100
       Input = (@('3:key:Enter') + (0..13 | ForEach-Object { "$(10 + 6 * $_):click:342,24" })) -join ';' },
    # The Thieves' Guild (ADR-079) from the town screen's button, closed with Escape, then the town screen too.
    @{ Name = 'game-thieves-guild'; App = 'EternalWars.Game'; Frames = 40; Map = $townsMap
       Input = '3:key:Enter;10:key:T;16:click:446,16;26:key:Escape;32:key:Escape' },
    @{ Name = 'editor-tools'; App = 'EternalWars.MapEditor'; Frames = 40
       Input = '3:click:103,24;6:click:700,600;9:click:740,600;12:click:160,24;15:click:800,500;18:click:231,24;21:click:900,450;24:click:492,24;27:click:900,450;30:click:1305,24' },
    # The week event settings on the conditions tab (ADR-084): tick Conflux for the weekly events, one more skipped week, undo both.
    @{ Name = 'editor-week-events'; App = 'EternalWars.MapEditor'; Frames = 40
       Input = '3:click:1310,24;8:click:1506,354;14:click:1583,250;20:click:545,86;26:click:545,86' }
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
