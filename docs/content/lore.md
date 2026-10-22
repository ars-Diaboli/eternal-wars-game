# Lore (draft proposal, for review)

> Status: **draft by Claude, not approved**. Names and premise are suggestions to react to. Code uses two things
> from this document: the **ring** (`TavernRules.Ring`) and the **rivals** (`BuiltInSkills.Rivals` in
> `Skills.cs`; today the sworn enemies, the arch-rivals across the ring from ADR-072). Per ADR-028/029, groups and rivalries have no combat effect; their only rule effects are the two
> below (ADR-048, ADR-060).

## Premise: the Wheel of Ash

The world of **Vael** is dying slowly. Its sun, the **Waning Eye**, grows dimmer with every age. Long
ago the Firstborn powers bound the world to a cycle to keep it alive: when the light fails, the
**Wheel** turns. Kingdoms burn, the dead outnumber the living, and from the ashes a new age begins.
Each turn of the Wheel is fuelled by war.

Nobody alive remembers the Wheel's purpose, but every faction feels its pull. Some fight to stop the
turning, some to hasten it, and some only to be standing when it stops. Scholars call the endless cycle
the **Eternal Wars**.

Tone: grim, weary, morally grey. No faction is purely good. The "light" is desperate and zealous, and
the "dark" often tells the truth.

## Groups (replacing the Light / Darkness / Wild / Primordial placeholders)

| Group (proposed name) | Towns | Stance on the Wheel |
|---|---|---|
| **The Dawn Covenant** | Haven, Sylvan, Academy | Seeks to **stop** the Wheel and keep the age alive, at any cost |
| **The Ashen Court** | Necropolis, Inferno, Dungeon | Seeks to **hasten** the turning and rule the next age |
| **The Unbound** | Stronghold, Swamp Bestiary | Rejects both. Survives, raids, and endures whatever age comes |
| **The Firstborn** (non-playable) | Conflux, Dragon Utopia | The Wheel's ancient keepers. They intervene only when the cycle itself is threatened |

## The ring and the rivalries

The six great towns stand around the Wheel in a **ring**: the Dawn Covenant on one arc, the Ashen Court on the
other, so that each town has two neighbours and one **arch-rival** directly across from it:

```
          Haven
        ╱       ╲
   Sylvan       Academy
      │             │
   Inferno       Dungeon
        ╲       ╱
        Necropolis
```

Clockwise: Sylvan, Haven, Academy, Dungeon, Necropolis, Inferno, and back to Sylvan. The two arcs meet at
Academy–Dungeon and Inferno–Sylvan, where the Covenant and the Court share a border.

| Town | Neighbours on the ring | Arch-rival (across the ring) |
|---|---|---|
| **Sylvan** | Inferno, Haven | **Dungeon** |
| **Haven** | Sylvan, Academy | **Necropolis** |
| **Academy** | Haven, Dungeon | **Inferno** |
| **Dungeon** | Academy, Necropolis | **Sylvan** |
| **Necropolis** | Dungeon, Inferno | **Haven** |
| **Inferno** | Necropolis, Sylvan | **Academy** |

Each Dawn Covenant town also has a **second sworn enemy** in the Ashen Court, so every dark town is hated by two
light ones. The arch-rival is always one of the two:

| Town | Sworn enemies | Why |
|---|---|---|
| **Haven** | **Necropolis**, Inferno | The faithful against the defilers of the dead and the burners of the living |
| **Sylvan** | **Dungeon**, Necropolis | The forest's roots are poisoned from below by the Dungeon and by grave-rot from Necropolis |
| **Academy** | **Inferno**, Dungeon | Rival seekers of forbidden power: demonic pacts and the lightless depths |
| **Necropolis** | **Haven**, Sylvan | (mirror of the above) |
| **Inferno** | **Academy**, Haven | |
| **Dungeon** | **Sylvan**, Academy | |

Stronghold and the Swamp Bestiary have **no sworn enemies**; they raid anyone. The Firstborn stand apart.
The rivalries drive campaign alliances, scenario setups and flavour text, and have **no combat effect** (ADR-029).
They have exactly two rule effects:

- **Tavern offers** (ADR-060): the ring orders who a tavern offers. Neighbours trade heroes most easily, the
  arch-rival's heroes come last.
