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
    [string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) '.shots'),
    # Run only the scenarios whose name matches this wildcard (e.g. 'game-*').
    [string]$Only = '*'
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
$siegeMap = Join-Path $PSScriptRoot 'smoke/siege.ewmap.json'
$caravanMap = Join-Path $PSScriptRoot 'smoke/caravan.ewmap.json'
# A map generated from the v7 connection options (ADR-112): garrison, border guard and gate with keymasters, portal, optional connection.
New-Item -ItemType Directory -Force $Out | Out-Null
$connectionsMap = Join-Path (Resolve-Path $Out) 'connections.ewmap.json'
dotnet run --project (Join-Path $PSScriptRoot 'EternalWars.Tools.Sim') --no-build -- generate (Join-Path $PSScriptRoot 'smoke/connections.ewtemplate.json') --out $connectionsMap --seed 1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Generating the connections map failed.' }
# The shipped two-player map (ADR-114) as a file, for the game and the editor.
$emberMap = Join-Path (Resolve-Path $Out) 'ember.ewmap.json'
dotnet run --project (Join-Path $PSScriptRoot 'EternalWars.Tools.Sim') --no-build -- export ember --out $emberMap | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Exporting Ember March failed.' }
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
    # Auto combat (ADR-090): the menu sets "Ask before each battle", the step into the monster asks, Auto combat shows the outcome, Accept
    # returns to the map without a second result screen. The question waits until the imps' first moves have played.
    @{ Name = 'game-auto-combat'; App = 'EternalWars.Game'; Frames = 85; Map = $battleMap
       Input = '3:key:Enter;8:key:F10;12:click:1020,554;15:click:1028,614;19:key:Escape;24:key:Right;60:click:773,509;68:click:773,561' },
    # Speed settings (ADR-090): the menu sets "Your heroes" to Slow, then the hero walks a route (its steps take twice as long).
    @{ Name = 'game-speed-settings'; App = 'EternalWars.Game'; Frames = 70
       Input = '3:key:Enter;8:key:F10;12:click:733,636;15:click:733,666;19:key:Escape;24:click:960,520;30:click:960,520' },
    # The turn timer (ADR-091): pick 1 minute on the new-game screen, start, open the hero screen and wait. The warnings come, the turn passes
    # to Red with the full time and the hero screen closes.
    @{ Name = 'game-turn-timer'; App = 'EternalWars.Game'; Frames = 640
       Input = '3:click:1025,419;8:click:1025,100;12:click:798,586;30:key:H' },
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
    # A random map (ADR-086): pick "Random map: Duel", Necropolis and its first hero for Blue, Start (Red is a computer player), K, end the
    # turn so the computer plays its turn.
    @{ Name = 'game-random-map'; App = 'EternalWars.Game'; Frames = 90
       Input = '4:click:750,314;8:click:783,428;12:click:765,578;16:click:749,719;20:click:925,578;24:click:925,638;28:click:798,648;45:key:K;55:key:Escape;60:click:342,24' },
    # The main menu (ADR-101), by keyboard: two Downs skip the disabled items to Leaderboard, Enter opens it, Escape returns, Enter on
    # New game, Enter on Start. The only game scenario without --new-game.
    @{ Name = 'game-main-menu'; App = 'EternalWars.Game'; Frames = 50; MainMenu = $true
       Input = '3:key:Down;6:key:Down;9:key:Enter;20:key:Escape;26:key:Enter;34:key:Enter' },
    # A siege (ADR-106, ADR-109): step into the neutral Castle-level town; walls, gate, moat, towers and keep. The footman defends, the
    # ballista (Basic Ballistics) shoots the upper wall, two more defend, quick combat mid-animation (ADR-112 check), Enter on the result.
    @{ Name = 'game-siege'; App = 'EternalWars.Game'; Frames = 150; Map = $siegeMap
       Input = '3:key:Enter;10:key:Up;62:key:D;72:click:955,280;90:key:D;100:key:D;125:key:Q;140:key:Enter' },
    # A garrison hero (ADR-112): step into the own town, "Orrin into the garrison", "Lead Orrin out", close the town screen.
    @{ Name = 'game-garrison-hero'; App = 'EternalWars.Game'; Frames = 60; Map = $townsMap
       Input = '3:key:Enter;10:key:Up;20:click:99,855;35:click:99,855;48:key:Escape' },
    # Dwellings and caravans (ADR-054, ADR-073): visit a map dwelling and recruit the week, close; T, scroll to the caravan section, hire
    # from the owned dwelling, close, end two turns (hot-seat) and look again: the footmen are in the garrison.
    @{ Name = 'game-dwelling-caravan'; App = 'EternalWars.Game'; Frames = 100; Map = $caravanMap
       Input = '3:key:Enter;10:key:Up;22:click:770,494;28:click:785,380;34:key:T;42:wheel:down,15;50:click:449,697;58:key:Escape;64:click:342,24;72:click:342,24;82:key:T;90:wheel:down,15' },
    # The generated connections map in the editor (no fog): pan to the portal, the blue border guard, the blue keymaster, the garrison,
    # the red border gate and the red keymaster (frames 16, 49, 86, 109, 153, 172).
    @{ Name = 'editor-generated-connections'; App = 'EternalWars.MapEditor'; Frames = 175; Map = $connectionsMap
       Input = '5:hold:S,8;18:hold:A,19;38:hold:W,8;51:hold:S,18;70:hold:D,13;88:hold:D,10;99:hold:S,7;111:hold:D,8;120:hold:W,30;155:hold:W,10;166:hold:D,3' },
    # The tavern screen (ADR-114): T, scroll to the tavern row, "Visit the tavern…", then Escape for the tooltip, the tavern and the town screen.
    @{ Name = 'game-tavern'; App = 'EternalWars.Game'; Frames = 50; Map = $townsMap
       Input = '3:key:Enter;10:key:T;16:wheel:down,25;24:click:190,794;34:key:Escape;38:key:Escape;44:key:Escape' },
    # Ember March (ADR-114): start, end the turn (the computer plays Red), open the town screen with its building tree, close it.
    @{ Name = 'game-ember-march'; App = 'EternalWars.Game'; Frames = 60; Map = $emberMap
       Input = '3:key:Enter;12:click:342,24;40:key:T;52:key:Escape' },
    # Inspector rows (ADR-114) on Ember March: Select, the Inspector tab, a Minor artifact pickup, the witch hut, the sphinx, the hero (artifacts).
    @{ Name = 'editor-inspector-objects'; App = 'EternalWars.MapEditor'; Frames = 38; Map = $emberMap
       Input = '3:click:544,24;6:click:1328,100;9:click:616,230;16:click:645,382;23:click:965,552;30:click:381,276' },
    @{ Name = 'editor-tools'; App = 'EternalWars.MapEditor'; Frames = 40
       Input = '3:click:103,24;6:click:700,600;9:click:740,600;12:click:160,24;15:click:800,500;18:click:231,24;21:click:900,450;24:click:492,24;27:click:900,450;30:click:1305,24' },
    # Generate from a template (ADR-087): "From template", Sylvan for Blue, Generate (Two against two at 128), then the tab again: the settings stay.
    @{ Name = 'editor-generate'; App = 'EternalWars.MapEditor'; Frames = 50
       Input = '3:click:470,94;8:click:1225,465;12:click:1210,552;16:click:1316,605;30:click:470,94' },
    # The template editor (ADR-117): "Templates", add a treasure zone, drag it, right-drag it onto start-1 to connect, Generate (opens the map).
    @{ Name = 'editor-template-editor'; App = 'EternalWars.MapEditor'; Frames = 60
       Input = '3:click:593,94;8:click:682,30;14:drag:624,408,708,560;20:rdrag:708,560,568,373;34:click:1226,30' },
    # The week event settings on the conditions tab (ADR-084): tick Conflux for the weekly events, one more skipped week, undo both.
    @{ Name = 'editor-week-events'; App = 'EternalWars.MapEditor'; Frames = 40
       Input = '3:click:1310,24;8:click:1506,354;14:click:1583,250;20:click:545,86;26:click:545,86' }
)

$failed = 0
foreach ($scenario in $scenarios | Where-Object { $_.Name -like $Only }) {
    $dir = Join-Path $Out $scenario.Name
    New-Item -ItemType Directory -Force $dir | Out-Null
    # Every game scenario starts from the default settings (ADR-090), whatever this computer's settings file says.
    $settings = Join-Path $dir 'settings.json'
    Remove-Item $settings -ErrorAction SilentlyContinue
    $userArgs = @("--autoinput=$($scenario.Input)", "--settings=$settings")
    if ($scenario.Map) { $userArgs += "--map=$($scenario.Map)" }
    # The game opens on the main menu; the scenarios start on the new-game screen unless they test the menu.
    if ($scenario.App -eq 'EternalWars.Game' -and -not $scenario.MainMenu) { $userArgs += '--new-game' }

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
