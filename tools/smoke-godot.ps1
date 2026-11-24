#!/usr/bin/env pwsh
# Smoke test of the Godot apps: runs each app with scripted input (AutoInput) under Godot's Movie Maker,
# saves the frames as pictures for a visual check and fails if Godot or C# reports an error.
#
#   ./tools/smoke-godot.ps1 -Godot "C:\path\Godot_v4.7.2-stable_mono_win64_console.exe"
#
# Needs a GPU/display (Movie Maker renders for real). Builds the solution first. Frames go to .shots/<scenario>/:
# frame.avi and its frames as frame00000000.jpg, …; with -Png lossless frame00000000.png, … instead.
# Scenarios run side by side (-Jobs); the apps' windows take no focus under Movie Maker (no_focus.movie in project.godot).
# Click coordinates assume the default window sizes in each project.godot.

param(
    [string]$Godot = $env:GODOT,
    [string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) '.shots'),
    # Run only the scenarios whose name matches this wildcard (e.g. 'game-*').
    [string]$Only = '*',
    # How many scenarios run at the same time (each is its own Godot process with its own settings and saves).
    [int]$Jobs = [Math]::Max(1, [Math]::Min(8, [int]([Environment]::ProcessorCount / 2))),
    # Skip the build (the solution is built already).
    [switch]$NoBuild,
    # Lossless PNG frames instead of JPEG ones: several times slower (compressing the PNGs takes most of a run), for comparing frames exactly.
    [switch]$Png,
    # Every scenario plays with this seed instead of its own (to look for a failure under other battles).
    [Nullable[ulong]]$Seed
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Godot -or -not (Test-Path $Godot)) {
    throw 'Pass -Godot <path to the Godot .NET console executable> or set $env:GODOT.'
}

