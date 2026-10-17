# The sandbox project (`reference/ew`)

An earlier Godot/C# prototype. It has two parallel code bases, a pure C# core (`EW.CoreLib`, `EW.CoreLib.Data`) and
a Godot client (`EW.Client.3D`), which disagree in places. This page records what we took, what we left, and what
looked wrong, so nobody has to mine it again.

## Taken
| What | Where it went |
|---|---|
| Creature names, tiers, lines, the few real stats, flags that made sense | `data/creatures.csv`, ADR-044 |
| Terrain textures (Poly Haven and others) | `textures/terrain/`, `CREDITS.md` |
| Terrain cost multipliers, several native terrains per town, beach / ice / shallow water / cursed lands / rocky ground | ADR-045 |
| Town footprint 7×7 with free corners | ADR-046 |
| Building list, costs, prerequisites, town hall income, marketplace income, dwelling cost formula | ADR-046 (draft) |
| Mine incomes (gold 1000, lumber/ore 2, rare 1) | already ours |
| 7 army slots, 7 of 9 dwellings with every tier covered, one build per day, 4-week months | already ours |

## Left out on purpose
- **Dwelling level 2 = +50 % growth**: our upgrade produces the upgraded creature (ADR-046).
- **Movement points** 2500 (core) or 250 (client) with straight 100 / diagonal 141: we use the HoMM3 table from the
  slowest creature (ADR-018).
- **Vision radius 8** (one shared fog): we keep 5 for heroes and 2 for mines, per player (ADR-017).
- **Map sizes 64–256**: ours are 48–256 (ADR-020). **12 player colours**: we allow 8 players.
- **Mage guild with 3 levels**: ours has 4 (ADR-047).
- **Gems** as a resource: we have mercury instead (ADR-019); gem costs were read as mercury.
- **Pickups with fixed amounts** (500 gold, 10 lumber): ours have per-resource defaults and map-set amounts.
- **AI** (walk to the nearest unowned object; random tiles) and the **combat skeleton** (initiative queue, no grid):
  ours are further along.
- **Scenario idea**: "The Gathering Storm: defeat the Necromancer in 30 days" may suit the campaign.

## Oddities found
- Creatures: most stats copy-pasted (all 10s, the class defaults, or copies of the Seraph); wrong display names
  ("Devil" for Skeleton, "Iron Golem"/"Steel Golem" for Sorcerer/Mage, "Pit Lord" for Fel Breeder, "Inp",
  "Berholder"); flying Cyclops and Behemoth; Pegasus not flying; shots on melee Djinn and Naga.
- Town interaction cell inside the blocked footprint (core) versus outside it (client); full 7×7 versus free corners.
- Mines are 3×2 in their scenes, but every placed mine was overridden to 1×1.
- "GemsPile" gave crystals; `PlayerColor.Cyan` lacked its alpha byte and equalled Blue.
- Calendar: week 2 began on day 7, `NewWeek` fired on day 8, `NewMonth` fired on days 2–6 only.
- Ice and shallow water cost 175 in the core and 200 in the client; the client ignored terrain costs entirely.
- The core's daily income and movement refill were never wired up; buildings were levelled before paying, and the
  spend result was ignored; a zero cost entry made a building unaffordable.
- Fort, blacksmith, mage guild and tavern had no effect.

## Trimmed (2026-10-16)
The folder is kept for reading, not for running. Removed to save space: the third-party addons (`gdUnit4`,
`terrain_3d` with its native binaries), the Terrain3D demo and `terrain_data`, Godot's `.uid` files, the 13 terrain
textures already copied to `textures/terrain/` (the `.tres` materials still name them), and the two baked 6 MB images
inside `Scenes/world_map.tscn` (the node tree is kept). Kept as candidate art: the five main menu backgrounds
(`Resources/Backgrounds`) and four unused terrain textures (`Resources/TerrainTextures`).
