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
$siegeMap = Join-Path $PSScriptRoot 'smoke/siege.ewmap.json'
$caravanMap = Join-Path $PSScriptRoot 'smoke/caravan.ewmap.json'
$questsMap = Join-Path $PSScriptRoot 'smoke/quests.ewmap.json'
$turnStartMap = Join-Path $PSScriptRoot 'smoke/turn-start.ewmap.json'
$placeholdersMap = Join-Path $PSScriptRoot 'smoke/placeholders.ewmap.json'
$uniqueMap = Join-Path $PSScriptRoot 'smoke/unique-buildings.ewmap.json'
$sitesMap = Join-Path $PSScriptRoot 'smoke/sites.ewmap.json'
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
       Input = (@('3:key:Enter', '10:click:960,520', '25:click:960,520') + (0..13 | ForEach-Object { "$(60 + 6 * $_):press:End turn" })) -join ';' },
    # Two own heroes meet (ADR-153): Orrin's blade onto Valeska's doll, his ring into her backpack, her griffins to him; Close, then the gate.
    @{ Name = 'game-exchange-and-gate'; App = 'EternalWars.Game'; Frames = 72; Map = $exchangeMap
       Input = '3:key:Enter;10:key:Right;16:click:@Weapon: Rusted Blade;19:click:@Weapon: Centaur''s Axe;22:click:@Iron Band (Ring);25:click:@Into the backpack#2;28:click:@2 | Griffin;31:click:@—;36:click:@Close;42:click:960,557;48:click:960,557' },
    # Artifact drag and drop (ADR-162): in the exchange Orrin's blade onto an empty slot of Valeska's doll (her axe goes to her backpack), his
    # Iron Band from his backpack onto his doll, her axe onto empty ground of his backpack; on his hero screen the axe onto the doll, the band
    # into the backpack.
    @{ Name = 'game-artifact-drag'; App = 'EternalWars.Game'; Frames = 62; Map = $exchangeMap
       Input = '3:key:Enter;10:key:Right;16:drag:@Weapon: Rusted Blade>@Head: —#2;24:drag:@Iron Band (Ring)>@Ring: —;30:drag:@Centaur''s Axe (Weapon)>420,536;36:click:@Close;40:key:H;46:drag:@Centaur''s Axe (Weapon)>@Shield: —;52:drag:@Ring: Iron Band>300,725' },
    # Information windows (ADR-154): right-click Orrin on the map, a click closes it; on the hero screen right-click his footmen, then his blade
    # (the second right click opens the next window), Escape closes it, then the Iron Band in his backpack; Escape closes it and the screen.
    # Touch (ADR-190): a finger held still on the hero opens its information window, which stays open when lifted; a tap closes it.
    @{ Name = 'game-info-windows'; App = 'EternalWars.Game'; Frames = 112; Map = $exchangeMap
       Input = '3:key:Enter;10:rclick:800,445;16:click:1500,850;20:key:H;24:rclick:@10 | Footman;28:rclick:@Weapon: Rusted Blade;32:key:Escape;36:rclick:@Iron Band (Ring);42:key:Escape;46:key:Escape;52:touch:800,445,45;106:touch:400,300' },
    # An arena map against the computer (ADR-097, ADR-159): Blue ends day 1, Red prepares, day 2's battle opens by itself at the combat place;
    # quick combat, the result screen (not ranked), then "Play again" starts the map anew.
    @{ Name = 'game-arena'; App = 'EternalWars.Game'; Frames = 110; Map = $arenaMap
       Input = '3:key:Enter;8:press:End turn;50:key:Q;75:key:Enter;85:press:Play again' },
    # A shipped arena map from the new-game screen's Arenas tab (ADR-180, ADR-201): Four Banners, two against two with random towns; start,
    # then end day 1 and the computer players prepare (their battles show; day 2 and the result are the game-arena scenario's).
    @{ Name = 'game-arena-shipped'; App = 'EternalWars.Game'; Frames = 60
       Input = '4:press:Arenas;8:click:@Four Banners;12:click:@Start;30:press:End turn' },
    # Step next to a savage monster: the 3D battle scene opens; wait, defend, a click on the field, quick combat, leave.
    @{ Name = 'game-battle-3d'; App = 'EternalWars.Game'; Frames = 80; Map = $battleMap
       Input = '3:key:Enter;10:key:Right;30:key:W;40:key:D;48:click:960,600;58:key:Q;68:key:Enter' },
    # Auto combat (ADR-090): the menu sets "Ask before each battle", the step into the monster asks, Auto combat shows the outcome, Accept
    # returns to the map without a second result screen. The question waits until the imps' first moves have played.
    @{ Name = 'game-auto-combat'; App = 'EternalWars.Game'; Frames = 85; Map = $battleMap
       Input = '3:key:Enter;8:key:F10;10:click:@Settings;13:click:@Off;16:choose:Ask before each battle;19:key:Escape;24:key:Right;60:click:@Auto combat;68:click:@Accept' },
    # Speed settings (ADR-090): the menu sets "Your heroes" to Slow, then the hero walks a route (its steps take twice as long).
    # The side panel (ADR-191): the framed column on the right by default, moved to the left in the "Layout" tab; the turn block stands beside it.
    @{ Name = 'game-speed-settings'; App = 'EternalWars.Game'; Frames = 100
       Input = '3:key:Enter;8:key:F10;10:click:@Settings;13:click:@Normal;16:choose:Slow;19:key:Escape;24:click:960,520;30:click:960,520;70:key:F10;72:click:@Layout;75:click:@Right;78:choose:Left;82:key:Escape' },
    # The "Layout" tab (ADR-156): the side panel off (ADR-191), then minimap bottom left, the lists along the bottom, the action buttons
    # bottom right (ADR-210), the initiative bar at the bottom; the map's HUD moves at once, then the step into the monster opens the battle with
    # its initiative bar at the bottom.
    @{ Name = 'game-layout'; App = 'EternalWars.Game'; Frames = 96; Map = $battleMap
       Input = '3:key:Enter;8:key:F10;10:click:@Layout;13:click:@Right;16:choose:Off (the parts stand free);20:click:@Top right;24:choose:Bottom left;28:click:@Beside the minimap;32:choose:Along the bottom;36:click:@Top right;40:choose:Bottom right;44:click:@Top;48:choose:Bottom;52:key:Escape;60:key:Right' },
    # The action buttons (ADR-210), 3×3 in the side panel: Magus' spell book from its button (Escape closes it), then with C (C closes it);
    # the hero screen from its button; the game menu from its button. Bookless selected, the spell book button is disabled.
    @{ Name = 'game-action-buttons'; App = 'EternalWars.Game'; Frames = 62; Map = $magicMap
       Input = '3:key:Enter;8:press:Magus;12:press:Spell book (C);20:key:Escape;24:key:C;31:key:C;36:press:Hero screen (H);42:key:Escape;46:press:Menu (F10);52:key:Escape;56:press:Bookless' },
    # The turn timer (ADR-091, ADR-225): tick it on the new-game screen and type 1 minute, start, open the hero screen and wait. The warnings come,
    # the turn passes to Red with the full time and the hero screen closes. At 2 frames a second, so the minute takes 120 frames instead of 600.
    @{ Name = 'game-turn-timer'; App = 'EternalWars.Game'; Frames = 140; Fps = 2
       Input = '2:press:Turn timer;6:text:1;8:key:Enter;10:click:@Start;14:key:H' },
    # The screens over the map on the towns map, each opened, closed with Escape, opened again and closed: the town screen (ADR-050) with T,
    # the kingdom overview (ADR-084) with K, the hero screen (ADR-050) with H. Then a garrison hero (ADR-112): step into the own town,
    # "Orrin into the garrison", "Lead Orrin out", close the town screen.
    @{ Name = 'game-town-screen'; App = 'EternalWars.Game'; Frames = 170; Map = $townsMap
       Input = '3:key:Enter;12:key:T;24:key:Escape;30:key:T;40:key:Escape;48:key:K;60:key:Escape;66:key:K;76:key:Escape;84:key:H;96:key:Escape;102:key:H;112:key:Escape;120:key:Up;130:press:Orrin into the garrison;145:press:Lead Orrin out;158:key:Escape' },
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
       Input = '3:press:Campaign;10:press:Start;17:press:Start the map;40:press:Next;47:press:OK;55:press:End turn' },
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
       Input = '3:key:Space;6:press:Next day;12:press:Play from here;24:press:End turn' },
    # A siege (ADR-106, ADR-109): step into the neutral Castle-level town; walls, gate, moat, towers and keep. The footman defends, the
    # ballista (Basic Ballistics) shoots the upper wall, two more defend, quick combat mid-animation (ADR-112 check), Enter on the result.
    @{ Name = 'game-siege'; App = 'EternalWars.Game'; Frames = 150; Map = $siegeMap
       Input = '3:key:Enter;10:key:Up;62:key:D;72:click:955,280;90:key:D;100:key:D;125:key:Q;140:key:Enter' },
    # Dwellings and caravans (ADR-054, ADR-073): visit a map dwelling and recruit the week, close; T, scroll to the caravan section, hire
    # from the owned dwelling, close, end two turns (hot-seat) and look again: the footmen are in the garrison.
    @{ Name = 'game-dwelling-caravan'; App = 'EternalWars.Game'; Frames = 100; Map = $caravanMap
       Input = '3:key:Enter;10:key:Up;22:click:@Recruit week (16) | 960 gold;28:click:@Close;34:key:T;42:wheel:down,15;50:click:@Hire 9 | 55 gold each;58:key:Escape;64:click:@End turn;72:click:@End turn;82:key:T;90:wheel:down,15' },
    # The generated connections map in the editor (no fog): pan to the portal, the blue border guard, the blue keymaster, the garrison,
    # the red border gate and the red keymaster (frames 16, 49, 86, 109, 153, 172).
    @{ Name = 'editor-generated-connections'; App = 'EternalWars.MapEditor'; Frames = 175; Map = $connectionsMap
       Input = '5:hold:S,8;18:hold:A,19;38:hold:W,8;51:hold:S,18;70:hold:D,13;88:hold:D,10;99:hold:S,7;111:hold:D,8;120:hold:W,30;155:hold:W,10;166:hold:D,3' },
    # Information windows on the screens (ADR-154, ADR-178): the kingdom overview's town row, then its total, each closed with Escape before
    # the screen is. Then the town screen: the Thieves' Guild (ADR-079) from its button, closed with Escape; scroll to the tavern row,
    # "Visit the tavern…" (ADR-114), the tavern's first hero (on its card), then the second (on its Hire button), each window closed with
    # Escape, then the tavern and the town screen.
    @{ Name = 'game-info-screens'; App = 'EternalWars.Game'; Frames = 88; Map = $townsMap
       Input = '3:key:Enter;10:key:K;16:rclick:468,299;20:key:Escape;22:rclick:457,353;26:key:Escape;28:key:Escape;32:key:T;38:click:@Thieves'' guild;48:key:Escape;54:wheel:down,25;60:click:@Visit the tavern…;68:rclick:590,450;72:key:Escape;74:rclick:862,607;78:key:Escape;80:key:Escape;82:key:Escape' },
    # Ember March (ADR-114): start, end the turn (the computer plays Red), open the town screen with its building tree, close it.
    @{ Name = 'game-ember-march'; App = 'EternalWars.Game'; Frames = 60; Map = $emberMap
       Input = '3:key:Enter;12:click:@End turn;40:key:T;52:key:Escape' },
    # Inspector rows (ADR-114) on Ember March: Select, the Inspector tab, a Minor artifact pickup, the witch hut, the sphinx, the hero (artifacts).
    @{ Name = 'editor-inspector-objects'; App = 'EternalWars.MapEditor'; Frames = 38; Map = $emberMap
       Input = '3:click:@Select;6:click:@Inspector;9:click:616,230;16:click:645,382;23:click:965,552;30:click:352,244' },
    # The tools on a blank map: roads, a river, an object, the gate link tool. Then the week event settings on the conditions tab
    # (ADR-084): tick Conflux for the weekly events, one more skipped week, undo both.
    @{ Name = 'editor-tools'; App = 'EternalWars.MapEditor'; Frames = 76
       Input = '3:click:@Road;6:click:700,600;9:click:740,600;12:click:@River;15:click:800,500;18:click:@Object;21:click:900,450;24:click:@LinkGates;27:click:900,450;30:click:1500,67;39:click:@Conditions;44:click:@conflux;50:click:1576,387;56:click:@Undo;62:click:@Undo' },
    # The height tools (ADR-182 g) on a blank map: radius 2, strength 8 (the spin boxes' up arrows), raise two strokes, lower one and undo
    # it; then highlands painted with Auto height on.
    # Then generate from a template (ADR-087): "From template", Sylvan for Blue, Generate (Two against two at 128), then the tab again: the settings stay.
    @{ Name = 'editor-heights'; App = 'EternalWars.MapEditor'; Frames = 106
       Input = '3:click:@Height;5:click:304,243;7:click:304,243;9:click:144,274;10:click:144,274;11:click:144,274;12:click:144,274;13:click:144,274;14:click:144,274;17:drag:460,600,900,600;21:drag:460,620,900,620;26:click:@Raise;29:choose:Lower;33:drag:460,560,900,560;38:click:@Undo;42:click:@Terrain;45:click:@Highlands;48:click:400,400;59:click:@From template;64:click:@Random;68:choose:sylvan;72:click:@Generate;86:click:@From template' },
    # The template editor (ADR-117) in the workspace's Template tab (EDIT-3): the tab, select the middle zone (its obelisk count, ADR-140), add a treasure zone, drag it,
    # right-drag it onto start-1 to connect, Generate (the map opens in the Map tab).
    @{ Name = 'editor-template-editor'; App = 'EternalWars.MapEditor'; Frames = 70
       Input = '3:click:@Template;10:click:710,405;18:click:@+ Treasure;24:drag:626,440,710,590;30:rdrag:710,590,570,405;44:click:@Generate' },
    # The Events tab (EVT-6) on the quests map: open the tab, pick the first event (its tree), then its first action (the node text).
    @{ Name = 'editor-events'; App = 'EternalWars.MapEditor'; Frames = 30; Map = $questsMap
       Input = '3:click:@Events;8:click:1096,170;14:click:1145,436' },
    # Bans and placeholders (ADR-126) on the placeholders map: the Bans tab, ban the first hero, undo it; Select, the Inspector tab, the random town.
    @{ Name = 'editor-bans'; App = 'EternalWars.MapEditor'; Frames = 30; Map = $placeholdersMap
       Input = '3:click:@Bans;7:click:@Agathe Sorrel (haven, Mage);11:click:@Undo;15:click:@Select;18:click:@Inspector;21:click:420,300' },
    # The arena switch (ARN-2) on the arena map: the Conditions tab (the switch on, the other victory rows off), untick it: Problems, in the
    # bottom region, says the combat place now does nothing.
    @{ Name = 'editor-arena'; App = 'EternalWars.MapEditor'; Frames = 20; Map = $arenaMap
       Input = '3:click:@Conditions;6:click:@Arena: a day to prepare, then one battle decides the game' }
    # The scenarios below find buttons by their text (press:) rather than by pixel.
    # Unique buildings (ADR-138, ADR-143): Orrin enters the Necropolis town, ticks a garrison and a hero stack for the Undead Transformer and
    # transforms them; Ilya enters the Sylvan town (Elder Grove: a notice and a level-up). Stables and the Marketplace screen (ADR-067):
    # Gareth enters the Haven town (Stables: a notice), then the HUD's Marketplace button.
    @{ Name = 'game-unique-buildings'; App = 'EternalWars.Game'; Frames = 100; Map = $uniqueMap
       Input = '3:key:Enter;10:key:Up;20:press:Garrison: 12 Pikeman;24:press:Hero: 20 Footman;30:press:Transform;36:key:Escape;40:press:Ilya;44:key:Up;56:press:→ Basic;60:key:Escape;64:press:OK;68:key:Escape;74:press:Gareth;78:key:Up;90:key:Escape;92:key:Escape;94:press:Marketplace' },
    # Map sites (ADR-079, ADR-080, ADR-082, ADR-105, ADR-139): each hero steps north onto its site: the University's lessons, the War
    # Academy's, the Arena's choice, Pandora's Box, the Witch Hut, the Sphinx (the map's riddle, answered wrong: a right answer's 5000 experience would bring level-ups), the Sacrificial Altar, the Artifact Merchant.
    # At the Witch Hut, information windows on a decision prompt (ADR-178), in the middle of the screen (ADR-186): right-click the prompt
    # (the hut), then the skill offered. After Blue's turn Red's map sites: the Refugee Camp and the Cartographer (Buy reveals the surface).
    @{ Name = 'game-map-sites'; App = 'EternalWars.Game'; Frames = 244; Map = $sitesMap
       Input = '3:key:Enter;8:press:Ulric;12:key:Up;24:key:Escape;28:press:Wenda;32:key:Up;44:key:Escape;48:press:Lior;52:key:Up;62:press:+2 Attack;68:press:Pella;72:key:Up;82:press:Open;90:key:Escape;94:press:Hagan;98:key:Up;108:rclick:800,430;112:key:Escape;114:rclick:640,477;118:key:Escape;120:press:Leave;124:press:Sefra;128:key:Up;138:press:A cave;144:press:Mortis;148:key:Up;158:key:Escape;160:press:Bram;164:key:Up;176:key:Escape;180:press:End turn;188:press:Corin;192:key:Up;204:key:Escape;208:press:Tamsin;212:key:Up;224:press:Buy' },
    # The Grail (ADR-140): Obelisa visits the obelisk (notice), P opens the puzzle map and closes it; Spade digs (an empty hole), Digby digs up the
    # Grail; two turns later Digby carries it into Castlebridge (the Grail building's notice).
    @{ Name = 'game-grail'; App = 'EternalWars.Game'; Frames = 100; Map = $grailMap
       Input = '3:key:Enter;8:press:Obelisa;12:key:Up;24:key:P;34:key:P;38:press:Spade;42:key:G;50:press:Digby;54:key:G;62:press:End turn;68:press:End turn;76:press:Digby;80:key:Up' },
    # Magic (ADR-110, ADR-121, ADR-122, ADR-123): Magus enters the guild town (fixed guild spells), the hero screen with ranks and the Assemble
    # button, Detect Magic's window; then Bookless buys a spell book in the second town.
    @{ Name = 'game-magic'; App = 'EternalWars.Game'; Frames = 84; Map = $magicMap
       Input = '3:key:Enter;8:press:Magus;12:key:Up;24:key:Escape;28:key:H;34:press:Detect Magic (4 mana);44:press:Close;50:press:Close;56:press:Bookless;60:key:Up;72:press:spell book' },
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
       Input = '3:key:Enter;8:press:Digger;12:key:G;20:press:End turn;44:press:Digger;50:key:Left' },
    # A guild spell set by hand in the editor: select Emberkeep, open the inspector, scroll to the guild spells (a press on a
    # slot further down, closed again), pick Brand for the first level-1 slot.
    @{ Name = 'editor-guild-spell'; App = 'EternalWars.MapEditor'; Frames = 40; Map = $spellsMap
       Input = '5:press:Select;10:click:400,300;16:click:@Inspector;20:press:drawn;23:key:Escape;26:click:1143,748;30:choose:Brand' },
    # The siege's gate, keep and towers and Earthquake (ADR-120): the ballista breaks the gate; a second run casts Earthquake.
    @{ Name = 'game-siege-gate'; App = 'EternalWars.Game'; Frames = 104; Map = $siegeMagicMap
       Input = '3:key:Enter;10:key:Up;62:key:D;72:click:958,432' },
    @{ Name = 'game-siege-earthquake'; App = 'EternalWars.Game'; Frames = 150; Map = $siegeMagicMap
       Input = '3:key:Enter;10:key:Up;62:press:Earthquake;100:key:D;110:key:D;120:key:D;135:key:D' },
    # The editor's Grail tool, the conditions panel's difficulty and hero limit rows (ADR-083, ADR-103) and the town inspector's faction and
    # guild spells (ADR-083, ADR-123).
    @{ Name = 'editor-grail-and-town'; App = 'EternalWars.MapEditor'; Frames = 38; Map = $grailMap
       Input = '5:click:@Conditions;10:press:Grail;14:click:400,450;20:press:Select;24:click:720,560;28:click:@Inspector' },
    # The workspace's regions (EDIT-1, ADR-250) on Ember March: the Monster tool's options wrap in the left region; the right region's
    # splitter dragged to the left; the left region collapsed; F11 hides the others and brings them back; the left region opens again;
    # "From template" brings the Generate tab to the front. Then the menus built from the command registry: Edit (Undo and Redo greyed:
    # nothing to undo), and Tools, where Hero is picked (the palette follows).
    @{ Name = 'editor-shell'; App = 'EternalWars.MapEditor'; Frames = 54; Map = $emberMap
       Input = '4:click:@Monster;8:drag:1036,400,936,400;14:click:@Left panel;19:key:F11;24:key:F11;29:click:@Left panel;33:click:@From template;37:click:60,22;42:key:Escape;45:click:190,22;48:choose:Hero' },
    # The bottom panels and the status bar (EDIT-1, ADR-250) on a blank map: a road, a river and an object are three steps; History lists
    # them and a click on the first jumps back to it (the river and the object are undone and dim); Output keeps the messages (the gate link tool, clicked on empty ground, says what it needs); the status
    # bar's problem counts open Problems, where the warnings are hidden and shown again and the kind Heroes is picked.
    @{ Name = 'editor-bottom-panels'; App = 'EternalWars.MapEditor'; Frames = 66
       Input = '3:click:@Road;6:click:700,600;9:click:@River;12:click:800,500;15:click:@Object;18:click:900,450;21:click:@LinkGates;24:click:1000,300;27:click:@History;31:click:500,736;36:click:@Output;43:click:1327,877;47:click:478,708;51:click:478,708;55:click:604,708;59:choose:Heroes (2)' },
    # The editor's files (EDIT-1, ADR-250) on a blank map: a road makes unsaved work; File > New asks over it and Cancel stays; New
    # again and "Don't save" opens the new map ("Not saved"); File > Save as shows the file dialog in the scenario's own maps folder, and Enter saves the map there ("Saved" with the time).
    @{ Name = 'editor-files'; App = 'EternalWars.MapEditor'; Frames = 60
       Input = '3:click:@Road;6:click:700,600;10:click:24,24;13:choose:New;20:click:@Cancel;24:click:24,24;27:choose:New;33:click:@Don''t save;40:click:24,24;43:choose:Save as;49:key:Enter' },
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
    # Portals are placed and linked with the LinkGates tool; the Select tool takes the first and the Inspector tab shows it with its
    # Identity and Links sections; the jump link selects the partner and shows it; the Links section is closed.
    @{ Name = 'editor-inspector-sections'; App = 'EternalWars.MapEditor'; Frames = 84
       Input = '3:click:@Object;6:click:160,354;8:text:portal;18:click:@Portal;22:click:600,400;26:click:900,520;30:click:@LinkGates;33:click:600,400;36:click:900,520;40:click:@Select;43:click:600,400;47:click:@Inspector;56:click:@Leads to Portal #2 at (20,21,L0);66:click:@Links' },
    # Scatter and scenery rotation (EDIT-6, ADR-252) on a blank map: the Scatter tool with only walls ticked and a brush of 5 tiles is
    # dragged over the grass (pieces of wall, ways kept open); the Select tool's rectangle takes them; Ctrl+R turns them a quarter
    # turn, Ctrl+M mirrors them; Undo takes the mirroring back.
    @{ Name = 'editor-scatter-rotate'; App = 'EternalWars.MapEditor'; Frames = 70
       Input = '3:click:@Scatter;7:click:@None;11:click:@Wall;14:click:112,302;17:click:112,302;20:click:112,302;23:click:112,302;27:drag:620,470,900,470;36:key:V;40:drag:560,360,960,620;48:key:Ctrl+R;56:key:Ctrl+M;64:click:@Undo' }
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
    # ... and its maps folder and its autosaves with the session marker, which a run ended by --quit-after leaves behind.
    Remove-Item (Join-Path $dir 'maps'), (Join-Path $dir 'autosave'), (Join-Path $dir 'stamps'), (Join-Path $dir 'cache') -Recurse -ErrorAction SilentlyContinue
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
    # The game opens on the main menu; the scenarios start on the new-game screen unless they test the menu.
    if ($scenario.App -eq 'EternalWars.Game' -and -not $scenario.MainMenu) { $userArgs += '--new-game' }
    $fps = if ($scenario.Fps) { $scenario.Fps } else { 10 }

    $movie = Join-Path $dir ($using:Png ? 'frame.png' : 'frame.avi')
    # With many Godot processes starting at once, one now and then dies at start without a word (seen about once in 400 starts): that
    # one is started again, once, and its line says so. A run that reports an error is never repeated.
    $tries = 0
    do {
        $tries++
        $log = & $using:Godot --path (Join-Path $using:root "src/Apps/$($scenario.App)") --write-movie $movie `
            --fixed-fps $fps --quit-after $scenario.Frames -- @userArgs 2>&1 | Out-String
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