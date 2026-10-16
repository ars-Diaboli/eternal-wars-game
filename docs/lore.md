# Lore (draft proposal, for review)

> Status: **draft by Claude, not approved**. Names and premise are suggestions to react to. Nothing in
> code depends on this document. Per ADR-028/029, groups and rivalries have **no rule effect** outside
> the campaign.

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

## Rivalries

Each Dawn Covenant town has **two sworn enemies** in the Ashen Court, so every dark town is hated by two
light ones. The rivalries interlock into a ring:

```
          Haven
        ╱       ╲
  Necropolis   Inferno
      │             │
   Sylvan ───── Academy
        ╲       ╱
         Dungeon
```

| Town | Sworn enemies | Why |
|---|---|---|
| **Haven** | Necropolis, Inferno | The faithful against the defilers of the dead and the burners of the living |
| **Sylvan** | Dungeon, Necropolis | The forest's roots are poisoned from below by the Dungeon and by grave-rot from Necropolis |
| **Academy** | Inferno, Dungeon | Rival seekers of forbidden power: demonic pacts and the lightless depths |
| **Necropolis** | Haven, Sylvan | (mirror of the above) |
| **Inferno** | Haven, Academy | |
| **Dungeon** | Academy, Sylvan | |

Stronghold and the Swamp Bestiary have **no sworn enemies**; they raid anyone. The Firstborn stand apart.
These rivalries are story only: they drive campaign alliances, scenario setups and flavour text, and
have **no combat or rules effect** (ADR-029).

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
  border against Inferno while its own graveyards rise for Necropolis.
- Uneasy alliances come from sharing an enemy: Haven and Academy against Inferno; Academy and Sylvan
  against Dungeon; Sylvan and Haven against Necropolis.

## Open for review
- World and sun names (Vael, the Waning Eye), group names, and the "Wheel" premise as a whole.
- The rivalry ring above (user direction, 2026-10-15): Haven vs Necropolis & Inferno, Academy vs Inferno
  & Dungeon, Sylvan vs Dungeon & Necropolis.
- Should the campaign reveal what the Wheel really is (and whether ending it is good)?
- Hero names/archetypes per town; tone of in-game text (terse chronicle vs. character voices).
