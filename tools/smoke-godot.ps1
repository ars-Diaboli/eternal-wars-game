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
$questsMap = Join-Path $PSScriptRoot 'smoke/quests.ewmap.json'
$turnStartMap = Join-Path $PSScriptRoot 'smoke/turn-start.ewmap.json'
$placeholdersMap = Join-Path $PSScriptRoot 'smoke/placeholders.ewmap.json'
$uniqueMap = Join-Path $PSScriptRoot 'smoke/unique-buildings.ewmap.json'
$sitesMap = Join-Path $PSScriptRoot 'smoke/sites.ewmap.json'
$grailMap = Join-Path $PSScriptRoot 'smoke/grail.ewmap.json'
$magicMap = Join-Path $PSScriptRoot 'smoke/magic.ewmap.json'
$necromancyMap = Join-Path $PSScriptRoot 'smoke/necromancy.ewmap.json'
$siegeMagicMap = Join-Path $PSScriptRoot 'smoke/siege-magic.ewmap.json'
$arenaMap = Join-Path $PSScriptRoot 'smoke/arena.ewmap.json'
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
       Input = '5:click:133,263;12:click:133,263;20:click:@Fight;28:click:@Quick combat;35:key:Q' },
    @{ Name = 'game-route-and-move'; App = 'EternalWars.Game'; Frames = 60
       Input = '3:key:Enter;10:click:960,520;25:click:960,520' },
    # Two own heroes meet (ADR-153): Orrin's blade onto Valeska's doll, his ring into her backpack, her griffins to him; Close, then the gate.
    @{ Name = 'game-exchange-and-gate'; App = 'EternalWars.Game'; Frames = 72; Map = $exchangeMap
       Input = '3:key:Enter;10:key:Right;16:click:@Weapon: Rusted Blade;19:click:@Weapon: Centaur''s Axe;22:click:@Iron Band (Ring);25:click:@Into the backpack#2;28:click:@2 | Griffin;31:click:@—;36:click:@Close;42:click:960,557;48:click:960,557' },
    # Artifact drag and drop (ADR-162): in the exchange Orrin's blade onto an empty slot of Valeska's doll (her axe goes to her backpack), his
    # Iron Band from his backpack onto his doll, her axe onto empty ground of his backpack; on his hero screen the axe onto the doll, the band
    # into the backpack.
    @{ Name = 'game-artifact-drag'; App = 'EternalWars.Game'; Frames = 62; Map = $exchangeMap
       Input = '3:key:Enter;10:key:Right;16:drag:@Weapon: Rusted Blade>@Head: —#2;24:drag:@Iron Band (Ring)>@Ring: —;30:drag:@Centaur''s Axe (Weapon)>420,536;36:click:@Close;40:key:H;46:drag:@Centaur''s Axe (Weapon)>@Shield: —;52:drag:@Ring: Iron Band>300,725' },
    # Information windows (ADR-154): right-click Orrin on the map, a click closes it; on the hero screen right-click his footmen, then his blade
    # (the second right click opens the next window), Escape closes it, then the Iron Band in his backpack.
    @{ Name = 'game-info-windows'; App = 'EternalWars.Game'; Frames = 42; Map = $exchangeMap
       Input = '3:key:Enter;10:rclick:800,445;16:click:1500,850;20:key:H;24:rclick:@10 | Footman;28:rclick:@Weapon: Rusted Blade;32:key:Escape;36:rclick:@Iron Band (Ring)' },
    # An arena map against the computer (ADR-097, ADR-159): Blue ends day 1, Red prepares, day 2's battle opens by itself at the combat place;
    # quick combat, the result screen (not ranked), then "Play again" starts the map anew.
    @{ Name = 'game-arena'; App = 'EternalWars.Game'; Frames = 110; Map = $arenaMap
       Input = '3:key:Enter;8:press:End turn;50:key:Q;75:key:Enter;85:press:Play again' },
    # Step next to a savage monster: the 3D battle scene opens; wait, defend, a click on the field, quick combat, leave.
    @{ Name = 'game-battle-3d'; App = 'EternalWars.Game'; Frames = 80; Map = $battleMap
       Input = '3:key:Enter;10:key:Right;30:key:W;40:key:D;48:click:960,600;58:key:Q;68:key:Enter' },
    # Auto combat (ADR-090): the menu sets "Ask before each battle", the step into the monster asks, Auto combat shows the outcome, Accept
    # returns to the map without a second result screen. The question waits until the imps' first moves have played.
    @{ Name = 'game-auto-combat'; App = 'EternalWars.Game'; Frames = 85; Map = $battleMap
       Input = '3:key:Enter;8:key:F10;10:click:@Settings;13:click:@Off;16:choose:Ask before each battle;19:key:Escape;24:key:Right;60:click:@Auto combat;68:click:@Accept' },
    # Speed settings (ADR-090): the menu sets "Your heroes" to Slow, then the hero walks a route (its steps take twice as long).
    @{ Name = 'game-speed-settings'; App = 'EternalWars.Game'; Frames = 70
       Input = '3:key:Enter;8:key:F10;10:click:@Settings;13:click:@Normal;16:choose:Slow;19:key:Escape;24:click:960,520;30:click:960,520' },
    # The "Layout" tab (ADR-156): minimap bottom left, the lists along the bottom, the actions left, the initiative bar at the bottom; the map's
    # HUD moves at once, then the step into the monster opens the battle with its initiative bar at the bottom.
    @{ Name = 'game-layout'; App = 'EternalWars.Game'; Frames = 90; Map = $battleMap
       Input = '3:key:Enter;8:key:F10;10:click:@Layout;14:click:@Top right;18:choose:Bottom left;22:click:@Beside the minimap;26:choose:Along the bottom;30:click:@Right;34:choose:Left;38:click:@Top;42:choose:Bottom;46:key:Escape;54:key:Right' },
    # The turn timer (ADR-091): pick 1 minute on the new-game screen, start, open the hero screen and wait. The warnings come, the turn passes
    # to Red with the full time and the hero screen closes.
    @{ Name = 'game-turn-timer'; App = 'EternalWars.Game'; Frames = 640
       Input = '3:click:@Off;8:choose:1 min;12:click:@Start;30:key:H' },
    # The town screen (ADR-050): T opens the first own town over the map, Escape closes it, T again, Escape.
    @{ Name = 'game-town-screen'; App = 'EternalWars.Game'; Frames = 50; Map = $townsMap
       Input = '3:key:Enter;12:key:T;24:key:Escape;30:key:T;40:key:Escape' },
    # The hero screen (ADR-050): select a hero, H opens it, Escape closes it, then the same through the hero list's Info button.
    @{ Name = 'game-hero-screen'; App = 'EternalWars.Game'; Frames = 50
       Input = '3:key:Enter;12:key:H;24:key:Escape;30:key:H;40:key:Escape' },
    # Quests (ADR-127, ADR-128): the Seer's Hut, Quest Guard and Quest Gate shapes; the hero walks into the hut and completes its first stage, the next one shows; Q opens the
    # quest log, Q closes it.
    @{ Name = 'game-quests'; App = 'EternalWars.Game'; Frames = 64; Map = $questsMap
       Input = '3:key:Enter;12:key:Up;20:key:Up;26:click:@Complete;32:click:@OK;40:key:Q;52:key:Q' },
    # Turn-start messages (EVT-2 f): Blue's player event reveals Grimhollow and says so, the camera moves there; Enter closes it; the town
    # event's message for Castlebridge moves the camera to the town; OK closes each.
    @{ Name = 'game-turn-start-camera'; App = 'EternalWars.Game'; Frames = 40; Map = $turnStartMap
       Input = '3:key:Enter;16:click:@OK;30:click:@OK' },
    # Random placeholders and bans (ADR-126): the new-game screen offers Blue a faction for its random town (Dungeon is picked);
    # the game starts with Castlebridge a Dungeon town, Blue's hero a Dungeon hero and the random monsters drawn.
    @{ Name = 'game-placeholders'; App = 'EternalWars.Game'; Frames = 30; Map = $placeholdersMap
       Input = '5:click:@Random;9:choose:dungeon;13:click:@Start' },
    # The kingdom overview (ADR-084): K opens it on the towns map, Escape closes it, K again, Escape.
    @{ Name = 'game-kingdom'; App = 'EternalWars.Game'; Frames = 50; Map = $townsMap
       Input = '3:key:Enter;12:key:K;24:key:Escape;30:key:K;40:key:Escape' },
    # A week of turns through "End turn" (two human seats, 14 clicks): the new week's notice and its event next to the date (ADR-084).
    @{ Name = 'game-weeks'; App = 'EternalWars.Game'; Frames = 100
       Input = (@('3:key:Enter') + (0..13 | ForEach-Object { "$(10 + 6 * $_):click:342,24" })) -join ';' },
    # The Thieves' Guild (ADR-079) from the town screen's button, closed with Escape, then the town screen too.
    @{ Name = 'game-thieves-guild'; App = 'EternalWars.Game'; Frames = 40; Map = $townsMap
       Input = '3:key:Enter;10:key:T;16:click:@Thieves'' guild;26:key:Escape;32:key:Escape' },
    # A random map (ADR-086): pick "Random map: Duel", Necropolis, its first hero and the Skeleton tier-1 dwelling for Blue, hero limit 7,
    # Start (Red is a computer player), K, end the turn so the computer plays its turn.
    @{ Name = 'game-random-map'; App = 'EternalWars.Game'; Frames = 105
       Input = '4:click:@Demo Valley;8:choose:Random map: Duel;12:click:@Random;16:choose:necropolis;20:click:@Random;24:choose:Morthal (warrior);28:click:@Random;32:choose:Skeleton;36:click:618,400;40:click:@Start;58:key:K;68:key:Escape;74:click:@End turn' },
    # The main menu (ADR-101, ADR-163) over its picture: Leaderboard opens, Escape returns, New game, Enter on Start. Pressed by name: Continue has the
    # focus when this computer has a save, New game when not. The only game scenario without --new-game.
    @{ Name = 'game-main-menu'; App = 'EternalWars.Game'; Frames = 50; MainMenu = $true
       Input = '3:press:Leaderboard;12:key:Escape;18:press:New game;28:key:Enter' },
    # A siege (ADR-106, ADR-109): step into the neutral Castle-level town; walls, gate, moat, towers and keep. The footman defends, the
    # ballista (Basic Ballistics) shoots the upper wall, two more defend, quick combat mid-animation (ADR-112 check), Enter on the result.
    @{ Name = 'game-siege'; App = 'EternalWars.Game'; Frames = 150; Map = $siegeMap
       Input = '3:key:Enter;10:key:Up;62:key:D;72:click:955,280;90:key:D;100:key:D;125:key:Q;140:key:Enter' },
    # A garrison hero (ADR-112): step into the own town, "Orrin into the garrison", "Lead Orrin out", close the town screen.
    @{ Name = 'game-garrison-hero'; App = 'EternalWars.Game'; Frames = 60; Map = $townsMap
       Input = '3:key:Enter;10:key:Up;20:press:Orrin into the garrison;35:press:Lead Orrin out;48:key:Escape' },
    # Dwellings and caravans (ADR-054, ADR-073): visit a map dwelling and recruit the week, close; T, scroll to the caravan section, hire
    # from the owned dwelling, close, end two turns (hot-seat) and look again: the footmen are in the garrison.
    @{ Name = 'game-dwelling-caravan'; App = 'EternalWars.Game'; Frames = 100; Map = $caravanMap
       Input = '3:key:Enter;10:key:Up;22:click:@Recruit week (16);28:click:@Close;34:key:T;42:wheel:down,15;50:click:@Hire 9 | 55 gold each;58:key:Escape;64:click:@End turn;72:click:@End turn;82:key:T;90:wheel:down,15' },
    # The generated connections map in the editor (no fog): pan to the portal, the blue border guard, the blue keymaster, the garrison,
    # the red border gate and the red keymaster (frames 16, 49, 86, 109, 153, 172).
    @{ Name = 'editor-generated-connections'; App = 'EternalWars.MapEditor'; Frames = 175; Map = $connectionsMap
       Input = '5:hold:S,8;18:hold:A,19;38:hold:W,8;51:hold:S,18;70:hold:D,13;88:hold:D,10;99:hold:S,7;111:hold:D,8;120:hold:W,30;155:hold:W,10;166:hold:D,3' },
    # The tavern screen (ADR-114): T, scroll to the tavern row, "Visit the tavern…", then Escape for the tooltip, the tavern and the town screen.
    @{ Name = 'game-tavern'; App = 'EternalWars.Game'; Frames = 50; Map = $townsMap
       Input = '3:key:Enter;10:key:T;16:wheel:down,25;24:click:@Visit the tavern…;34:key:Escape;38:key:Escape;44:key:Escape' },
    # Ember March (ADR-114): start, end the turn (the computer plays Red), open the town screen with its building tree, close it.
    @{ Name = 'game-ember-march'; App = 'EternalWars.Game'; Frames = 60; Map = $emberMap
       Input = '3:key:Enter;12:click:@End turn;40:key:T;52:key:Escape' },
    # Inspector rows (ADR-114) on Ember March: Select, the Inspector tab, a Minor artifact pickup, the witch hut, the sphinx, the hero (artifacts).
    @{ Name = 'editor-inspector-objects'; App = 'EternalWars.MapEditor'; Frames = 38; Map = $emberMap
       Input = '3:click:@Select;6:click:@Inspector;9:click:616,230;16:click:645,382;23:click:965,552;30:click:381,276' },
    @{ Name = 'editor-tools'; App = 'EternalWars.MapEditor'; Frames = 40
       Input = '3:click:@Road;6:click:700,600;9:click:740,600;12:click:@River;15:click:800,500;18:click:@Object;21:click:900,450;24:click:@LinkGates;27:click:900,450;30:click:1305,24' },
    # Generate from a template (ADR-087): "From template", Sylvan for Blue, Generate (Two against two at 128), then the tab again: the settings stay.
    @{ Name = 'editor-generate'; App = 'EternalWars.MapEditor'; Frames = 50
       Input = '3:click:@From template;8:click:@Random;12:choose:sylvan;16:click:@Generate;30:click:@From template' },
    # The template editor (ADR-117): "Templates", add a treasure zone, drag it, right-drag it onto start-1 to connect, Generate (opens the map).
    @{ Name = 'editor-template-editor'; App = 'EternalWars.MapEditor'; Frames = 60
       Input = '3:click:@Templates;8:click:@+ Treasure;14:drag:624,408,708,560;20:rdrag:708,560,568,373;34:click:@Generate' },
    # The Events tab (EVT-6) on the quests map: open the tab, pick the first event (its tree), then its first action (the node text).
    @{ Name = 'editor-events'; App = 'EternalWars.MapEditor'; Frames = 30; Map = $questsMap
       Input = '3:click:@Events;8:click:1090,168;14:click:1133,465' },
    # Bans and placeholders (ADR-126) on the placeholders map: the Bans tab, ban the first hero, undo it; Select, the Inspector tab, the random town.
    @{ Name = 'editor-bans'; App = 'EternalWars.MapEditor'; Frames = 30; Map = $placeholdersMap
       Input = '3:click:@Bans;7:click:@Agathe Sorrel (haven, Mage);11:click:@Undo;15:click:@Select;18:click:@Inspector;21:click:420,300' },
    # The week event settings on the conditions tab (ADR-084): tick Conflux for the weekly events, one more skipped week, undo both.
    @{ Name = 'editor-week-events'; App = 'EternalWars.MapEditor'; Frames = 40
       Input = '3:click:@Conditions;8:click:@conflux;14:click:1583,250;20:click:@Undo;26:click:@Undo' },
    # The arena switch (ARN-2) on the arena map: the Conditions tab (the switch on, the other victory rows off), untick it, scroll the tabs to
    # Validation (the combat place now does nothing).
    @{ Name = 'editor-arena'; App = 'EternalWars.MapEditor'; Frames = 40; Map = $arenaMap
       Input = '3:click:@Conditions;6:click:@Arena: a day to prepare, then one battle decides the game;10:click:1584,100;13:click:1584,100;16:click:1584,100;19:click:1584,100;24:click:1318,100' }
    # The scenarios below find buttons by their text (press:) rather than by pixel.
    # Unique buildings (ADR-138, ADR-143): Orrin enters the Necropolis town, ticks a garrison and a hero stack for the Undead Transformer and
    # transforms them; Ilya enters the Sylvan town (Elder Grove: a notice and a level-up), Gareth the Haven town (Stables: a notice).
    @{ Name = 'game-unique-buildings'; App = 'EternalWars.Game'; Frames = 64; Map = $uniqueMap
       Input = '3:key:Enter;10:key:Up;20:press:Garrison: 12 Pikeman;24:press:Hero: 20 Footman;30:press:Transform;36:key:Escape;40:press:Ilya;44:key:Up;56:press:→ Basic;60:key:Escape' },
    # Stables and the Marketplace screen (ADR-067): Gareth enters the Haven town, then the HUD's Marketplace button.
    @{ Name = 'game-stables-market'; App = 'EternalWars.Game'; Frames = 34; Map = $uniqueMap
       Input = '3:key:Enter;8:press:Gareth;12:key:Up;24:key:Escape;26:key:Escape;28:press:Marketplace' },
    # Map sites (ADR-079, ADR-080, ADR-082, ADR-105, ADR-139): each hero steps north onto its site: the University's lessons, the War
    # Academy's, the Arena's choice, Pandora's Box, the Witch Hut, the Sphinx (the map's riddle, answered wrong: a right answer's 5000 experience would bring level-ups), the Sacrificial Altar, the Artifact Merchant.
    @{ Name = 'game-map-sites'; App = 'EternalWars.Game'; Frames = 160; Map = $sitesMap
       Input = '3:key:Enter;8:press:Ulric;12:key:Up;24:key:Escape;28:press:Wenda;32:key:Up;44:key:Escape;48:press:Lior;52:key:Up;62:press:+2 Attack;68:press:Pella;72:key:Up;82:press:Open;90:key:Escape;94:press:Hagan;98:key:Up;108:press:Leave;112:press:Sefra;116:key:Up;126:press:A cave;132:press:Mortis;136:key:Up;146:key:Escape;148:press:Bram;152:key:Up' },
    # Red's map sites: the Refugee Camp and the Cartographer (Buy reveals the surface).
    @{ Name = 'game-map-sites-red'; App = 'EternalWars.Game'; Frames = 72; Map = $sitesMap
       Input = '3:key:Enter;8:press:End turn;16:press:Corin;20:key:Up;32:key:Escape;36:press:Tamsin;40:key:Up;52:press:Buy' },
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
    @{ Name = 'game-battle-mirror'; App = 'EternalWars.Game'; Frames = 150; Map = $magicMap
       Input = '3:key:Enter;8:press:Magus;12:key:H;18:press:Distant Vision;24:click:800,500;36:press:Close;40:key:Up;50:press:Close;56:key:Down;72:click:800,552;78:click:800,552;108:press:Mirror Image;114:click:410,262;132:key:D' },
    # A deployment step and war machines on the field (ADR-076), then Necromancy (ADR-078): the battle width (Tactics), Start, quick combat,
    # the result, then the Necromancy screen.
    @{ Name = 'game-necromancy'; App = 'EternalWars.Game'; Frames = 122; Map = $necromancyMap
       Input = '3:key:Enter;10:key:Right;40:press:32;70:key:Enter;84:key:Q;112:key:Enter' },
    # Drag and drop in the deployment step (ADR-155): the same battle; the 60 stack is dragged down the zone (carried at frame 61, placed at
    # 62), the 20 stack is dropped outside the zone (refused, back in its place at 68), then the battle starts.
    @{ Name = 'game-deploy-drag'; App = 'EternalWars.Game'; Frames = 84; Map = $necromancyMap
       Input = '3:key:Enter;10:key:Right;40:press:32;60:drag:425,305,360,600;66:drag:370,480,900,480;76:key:Enter' },
    # The siege's gate, keep and towers and Earthquake (ADR-120): the ballista breaks the gate; a second run casts Earthquake.
    @{ Name = 'game-siege-gate'; App = 'EternalWars.Game'; Frames = 104; Map = $siegeMagicMap
       Input = '3:key:Enter;10:key:Up;62:key:D;72:click:958,432' },
    @{ Name = 'game-siege-earthquake'; App = 'EternalWars.Game'; Frames = 150; Map = $siegeMagicMap
       Input = '3:key:Enter;10:key:Up;62:press:Earthquake;100:key:D;110:key:D;120:key:D;135:key:D' },
    # The victory screen (ADR-103): quick combat beats the last enemy hero; the screen shows the score (nothing is pressed, so the ranking and
    # the replays folder stay as they are).
    @{ Name = 'game-victory'; App = 'EternalWars.Game'; Frames = 120; Map = $magicMap
       Input = '3:key:Enter;8:press:Magus;12:key:Up;22:press:Close;28:key:Down;44:click:800,552;50:click:800,552;80:key:Q;110:key:Enter' },
    # The editor's Grail tool, the conditions panel's difficulty and hero limit rows (ADR-083, ADR-103) and the town inspector's faction and
    # guild spells (ADR-083, ADR-123).
    @{ Name = 'editor-grail-and-town'; App = 'EternalWars.MapEditor'; Frames = 38; Map = $grailMap
       Input = '5:click:@Conditions;10:press:Grail;14:click:400,450;20:press:Select;24:click:720,560;28:click:@Inspector' },
    # The template editor's obelisk count (ADR-140): Templates, select the middle zone.
    @{ Name = 'editor-template-obelisks'; App = 'EternalWars.MapEditor'; Frames = 16
       Input = '3:press:Templates;10:click:708,373' }
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