& (Join-Path $PSScriptRoot 'link-shared-assets.ps1') | Out-Null
if (-not $NoBuild) {
    dotnet build (Join-Path $root 'EternalWars.sln') --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

$exchangeMap = Join-Path $PSScriptRoot 'smoke/exchange.ewmap.json'
$battleMap = Join-Path $PSScriptRoot 'smoke/battle.ewmap.json'
$townsMap = Join-Path $PSScriptRoot 'smoke/towns.ewmap.json'
$townLayoutsMap = Join-Path $PSScriptRoot 'smoke/town-layouts.ewmap.json'
$townStatesMap = Join-Path $PSScriptRoot 'smoke/town-states.ewmap.json'
$siegeMap = Join-Path $PSScriptRoot 'smoke/siege.ewmap.json'
$caravanMap = Join-Path $PSScriptRoot 'smoke/caravan.ewmap.json'
$questsMap = Join-Path $PSScriptRoot 'smoke/quests.ewmap.json'
$turnStartMap = Join-Path $PSScriptRoot 'smoke/turn-start.ewmap.json'
$placeholdersMap = Join-Path $PSScriptRoot 'smoke/placeholders.ewmap.json'
$uniqueMap = Join-Path $PSScriptRoot 'smoke/unique-buildings.ewmap.json'
$sitesMap = Join-Path $PSScriptRoot 'smoke/sites.ewmap.json'
$gatesMap = Join-Path $PSScriptRoot 'smoke/gates.ewmap.json'
$grailMap = Join-Path $PSScriptRoot 'smoke/grail.ewmap.json'
$magicMap = Join-Path $PSScriptRoot 'smoke/magic.ewmap.json'
$necromancyMap = Join-Path $PSScriptRoot 'smoke/necromancy.ewmap.json'
$siegeMagicMap = Join-Path $PSScriptRoot 'smoke/siege-magic.ewmap.json'
$spellsMap = Join-Path $PSScriptRoot 'smoke/spells.ewmap.json'
$arenaMap = Join-Path $PSScriptRoot 'smoke/arena.ewmap.json'
# Made by the simulator, side by side: a map generated from the v7 connection options (ADR-112: garrison, border guard and gate with
# keymasters, portal, optional connection); the shipped two-player map (ADR-114) as a file, for the game and the editor; a short computer
# game with a battle (the sim's demo record), for the replay viewer (ADR-164).
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$connectionsMap = Join-Path $Out 'connections.ewmap.json'
$emberMap = Join-Path $Out 'ember.ewmap.json'
$replayFile = Join-Path $Out 'smoke.ewreplay.json'
$computerReplayFile = Join-Path $Out 'smoke-computer.ewreplay.json'
$sim = Join-Path $PSScriptRoot 'EternalWars.Tools.Sim'
$simFailed = @(
    @{ What = 'Generating the connections map'; Args = @('generate', (Join-Path $PSScriptRoot 'smoke/connections.ewtemplate.json'), '--out', $connectionsMap, '--seed', '1') },
    @{ What = 'Exporting Ember March'; Args = @('export', 'ember', '--out', $emberMap) },
    @{ What = 'Recording the replay'; Args = @('record', $replayFile, '--days', '4') },
    @{ What = 'Recording the replay with a computer player'; Args = @('record', $computerReplayFile, '--days', '4', '--computer') }
) | ForEach-Object -Parallel {
    $simArgs = $_.Args
    dotnet run --project $using:sim --no-build -- @simArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { $_.What }
}
if ($simFailed) { throw "$($simFailed -join ', ') failed." }
$scenarios = @(
    @{ Name = 'harness-guard-battle'; App = 'EternalWars.Harness2D'; Frames = 45
       Input = '5:click:133,263;12:click:133,263;20:click:@Fight;28:click:@Quick combat;35:key:Q' },
    # A route and a walk, then a week of turns through "End turn" (two human seats, 14 clicks): the new week's notice and its event next
    # to the date (ADR-084).
    @{ Name = 'game-route-and-move'; App = 'EternalWars.Game'; Frames = 150
       Input = (@('3:key:Enter', '10:click:960,520', '25:click:960,520') + (0..13 | ForEach-Object { "$(60 + 6 * $_):press:End turn;$(64 + 6 * $_):key:Enter" })) -join ';' },
    # Two own heroes meet (ADR-153): Orrin's blade onto Valeska's doll, his ring into her backpack, her griffins to him; Close, then the gate.
    @{ Name = 'game-exchange-and-gate'; App = 'EternalWars.Game'; Frames = 72; Map = $exchangeMap
       Input = '3:key:Enter;10:key:Right;16:click:@Weapon: Rusted Blade;19:click:@Weapon: Centaur''s Axe;22:click:@Iron Band (Ring);25:click:@Into the backpack#2;28:click:@2 | Griffin;31:click:@—;36:click:@Close;42:click:960,557;48:click:960,557' },
    # Artifact drag and drop (ADR-162): in the exchange Orrin's blade onto an empty slot of Valeska's doll (her axe goes to her backpack), his
    # Iron Band from his backpack onto his doll, her axe onto empty ground of his backpack; on his hero screen the axe onto the doll, the band
    # into the backpack.
    @{ Name = 'game-artifact-drag'; App = 'EternalWars.Game'; Frames = 62; Map = $exchangeMap
       Input = '3:key:Enter;10:key:Right;16:drag:@Weapon: Rusted Blade>@Head: —#2;24:drag:@Iron Band (Ring)>@Ring: —;30:drag:@Centaur''s Axe (Weapon)>420,592;36:click:@Close;40:key:H;46:drag:@Centaur''s Axe (Weapon)>@Shield: —;52:drag:@Ring: Iron Band>300,779' },
    # Information windows (ADR-154): right-click Orrin on the map, a click closes it; on the hero screen right-click his footmen, then his blade
    # (the second right click opens the next window), Escape closes it, then the Iron Band in his backpack; Escape closes it and the screen.
    # Touch (ADR-190): a finger held still on the hero opens its information window, which stays open when lifted; a tap closes it.
    @{ Name = 'game-info-windows'; App = 'EternalWars.Game'; Frames = 112; Map = $exchangeMap
       Input = '3:key:Enter;10:rclick:800,445;16:click:1500,850;20:key:H;24:rclick:@10 | Footman;28:rclick:@Weapon: Rusted Blade;32:key:Escape;36:rclick:@Iron Band (Ring);42:key:Escape;46:key:Escape;52:touch:800,445,45;106:touch:400,300' },
    # An arena map against the computer (ADR-097, ADR-159): Blue ends day 1, Red prepares, day 2's battle opens by itself at the combat place;
    # quick combat, the result screen (not ranked), then "Play again" starts the map anew.
    @{ Name = 'game-arena'; App = 'EternalWars.Game'; Frames = 110; Map = $arenaMap
       Input = '3:key:Enter;8:press:End turn;12:key:Enter;50:key:Q;75:key:Enter;85:press:Play again' },
    # A shipped arena map from the new-game screen's Arenas tab (ADR-180, ADR-201): Four Banners, two against two with random towns; start,
    # then end day 1 and the computer players prepare (their battles show; day 2 and the result are the game-arena scenario's).
    @{ Name = 'game-arena-shipped'; App = 'EternalWars.Game'; Frames = 60
       Input = '4:press:Arenas;8:click:@Four Banners;12:click:@Start;30:press:End turn;34:key:Enter' },
    # Step next to a savage monster: the 3D battle scene opens; wait, defend, a click on the field, quick combat, leave.
    @{ Name = 'game-battle-3d'; App = 'EternalWars.Game'; Frames = 80; Map = $battleMap
       Input = '3:key:Enter;10:key:Right;30:key:W;40:key:D;48:click:960,600;58:key:Q;68:key:Enter' },
    # Auto combat (ADR-090): the menu sets "Ask before each battle", the step into the monster asks, Auto combat shows the outcome, Accept
    # returns to the map without a second result screen. The question waits until the imps' first moves have played.
    @{ Name = 'game-auto-combat'; App = 'EternalWars.Game'; Frames = 85; Map = $battleMap
       Input = '3:key:Enter;8:key:F10;10:click:@Settings;13:click:@Off;16:choose:Ask before each battle;19:key:Escape;24:key:Right;60:click:@Auto combat;68:click:@Accept' },
    # Speed settings (ADR-090): the menu sets "Your heroes" to Slow, then the hero walks a route (its steps take twice as long).
    # The side panel (ADR-191): the framed column on the right by default, moved to the left by the "Layout" tab's mirror (ADR-309).
    @{ Name = 'game-speed-settings'; App = 'EternalWars.Game'; Frames = 100
       Input = '3:key:Enter;8:key:F10;10:click:@Settings;13:click:@Normal;16:choose:Slow;19:key:Escape;24:click:960,520;30:click:960,520;70:key:F10;72:click:@Layout;75:click:@Minimap on the right;78:choose:Mirrored: minimap on the left;82:key:Escape' },
    # The "Layout" tab (ADR-156, ADR-309): the adventure map mirrored, the initiative bar at the bottom; the map's HUD moves at once, then the
    # step into the monster opens the battle with its initiative bar at the bottom.
    @{ Name = 'game-layout'; App = 'EternalWars.Game'; Frames = 96; Map = $battleMap
       Input = '3:key:Enter;8:key:F10;10:click:@Layout;13:click:@Minimap on the right;16:choose:Mirrored: minimap on the left;20:click:@Top;24:choose:Bottom;28:key:Escape;36:key:Right' },
    # The action buttons (ADR-210), 3×3 in the side panel: Magus' spell book from its button (Escape closes it), then with C (C closes it);
    # the hero screen from its button; the game menu from its button. Bookless selected, the spell book button is disabled.
    @{ Name = 'game-action-buttons'; App = 'EternalWars.Game'; Frames = 62; Map = $magicMap
       Input = '3:key:Enter;8:press:Magus;12:press:Spell book [C];20:key:Escape;24:key:C;31:key:C;36:press:Hero screen [H];42:key:Escape;46:press:Menu [F10];52:key:Escape;56:press:Bookless' },
    # The adventure HUD's parts (ADR-309) on the towns map: the turn banner, the minimap with its pictograms, the camera's frame and the zoom
    # buttons under it, pressed in and out; the hero list's entry hovered (its card beside the list, its name on the status bar); the town list.
    @{ Name = 'game-hud-parts'; App = 'EternalWars.Game'; Frames = 44; Map = $townsMap
       Input = '3:key:Enter;14:move:46,78;22:move:800,450;26:press:Zoom in;30:press:Zoom in;36:press:Zoom out' },
    # The bottom row (ADR-309) on the towns map: Tab selects Orrin (his card and army show), a click plans a route (day colours and tags),
    # Move walks it; a right click on his first stack opens its information window, Escape closes it; End turn asks while he can still move,
    # Cancel; Z asks again and Z answers it.
    @{ Name = 'game-bottom-row'; App = 'EternalWars.Game'; Frames = 76; Map = $townsMap
       Input = '3:key:Enter;8:key:Tab;14:click:1100,300;22:press:Move [Space];34:rclick:528,800;40:key:Escape;46:press:End turn [Z];52:press:Cancel;58:key:Z;64:key:Z' },
    # The turn timer (ADR-091, ADR-225): tick it on the new-game screen and type 1 minute, start, open the hero screen and wait. The warnings come,
    # the turn passes to Red with the full time and the hero screen closes. At 2 frames a second, so the minute takes 120 frames instead of 600.
    @{ Name = 'game-turn-timer'; App = 'EternalWars.Game'; Frames = 140; Fps = 2
       Input = '2:press:Turn timer;6:text:1;8:key:Enter;10:click:@Start;14:key:H' },
    # The screens over the map on the towns map, each opened, closed with Escape, opened again and closed: the town screen (ADR-050) with T,
    # the kingdom overview (ADR-084) with K, the hero screen (ADR-050) with H. Then a garrison hero (ADR-112): step into the own town,
    # "Orrin into the garrison", "Lead Orrin out", close the town screen.
    @{ Name = 'game-town-screen'; App = 'EternalWars.Game'; Frames = 170; Map = $townsMap
       Input = '3:key:Enter;12:key:T;24:key:Escape;30:key:T;40:key:Escape;48:key:K;60:key:Escape;66:key:K;76:key:Escape;84:key:H;96:key:Escape;102:key:H;112:key:Escape;120:key:Up;130:press:Orrin into the garrison;145:press:Lead Orrin out;158:key:Escape' },
    # The town screen's shell (ADR-294, ADR-296): T opens Castlebridge over Haven's key painting; B opens the Hall (building tiles
    # with their states), Escape closes it; R the recruitment window (creature cards), Recruit Footman opens the recruit dialog, Max, Recruit;
    # G the mage guild's window, a click on the dimmed painting closes it; Escape leaves the town.
    @{ Name = 'game-town-shell'; App = 'EternalWars.Game'; Frames = 90; Map = $townsMap
       Input = '3:key:Enter;12:key:T;20:key:B;32:key:Escape;38:key:R;48:press:Recruit Footman;56:press:Max;60:press:Recruit;68:key:G;74:click:200,300;80:key:Escape' },
    # The second font pairing (Marcellus SC and Fira Sans, ADR-294) on the same screens, for the owner's pick beside game-town-shell.
    @{ Name = 'game-fonts-b'; App = 'EternalWars.Game'; Frames = 60; Map = $townsMap; Args = @('--fonts=b')
       Input = '3:key:Enter;12:key:T;20:key:B;32:key:Escape;38:key:R;48:key:Escape;52:key:Escape;54:key:H' },
    # The army bars (ADR-294) on the new screen's bottom bar: Orrin walks into Castlebridge; his pikemen dragged into the garrison; Shift + drag
    # of the archers asks how many (the split dialog), Split takes half; Alt + click spreads the pikemen over the empty garrison slots; Orrin's
    # portrait dragged onto the garrison's empty frame moves him in; Swap is greyed with one hero; his frame dragged back leads him out.
    @{ Name = 'game-town-armies'; App = 'EternalWars.Game'; Frames = 96; Map = $townsMap
       Input = '3:key:Enter;12:key:Up;26:drag:@20 | Pikeman>@—#1;34:hold:Shift,8;35:drag:@10 | Archer>@—#2;46:press:Split;54:hold:Alt,4;55:click:@20 | Pikeman;66:drag:@Hero Orrin>@No hero;78:drag:@Hero Orrin>@No hero' },
    # Arrival (TOWN-23, ADR-306): Haven's layout loads in the background at the turn's start (Castlebridge is the town T opens); Orrin walks
    # into Castlebridge and the map's last frame fades through black into the hero shot; Escape leaves; T opens it again, faded the same way.
    @{ Name = 'game-town-arrival'; App = 'EternalWars.Game'; Frames = 64; Map = $townsMap
       Input = '3:key:Enter;12:key:Up;34:key:Escape;44:key:T;58:key:Escape' },
    # A route to a town loads its layout ahead (ADR-306): on the layouts map a click on Mosswatch's gate shows Orrin's route there (Sylvan's layout
    # loads in place of Haven's, loaded at the turn's start); a second click walks him in (the Elder Grove's experience: OK, a skill for the
    # level) and the screen fades into Mosswatch's scene, opened at once; Escape leaves.
    @{ Name = 'game-town-route'; App = 'EternalWars.Game'; Frames = 52; Map = $townLayoutsMap
       Input = '3:key:Enter;12:click:1103,398;20:click:1103,398;28:press:OK;32:click:822,462;46:key:Escape' },
    # The town screen opened (ADR-296): T opens Castlebridge (Haven's 3D scene behind the bars, the building strip, the notices, ADR-308); a click on its name renames it
    # Brightwatch; the governor list from the portrait; Escape twice leaves.
    @{ Name = 'game-town-open'; App = 'EternalWars.Game'; Frames = 70; Map = $townsMap
       Input = '3:key:Enter;12:key:T;22:click:@Castlebridge;28:text:Brightwatch;42:key:Enter;48:press:No governor;58:key:Escape;62:key:Escape' },
    # The town scene's budget (ADR-308): the overlay shown from the start and the 3D rendered 2560 wide, as at 1440p (--budget=2560x1440: its
    # numbers in the log every ten frames, each line naming the town type); T opens the first of the layouts map's towns, all fully built, ]
    # goes through the other seven, a while at each, then round them all again (the video memory stays level: one layout kept); Escape leaves.
    @{ Name = 'game-town-budget'; App = 'EternalWars.Game'; Frames = 300; Map = $townLayoutsMap; Args = @('--budget=2560x1440')
       Input = '3:key:Enter;12:key:T;34:key:Bracketright;56:key:Bracketright;78:key:Bracketright;100:key:Bracketright;122:key:Bracketright;144:key:Bracketright;166:key:Bracketright;188:key:Bracketright;200:key:Bracketright;212:key:Bracketright;224:key:Bracketright;236:key:Bracketright;248:key:Bracketright;260:key:Bracketright;272:key:Bracketright;290:key:Escape' },
    # The town's 3D scene (ADR-297): T opens Castlebridge (Haven's layout behind the bars, the map put away); a click on the tavern opens the
    # Tavern window, one on the keep the Hall; a right click on the Footman's dwelling (a greybox block) its information; Escape leaves, and
    # the map is back.
    @{ Name = 'game-town-scene'; App = 'EternalWars.Game'; Frames = 76; Map = $townsMap
       Input = '3:key:Enter;12:key:T;20:click:640,470;30:key:Escape;36:click:1140,250;46:key:Escape;52:rclick:470,525;62:key:Escape;66:key:Escape;70:key:Escape' },
    # The town camera (TOWN-14, ADR-302): T opens Castlebridge at the hero shot; a click on the tavern opens its window at once while the camera
    # eases toward it, Escape eases back; E held turns the view to its 25° stop, two wheel turns zoom in two steps; B opens the Hall and the
    # camera eases to the keep from there, Escape back; Q held turns it the other way, the wheel and − zoom out; Escape leaves.
    @{ Name = 'game-town-camera'; App = 'EternalWars.Game'; Frames = 110; Map = $townsMap
       Input = '3:key:Enter;12:key:T;20:click:640,470;34:key:Escape;44:hold:E,24;56:wheel:up,2,800,300;70:key:B;82:key:Escape;88:hold:Q,14;96:wheel:down,1,800,300;98:key:Minus;106:key:Escape' },
    # Hover and pick (ADR-304): T opens Castlebridge; the pointer over the tavern outlines it in bronze (its name and state on the status bar),
    # over the caravan's empty plot a ghost of it shows; → walks the ring of plots and ← back, Enter opens the one reached (the Stables' land: the
    # Hall with the Stables selected); "Show in town" closes the Hall and shows the Stables' ghost, the camera eased toward it; Escape puts it away;
    # a click on the caravan's land opens the Hall with the Caravan selected; Escape twice leaves.
    @{ Name = 'game-town-pick'; App = 'EternalWars.Game'; Frames = 124; Map = $townsMap
       Input = '3:key:Enter;12:key:T;20:move:640,470;28:move:900,600;34:key:Right;40:key:Right;46:key:Right;52:key:Right;58:key:Right;64:key:Right;70:key:Left;76:key:Enter;84:press:Show in town;98:key:Escape;106:click:900,600;114:key:Escape;118:key:Escape' },
    # The eight town layouts (ADR-300): Blue holds a town of each type with everything built; T opens the first, ] goes through the other seven
    # (each layout loaded, every plot shown; a layout without a plot for one of its town's buildings or lines reports an error); Escape leaves.
    @{ Name = 'game-town-layouts'; App = 'EternalWars.Game'; Frames = 104; Map = $townLayoutsMap
       Input = '3:key:Enter;12:key:T;24:key:Bracketright;34:key:Bracketright;44:key:Bracketright;54:key:Bracketright;64:key:Bracketright;74:key:Bracketright;84:key:Bracketright;96:key:Escape' },
    # The town scene's states (ADR-301): four Haven towns, T opens the village (Village Hall, no fort: no walls, the village's houses), ] the
    # town (Town Hall, Fort: wall and gate, more houses), ] the city (City Hall, Citadel: the moat), ] the Capitol (Castle: towers with
    # banners; the two closed dwelling lines show citizens' homes); Escape leaves.
    @{ Name = 'game-town-states'; App = 'EternalWars.Game'; Frames = 60; Map = $townStatesMap
       Input = '3:key:Enter;12:key:T;24:key:Bracketright;34:key:Bracketright;44:key:Bracketright;54:key:Escape' },
    # Switching towns (ADR-296): T opens Castlebridge, ] opens Dawnford, [ comes back, the Next town button goes on; Escape leaves.
    @{ Name = 'game-town-switch'; App = 'EternalWars.Game'; Frames = 64; Map = $magicMap
       Input = '3:key:Enter;12:key:T;22:key:Bracketright;34:key:Bracketleft;46:press:Next town;58:key:Escape' },
    # The Hall (ADR-296) over Haven's 3D scene (ADR-308): B opens it; the Brotherhood tile (the tavern's second level) selected shows its card, Build builds it: the Hall
    # closes and the construction scene plays (ADR-304: the tavern rising through its scaffolding); B opens the Hall again, the Archer's
    # dwelling selected shows what it needs; Escape twice leaves.
    @{ Name = 'game-town-build'; App = 'EternalWars.Game'; Frames = 90; Map = $townsMap
       Input = '3:key:Enter;12:key:T;20:key:B;30:click:@Brotherhood;40:press:Build;60:key:B;68:click:@Archer;80:key:Escape;84:key:Escape' },
    # Recruiting (ADR-296): R, Recruit Footman with its destination, Max, Recruit; Recruit all shows its summary, Recruit; then the Pikeman's
    # dwelling window from the building strip; Escape twice leaves.
    @{ Name = 'game-town-recruit'; App = 'EternalWars.Game'; Frames = 92; Map = $townsMap
       Input = '3:key:Enter;12:key:T;20:key:R;30:press:Recruit Footman;38:press:Max;44:press:Recruit;52:press:Recruit all;62:press:Recruit;70:key:Escape;74:press:Pikeman dwelling;84:key:Escape;88:key:Escape' },
    # The other windows (ADR-296) in a town with them all: G the mage guild's shelves, V the tavern, M the marketplace (gold for ore, Trade),
    # S the blacksmith's cards, F the defences, C the caravan; Escape twice leaves.
    @{ Name = 'game-town-windows'; App = 'EternalWars.Game'; Frames = 100; Map = $caravanMap
       Input = '3:key:Enter;12:key:T;20:key:G;30:key:V;40:key:M;50:press:Trade;58:key:S;68:key:F;78:key:C;88:key:Escape;94:key:Escape' },
    # The hero screen's army bar (ADR-294): Tab selects Orrin, H opens his screen; the pikemen dragged to an empty slot; Ctrl + drag moves one
    # archer; Alt + click spreads the archers; a right click on a stack opens its information.
    @{ Name = 'game-hero-armies'; App = 'EternalWars.Game'; Frames = 70; Map = $townsMap
       Input = '3:key:Enter;8:key:Tab;12:key:H;20:drag:@20 | Pikeman>@—#1;30:hold:Ctrl,6;31:drag:@10 | Archer>@—#1;42:hold:Alt,4;43:click:@9 | Archer;54:rclick:@20 | Pikeman;64:key:Escape' },
    # Haven's style frame (ADR-294, step 0): the greybox scene after key_haven-1 with its light, fog and grading, rendered alone.
    @{ Name = 'town-style-frame-haven'; App = 'EternalWars.Game'; Frames = 8; Scene = 'res://shared/scenes/towns/haven.tscn'; Input = '' },
    # Quests (ADR-127, ADR-128): the Seer's Hut, Quest Guard and Quest Gate shapes; the hero walks into the hut and completes its first stage, the next one shows; Q opens the
    # quest log, Q closes it.
    @{ Name = 'game-quests'; App = 'EternalWars.Game'; Frames = 64; Map = $questsMap
       Input = '3:key:Enter;12:key:Up;20:key:Up;26:click:@Complete;32:click:@OK;40:key:Q;52:key:Q' },
    # Turn-start messages (EVT-2 f): Blue's player event reveals Grimhollow and says so, the camera moves there; Enter closes it; the town
    # event's message for Castlebridge moves the camera to the town; OK closes each.
    @{ Name = 'game-turn-start-camera'; App = 'EternalWars.Game'; Frames = 40; Map = $turnStartMap
       Input = '3:key:Enter;16:click:@OK;30:click:@OK' },
    # Random placeholders and bans (ADR-126): the new-game screen offers Blue a faction for its random town, its crest "?" (ADR-225); the 2 × 4
    # crest panel opens and Dungeon is picked; the game starts with Castlebridge a Dungeon town, Blue's hero a Dungeon hero and the random monsters drawn.
    @{ Name = 'game-placeholders'; App = 'EternalWars.Game'; Frames = 30; Map = $placeholdersMap
       Input = '5:click:@?;9:click:@Dungeon;13:click:@Start' },
    # A random map (ADR-086, ADR-201): the Random maps tab, Duel, Necropolis, its first hero and the Skeleton tier-1 dwelling for Blue, hero limit 7,
    # Start (Red is a computer player), K, end the turn so the computer plays its turn.
    @{ Name = 'game-random-map'; App = 'EternalWars.Game'; Frames = 105
       Input = '4:press:Random maps;8:click:@Duel;12:click:@?;16:click:@Necropolis;20:click:@Random;24:choose:Morthal (warrior);28:click:@Random;32:choose:Skeleton;36:click:676,651;40:click:@Start;58:key:K;68:key:Escape;74:click:@End turn' },
    # The players of a random map (ADR-225): Free for all's four, Red removed with its "−" (Yellow and Green move up a place with their colours),
    # Blue's swatch swapped with Green's, "+" adds a fourth player in the first free colour (Red); Hard pressed, then Start: the first player plays
    # in green and is called Green.
    @{ Name = 'game-new-game-players'; App = 'EternalWars.Game'; Frames = 60
       Input = '4:press:Random maps;8:click:@Free for all;12:press:Remove Red;16:click:@Colour Blue;20:click:@Green;24:press:Add a player;28:click:@Hard;32:click:@Start' },
    # The main menu (ADR-101, ADR-163) over its picture: Leaderboard opens, Escape returns; "Watch a replay" (ADR-164) opens the Replays and
    # Saves tabs, Saves, Escape back to the main menu; New game, Enter on Start. Pressed by name: Continue has the focus when there is a
    # save, New game when not. The only game scenario without --new-game.
    @{ Name = 'game-main-menu'; App = 'EternalWars.Game'; Frames = 69; MainMenu = $true
       Input = '3:press:Leaderboard;12:key:Escape;18:press:Watch a replay;25:click:@Saves;31:key:Escape;37:press:New game;47:key:Enter' },
    # A campaign (ADR-241, ADR-242): Campaign in the main menu, Border Watch at Normal, the first map's intro, then its briefing dialogue line by
    # line (Next, OK) and the end of Blue's first turn.
    @{ Name = 'game-campaign'; App = 'EternalWars.Game'; Frames = 75; MainMenu = $true
       Input = '3:press:Campaign;10:press:Start;17:press:Start the map;40:press:Next;47:press:OK;55:press:End turn;59:key:Enter' },
    # The viewer on a recorded computer game (the sim's demo record, 4 days): pause at once, Fast, next day three times (day 4), next turn (Red's,
    # with the battle against the wolves), Normal, play: Red walks, the computer's battle plays move by move; skip to its result, which closes
    # by itself, then the replay plays to its end.
    @{ Name = 'game-replay-viewer'; App = 'EternalWars.Game'; Frames = 130; Replay = $replayFile
       Input = '3:key:Space;6:click:@Normal;9:choose:Fast;12:press:Next day;16:press:Next day;20:press:Next day;24:press:Next turn;28:click:@Fast;31:choose:Normal;34:key:Space;90:press:Skip to the result' },
    # A battle only the computer fights taken over (ADR-233): the same record and steps as game-replay-viewer, then Play from here in Red's battle
    # with the wolves: it starts again from its start with Red commanded by hand; quick combat, Enter on the result: back to the replay, paused.
    @{ Name = 'game-replay-take-over'; App = 'EternalWars.Game'; Frames = 130; Replay = $replayFile
       Input = '3:key:Space;6:click:@Normal;9:choose:Fast;12:press:Next day;16:press:Next day;20:press:Next day;24:press:Next turn;28:click:@Fast;31:choose:Normal;34:key:Space;88:press:Play from here;100:key:Q;118:key:Enter' },
    # A battle only the computer fought taken over (ADR-233): the record with Red a computer player (`sim record --computer`), the same steps; Red's
    # battle with the wolves was fought within its move, so Play from here starts it again from its start with Red commanded by hand; quick
    # combat, Enter on the result: back to the replay, paused.
    @{ Name = 'game-replay-computer-battle'; App = 'EternalWars.Game'; Frames = 130; Replay = $computerReplayFile
       Input = '3:key:Space;6:click:@Normal;9:choose:Fast;12:press:Next day;16:press:Next day;20:press:Next day;24:press:Next turn;28:click:@Fast;31:choose:Normal;34:key:Space;88:press:Play from here;100:key:Q;118:key:Enter' },
    # "Play from here" on the map (ADR-185): the same record paused, next day (Blue's day 2), Play from here: the viewer's bar goes, the game's
    # HUD comes with End turn; End turn passes to Red.
    @{ Name = 'game-replay-play-on'; App = 'EternalWars.Game'; Frames = 40; Replay = $replayFile
       Input = '3:key:Space;6:press:Next day;12:press:Play from here;24:press:End turn;28:key:Enter' },
    # A siege (ADR-106, ADR-109): step into the neutral Castle-level town; walls, gate, moat, towers and keep. The footman defends, the
    # ballista (Basic Ballistics) shoots the upper wall, two more defend, quick combat mid-animation (ADR-112 check), Enter on the result.
    @{ Name = 'game-siege'; App = 'EternalWars.Game'; Frames = 150; Map = $siegeMap
       Input = '3:key:Enter;10:key:Up;62:key:D;72:click:955,280;90:key:D;100:key:D;125:key:Q;140:key:Enter' },
    # Dwellings and caravans (ADR-054, ADR-073): visit a map dwelling and recruit the week, close; T, C opens the caravan window, hire
    # from the owned dwelling, Escape twice, end two turns (hot-seat) and look again: the footmen are in the garrison.
    @{ Name = 'game-dwelling-caravan'; App = 'EternalWars.Game'; Frames = 100; Map = $caravanMap
       Input = '3:key:Enter;10:key:Up;22:click:@Recruit week (16) | 960 gold;28:click:@Close;34:key:T;42:key:C;50:click:@Hire 9 | 55 gold each;56:key:Escape;60:key:Escape;64:click:@End turn;72:click:@End turn;82:key:T;90:key:C' },
    # The generated connections map in the editor (no fog): pan to the portal, the blue border guard, the blue keymaster, the garrison,
    # the red border gate and the red keymaster (frames 16, 49, 86, 109, 153, 172).
    @{ Name = 'editor-generated-connections'; App = 'EternalWars.MapEditor'; Frames = 175; Map = $connectionsMap
       Input = '5:hold:S,8;18:hold:A,19;38:hold:W,8;51:hold:S,18;70:hold:D,13;88:hold:D,10;99:hold:S,7;111:hold:D,8;120:hold:W,30;155:hold:W,10;166:hold:D,3' },
    # Information windows on the screens (ADR-154, ADR-178): the kingdom overview's town row, then its total, each closed with Escape before
    # the screen is. Then the town screen's tavern window (V, ADR-296): the Thieves' Guild (ADR-079) from its button, closed with Escape;
    # the first hero's and the second hero's information on their Hire buttons, each closed with Escape, then the tavern window and the
    # town screen.
    @{ Name = 'game-info-screens'; App = 'EternalWars.Game'; Frames = 88; Map = $townsMap
       Input = '3:key:Enter;10:key:K;16:rclick:468,299;20:key:Escape;22:rclick:457,353;26:key:Escape;28:key:Escape;32:key:T;36:key:V;42:click:@Thieves'' guild;50:key:Escape;56:rclick:@Hire | 2500 gold#1;62:key:Escape;66:rclick:@Hire | 2500 gold#2;72:key:Escape;76:key:Escape;80:key:Escape' },
    # Ember March (ADR-114): start, end the turn (the computer plays Red), open the town screen with its building tree, close it.
    @{ Name = 'game-ember-march'; App = 'EternalWars.Game'; Frames = 60; Map = $emberMap
       Input = '3:key:Enter;12:click:@End turn;40:key:T;52:key:Escape' },
    # Inspector rows (ADR-114) on Ember March: Select, then in the Inspector (showing from the start) a Minor artifact pickup, the witch hut, the sphinx, the hero (artifacts).
    @{ Name = 'editor-inspector-objects'; App = 'EternalWars.MapEditor'; Frames = 38; Map = $emberMap
       Input = '3:click:@Select;9:click:616,230;16:click:645,382;23:click:965,552;30:click:352,244' },
    # The tools on a blank map: roads, a river, an object, the gate link tool. Then the map settings window's Week events tab (F7, ADR-084,
    # ADR-256): tick Conflux for the weekly events, switch the events off, undo both with the window's Undo, Escape closes it.
    @{ Name = 'editor-tools'; App = 'EternalWars.MapEditor'; Frames = 80
       Input = '3:click:@Road;6:click:700,600;9:click:740,600;12:click:@River;15:click:800,500;18:click:@Object;21:click:900,450;24:click:@LinkGates;27:click:900,450;30:click:1500,67;39:key:F7;42:click:@Week events;46:click:@conflux;50:click:@Week and month events;56:click:@Undo;62:click:@Undo;70:key:Escape' },
    # The height tools (ADR-182 g) on a blank map: radius 2, strength 8 (the spin boxes' up arrows), raise two strokes, lower one and undo
    # it; then highlands painted with Auto height on.
    # Then generate from a template (ADR-087): the Generate tab, Sylvan for Blue, Generate (Two against two at 128), then the Inspector and the
    # Generate tab again: the settings stay.
    @{ Name = 'editor-heights'; App = 'EternalWars.MapEditor'; Frames = 110
       Input = '3:click:@Height;5:click:183,271;7:click:183,271;9:click:196,304;10:click:196,304;11:click:196,304;12:click:196,304;13:click:196,304;14:click:196,304;17:drag:460,600,900,600;21:drag:460,620,900,620;26:click:@Raise;29:choose:Lower;33:drag:460,560,900,560;38:click:@Undo;42:click:@Terrain;45:click:@Highlands;48:click:400,400;59:click:@Generate;64:click:@Random;68:choose:sylvan;72:click:@Generate;86:click:@Inspector;90:click:@Generate' },
    # The template editor (ADR-117) in the workspace's Template tab (EDIT-3): the tab, select the middle zone's card (its obelisk count, ADR-140),
    # add a treasure zone, drag it (on the grid, ADR-270), right-drag it onto start-1 to connect, Generate (off the main thread, ADR-274: the
    # graph stays usable while it runs).
    @{ Name = 'editor-template-editor'; App = 'EternalWars.MapEditor'; Frames = 60
       Input = '3:click:@Template;10:click:781,405;18:click:@+ Treasure;24:drag:600,510,740,600;30:rdrag:740,625,527,420;44:click:@Generate' },
    # The preview (ADR-274): Generate shows the new template's map in the Preview tab, its zones outlined and named, the template still open;
    # Open in map editor opens it in the Map tab.
    @{ Name = 'editor-template-preview'; App = 'EternalWars.MapEditor'; Frames = 140
       Input = '3:click:@Template;8:click:@Generate;120:click:@Open in map editor' },
    # Generate many (ADR-274): at 72×72, the Seeds tab's Generate many checks 20 seeds off the main thread; the tab fills with thumbnails,
    # each seed's checks and fairness, and their spread over the batch.
    @{ Name = 'editor-template-batch'; App = 'EternalWars.MapEditor'; Frames = 200
       Input = '3:click:@Template;6:click:@96×96;9:choose:72×72;12:click:@Seeds;15:click:@Generate many' },
    # The template graph's selection and clipboard (ADR-270, ADR-271): pan up with the middle button, a marquee round start-1 and middle, Ctrl+C,
    # a click on empty ground, Ctrl+V pastes them there (new ids, the connection between them), Ctrl+D duplicates the pasted pair beside it;
    # the History tab, Ctrl+A, Delete removes every zone as one step, Ctrl+Z brings them back.
    @{ Name = 'editor-template-graph'; App = 'EternalWars.MapEditor'; Frames = 50
       Input = '3:click:@Template;6:mdrag:300,560,300,400;10:drag:420,190,800,320;14:key:Ctrl+C;17:click:680,420;21:key:Ctrl+V;26:key:Ctrl+D;31:click:@History;34:key:Ctrl+A;38:key:Delete;42:key:Ctrl+Z' },
    # Mirror tools (ADR-272): open the shipped Mirror duel (mirrored by an axis), add a treasure zone and press Mirror: its image comes in on the
    # other side with a dotted mirror line, both selected; the History tab shows the step.
    @{ Name = 'editor-template-mirror'; App = 'EternalWars.MapEditor'; Frames = 34
       Input = '3:click:@Template;8:click:@Shipped…;11:choose:Mirror duel;16:click:@+ Treasure;20:click:@Mirror;25:click:@History' },
    # The inspector's pickers and zone presets (ADR-273): add a treasure zone (its inspector: terrains, town types, presets), apply the
    # built-in Rich treasure, save it as a preset of the user's (Enter takes the proposed name), add a zone from that preset with + Preset.
    @{ Name = 'editor-template-presets'; App = 'EternalWars.MapEditor'; Frames = 44
       Input = '3:click:@Template;8:click:@+ Treasure;12:press:Apply preset;16:choose:Rich treasure;20:press:Save as preset…;25:key:Enter;29:click:@+ Preset;32:choose:My zone 1 (yours);38:click:@History' },
    # Template undo (EDIT-2, ADR-250): on the Template tab add a treasure zone and a junction, open the template's History under the graph;
    # Ctrl+Z twice undoes both (dim in the list), Ctrl+Y redoes the treasure zone; on the Map tab Undo is the map's (nothing to undo).
    @{ Name = 'editor-template-undo'; App = 'EternalWars.MapEditor'; Frames = 44
       Input = '3:click:@Template;8:click:@+ Treasure;12:click:@+ Junction;16:click:@History;20:key:Ctrl+Z;24:key:Ctrl+Z;28:key:Ctrl+Y;34:click:@Map;38:key:Ctrl+Z' },
    # Template files (ADR-250): a treasure zone makes the template unsaved ("Template*"); Save asks where (no file yet) and Enter saves it to
    # the scenario's templates folder; a junction, then a shipped template (Duel) asks over the unsaved work and "Don't save" opens it
    # read-only; Ctrl+S on the Template tab asks where to save a copy, and Enter saves it.
    @{ Name = 'editor-template-files'; App = 'EternalWars.MapEditor'; Frames = 60
       Input = '3:click:@Template;8:click:@+ Treasure;12:click:@Save;18:key:Enter;24:click:@+ Junction;28:click:@Shipped…;31:choose:Duel;36:click:@Don''t save;42:key:Ctrl+S;48:key:Enter' },
    # The Events tab (EVT-6) on the quests map: open the tab, pick the first event (its tree), then its first action (the node text).
    @{ Name = 'editor-events'; App = 'EternalWars.MapEditor'; Frames = 30; Map = $questsMap
       Input = '3:click:@Events;8:click:1096,170;14:click:1145,436' },
    # Bans and placeholders (ADR-126) on the placeholders map: the map settings window's Bans tab, ban the first hero, undo it, close; Select,
    # the Inspector, the random town.
    @{ Name = 'editor-bans'; App = 'EternalWars.MapEditor'; Frames = 36; Map = $placeholdersMap
       Input = '3:key:F7;6:click:@Bans;10:click:@Agathe Sorrel (haven, Mage);14:click:@Undo;17:key:Escape;21:click:@Select;27:click:420,300' },
    # The arena switch (ARN-2) on the arena map: the map settings window's Victory and loss tab (the arena note, the victory rows off), the
    # General tab, untick the switch, close: Problems, in the bottom region, says the combat place now does nothing.
    @{ Name = 'editor-arena'; App = 'EternalWars.MapEditor'; Frames = 26; Map = $arenaMap
       Input = '3:key:F7;6:click:@Victory and loss;10:click:@General;13:click:@Arena: a day to prepare, then one battle decides the game;17:key:Escape' }
    # The scenarios below find buttons by their text (press:) rather than by pixel.
    # Unique buildings (ADR-138, ADR-143): Orrin enters the Necropolis town, ticks a garrison and a hero stack for the Undead Transformer and
    # transforms them; Ilya enters the Sylvan town (Elder Grove: a notice and a level-up). Stables and the Marketplace screen (ADR-067):
    # Gareth enters the Haven town (Stables: a notice), then the HUD's Marketplace button.
    @{ Name = 'game-unique-buildings'; App = 'EternalWars.Game'; Frames = 100; Map = $uniqueMap
       Input = '3:key:Enter;10:key:Up;17:press:Undead Transformer;20:press:Garrison: 12 Pikeman;24:press:Hero: 20 Footman;30:press:Transform;34:key:Escape;37:key:Escape;40:press:Ilya;44:key:Up;56:press:→ Basic;60:key:Escape;64:press:OK;68:key:Escape;74:press:Gareth;78:key:Up;90:key:Escape;92:key:Escape;94:press:Marketplace' },
    # Map sites (ADR-079, ADR-080, ADR-082, ADR-105, ADR-139): each hero steps north onto its site: the University's lessons, the War
    # Academy's, the Arena's choice, Pandora's Box, the Witch Hut, the Sphinx (the map's riddle, answered wrong: a right answer's 5000 experience would bring level-ups), the Sacrificial Altar, the Artifact Merchant.
    # At the Witch Hut, information windows on a decision prompt (ADR-178), in the middle of the screen (ADR-186): right-click the prompt
    # (the hut), then the skill offered. After Blue's turn Red's map sites: the Refugee Camp and the Cartographer (Buy reveals the surface).
    @{ Name = 'game-map-sites'; App = 'EternalWars.Game'; Frames = 244; Map = $sitesMap
       Input = '3:key:Enter;8:press:Ulric;12:key:Up;24:key:Escape;28:press:Wenda;32:key:Up;44:key:Escape;48:press:Lior;52:key:Up;62:press:+2 Attack;68:press:Pella;72:key:Up;82:press:Open;90:key:Escape;94:press:Hagan;98:key:Up;108:rclick:800,430;112:key:Escape;114:rclick:640,477;118:key:Escape;120:press:Leave;124:press:Sefra;128:key:Up;138:press:A cave;144:press:Mortis;148:key:Up;158:key:Escape;160:press:Bram;164:key:Up;176:key:Escape;180:press:End turn;184:key:Enter;188:press:Corin;192:key:Up;204:key:Escape;208:press:Tamsin;212:key:Up;224:press:Buy' },
    # The Grail (ADR-140): Obelisa visits the obelisk (notice), P opens the puzzle map and closes it; Spade digs (an empty hole), Digby digs up the
    # Grail; two turns later Digby carries it into Castlebridge (the Grail building's notice).
    @{ Name = 'game-grail'; App = 'EternalWars.Game'; Frames = 100; Map = $grailMap
       Input = '3:key:Enter;8:press:Obelisa;12:key:Up;24:key:P;34:key:P;38:press:Spade;42:key:G;50:press:Digby;54:key:G;62:press:End turn;66:key:Enter;68:press:End turn;72:key:Enter;76:press:Digby;80:key:Up' },
    # Magic (ADR-110, ADR-121, ADR-122, ADR-123): Magus enters the guild town (fixed guild spells), the hero screen with ranks and the Assemble
    # button, Detect Magic's window; then Bookless buys a spell book in the second town's mage guild window (G).
    @{ Name = 'game-magic'; App = 'EternalWars.Game'; Frames = 86; Map = $magicMap
       Input = '3:key:Enter;8:press:Magus;12:key:Up;24:key:Escape;28:key:H;34:press:Detect Magic (4 mana);44:press:Close;50:press:Close;56:press:Bookless;60:key:Up;68:key:G;74:press:spell book' },
    # Battle magic (ADR-122, ADR-123) and surrender (ADR-081): Distant Vision on the computer mage, then Magus attacks it, casts Blind, defends
    # until the AI has cast its spells, surrenders.
    @{ Name = 'game-battle-magic'; App = 'EternalWars.Game'; Frames = 196; Map = $magicMap
       Input = '3:key:Enter;8:press:Magus;12:key:H;18:press:Distant Vision;24:click:800,500;36:press:Close;40:key:Up;50:press:Close;56:key:Down;72:click:800,552;78:click:800,552;108:press:Blind;114:click:1257,490;128:key:D;138:key:D;148:key:D;180:press:Surrender' },
    # A field zone (ADR-149): the same battle, Darkness cast on the computer mage's stack and drawn over its cells, still there in round 2.
    @{ Name = 'game-battle-zone'; App = 'EternalWars.Game'; Frames = 210; Map = $magicMap
       Input = '3:key:Enter;8:press:Magus;12:key:H;18:press:Distant Vision;24:click:800,500;36:press:Close;40:key:Up;50:press:Close;56:key:Down;72:click:800,552;78:click:800,552;108:press:Darkness;114:click:1257,490;128:key:D;138:key:D;148:key:D;158:key:D;168:key:D;178:key:D;200:press:Surrender' },
    # A control spell (ADR-150): the same battle, Hypnotize cast on the computer mage's stack, which turns to Magus' colour and side until its turn
    # is over.
    @{ Name = 'game-battle-control'; App = 'EternalWars.Game'; Frames = 200; Map = $magicMap
       Input = '3:key:Enter;8:press:Magus;12:key:H;18:press:Distant Vision;24:click:800,500;36:press:Close;40:key:Up;50:press:Close;56:key:Down;72:click:800,552;78:click:800,552;108:press:Hypnotize;114:click:1257,490;128:key:D;138:key:D;148:key:D;158:key:D;168:key:D;184:press:Surrender' },
    # A two-click spell (ADR-151): the same battle, Teleport cast on Magus' footmen (first click), which land 8 cells on (second click); the Griffin then defends.
    @{ Name = 'game-battle-teleport'; App = 'EternalWars.Game'; Frames = 150; Map = $magicMap
       Input = '3:key:Enter;8:press:Magus;12:key:H;18:press:Distant Vision;24:click:800,500;36:press:Close;40:key:Up;50:press:Close;56:key:Down;72:click:800,552;78:click:800,552;108:press:Teleport;114:click:410,262;120:click:650,290;132:key:D' },
    # A copy (ADR-152): the same battle, Mirror Image cast on Magus' footmen; the copy stands next to them; the Footman then defends.
    # Then the victory screen (ADR-103): quick combat beats the last enemy hero; the screen shows the score (nothing is pressed, so the
    # ranking and the replays folder stay as they are).
    @{ Name = 'game-battle-mirror'; App = 'EternalWars.Game'; Frames = 190; Map = $magicMap
       Input = '3:key:Enter;8:press:Magus;12:key:H;18:press:Distant Vision;24:click:800,500;36:press:Close;40:key:Up;50:press:Close;56:key:Down;72:click:800,552;78:click:800,552;108:press:Mirror Image;114:click:410,262;132:key:D;150:key:Q;180:key:Enter' },
    # A deployment step and war machines on the field (ADR-076): the battle width (Tactics); drag and drop in the deployment step (ADR-155):
    # the 60 stack is dragged down the zone (carried at frame 61, placed at 62), the 20 stack is dropped outside the zone (refused, back in
    # its place at 68); Start, quick combat, the result, then the Necromancy screen (ADR-078).
    @{ Name = 'game-necromancy'; App = 'EternalWars.Game'; Frames = 128; Map = $necromancyMap
       Input = '3:key:Enter;10:key:Right;40:press:32;60:drag:425,305,360,600;66:drag:370,480,900,480;76:key:Enter;90:key:Q;118:key:Enter' },
    # What ADR-148 had not seen, on the spells map (ADR-235), each with a fixed seed so its battle is the same every run.
    # Meteor Shower III's rubble: Pyro visits Emberkeep (the guild raises the spell to rank III), walks into the wolves and casts it on the
    # second wolf stack; stones lie on the free cells it hit.
    @{ Name = 'game-meteor-rubble'; App = 'EternalWars.Game'; Frames = 100; Map = $spellsMap; Seed = 1
       Input = '3:key:Enter;8:press:Pyro;12:key:Up;24:key:Escape;28:key:Down;36:key:Down;62:press:Meteor Shower III (18);66:click:1205,300' },
    # Grave Whisper (its window), Ledger of Ages (next week, the Red heroes' armies), then Eavesdrop on Gloomhold (its buildings and garrison).
    @{ Name = 'game-adventure-intel'; App = 'EternalWars.Game'; Frames = 76; Map = $spellsMap; Seed = 1
       Input = '3:key:Enter;8:press:Seer;12:key:H;18:press:Grave Whisper (5 mana);28:press:Ledger of Ages (10 mana);38:press:Close;42:press:Eavesdrop (8 mana);50:click:800,330' },
    # The computer's battle magic: Medic attacks Warlock and defends; Warlock casts Bless, Slow, Resurrection and Haste (the godot.log's
    # "battle log:" lines); Medic casts Resurrection on the archers in round 3.
    @{ Name = 'game-ai-spells'; App = 'EternalWars.Game'; Frames = 310; Map = $spellsMap; Seed = 1
       Input = '3:key:Enter;8:press:Medic;12:key:Right;36:key:D;46:key:D;56:key:D;66:key:D;76:key:D;86:key:D;96:key:D;106:key:D;116:key:D;120:press:Resurrection (25);124:click:335,490;136:key:D;146:key:D;156:key:D;166:key:D;176:key:D;186:key:D;196:key:D;206:key:D;216:key:D;226:key:D;236:key:D;246:key:D;256:key:D;266:key:D;276:key:D;286:key:D;296:key:D' },
    # A dug hole on the map: Digger digs (G), the turn ends, and on day 2 Digger steps west off the hole.
    @{ Name = 'game-dug-hole'; App = 'EternalWars.Game'; Frames = 70; Map = $spellsMap; Seed = 1
       Input = '3:key:Enter;8:press:Digger;12:key:G;20:press:End turn;24:key:Enter;44:press:Digger;50:key:Left' },
    # A guild spell set by hand in the editor: select Emberkeep, open the inspector, open a guild slot's picker (the press scrolls the slots into
    # view), then the first level-1 slot's, which closes the other, and pick Brand from its grid (ADR-255).
    @{ Name = 'editor-guild-spell'; App = 'EternalWars.MapEditor'; Frames = 40; Map = $spellsMap
       Input = '5:press:Select;10:click:400,300;20:press:— drawn —;26:click:@— drawn —#1;34:press:Brand' },
    # The siege's gate, keep and towers and Earthquake (ADR-120): the ballista breaks the gate; a second run casts Earthquake.
    @{ Name = 'game-siege-gate'; App = 'EternalWars.Game'; Frames = 104; Map = $siegeMagicMap
       Input = '3:key:Enter;10:key:Up;62:key:D;72:click:958,432' },
    @{ Name = 'game-siege-earthquake'; App = 'EternalWars.Game'; Frames = 150; Map = $siegeMagicMap
       Input = '3:key:Enter;10:key:Up;62:press:Earthquake;100:key:D;110:key:D;120:key:D;135:key:D' },
    # The editor's Grail tool, the map settings window's difficulty and hero limit rows (ADR-083, ADR-103) and the town inspector's faction
    # and guild spells (ADR-083, ADR-123).
    @{ Name = 'editor-grail-and-town'; App = 'EternalWars.MapEditor'; Frames = 42; Map = $grailMap
       Input = '3:key:F7;6:click:@General;9:key:Escape;12:press:Grail;16:click:400,450;22:press:Select;26:click:720,560' },
    # The map settings window (F7, ADR-256) on a blank map: every tab; a player added on the Players tab, the arena switch on the General
    # tab, both undone with the window's Undo; F7 closes it on the Bans tab and opens it again there.
    @{ Name = 'editor-map-settings'; App = 'EternalWars.MapEditor'; Frames = 60
       Input = '3:key:F7;6:click:@Players;10:click:@Add player;14:click:@Victory and loss;18:click:@Bans;22:click:@Week events;26:click:@General;30:click:@Arena: a day to prepare, then one battle decides the game;34:click:@Undo;38:click:@Undo;41:click:@Bans;44:key:F7;48:key:F7' },
    # The map's content (EDIT-12, ADR-257) on a blank map: the map settings window's Rumours tab, a rumour typed and applied; the Players tab
    # with its Factions column ("Any faction"); Escape closes the window. Then a treasure chest placed, selected and named "Hoard" in the
    # inspector's Name field.
    @{ Name = 'editor-map-content'; App = 'EternalWars.MapEditor'; Frames = 100
       Input = '3:key:F7;6:click:@Rumours;10:click:800,330;13:text:They say the old bridge is guarded;50:click:@Apply rumours;54:click:@Players;60:key:Escape;63:click:@Object;66:click:900,450;70:click:@Select;73:click:900,450;79:click:1180,188;82:text:Hoard;90:key:Enter' },
    # The map's texts (EDIT-11, ADR-258) on a blank map: the map settings window's Texts tab; the second player's name edited in place and
    # applied with Enter; the search keeps the rows with "Banner"; the window's Undo takes the name back.
    @{ Name = 'editor-texts'; App = 'EternalWars.MapEditor'; Frames = 44
       Input = '3:key:F7;6:click:@Texts;10:click:930,466;13:text: Banner;20:key:Enter;24:click:800,276;27:text:Banner;36:click:@Undo' },
    # The empty state and the new-map wizard (ADR-259): the editor starts on a blank map with the empty state's card; Ctrl+N opens the wizard:
    # 96 × 96, two levels, snow; Red a computer player; Create map. Then the wizard again with the Duel template: it ends in the Generate tab.
    @{ Name = 'editor-new-map'; App = 'EternalWars.MapEditor'; Frames = 76
       Input = '6:key:Ctrl+N;10:click:@96 × 96;13:click:@The surface and the underground;16:click:@Snow;20:click:@Next;24:click:@Computer#2;28:click:@Next;32:click:@Create map;44:key:Ctrl+N;48:click:@Next;51:click:@Next;55:click:@Duel;59:click:@Go to Generate' },
    # The workspace's regions (EDIT-1, ADR-250) on Ember March: the Monster tool's options wrap in the left region; the right region's
    # splitter dragged to the left; the left region collapsed; F11 hides the others and brings them back; the left region opens again;
    # the Generate tab is brought to the front. Then the menus built from the command registry: Edit (Undo and Redo greyed:
    # nothing to undo), and Tools, where Hero is picked (the palette follows).
    @{ Name = 'editor-shell'; App = 'EternalWars.MapEditor'; Frames = 54; Map = $emberMap
       Input = '4:click:@Monster;8:drag:1036,400,936,400;14:click:@Left panel;19:key:F11;24:key:F11;29:click:@Left panel;33:click:@Generate;37:click:60,22;42:key:Escape;45:click:190,22;48:choose:Hero' },
    # The side bars (ADR-282) on Ember March: Layers on the right bar shows the Layers panel, pressed again it collapses the right region;
    # History at the left bar's foot shows History in the bottom region, pressed again it collapses that; Tools collapses the left region
    # and brings it back; the right region opens on Fairness, and its header's Hide button collapses it; Ctrl+F opens Find on the map
    # and a kind ("dwelling") lists the objects of that kind, named or not.
    @{ Name = 'editor-side-bars'; App = 'EternalWars.MapEditor'; Frames = 60; Map = $emberMap
       Input = '4:click:@Layers;9:click:@Layers;14:click:@History;19:click:@History;24:click:@Tools;29:click:@Tools;34:click:@Fairness;40:click:@Hide#2;45:key:Ctrl+F;49:text:dwelling;56:key:Escape' },
    # The bottom panels and the status bar (EDIT-1, ADR-250) on a blank map: a road, a river and an object are three steps; History lists
    # them and a click on the first jumps back to it (the river and the object are undone and dim); Output keeps the messages (the gate link tool, clicked on empty ground, says what it needs); the status
    # bar's problem counts open Problems, where the warnings are hidden and shown again and the kind Heroes is picked.
    @{ Name = 'editor-bottom-panels'; App = 'EternalWars.MapEditor'; Frames = 66
       Input = '3:click:@Road;6:click:700,600;9:click:@River;12:click:800,500;15:click:@Object;18:click:900,450;21:click:@LinkGates;24:click:1000,300;27:click:@History;31:click:500,736;36:click:@Output;43:click:1327,877;47:click:478,708;51:click:478,708;55:click:604,708;59:choose:Heroes (2)' },
    # The editor's files (EDIT-1, ADR-250) on a blank map: a road makes unsaved work; File > New map, the wizard's Create map asks over it
    # and Cancel stays; Ctrl+N and Create map again, and "Don't save" opens the new map ("Not saved"); File > Save as shows the file dialog in the scenario's own maps folder, and Enter saves the map there ("Saved" with the time).
    @{ Name = 'editor-files'; App = 'EternalWars.MapEditor'; Frames = 60
       Input = '3:click:@Road;6:click:700,600;10:click:24,24;13:choose:New map…;16:click:@Create map;20:click:@Cancel;24:key:Ctrl+N;28:click:@Create map;33:click:@Don''t save;40:click:24,24;43:choose:Save as;49:key:Enter' },
    # Recovery (EDIT-1, ADR-250): a session marker and an autosave (Ember March) are planted, as a crashed editor leaves them; the editor
    # starts on its blank map and offers the autosave; Restore opens it as unsaved work.
    @{ Name = 'editor-recovery'; App = 'EternalWars.MapEditor'; Frames = 30; Recovery = $emberMap
       Input = '12:click:@Restore' },
    # The command palette (EDIT-1, ADR-250) on a blank map: Ctrl+Shift+P, "roa" leaves the Road tool, Enter puts it in use; the
    # palette again, "panel" lists the panel commands with their shortcuts, Down and Enter run the second (the bottom panel collapses).
    @{ Name = 'editor-palette'; App = 'EternalWars.MapEditor'; Frames = 50
       Input = '3:key:Ctrl+Shift+P;10:text:roa;18:key:Enter;24:key:Ctrl+Shift+P;30:text:panel;38:key:Down;42:key:Enter' },
    # Preferences (EDIT-1, ADR-250): Ctrl+Comma opens the window; the UI scale goes to 150 %, OK applies it; the window again shows
    # what was kept.
    @{ Name = 'editor-preferences'; App = 'EternalWars.MapEditor'; Frames = 40
       Input = '3:key:Ctrl+Comma;9:click:@100 %;13:choose:150 %;19:click:@OK;28:key:Ctrl+Comma' },
    # The shortcut editor (ADR-275): Preferences' Shortcuts tab, "undo" in the filter; Undo's key gets Ctrl+S, Save's (the conflict in
    # red, OK disabled), then Ctrl+U; OK keeps it; the tab again shows Undo on Ctrl+U.
    @{ Name = 'editor-shortcuts'; App = 'EternalWars.MapEditor'; Frames = 50
       Input = '3:key:Ctrl+Comma;8:click:@Shortcuts;12:text:undo;16:click:@Ctrl+Z;19:key:Ctrl+S;24:click:@Ctrl+S;27:key:Ctrl+U;31:click:@OK;36:key:Ctrl+Comma;40:click:@Shortcuts;44:text:undo' },
    # F1 help (ADR-276): F1 over the Erase tool's button opens the help at the Erase page (made from tools.md); Escape closes it; F1 over
    # the Events tab opens Map events (not the inspector's Events); on the Template tab F1 over the Preview tab opens Preview (random-maps.md).
    @{ Name = 'editor-help'; App = 'EternalWars.MapEditor'; Frames = 60
       Input = '3:click:@Erase;8:key:F1;20:key:Escape;22:click:@Events#2;26:key:F1;34:key:Escape;38:click:@Template;44:click:@Preview;48:key:F1' },
    # The UI's scale and contrast (ADR-277) on Ember March: every panel and window in turn (the left region with the Object tool, the right
    # and bottom tabs, the map settings, Preferences, the wizard, the palette, the help, the Template tab's panels) at 100, 150 and 200 %
    # and in high contrast, which Preferences then turns off in the running editor. Points are the UI's, so the same script runs at every
    # scale.
    @{ Name = 'editor-scale-100'; App = 'EternalWars.MapEditor'; Frames = 100; Map = $emberMap
       Input = '4:click:@Object;8:click:@Layers;11:click:@Overlays;14:click:@Fairness;18:click:@Events;22:click:@Generate;26:click:@History;29:click:@Output;32:click:@Problems;36:key:F7;40:click:@Players;44:click:@Texts;48:key:Escape;51:key:Ctrl+Comma;55:click:@Shortcuts;59:key:Escape;62:key:Ctrl+N;66:key:Escape;69:key:Ctrl+Shift+P;73:key:Escape;76:key:F1;80:key:Escape;83:click:@Template;88:click:@Preview;92:click:@Seeds;96:click:@Graph' },
    @{ Name = 'editor-scale-150'; App = 'EternalWars.MapEditor'; Frames = 100; Map = $emberMap; Preferences = 'ui_scale=150'
       Input = '4:click:@Object;8:click:@Layers;11:click:@Overlays;14:click:@Fairness;18:click:@Events;22:click:@Generate;26:click:@History;29:click:@Output;32:click:@Problems;36:key:F7;40:click:@Players;44:click:@Texts;48:key:Escape;51:key:Ctrl+Comma;55:click:@Shortcuts;59:key:Escape;62:key:Ctrl+N;66:key:Escape;69:key:Ctrl+Shift+P;73:key:Escape;76:key:F1;80:key:Escape;83:click:@Template;88:click:@Preview;92:click:@Seeds;96:click:@Graph' },
    @{ Name = 'editor-scale-200'; App = 'EternalWars.MapEditor'; Frames = 100; Map = $emberMap; Preferences = 'ui_scale=200'
       Input = '4:click:@Object;8:click:@Layers;11:click:@Overlays;14:click:@Fairness;18:click:@Events;22:click:@Generate;26:click:@History;29:click:@Output;32:click:@Problems;36:key:F7;40:click:@Players;44:click:@Texts;48:key:Escape;51:key:Ctrl+Comma;55:click:@Shortcuts;59:key:Escape;62:key:Ctrl+N;66:key:Escape;69:key:Ctrl+Shift+P;73:key:Escape;76:key:F1;80:key:Escape;83:click:@Template;88:click:@Preview;92:click:@Seeds;96:click:@Graph' },
    @{ Name = 'editor-contrast'; App = 'EternalWars.MapEditor'; Frames = 118; Map = $emberMap; Preferences = 'high_contrast=true'
       Input = '4:click:@Object;8:click:@Layers;11:click:@Overlays;14:click:@Fairness;18:click:@Events;22:click:@Generate;26:click:@History;29:click:@Output;32:click:@Problems;36:key:F7;40:click:@Players;44:click:@Texts;48:key:Escape;51:key:Ctrl+Comma;55:click:@Shortcuts;59:key:Escape;62:key:Ctrl+N;66:key:Escape;69:key:Ctrl+Shift+P;73:key:Escape;76:key:F1;80:key:Escape;83:click:@Template;88:click:@Preview;92:click:@Seeds;96:click:@Graph;100:key:Ctrl+Comma;105:click:@High contrast;109:click:@OK' },
    # Sample maps (ADR-259) from the Help menu: on the Template tab, Help > Sample maps > Two Towns opens it as a new map on the Map tab.
    @{ Name = 'editor-samples'; App = 'EternalWars.MapEditor'; Frames = 40
       Input = '3:click:@Template;8:click:345,22;12:choose:Sample maps;16:choose:Two Towns' },
    # Content picked from pictures, not lists (ADR-281): the Monster tool's creature picker searched for Dwarf (Enter takes the first found) and
    # a Dwarf placed; the Victory and loss tab's artifact picker searched for Iron Band and an Obtain artifact victory added.
    @{ Name = 'editor-content-pickers'; App = 'EternalWars.MapEditor'; Frames = 100
       Input = '4:click:@Monster;8:click:@Timber Wolf (T1);12:text:dwarf;22:key:Enter;26:click:900,450;32:key:F7;36:click:@Victory and loss;40:click:@Victory: capture a town;44:choose:Victory: obtain an artifact;48:click:@Badge of Courage;52:text:iron band;64:key:Enter;68:click:@Add condition;90:key:Escape' },
    # A first map (ADR-279), as a newcomer makes it: the empty state's New map…, the wizard's defaults (two players, blank); Academy from the
    # browser's Towns placed for Blue (the Object tool's Owner) and Dungeon for Red; the two heroes' army warnings fixed from the Problems
    # panel (no problem left); saved as first-map; Test (F5) shows its window and is cancelled.
    @{ Name = 'editor-first-use'; App = 'EternalWars.MapEditor'; Frames = 166
       Input = '6:click:@New map…;14:click:@Next;22:click:@Next;30:click:@Create map;40:click:@Towns;46:click:@Academy;50:click:@Neutral;54:choose:Blue;60:click:600,250;66:click:@Dungeon;70:click:@Blue;74:choose:Red;80:click:1000,600;90:click:700,738;94:click:@Fix;104:click:700,738;108:click:@Fix;118:key:Ctrl+S;124:text:first-map;140:key:Enter;150:key:F5;160:click:@Cancel' },
    # Selection and move (EDIT-1, ADR-250) on a blank map: two chests are placed; the Select tool (V) drags a rectangle over them (the
    # marquee, then tiles and chests selected); a drag from a selected chest moves both (the ghost, then the move); Ctrl+A selects
    # everything, Escape nothing; the Move tool (M) drags one chest onto the other (red ghost, refused); a click selects it, Delete removes it.
    @{ Name = 'editor-selection'; App = 'EternalWars.MapEditor'; Frames = 70
       Input = '3:click:@Object;6:click:700,500;9:click:760,500;12:key:V;15:drag:640,440,820,560;22:drag:700,500,700,400;30:key:Ctrl+A;34:key:Escape;38:key:M;41:drag:730,395,768,395;50:click:730,395;56:key:Delete' }
    # The clipboard, the brushes and the eraser (EDIT-4, ADR-250): a chest and a road drawn with one drag, a rectangle round both, Ctrl+C,
    # Ctrl+V and two clicks (two pasted copies, then the red ghost over the last one), Escape; the Terrain tool (T) with Fill on fills
    # the grass with snow; the eraser (E) dragged over the first chest; undo brings it back.
    @{ Name = 'editor-clipboard'; App = 'EternalWars.MapEditor'; Frames = 84
       Input = '3:click:@Object;6:click:700,500;9:click:@Road;12:drag:650,470,790,470;18:key:V;21:drag:640,440,820,560;27:key:Ctrl+C;30:key:Ctrl+V;33:click:700,300;40:click:1000,500;46:key:Escape;49:key:T;52:click:@Snow;55:click:@Fill;58:click:500,600;66:key:E;69:drag:660,500,740,500;76:click:@Undo' },
    # Resize (EDIT-8, ADR-250) on a blank 36 x 36 map with a chest: the palette opens "Resize map"; the 48 preset grows the map
    # (nothing is lost); the window again, 24 typed as the width: the red hero would be off the map, so the window lists it, and
    # "Remove and resize" makes the map 24 x 48; Undo brings the 48 x 48 map and the hero back.
    @{ Name = 'editor-resize'; App = 'EternalWars.MapEditor'; Frames = 96
       Input = '3:click:@Object;6:click:700,500;10:key:Ctrl+Shift+P;14:text:resize;22:key:Enter;28:click:@48;33:click:@Resize;42:key:Ctrl+Shift+P;46:text:resize;54:key:Enter;60:text:24;66:click:@Resize;74:click:@Remove and resize;86:click:@Undo' },
    # Select, move, stamp and paste (ADR-250): two chests; the Select tool's rectangle takes them and their tiles; a drag moves both;
    # "Save selection as stamp" (the palette) asks for a name and Enter takes "Stamp 1"; Ctrl+C and Ctrl+V paste a copy; the object
    # browser lists the stamp, a click on it puts it on the pointer and a click on the map stamps it; Undo takes the stamp off.
    @{ Name = 'editor-select-move-paste'; App = 'EternalWars.MapEditor'; Frames = 100
       Input = '3:click:@Object;6:click:700,500;9:click:760,500;12:key:V;15:drag:640,440,820,560;22:drag:700,500,700,400;30:key:Ctrl+Shift+P;33:text:stamp;41:key:Enter;48:key:Enter;54:key:Ctrl+C;57:key:Ctrl+V;60:click:950,330;66:key:Escape;70:click:@Object;76:click:@Stamp 1;82:click:560,600;90:click:@Undo' },
    # The object browser (EDIT-1, ADR-253): with the Object tool in use, the Mines heading is opened and the gold mine chosen, placed
    # with a click and made a favourite with a right click; then a monster is chosen from its faction and placed; Undo takes it back.
    @{ Name = 'editor-browser-place'; App = 'EternalWars.MapEditor'; Frames = 70
       Input = '3:click:@Object;7:click:@Mines and providers;12:click:@Gold mine;18:click:700,500;24:rclick:@Gold mine;30:click:@Mines and providers;34:click:@Monsters;40:click:@Dwarf;46:click:900,500;54:click:@Undo;62:click:@Redo' },
    # The inspector's sections and jump links (EDIT-1, ADR-254) on a blank map: "portal" is typed into the browser's search, two
    # Portals are placed and linked with the LinkGates tool; the Select tool takes the first and the Inspector shows it with its
    # Identity and Links sections; the jump link selects the partner and shows it; the Links section is closed.
    @{ Name = 'editor-inspector-sections'; App = 'EternalWars.MapEditor'; Frames = 84
       Input = '3:click:@Object;6:click:160,292;8:text:portal;18:click:@Portal;22:click:600,400;26:click:900,520;30:click:@LinkGates;33:click:600,400;36:click:900,520;40:click:@Select;43:click:600,400;56:click:@Leads to Portal #2 at (20,21,L0);66:click:@Links' },
    # Multi-edit and the picture pickers (EDIT-1, ADR-255) on a blank map: two Dwarves are placed from the browser; the Select tool
    # takes the first and the inspector makes it Friendly; a rectangle takes both, the inspector shows "2 × Monster" with "—" for the
    # differing disposition, and Hostile is set on both; the creature picker opens its grid, "timber" narrows it and the Timber Wolf is picked
    # for both; Undo takes the wolves back.
    @{ Name = 'editor-multi-edit'; App = 'EternalWars.MapEditor'; Frames = 110
       Input = '3:click:@Object;6:click:160,292;8:text:dwarf;18:click:@Dwarf;22:click:600,400;26:click:800,400;30:click:@Select;33:click:600,400;42:click:@Aggressive;46:choose:Friendly;52:drag:540,340,880,470;58:click:@—;62:choose:Hostile;70:click:@Dwarf (T1);76:text:timber;84:click:@Timber Wolf (T1);94:click:@Undo' },
    # Scatter and scenery rotation (EDIT-6, ADR-252) on a blank map: the Scatter tool with only walls ticked and a brush of 5 tiles is
    # dragged over the grass (pieces of wall, ways kept open); the Select tool's rectangle takes them; Ctrl+R turns them a quarter
    # turn, Ctrl+M mirrors them; Undo takes the mirroring back.
    @{ Name = 'editor-scatter-rotate'; App = 'EternalWars.MapEditor'; Frames = 70
       Input = '3:click:@Scatter;7:click:@None;11:click:@Wall;14:click:164,308;17:click:164,308;20:click:164,308;23:click:164,308;27:drag:620,470,900,470;36:key:V;40:drag:560,360,960,620;48:key:Ctrl+R;56:key:Ctrl+M;64:click:@Undo' },
    # The editor's camera (ADR-260) on Ember March: Home frames the whole map; Ctrl+1 stores the view; the wheel zooms in and the middle
    # button drags the map; the Select tool's rectangle and F frame the selection; 1 returns to the stored view; the palette's Top-down view
    # looks straight down; Home again.
    @{ Name = 'editor-camera'; App = 'EternalWars.MapEditor'; Frames = 70; Map = $emberMap
       Input = '3:key:Home;8:key:Ctrl+Key1;11:wheel:up,6;15:mdrag:640,400,840,500;20:key:V;23:drag:700,240,950,350;28:key:F;34:key:Key1;40:key:Ctrl+Shift+P;44:text:Top-down;54:key:Enter;62:key:Home' },
    # The Measure tool (U, ADR-283) on Ember March: a click where a hero starts, a click where it goes: the route drawn by day, its steps
    # and days in the status bar.
    @{ Name = 'editor-measure'; App = 'EternalWars.MapEditor'; Frames = 30; Map = $emberMap
       Input = '3:key:U;6:click:540,545;12:click:420,270' },
    # The gates' links (ADR-290): the Link tool (K) draws a cyan arc with a head at each end between two linked two-way monoliths, an orange
    # one from a one-way entrance to its exit, and a post pointing up on the gate whose partner is underground; Escape with nothing
    # selected drops the tool back to Select, and the arrows go.
    @{ Name = 'editor-link-arrows'; App = 'EternalWars.MapEditor'; Frames = 24; Map = $gatesMap
       Input = '3:key:Home;6:key:K;16:key:Escape' },
    # The context menus and the Object tool's options (ADR-286) on Ember March: a right click on the map offers Select all, then (things
    # selected) Deselect; one on a browser entry (found by the search) offers Favourite, then the Object tool; one on a side bar button offers
    # the region's other panels. Then the Object tool's "Snap entrance to a road" and a click that places the chest by the nearest road.
    @{ Name = 'editor-context-menus'; App = 'EternalWars.MapEditor'; Frames = 84; Map = $emberMap
       Input = '3:rclick:540,545;8:choose:Select all;14:rclick:540,545;19:choose:Deselect;24:click:180,491;27:text:chest;36:rclick:@Treasure chest;41:choose:Favourite;46:rclick:@Treasure chest;51:choose:Object;56:rclick:@History;61:choose:Output;66:click:@Snap entrance to a road;72:click:540,545' },
    # The minimap and Go to (ADR-261) on Ember March: the minimap under the right region's tabs outlines the view; a click on it, then a drag
    # over it, move the view; Ctrl+G with "Castle" goes to the town and selects it; Ctrl+G with "20 10" goes to that tile. Then a new map of
    # two levels: the underground with the surface ghosted on the minimap and in the map view (ADR-287).
    @{ Name = 'editor-minimap'; App = 'EternalWars.MapEditor'; Frames = 96; Map = $emberMap
       Input = '3:key:Home;6:click:1260,720;12:drag:1250,800,1390,700;18:key:Ctrl+G;22:text:Castle;32:key:Enter;40:key:Ctrl+G;44:text:20 10;52:key:Enter;60:key:Ctrl+N;64:click:@The surface and the underground;67:click:@Create map;80:key:L;84:click:@Ghost the other level' }
    # The layers (ADR-262) on Ember March: the Layers tab; monsters locked, then Select all takes every thing but them; scenery and the terrain
    # hidden; then all shown and unlocked, and Select all takes the monsters too.
    @{ Name = 'editor-layers'; App = 'EternalWars.MapEditor'; Frames = 60; Map = $emberMap
       Input = '3:key:Home;6:click:@Layers;10:click:@Locked#5;14:key:Ctrl+A;24:click:@Scenery;30:click:@Terrain#2;40:click:@Show and unlock all;50:key:Ctrl+A' }
    # The overlays (ADR-264) on Ember March, one after another with their legend over the map: passability, owners, guard strength, reach
    # from the first start, height contours, the start zones (not a generated map). Then a map generated in the Generate tab, and its zones
    # laid out again from its description.
    @{ Name = 'editor-overlays'; App = 'EternalWars.MapEditor'; Frames = 100; Map = $emberMap
       Input = '3:key:Home;6:click:@Overlays;10:click:@Passability;18:click:@Owners;26:click:@Guard strength;34:click:@Reach;42:click:@Height contours;50:click:@Zones;58:click:@Generate;62:click:@Generate;80:click:@Overlays;84:click:@Zones' }
    # The Fairness tab (EDIT-9, ADR-266) on Ember March: each start's numbers and the warnings. File > Export report writes the Markdown
    # beside the map (Enter in the file dialog), File > Export map picture the level shown top-down as a PNG.
    @{ Name = 'editor-fairness'; App = 'EternalWars.MapEditor'; Frames = 80; Map = $emberMap; Clean = @('ember report.md', 'ember.png')
       Input = '3:key:Home;6:click:@Fairness;16:click:24,24;20:choose:Export report (Markdown)…;30:key:Enter;44:click:24,24;48:choose:Export map picture (PNG)…;58:key:Enter' }
    # Test (EDIT-5, ADR-267) on Ember March: F5 opens the Test window; player 2, the others off, the map revealed and the cheats change the
    # command it shows, seen in the frames (PlayTestTests checks the arguments themselves). Cancel closes it: nothing is started.
    @{ Name = 'editor-playtest-args'; App = 'EternalWars.MapEditor'; Frames = 40; Map = $emberMap
       Input = '3:key:F5;8:click:@2 Red;12:click:@The other players do nothing (off);16:click:@Reveal the map;20:click:@Cheat panel (resources, levels, creatures);32:click:@Cancel' }
    # A play-test as the editor's Test starts it (ADR-267): Ember March at once as player 1 on day 8, the others off, the map revealed, the
    # cheat panel; Resources gives the resources.
    @{ Name = 'game-playtest'; App = 'EternalWars.Game'; Frames = 40; Map = $emberMap; Args = @('--player=1', '--others=off', '--reveal', '--day=8', '--cheats')
       Input = '20:click:@Resources' }
)

# Every scenario has a seed of its own, so its games and battles are the same on every run (ADR-239): the entry's Seed, else one made from
# its name (32-bit FNV-1a); -Seed overrides both. Scenarios without a new game (the main menu, a replay, the editor) pass it unused.
foreach ($scenario in $scenarios) {
    if ($null -ne $Seed) { $scenario.Seed = $Seed }
    elseif (-not $scenario.ContainsKey('Seed')) {
        [uint32]$hash = 2166136261
        foreach ($byte in [Text.Encoding]::UTF8.GetBytes($scenario.Name)) { $hash = [uint32](([uint64]($hash -bxor $byte) * 16777619) % 4294967296) }
        $scenario.Seed = $hash
    }
}
$selected = @($scenarios | Where-Object { $_.Name -like $Only } | Sort-Object { -$_.Frames }) # the longest first, so they do not end the run alone
$watch = [Diagnostics.Stopwatch]::StartNew()
$results = $selected | ForEach-Object -ThrottleLimit ([Math]::Max(1, $Jobs)) -Parallel {
    $scenario = $_
    $started = [Diagnostics.Stopwatch]::StartNew()
    $dir = Join-Path $using:Out $scenario.Name
    New-Item -ItemType Directory -Force $dir | Out-Null
    # No frames of an earlier, longer run stay behind.
    Remove-Item (Join-Path $dir 'frame*') -ErrorAction SilentlyContinue
    # Every game scenario starts from the default settings (ADR-090), whatever this computer's settings file says, and keeps its
    # autosaves to itself: scenarios run side by side, and this computer's saves stay as they are.
    $settings = Join-Path $dir 'settings.json'
    Remove-Item $settings -ErrorAction SilentlyContinue
    # The editor keeps its workspace layout beside the settings file: every scenario starts from the default layout.
    Remove-Item (Join-Path $dir 'editor-settings.cfg') -ErrorAction SilentlyContinue
    # ... unless the scenario starts with preferences of its own (the UI's scale, the contrast: ADR-277).
    if ($scenario.Preferences) { Set-Content (Join-Path $dir 'editor-settings.cfg') "[preferences]`n$($scenario.Preferences)" }
    # ... and its maps folder and its autosaves with the session marker, which a run ended by --quit-after leaves behind.
    Remove-Item (Join-Path $dir 'maps'), (Join-Path $dir 'autosave'), (Join-Path $dir 'stamps'), (Join-Path $dir 'cache'), (Join-Path $dir 'templates'), (Join-Path $dir 'template-autosave') -Recurse -ErrorAction SilentlyContinue
    # ... and the files it writes beside its map (exports), so no save dialog asks to overwrite them.
    foreach ($file in @($scenario.Clean)) { if ($file) { Remove-Item (Join-Path $using:Out $file) -ErrorAction SilentlyContinue } }
    if ($scenario.Recovery) {
        $autosave = Join-Path $dir 'autosave'
        New-Item -ItemType Directory -Force $autosave | Out-Null
        Set-Content (Join-Path $autosave 'session.marker') 'planted'
        Copy-Item $scenario.Recovery (Join-Path $autosave 'autosave-20261120-101500.ewmap.json')
    }
    $saves = Join-Path $dir 'saves'
    Remove-Item $saves -Recurse -ErrorAction SilentlyContinue
    $userArgs = @("--autoinput=$($scenario.Input)", "--settings=$settings", "--saves=$saves")
    if ($scenario.Map) { $userArgs += "--map=$($scenario.Map)" }
    $userArgs += "--seed=$($scenario.Seed)"
    if ($scenario.Replay) { $userArgs += "--replay=$($scenario.Replay)" }
    if ($scenario.Args) { $userArgs += $scenario.Args } # further launch arguments (a play-test's, ADR-267)
    # The game opens on the main menu; the scenarios start on the new-game screen unless they test the menu.
    if ($scenario.App -eq 'EternalWars.Game' -and -not $scenario.MainMenu -and -not $scenario.Scene) { $userArgs += '--new-game' }
    $fps = if ($scenario.Fps) { $scenario.Fps } else { 10 }

    $movie = Join-Path $dir ($using:Png ? 'frame.png' : 'frame.avi')
    # With many Godot processes starting at once, one now and then dies at start without a word (seen about once in 400 starts): that
    # one is started again, once, and its line says so. A run that reports an error is never repeated.
    $tries = 0
    do {
        $tries++
        # A scenario with a Scene runs that scene instead of the app's main one (a style frame, ADR-294).
        $scene = if ($scenario.Scene) { @("--scene", $scenario.Scene) } else { @() }
        $log = & $using:Godot --path (Join-Path $using:root "src/Apps/$($scenario.App)") --write-movie $movie `
            --fixed-fps $fps --quit-after $scenario.Frames @scene -- @userArgs 2>&1 | Out-String
        $exit = $LASTEXITCODE
    } while ($tries -lt 2 -and $exit -ne 0 -and -not $log.Trim())
    Set-Content (Join-Path $dir 'godot.log') $log
    if (-not $using:Png -and (Test-Path $movie)) {
        # The film is Motion JPEG: every video chunk of its "movi" list is one frame's JPEG file, written out as it is.
        $bytes = [IO.File]::ReadAllBytes($movie)
        $at = 12
        $frame = 0
        while ($at + 8 -le $bytes.Length) {
            $id = [Text.Encoding]::ASCII.GetString($bytes, $at, 4)
            $size = [BitConverter]::ToUInt32($bytes, $at + 4)
            if ($id -eq 'LIST' -and [Text.Encoding]::ASCII.GetString($bytes, $at + 8, 4) -eq 'movi') {
                $at += 12 # into the list: its chunks follow
                continue
            }

            if ($id -in '00db', '00dc') {
                $file = [IO.File]::Create((Join-Path $dir ('frame{0:D8}.jpg' -f $frame++)))
                $file.Write($bytes, $at + 8, $size)
                $file.Dispose()
            }

            $at += 8 + $size + $size % 2
        }
    }

    $errors = @($log -split "`n" | Where-Object { $_ -match 'ERROR|SCRIPT ERROR|Unhandled exception|Exception:' })
    $ok = $exit -eq 0 -and -not $errors
    $seconds = '{0,5:N1}s' -f $started.Elapsed.TotalSeconds
    if ($tries -gt 1) { $seconds += ' (second start)' }
    if ($ok) {
        Write-Host "ok    $seconds  $($scenario.Name)  ($dir)"
    }
    else {
        Write-Host "FAIL  $seconds  $($scenario.Name)  (seed $($scenario.Seed), exit code $exit, $(Join-Path $dir 'godot.log'))"
        $errors | ForEach-Object { Write-Host "      $_" }
    }

    $ok
}

$failed = @($results | Where-Object { -not $_ }).Count
Write-Host ('{0} scenario(s), {1} failed, {2:N0}s' -f $selected.Count, $failed, $watch.Elapsed.TotalSeconds)
if ($failed -gt 0) { throw "$failed smoke scenario(s) failed." }