- **Skill offers** (ADR-048, ADR-072): the **arch-rival's** magic school (the town across the ring) is never in the normal
  level-up pool; it takes one of the three offers in only **5 %** of level-ups (`SkillOffers.RivalSchoolChance`). Both rules
  use the ring (user decision, HERO-3); the second sworn enemy is flavour only. Not built: the code still uses both sworn enemies.

## Towns

- **Haven**: a crumbling human theocracy holding the last lit cities. Iron discipline, flagellant
  zeal, war machines. They burn heretics to "feed the light". Sworn enemies: **Necropolis**, whose
  armies are Haven's own dead, and **Inferno**, which harvests the provinces Haven loses.
  Native terrain: **highlands** and **grass**.
- **Sylvan**: elves of the last old-growth forests, which are dying from the root. Archers and
  wardens who trade their memories to keep the trees alive. Sworn enemies: **Dungeon**, which tunnels
  beneath the groves and drains them, and **Necropolis**, whose blight rots what the Dungeon leaves.
  Native terrain: **forest floor** and **grass**.
- **Academy**: mage-cities on frozen peaks that study the Wheel itself. Constructs, bound
  elementals, and rumours that they want to *control* the turning, not stop it. Sworn enemies:
  **Inferno**, whose pact-magic tempts and corrupts their apprentices, and **Dungeon**, rival hoarders
  of forbidden lore in the deep places. Native terrain: **snow** and **ice**.
- **Necropolis**: the dead who remember every previous age and know the Wheel cannot be stopped,
  only ruled. Hated by **Haven** and **Sylvan**. Native terrain: **cursed lands**.
- **Inferno**: demons of the ash beneath the world, born from each burned age. They see every war as
  harvest. Hated by **Haven** and **Academy**. Native terrain: **lava**.
- **Dungeon**: dark elves and the lightless things they serve, untouched by the dying sun and
  ready to inherit. Hated by **Academy** and **Sylvan**. Native terrain: **subterranean**.
- **Stronghold**: orc clans who have survived several turnings by never building anything worth burning.
  Native terrain: **rough** and **wasteland**.
- **Swamp Bestiary**: beast-cults of the drowned lowlands, where old-age ruins sink into the mire.
  Native terrain: **swamp** and **shallow water**.
- **Conflux** (non-playable): elemental wardens of the Wheel's mechanism, neutral guardians.
- **Dragon Utopia** (non-playable): the last dragons, who remember the world *before* the Wheel.
  Bone dragons belong to Necropolis: dragons that died in earlier turnings.

## Vertical slice framing: Haven vs Inferno
A border province where the Waning Eye has gone dark for good: Haven's crusade marches in to relight it,
while Inferno pours out of the cracks to harvest the dying land. Good for a first campaign prologue
and for skirmish flavour text.

## Campaign hooks from the ring
- Each light town has a two-front war, so natural campaign arcs come in pairs: e.g. Haven holds the
  border against Inferno while its own graveyards rise for Necropolis, its arch-rival across the ring.
- The two borders where the arcs meet, Academy–Dungeon and Inferno–Sylvan, are where wars start and
  defectors cross over.
- Uneasy alliances come from sharing an enemy: Haven and Academy against Inferno; Academy and Sylvan
  against Dungeon; Sylvan and Haven against Necropolis.

## Open for review
- World and sun names (Vael, the Waning Eye), group names, and the "Wheel" premise as a whole.
- The ring (user direction, 2026-10-16): Sylvan, Haven, Academy, Dungeon, Necropolis, Inferno, with the arch-rivals
  across it (Sylvan–Dungeon, Academy–Inferno, Haven–Necropolis); and the sworn enemies (user direction, 2026-10-15):
  Haven vs Necropolis & Inferno, Academy vs Inferno & Dungeon, Sylvan vs Dungeon & Necropolis.
- Should the campaign reveal what the Wheel really is (and whether ending it is good)?
- Hero names/archetypes per town; tone of in-game text (terse chronicle vs. character voices).
- Naming mismatch: `data/artifact_sets.csv` calls a set "Ash Court Regalia", while the group here is the "Ashen Court".
