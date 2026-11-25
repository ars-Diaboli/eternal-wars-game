# Decisions: Magic

**Covers**: Schools, spell levels and ranks, guilds and spell books, scrolls, every battle and adventure spell.

**Current rules**: [docs/design/magic.md](../../docs/design/magic.md), [docs/content/spells.md](../../docs/content/spells.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [047](#adr-047-magic-structure) | Magic structure | accepted; partly superseded |
| [106](#adr-106-siege-rules-guild-spell-draw-spell-elements-and-summons-cmb-11-mag-13-2026-10-30) | Siege rules; guild spell draw, spell elements and summons (CMB-11, MAG-13, 2026-10-30) | accepted |
| [110](#adr-110-magic-first-slice-spell-table-guilds-spell-books-casting-in-battle-adr-047-adr-071-adr-072-adr-106-2026-10-30) | Magic, first slice: spell table, guilds, spell books, casting in battle (ADR-047, ADR-071, ADR-072, ADR-106, 2026-10-30) | accepted; partly superseded |
| [120](#adr-120-phase-5-magic-and-sieges-summons-creature-casters-adventure-spells-shrines-spell-books-in-the-map-format-the-breakable-gate-the-keeps-shooter-earthquake-adr-071-adr-072-adr-073-adr-106-adr-109-adr-110-2026-11-03) | Phase 5 magic and sieges: summons, creature casters, adventure spells, shrines, spell books in the map format; the breakable gate, the keep's shooter, Earthquake (ADR-071, ADR-072, ADR-073, ADR-106, ADR-109, ADR-110, 2026-11-03) | implemented |
| [122](#adr-122-spell-ranks-ii-and-iii-and-the-school-mastery-skill-adr-047-adr-072-adr-099-adr-110-mag-3-2026-11-03) | Spell ranks II and III and the school Mastery skill (ADR-047, ADR-072, ADR-099, ADR-110, MAG-3, 2026-11-03) | implemented |
| [123](#adr-123-the-rest-of-phase-5-magic-control-revival-and-most-battle-spells-the-ais-buffs-elements-guild-spells-fixed-by-the-map-v30-the-show-only-adventure-spells-and-the-adventure-spells-ranks-adr-099-adr-106-adr-110-adr-120-adr-122-2026-11-04) | The rest of Phase 5 magic: control, revival and most battle spells, the AI's buffs, elements, guild spells fixed by the map (v30), the show-only adventure spells and the adventure spells' ranks (ADR-099, ADR-106, ADR-110, ADR-120, ADR-122, 2026-11-04) | implemented |
| [136](#adr-136-spell-scrolls-built-adr-126-2026-11-05) | Spell scrolls built (ADR-126, 2026-11-05) | implemented |
| [149](#adr-149-field-zone-battle-spells-consecrate-thornwall-bone-wall-fire-wall-darkness-adr-099-adr-122-adr-123-2026-11-09) | Field-zone battle spells: Consecrate, Thornwall, Bone Wall, Fire Wall, Darkness (ADR-099, ADR-122, ADR-123, 2026-11-09) | implemented |
| [150](#adr-150-control-spells-frenzy-hypnotize-puppet-master-and-spells-that-last-turns-adr-099-adr-122-adr-123-2026-11-09) | Control spells: Frenzy, Hypnotize, Puppet Master, and spells that last turns (ADR-099, ADR-122, ADR-123, 2026-11-09) | — |
| [151](#adr-151-two-click-spells-teleport-shadow-step-drain-life-adr-099-adr-122-adr-149-2026-11-09) | Two-click spells: Teleport, Shadow Step, Drain Life (ADR-099, ADR-122, ADR-149, 2026-11-09) | — |
| [152](#adr-152-the-last-battle-spells-martyrdom-mirror-image-clockwork-precision-raise-dead-blood-price-puppet-master-iii-adr-099-adr-122-adr-150-adr-151-2026-11-09) | The last battle spells: Martyrdom, Mirror Image, Clockwork Precision, Raise Dead, Blood Price, Puppet Master III (ADR-099, ADR-122, ADR-150, ADR-151, 2026-11-09) | implemented |
| [177](#adr-177-the-last-rank-changes-heal-purge-meteor-shower-summon-demons-ashfall-bind-elemental-the-familiars-entangle-unravel-dispel-hellfire-inferno-sanctuary-unseen-adr-122-adr-123-2026-11-11) | The last rank changes: Heal, Purge, Meteor Shower, Summon Demons, Ashfall, Bind Elemental, the Familiars, Entangle, Unravel, Dispel, Hellfire, Inferno, Sanctuary, Unseen (ADR-122, ADR-123, 2026-11-11) | accepted |


### ADR-047: Magic structure
**Status**: accepted (2026-10-16), from [ideas.md](../ideas.md); spells and numbers come with Phase 5; partly superseded by
ADR-063 (eight schools → six: Stronghold and Swamp Bestiary get faction ability groups; attunement effects)
- **Eight schools**, one per playable town (user decision: Stronghold and Swamp Bestiary get one too). Working titles:
  Light (Haven), Nature (Sylvan), Arcana (Academy), Death (Necropolis), Chaos (Inferno), Shadow (Dungeon), Shamanism
  (Stronghold), Primal (Swamp Bestiary). Each school is also a hero skill group (ADR-048).
- **Four spell levels**, matched by **four mage guild levels**. Every spell has **three levels of its own** (learned,
  then upgraded twice).
- A school's skill group (4 + 4 + 2 = 10 points): **magic** (4 levels, each allowing spells of that level),
  **attunement** (4 levels: mana pool and/or regeneration, and per school spell cost or potency), **mastery** (2 levels:
  upgrade learned spells).
- Any hero may learn any school; a rival town's school is rarely offered (ADR-048).
- Replaces "four schools, five spell levels" in the game design and the sandbox's three guild levels.

### ADR-106: Siege rules; guild spell draw, spell elements and summons (CMB-11, MAG-13, 2026-10-30)
**Status**: accepted (2026-10-30), the owner's answers to CMB-11 and MAG-13 (ADR-099): the proposals, with the changes below. The sieges
are **built in ADR-109**; the magic part is not (magic is not built). Points marked *(assumed)* are the agent's reading of a short answer; numbers are
placeholders.

- **Sieges** (CMB-11; [combat.md](../../docs/design/combat.md#sieges)):
  - As proposed: every battle at a town with a Fort is a siege, hero inside or garrison only; a town without one has an ordinary battle (a).
    **4 wall sections and the gate**, 2 hit points a section at Fort and Citadel, 3 at Castle; a destroyed section is a gap (b). The keep
    (Citadel) and the two towers (Castle) shoot once a round each, **10 + 2 per town building**, the keep double, and can be destroyed (3
    points) (d). Flyers and teleporters cross the walls, shooters behind them get the 1/2 cover, one defending shooting stack may stand in the
    keep (g). The AI attacks a fortified town only when its estimate wins against the walls, and defends behind them (h).
  - **The gate** (c): the defenders pass, the attackers break it; it has **1 hit point**. A machine **firing by itself** aims at the gate
    only once **all four wall sections are destroyed**; a player in control may aim at it any time *(assumed)*. After the gate, the keep and
    towers *(assumed order)*.
  - **The moat** (e; with the Citadel): a creature entering it stops and takes damage worth **about two tower shots**: placeholder
    **2 × (10 + 2 per town building)** to the stack, each time it enters (the proposal had 10 % of its health).
  - **No free siege catapult** (f; the proposal had one for every attacker): the attacking hero's **own offensive machine** (ballista,
    cannon, catapult or glaivethrower) is what hits the walls, under ADR-063's siege Ballistics. The catapult is only Stronghold's and Swamp
    Bestiary's offensive machine, with no siege role of its own. The owner's answer says "ballista" for the attacking machine; it is read
    as any town's offensive machine *(assumed)*. An attacker whose machine was destroyed (and not bought again at a blacksmith) cannot break
    walls; Earthquake still can.
- **Guild spells** (MAG-13; [spells.md](../../docs/content/spells.md), [magic.md](../../docs/design/magic.md)):
  - As proposed: a guild's 3 / 3 / 2 / 2 spells are **drawn per town when the game starts** from the game's random source, the editor may
    fix some (a, a map format bump with the item); the extra-school buildings draw theirs the same way, from their own school (c).
  - **Adventure spells** are drawn like the others (b): no guild level always offers one (no guaranteed Town Portal in Academy guild 4).
- **The list** (MAG-13 d): as drafted, with the new tags `holy` and `summon`, and **every spell gets an element**: air, earth, fire or water
  (a new "Element" column in spells.md, one per spell, drafted with HoMM3's spell elements as reference; Magic Arrow is all four, Bind
  Elemental takes the bound elemental's). The element is not an immunity tag. The **elemental orbs** now raise an **element's** spells of
  any school: ADR-100's six school orbs become four (Orb of the Firmament, Orb of Silt, Orb of Tempestuous Fire, Orb of Driving Rain; effect
  kind `element-power`, was `school-power`), so the artifact list has **92** items (46 Major; ADR-100's count of 49 Major was one too high).
- **Summoned creatures** (MAG-13 e; the proposal had a new neutral Wolf): summons call **town creatures**, never neutrals; elementals are the
  exception (Conflux rows). Summon the Pack becomes **Call the Herd** (same id `nature.summon-pack`): Sylvan Pegasi, Silver Pegasi at rank
  III *(assumed creature)*; Summon Demons calls Inferno Horned Demons *(assumed creature)*; Awaken the Grove Sylvan Treants (Ancient at rank
  III); Hellgate Inferno Imps (Familiars); Bind Elemental Conflux air, earth, fire and water elementals (was "ice").

### ADR-110: Magic, first slice: spell table, guilds, spell books, casting in battle (ADR-047, ADR-071, ADR-072, ADR-106, 2026-10-30)
**Status**: accepted (2026-10-30), built; superseded in part by ADR-314 (battle spells in a spell book window, not one button each). The first Phase 5 magic slice: rank I only, numbers placeholders. Points marked *(assumed)* are the
agent's choices where the earlier ADRs say nothing.

- **Spell table**: `data/spells.csv` (embedded, `SpellTable`, `SpellCatalog`, `GameState.Spells`) holds all 102 spells of the re-draft: id,
  name, school (the town id), level, mana, element, tags, whether it is an adventure spell, its rank I battle effect and its notes. The
  **battle effect** is a small grammar (`SpellEffect.Parse`): `damage N`, `heal N`, `buff …` and `curse …` with the stats they change
  (`attack`, `defense`, `speed`, `speed%`, `initiative%`, `melee%`, `ranged%`, `taken%`, `melee-taken%`, `ranged-taken%`, `spell-taken%`,
  `morale`, `luck`, `roll max|min`), `rounds N|battle`, the reach (`area N`, `enemies` / `allies`, `all`) and a filter (`living`, `undead`,
  `undead-demon`, `shooter`). 37 spells have one; the rest (summons, control, resurrection, adventure spells, ranks II / III) wait. The table
  is generated from `spells.md` for now.
- **Spell books**: a Mage of a magic town starts with a book holding its specialty spell; warriors and Primal Fury heroes have none. A book
  costs **500 gold** in a mage guild (`BuySpellBookCommand`, the town's visiting hero). Books and known spells are not part of the map format
  yet: roster heroes get theirs from the class, the rest come from play.
- **Guilds** (ADR-106): when the game starts, every town by id draws 3 / 3 / 2 / 2 of its school's spells per level, without repeats, from
  `GameState.Random` (`MagicRules.DrawGuilds`), whether the guild is built or not; Stronghold and Swamp Bestiary towns draw none. A guild of
  level L teaches levels 1 to L. The map cannot fix guild spells yet.
- **Learning and mana**: an own hero on a town's entrance with a mage guild learns the guild's spells its school's **Magic** skill allows
  (Basic level 1 … Master level 4): on entering or capturing the town, at the start of the owner's turn, when a guild is built or a book
  bought while it stands there, and when it is hired there *(assumed: all of these)*. Its mana fills to the maximum only after **a night in
  the town**: at the start of the owner's turn with the hero still on the entrance, not on entering (the user's rule).
- **Casting** (ADR-071): once a round per side, during the turn of one of its own units, which keeps its action (`BattleCastCommand`; the AI
  too). The battle copies the hero's book, Spell Power, level and mana (`BattleCaster`); mana spent is written back when the battle ends
  (`HeroManaSpent`). Stack spells aim at an enemy stack (damage, curses) or an own one (healing, buffs) they work on; area spells at a cell
  (a square of N cells centred there, hitting both sides); side spells at the enemy or own side; field spells at every stack.
- **Numbers** (ADR-072, MAG-6): damage and healing are base × (10 + Spell Power) / 10; a spell specialty adds 3 % a hero level to the amount
  (not the duration) *(assumed)*; holy spells do double damage to undead and demons. Durations are rounds × (10 + Spell Power) / 10, at least 1,
  and count **after the round of the cast** *(assumed, like warcries)*: the effect lasts the rest of this round and that many more. The enemy
  hero's **Spell resistance** (8 / 14 / 20 %) cuts hostile damage and duration (rounded up, at least 1); **Interference** takes its Spell
  Power off and adds its mana to the cost. A spell cast again on a stack replaces its earlier effect; different spells add up.
- **What stops a spell**: creature immunities to the spell's tags; creature spell resistance against hostile spells; holy creatures ignore
  holy spells; war machines take only damage spells without a filter *(assumed)*; the filters. Such stacks are refused as targets, and
  reported (`BattleSpellResisted`) when an area or side spell reaches them.
- **Effects in battle**: Attack and Defense add to the damage formula's; melee, ranged and damage-taken percents multiply the damage; speed is
  flat then percent (at least 1); initiative percent joins the warcries'; morale and luck add; Bless rolls the maximum and Curse the
  minimum (no random draw). Effects end as a round starts (`BattleSpellExpired`).
- **AI**: the battle AI casts the damage spell and aim worth most (the creature value of the hit points taken off enemy stacks, less its own),
  if any is worth something; it does not buff yet. The adventure AI buys a book for a visiting hero with a Magic skill when it has gold to spare.
- **Front-ends**: the battle HUD shows the acting hero's mana and one button per battle spell; a stack or area spell then waits for a click on
  the field (Esc cancels). The 2D harness arms spells with C. The hero panel lists the book's spells.
- **Not built**: ranks II / III and Mastery; adventure spells; summons, control, resurrection and the other effects without a battle line;
  creature casters; shrines; the extra-school spell buildings and the Primal Fury building; guild spells set by the map; spell books and
  spells in the map format; the AI's buffs; the orbs' element power (built since, ADR-111).

### ADR-120: Phase 5 magic and sieges: summons, creature casters, adventure spells, shrines, spell books in the map format; the breakable gate, the keep's shooter, Earthquake (ADR-071, ADR-072, ADR-073, ADR-106, ADR-109, ADR-110, 2026-11-03)
**Status**: implemented (2026-11-03); the owner's gate note (ideas.md) is built as written, the rest are the agent's proposals *(proposal)*,
for play-testing. The 3D views are written but not run in Godot yet; the 2D harness has no creature casts.

- **The gate** (owner's note): a hero can arrive without a war machine (never bought, or destroyed earlier), so the attackers' **troops
  break the gate too**: the gate has **100** hit points, **150** with upgraded fortifications (*Citadel and Castle are "upgraded"*, assumed),
  and **10 Defense**. A stack's blow at the gate is rolled like an attack on a creature with Defense 10 (+5 % per Attack point above, −2.5 % per
  point below, a melee penalty halves it) and **capped at 50**, so a large stack breaks it in 2–3 blows. **War machine rules do not change**: a
  machine's hit takes the whole gate. Only the attackers' troops strike it, and only the gate (`Siege.StructureAttackRefusal`); a stack
  next to the gate attacks it like an enemy (`BattleAttackStructureCommand`, pointing at the gate in both views). Event
  `SiegeStructureDamaged`. The battle AI breaks the gate when no wall is open and its machine is gone (`BattleAi.BreakGate`).
- **One shooter in the keep** (ADR-106 g) *(proposal)*: from the Citadel up, the defenders' strongest shooting stack starts behind the
  walls next to the keep, **in the keep**: it never moves, cannot be attacked in melee, and enemy shots at it take the wall cover. When the
  keep falls the stack leaves it (`BattleUnitLeftKeep`) and fights normally. Deployment cannot move it.
- **Earthquake** (rank I) *(proposal)*: 40 damage to every stack, −20 % initiative for 2 rounds, and **1 hit point off each of 2 random standing
  wall sections** (`walls 2` in the effect grammar). Outside sieges only the damage and the slow work.
- **Summons** (`summon id|id N [rooted]`): Summon Pack (150 Pegasi worth), Awaken Grove (400, rooted Treants), Bind Elemental (200; the
  first element not yet summoned in this battle *(proposal)*), Hellgate (80 Imps), Summon Demons (250 Horned Demons). The number is
  the health called (count = amount / the creature's health) and grows with Spell Power like damage; the stack appears at the caster's edge
  nearest the middle row, a second cast adds to it, it vanishes after the battle, gives no experience, counts for no casualties, Necromancy or the guard count. A rooted summon
  never moves. Event `BattleUnitSummoned`.
- **Creature casters** (`Caster <spell> <n>` in `creatures.csv`): **n is casts per battle**; a caster stack's **Spell Power is its count
  / 2** *(proposal)*, rank I effects, no mana. Blind, Confusion and Resurrection are not castable by creatures yet (their effect
  kinds are missing). The Phoenix's **rebirth**: a destroyed stack comes back once with **30 %** of its creatures *(proposal)*
  (`BattleUnitReborn`). Command `BattleCreatureCastCommand`; the battle AI weighs a cast (damage, summons, healing) against the stack's attack; the 3D battle
  HUD has a cast button for the caster stack.
- **Adventure spells** (ADR-073, MAG-5): cast from the book on the map for the mana and a fixed **500 movement points** (fewer left: no
  cast). Built: **Town Portal** (the nearest own town with a free entrance), **Dimension Door** (up to 6 tiles in a straight line to an
  explored, free, passable, unguarded tile, once a day), **View Terrain** (reveals radius 12), **Pathfinder's Call** and **Ashwalk** (three
  terrains native for the rest of the day). The spells that only show something (Distant Vision, Detect Magic, Grave Whisper, Ledger of
  Ages, Veil, Eavesdrop) wait for their screens. Command `CastAdventureSpellCommand`, events `AdventureSpellCast`, `AreaRevealed`; the hero
  panel lists them, Dimension Door arms a click on the map. The adventure AI uses Town Portal to come home to a threatened town.
- **Shrines** (ADR-068): three objects, `shrine.1` … `shrine.3`, each teaching **one spell of its level** (any school), set in the editor or
  drawn at game start; a visiting hero with a spell book and the school's Magic at the spell's level learns it, any number of visits.
  Generator values **500 / 1500 / 2500** *(proposal)*; the AI values a learnable shrine at 400 × its level.
- **Spell books in the map format** (**v29**): a map hero has `spellBook` and `spells` (absent in older maps: the hero's class decides, as
  before); a shrine has its `spell`. The editor sets them (`SetHeroSpells`, `SetShrineSpell`, undoable; inspector rows); the validator
  refuses a hero with spells and no book.

### ADR-122: Spell ranks II and III and the school Mastery skill (ADR-047, ADR-072, ADR-099, ADR-110, MAG-3, 2026-11-03)
**Status**: implemented (2026-11-03). The roadmap items "School skills take effect" and the ranks part of "Spell table follow-ups";
*(proposal)* marks the agent's choices.

- **School skills**: Magic (spell levels, ADR-071) and attunement (ADR-063) were already in; **Mastery** is new. `MagicRules.MaxRank`: rank 1
  without it, **2 at Basic, 3 at Advanced** (MAG-3). When a hero with a book is in an own town with a mage guild (on entering, at the start of
  its turn there, after a building or book is bought), every spell the guild or a spell building **offers** that the hero knows rises,
  free, **straight to the rank its Mastery allows** (MAG-3's assumption); spells learned in that visit rise in the same visit. A spell learned
  elsewhere (a Shrine, the map) rises when the hero visits a guild that offers it. `SpellsRaised` (spell → new rank) after `SpellsLearned`.
  Shrines teach at rank I; the Unwritten Codex's spells are rank I *(proposal)*.
- **Table**: `spells.csv` gets `battle2` and `battle3` after `battle`, each the **whole** effect at that rank in the same grammar (not a
  difference); empty means the rank below's (`SpellDefinition.EffectAt`). A rank needs the rank I effect and keeps its kind (the parser refuses
  otherwise). 42 of the 43 battle spells have a rank II or III effect (not Smite).
- **New words**: `targets N` (a stack spell also reaches the **N − 1 stacks nearest the aimed one** on its side that it works on, nearer
  first by cell distance, then by id *(proposal: no extra clicks, the aimed stack chooses the group)*); `spare-own` (an area or field spell
  leaves the caster's stacks alone); filters `melee` (no shots) and `ground` (not flying; war machines count as on the ground).
- **Battle**: `KnownSpell.Effect` is the effect at its rank, used for refusals, reach, damage, durations, summons, the AI's values and the
  HUD; rank III already counted one level higher against creature spell resistance (ADR-049). The battle HUD and hero screen show "Bless II".
- **Rank changes not built** (the rank keeps the one below): Smite's splash and its ignoring spell resistance, Holy Word's lost action, Heal's
  cure and revival, Call Lightning's jump, Barkskin's thorns, Bind Elemental's two kinds, Firebolt's and Dark Bolt's burning, Meteor Shower's
  rubble, Summon Demons staying, Ashfall sparing own demons, Frost Bolt's freezing; the Familiars' mana theft (Hellgate III calls them). The
  adventure spells' ranks (radius, Dimension Door's reach and casts, Town Portal's choice, every-terrain walking) are not built either.
- **Not changed**: the map format (map heroes' spells start at rank I; no format bump); the adventure AI does not plan guild visits for ranks
  (it gets them when it passes through); mana costs are the same at every rank (spells.md). Old replays whose heroes had Mastery and visited
  guilds may now differ.

### ADR-123: The rest of Phase 5 magic: control, revival and most battle spells, the AI's buffs, elements, guild spells fixed by the map (v30), the show-only adventure spells and the adventure spells' ranks (ADR-099, ADR-106, ADR-110, ADR-120, ADR-122, 2026-11-04)
**Status**: implemented (2026-11-04); *(proposal)* marks the agent's choices, for play-testing. The 3D battle lines, the HUD, the map editor's
guild-spell rows and the adventure-spell screens are written but not run in Godot yet.

- **Effect grammar** (`SpellEffect`): new kinds `revive N [living|undead] [lasting] [fallen]`, `dispel own|enemy|any` and `slay N` (percent of the
  creatures, at least 1). Statuses on buffs and curses: `blind`, `confused`, `entangled`, `no-shoot`, `no-attack`, `no-retaliation`, `fearless`,
  `vigil`, `true-shot`, `sanctuary`, `stasis`, `unseen`, `phase`, `ward-mind`; numbers `burn`, `regen` (each round as it starts, scaled by
  Spell Power when cast), `decay%`, `immune-level` (every spell up to the level, the own too), `shroud` (the enemy's), `thorns%`, `lifesteal%`.
  Damage words `splash`, `chain`, `pierce`, `stun%`, `execute`, `debt%`, `ring`, `column`. `rounds 0` is the rest of this round.
- **Blind** (rounds 2 / 3 / 3 and 2 stacks): the stack's turns pass (`BattleUnitSpellbound`); any hit ends it, after the blow, so it still
  retaliates **at half** (ranged retaliation: none). **Confusion** (1 / 2 / 2 rounds and 2 stacks): the stack's turn is played by itself, for any
  side, at random from the battle's generator: one of the enemies it can shoot or reach, or a move to a placement it can reach *(proposal:
  enemies only; Frenzy is the friend-or-foe spell)*; its hero does not cast in that turn. **Resurrection** (150, living): the top creature first,
  then the dead up to the stack's starting count; rank I's revived are **lost again after the battle** and die first when the stack loses
  creatures (`BattleUnit.Revived`, `KeptCount` for survivors, casualties, experience and Necromancy); rank II they stay; rank III also brings
  back an own stack destroyed in the battle when its place is free (it acts from the next round; the HUD aims at the fallen stack's cell).
  Creature casters (the Shadow Mistress' and Shadow Matriarch's Blind, the Djinn Sultan's Confusion, the Seraph's Resurrection) cast them at rank I.
- **Other battle spells now built**, with their ranks from spells.md: Purge, Steadfast, Vigil, Sanctuary, Entangle, Hunter's Sight, Regrowth,
  Fleetfoot, Chain Lightning (70, then 35 and 17 to the nearest stacks, own too; III enemies only), Oakheart, Frost Ring, Unravel, Anti-Magic,
  Dispel, Stasis (no turns; cannot be attacked or hit by spells but a dispel), Animate Dead, Call the Debt (the damage the stack dealt; not
  scaled by Spell Power *(proposal)*), Plague, Vampirism, Final Reckoning, Terror (cannot attack or shoot, retaliates at half; fearless stacks,
  Steadfast, ignore it and the Terror warcry), Inferno, Pact of Ash, Hellfire, Harvest, Shroud, Forgetfulness, Night Terrors, Unseen (cannot be
  attacked or aimed at by the enemy until it acts). 75 of the 91 battle spells work; still to come: Consecrate, Martyrdom, Thornwall, Mirror
  Image, Teleport, Clockwork Precision, Drain Life, Raise Dead, Bone Wall, Frenzy, Fire Wall, Blood Price, Shadow Step, Darkness, Hypnotize,
  Puppet Master (they need field zones, copies, control changes or two-click aiming).
- **ADR-122's rank changes built**: Smite's splash (II, enemy stacks next to it at half) and its ignoring spell resistance (III), Holy Word's
  lost action (III, 30 %), Call Lightning's jump (III), Barkskin's thorns (III, 10 %), Firebolt's (III) and Dark Bolt's (II) burning, Frost
  Bolt's freezing (III, the next action lost). **Still not built**: Heal's cure and revival, Purge II's healing, Meteor Shower's rubble, Summon
  Demons staying, Ashfall sparing own demons, Bind Elemental's two kinds, the Familiars' mana theft, Entangle III's damage, Unravel III's Spell
  Power, Dispel III's half mana, Hellfire III, Inferno III's +20 %, Sanctuary III's line, Unseen III's first strike.
- **The battle AI** casts every kind: damage (splash and chain hits counted), summons, healing, revives (the value of the health), kills,
  dispels and buffs and curses: **4 %** of the stack's creature value per round it lasts, **10 %** for Blind, Stasis and Confusion, at most 5 rounds
  counted (a spell for the battle counts 5) *(proposal)*; nothing for a spell the stack already has or a ward when the enemy cannot cast.
  Creature casters use the same values (a buff still only when the caster has nothing to hit).
- **Elements** (ADR-106): the orbs' `element-power` was already in (ADR-111); Magic Arrow now takes the **best** of the four orbs *(proposal)*,
  Bind Elemental the element of the kind it binds (from the creature id). The catalog refuses `bound` on anything but a summon of several kinds.
- **Tags and creature casters** (ADR-099): `holy` and `summon` were in (ADR-110, ADR-120); the catalog now refuses a summon without the `summon`
  tag or the tag without a summon. Every caster id in `creatures.csv` maps to a spell with a battle effect (or is the Phoenix's rebirth); a test
  keeps it so.
- **Guild spells fixed by the map** (map format **v30**): a town's optional `guildSpells` (spell ids of the town's own school, at most 3 / 3 / 2 / 2
  a level, no repeats); the rest is drawn at game start as before. Spell buildings now also draw their spells at game start from
  `GameState.Random` (they were picked from a shuffle seeded by the game seed, town and school); the map cannot fix those *(proposal)*. Changing
  a town's faction in the editor drops its fixed spells (undoable). Editor: `SetTownGuildSpells`, a "Guild spells" section in the town inspector;
  the loader and validator refuse bad lists.
- **Show-only adventure spells** (each mana + 500 movement points): Distant Vision (armies within 10 of a cell in sight; II 14, III under the
  fog), Detect Magic (what objects within 10 hold; II 15, III the banks' rewards), Grave Whisper (+10 % Necromancy on the next victory that raises
  undead, until the week ends; II 15 %, III the guards' numbers within 10), Ledger of Ages (next week's event, enemy heroes in sight; II in towns
  too, III read again free that week), Veil (other players see the army as amount words until the week ends; II artifacts hidden, III the hero
  hidden from players with no hero within 3), Eavesdrop (an enemy or neutral town in sight, read-only; II any explored one, III again free that
  week). Events `ArmiesSighted`, `MagicDetected`, `GravesMarked`, `LedgerRead`, `TownOverheard`; `AdventureIntel.ShowHero` is the one query that
  shows another player's army; the hero panel arms a map click for every aimed spell (`AdventureSpellArmed`); `AdventureIntelPanel` shows them.
- **Adventure spells' ranks**: Dimension Door 9 cells (II), twice a day (III); View Terrain radius 18 (II); Pathfinder's Call and Ashwalk every
  terrain (II), +10 % of the day's movement (III) *(proposal)*; Town Portal to a town the player clicks (II, III); at ranks I–II the portal ends the
  day's movement *(proposal: reading III's "no movement points beyond the 500")*, which changes the built behaviour and the AI's portal home.
- **Not done**: the adventure AI casts none of the show-only spells; old replays with casting heroes may differ (the AI casts more, spell
  buildings draw from the game's generator).

### ADR-136: Spell scrolls built (ADR-126, 2026-11-05)
**Status**: implemented (2026-11-05); core tested (`SpellScrollTests`); the Godot part (scrolls on the paper doll, their spells in the spell
book and the hero screen's artifact line, the editor's spell choice on a scroll pickup) not run yet. Builds ADR-126's spell scrolls, with the
owner's changes of 2026-11-05 (one scroll per spell level, 5 % of the rolls, the spell decided by the generator or the editor, values by
level); points marked *(proposal)* are the agent's choices made while building and stand unless the owner changes them. Rules:
[artifacts.md](../../docs/content/artifacts.md#how-artifacts-work).

- **One scroll per spell level**: four rows in `artifacts.csv`, `spell-scroll-1` … `spell-scroll-4` (Spell Scroll I–IV, Misc), each with the
  effect `scroll <level>` (a new effect kind) and no set or pieces; tiers 1 Minor, 2 Major, 3 and 4 Relic. A scroll with a spell written on it
  is the id `spell-scroll-1:light.bless` ("Scroll of Bless"): the catalog looks it up from its level's row (a spell of another level is not an
  artifact), so heroes, pickups, loot, trade, events and the map file carry it like any artifact id. No map format change.
- **When the spell is decided**: the random map generator writes it when it places a scroll. In the editor the author picks the scroll and
  then a spell of its level or "random"; a random (blank) scroll gets its spell at game start, as do blank scrolls a hero starts with.
  A scroll from a roll during play (rewards, banks, merchants, loot sites) is written when drawn, and a blank one given by a map event, a
  Pandora's Box or a quest when given. The spell is drawn from the game's random source among the level's spells of every school, without
  the map's banned spells unless they ban the whole level.
- **Casting**: a worn written scroll adds its spell to the spells the hero can cast (`MagicRules.Known`), so the battle caster, adventure
  spells, the spell book and the AI see it. It needs a spell book and pays the normal mana. *(proposal)* It needs no school skill (as
  HoMM3's scrolls need no Wisdom), and its rank is what the school's Mastery allows; a learned spell keeps the higher of the two ranks.
  *(proposal)* "Carrying" means wearing: like every other artifact, a scroll in the backpack does nothing, so a hero uses at most three at a
  time (the Misc slots). Nothing is learned from a scroll.
- **Rolls**: every roll (pickups left open, rewards, banks, merchants, loot sites, the generator's treasure) goes through
  `ArtifactRolls.Pick`: the tier's scroll takes **5 %** of the rolls, the other artifacts share the rest. Level 4 scrolls are never rolled
  (`ArtifactCatalog.OfTier` leaves them out), only placed by the map's author. A relic scroll counts once in the relic pool's "one of each
  before repeats", whatever spell it holds; the generator places at most one level 3 scroll per map, like any relic.
- **Value**: by spell level, blank or written (`ArtifactValue.OfScroll`): 5 / 10 / 18 / 28 for levels 1–4 (+1 Attack is 10). The AI uses it to
  choose and equip artifacts and to weigh pickups; loot that does not fit goes by it.

### ADR-149: Field-zone battle spells: Consecrate, Thornwall, Bone Wall, Fire Wall, Darkness (ADR-099, ADR-122, ADR-123, 2026-11-09)
**Status**: implemented; *(proposal)* marks the agent's choices, for play-testing. Seen in Godot: Darkness cast and drawn
(`game-battle-zone`); the other four only in tests. The zones' looks are placeholders (translucent blocks in `ZoneLook`).

- **Grammar**: a new kind `zone` with `area N` (a square) or `line N [wide W]` (W columns, N cells down the field, centred on the aimed
  cell), the triggers `enter N`, `start N`, `leave N`, `beside N`, `heal N`, the flags `blocks`, `blocks-shots`, `own-undead`, `dims-shots`,
  `defense N` (own stacks in it), the filters and `spare-own` for whom its damage hits, and `rounds N` (at least 1). A zone counts as hostile,
  so creature spell resistance and the army's immunities count against it.
- **The spells** (spells.md's ranks): Consecrate `area 3 start 15 undead-demon spare-own` (II area 5, III also `heal 10`); Thornwall
  `line 5 blocks leave 20` (II line 7, III also `blocks-shots`); Bone Wall `line 5 blocks blocks-shots own-undead` (II line 7, III also
  `beside 20 living spare-own`); Fire Wall `line 5 enter 30 start 30` (II line 7, III `line 5 wide 3`, the 3 × 5 area); Darkness
  `area 5 dims-shots` (II area 7, III also `defense 2`). All last 3 rounds.
- **Aiming** *(proposal)*: one click, like an area spell; a line always runs down the field (across the armies' way), so no direction is
  chosen. The zone is clipped to the field and may be cast over stacks and obstacles. Several zones may stand at once.
- **Movement**: blocking cells keep walkers out, except the cells a stack already covers, so a stack caught in a wall can walk out
  (Thornwall then tears it). Flyers and teleporters pass over walls. `enter` hits a walker whose path (`BattleGrid.Path`, the placements along
  the path `Reachable` found) crosses the zone from outside it, and a flyer or teleporter only where it lands *(proposal)*. `leave` hits a
  stack that moves while standing in the zone. Move damage comes after the moat's.
- **Shots**: `blocks-shots` makes a shot impossible when its line of fire crosses a zone cell that is neither the shooter's nor the target's
  (ranged retaliation too). `dims-shots` halves a shot whose shooter or target stands in the zone, once even when both do.
- **Rounds**: a zone lasts the rest of the round it is cast in plus its rounds × (10 + Spell Power) / 10 (not cut by the enemy's Spell
  resistance, which is per stack), and goes as a round starts (`BattleZoneExpired`). As a round starts, after burning and regrowth, each zone
  in id order deals `start` to the stacks in it and `beside` to the stacks next to it that it hurts, and heals own creature stacks in it.
- **Amounts**: grown with Spell Power, the specialty and the orbs when cast, like burning; each hit less the enemy hero's Spell resistance and
  changed by the stack's spell-taken percent. Holy does not double Consecrate's 15 *(proposal: spells.md gives it as the number taken)*. War
  machines are never touched (as for other non-damage spells).
- **AI** *(proposal)*: a zone is worth one round of its damage to the stacks standing in or next to it now (Thornwall's tearing counted for
  stacks in it), enemies for, own against, plus its healing; it aims at the enemy stacks' centres. Blocking and darkening are not valued, so the
  AI casts the walls and Darkness only for damage. Moving stacks do not avoid fire or thorns.
- **Code**: `SpellEffectKind.Zone`, `ZoneEffect`, `SpellEffect.AreaHeight`; `BattleZone` and `BattleZones` (rules), `BattleState.Zones`;
  the engine's `MakeZone`, `TickZones` and `MoveUnit`; `BattleGrid.Path` and the clearance; `BattleZoneCreated` and `BattleZoneExpired`, drawn by
  the 3D field and the 2D board. Battles are not saved as state (saves replay commands), so no save format change; replays of battles where a
  hero knew one of these spells may now differ (the AI casts them).

### ADR-150: Control spells: Frenzy, Hypnotize, Puppet Master, and spells that last turns (ADR-099, ADR-122, ADR-123, 2026-11-09)

- **Grammar**: the statuses `frenzied` and `controlled` (a curse on one stack only), `turns N` instead of `rounds N` for a buff or curse (the
  stack's next N turns), and `health N` for a `controlled` spell (the most total health a stack may have to be taken; the cast is refused
  otherwise).
- **The spells** (spells.md's ranks): Frenzy `curse frenzied turns 1` (II 2 turns, III also `taken% 20`); Hypnotize
  `curse controlled health 300 turns 1` (II 450, III 2 turns); Puppet Master `curse controlled rounds 2` (II 3 rounds). **Not built**: Puppet
  Master III's split (up to 600 health comes over, the rest stays); rank III keeps rank II's effect. spells.md's "(not against its own hero's
  stacks once it would die)" is dropped as unclear *(proposal)*: the puppet fights its own side's stacks like any enemy.
- **Sides**: a stack has an `Owner` (the side it came with) and a `Side` (the side it fights for): the caster's while a `controlled` spell is on
  it, else the owner's. Everything in the battle goes by `Side`: who commands its turns (a person or the AI), who may cast during them, its
  friends and enemies, and the hero whose skills, morale, luck and artifacts count for it *(proposal, like HoMM3)*. The owner counts when the
  battle ends: survivors, casualties, experience, Necromancy, a guard's count and the surrender price; and a side stays in the battle while it
  owns a living creature stack, even one the enemy controls *(proposal)*. The control ends when the spell runs out, when a dispel takes it off
  (the owner's Unravel or anyone's Dispel can free it; the caster's Purge cannot), or when the stack dies (`BattleUnitSideChanged` each time).
- **Turns**: a spell that lasts turns lasts the stack's next N turns, played or passed (Blind, Stasis, Terror, bad morale); waiting does not use
  one, and a morale extra action comes after the turn that used it. N grows with Spell Power and is cut by the enemy's Spell resistance like
  rounds. Hypnotize's health limit grows with Spell Power like an amount *(proposal; HoMM3's limit grows too)*.
- **Frenzy**: the frenzied stack's turn is played by itself, for any side (like Confusion): it goes for the nearest other stack, friend or foe
  (the gap between footprints, then the id), not in Stasis or unseen by it; it shoots it when it can, else attacks it from the nearest
  placement, else walks to the reachable placement nearest it, else defends. Every stack is its foe that turn (it cannot shoot with any stack
  next to it; cleave and multi-head hit friends too); a friend it hits retaliates. Its hero does not cast during that turn.
- **AI** *(proposal)*: Frenzy is valued like the other control curses (10 % of the stack's value a turn), Hypnotize and Puppet Master at 20 %
  a turn or round; a controlled stack is played by its new side's AI like an own stack.
- **Code**: `SpellStatus.Frenzied` and `Controlled`, `SpellEffect.Turns` and `HealthLimit`; `BattleUnit.Owner`, `Side` from the spells,
  `ActiveSpell.TurnsLeft`, `BattleState.OwnedArmyOf`; `BattleMagic.Turns` and `HealthLimit`; the engine's `UseTurn` and `SideChanged`;
  `BattleGrid.IsFoe`; `BattleAi.ActFrenzied`; `BattleSpellApplied.Turns` and `BattleUnitSideChanged`, which turns the 3D stack (plate, nose,
  facing) and redraws the 2D board. No save format change (saves replay commands); replays of battles where a hero knew one of these spells
  may now differ.

### ADR-151: Two-click spells: Teleport, Shadow Step, Drain Life (ADR-099, ADR-122, ADR-149, 2026-11-09)

- **Grammar**: a new kind `move N|field [shadow] [again|again-attack]` (an own stack to a free cell within N cells or anywhere; `shadow` only
  where it is not next to an enemy stack; `again` / `again-attack` for rank III); for damage aimed at a stack, `drain% N` (that percent of the
  damage dealt heals an own stack the caster picks) and `drain-revive` (the healing also revives its dead).
- **The spells** (spells.md's ranks): Teleport `move 10` (II `move field`, III `move field again`); Shadow Step `move field shadow` (II
  `move field`: "anywhere" lifts the shadow rule, as rank I already reaches the whole field; III `move field again-attack`); Drain Life
  `damage 50 living drain% 50` (II `targets 2`, III also `drain-revive`).
- **Aiming**: `BattleCastCommand` gains `Second`. A move takes `Target` (the stack) and `Cell` (its footprint's top-left; the game centres the
  footprint on the clicked cell); a drain takes `Target` (the enemy) and `Second` (the own stack), which may be left out (nothing is healed).
  The battle HUD arms the spell, takes the stack on the first click and the cell or own stack on the second; Esc cancels.
- **Moves**: any living own creature stack, the active one too (it keeps its action, as for every hero spell); not a rooted stack, the keep's
  stack or a war machine; Stasis wards it off like every spell. The footprint must fit (inside the field, no obstacle, no stack, no wall zone
  for a walker), elsewhere than it stands. "Within 10" is the straight line between the top-left cells, with a flyer's 0.25 slack, not grown by
  Spell Power *(proposal)*. "In shadow" is read from spells.md's parenthetical: not next to an enemy stack (Darkness is not needed). The stack
  lands over everything between: a zone it leaves or lands in, and the moat, hurt it as they would a flyer (`BattleUnitTeleported`).
- **Rank III** *(proposal)*: "can still act / attack this round" means a stack that had already taken its turn this round gets one more, after
  the waiting stacks; it may not wait then. Teleport's is a whole turn; Shadow Step's is a turn without moving (it may attack what stands next
  to it, shoot or defend). A stack still to act, or the active one, gets nothing more (`BattleUnitTurnRegained`).
- **Drain**: the own stack (living, not a war machine, not in Stasis) gets half the damage dealt to all the stacks hit (the damage they really
  lost), as its top creature's wounds; at rank III as a revive, for good (like Vampirism) *(proposal)*. Undead and constructs are not hit
  (`living`), but any own stack may drink, undead too.
- **AI** *(proposal)*: a move only for the active stack, when it fights in melee, cannot shoot now and cannot reach an enemy, to a cell next to
  an enemy it may land on, worth the blow it then strikes (so Shadow Step I is never cast by the AI); a drain picks the own stack whose healing
  is worth most and adds that to the damage's value.
- **Code**: `SpellEffectKind.Move`, `SpellTurnAgain`, `SpellEffect.MoveRange`, `Shadow`, `Again`, `DrainPercent`, `DrainRevives`, `TwoClicks`;
  `BattleMagic.MoveRefusal`, `CanLand`, `DrainRefusal`, `Drained`, `DrainHeals`; `BattleUnit.StaysPut` (in `IsFixed`); the
  engine's `Teleport` and `Drain`; `BattleTargeting.SpellPlacement`; `BattleUnitTeleported` (the 3D stack sinks and rises at its new place, the
  2D board redraws) and `BattleUnitTurnRegained`; `BattleSpellCast.Second`. No save format change (saves replay commands; `Second` is optional).

### ADR-152: The last battle spells: Martyrdom, Mirror Image, Clockwork Precision, Raise Dead, Blood Price, Puppet Master III (ADR-099, ADR-122, ADR-150, ADR-151, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing. All 91 battle spells now work at every rank.

- **Grammar**: new kinds `copy rounds N [survives N]`, `raise N [once|each-round]` (no aim) and `sacrifice N per M`; for buffs `martyr% N`
  (another own stack, the second aim, takes that share of each blow meant for the stack) and `martyr-defense N` (for the martyr); the status
  `twice` (a second turn right after the first, once a round) and the filter `machine` (war machines only; they take any spell with it); for a
  `controlled` curse with `health N`, `split`.
- **The spells**: Martyrdom `buff martyr% 50 rounds 3` (II 100 %, III also `martyr-defense 2`); Mirror Image `copy rounds 2` (II 3 rounds,
  III `survives 1`); Clockwork Precision `buff melee% 50 ranged% 50 twice rounds 2 allies machine` (II 3 rounds, III also `true-shot`: "ignores
  cover" given to every own machine, only the offensive one shoots); Raise Dead `raise 15` (II `once`, III `each-round`); Blood Price
  `sacrifice 10 per 5` (II per 4, III per 3); Puppet Master III `curse controlled rounds 3 health 600 split`.
- **Martyrdom**: aimed with two clicks (ADR-151), the guarded stack, then the martyr (another living own creature stack, not a copy). A *blow*
  is an attack, a damage spell, a field zone's damage, a tower's shot or the moat; the martyr takes its share after the blow (it does not pass it
  on again), and none while it is in Stasis. Final Reckoning, decay (Plague, Pact of Ash) and Blood Price are losses, not blows *(proposal)*.
  The link ends with the spell or when either stack dies.
- **Mirror Image** *(proposal for the open points)*: the copy has the stack's count, its top creature's health and its shots, none of its spells
  or casts; it stands on the free placement nearest the stack, for the caster's side, and acts from the next round. It is summoned (gone after the
  battle, worth no experience, nobody's loss) and does not keep a side in the battle. Any blow it takes uses a hit, after the damage: it vanishes
  when none is left (rank III: the second blow), or when its rounds (lengthened by Spell Power) end. No copy of a copy, nor of a war machine.
- **Clockwork Precision**: the second turn comes right after the machine's turn, like good morale's extra action, and not after defending.
- **Raise Dead** *(proposal)*: cast with no aim, once a battle per side. After the battle, when its hero wins against a defeated enemy,
  skeletons worth 15 % of the health of the enemy creatures slain (those Necromancy counts, not the stacks it raised in the battle; the
  percent is not raised by Spell Power) join the army with Necromancy's undead, reported in the same `UndeadRaised`. "From a destroyed stack"
  (II) and "each round" (III): when cast, and then as each round starts, the destroyed stack worth most health (either side's, not a war machine,
  summoned stack, copy or raised skeletons, nor constructs or elementals) rises on its place as a new stack of skeletons worth 15 % of its
  starting health, at least 1, for the caster; rank II raises one in all (the first chance), rank III one a round. Raised skeletons act from the
  next round and join the army whenever their side's hero wins, however the battle ended.
- **Blood Price**: an own creature stack (not a copy) loses 10 % of its creatures, at least 1, the wounded top creature first; the hero gains
  the health lost / 5 (4, 3) in mana at once, rounded down, with no cap in battle *(proposal)*. Mana left above what the hero brought is lost
  when the battle ends (the rule since ADR-110).
- **Puppet Master III**: a stack within 600 health (lengthened by Spell Power, like Hypnotize's limit) is taken whole; a bigger one is split:
  as many full-health creatures as the limit holds leave it as a stack of their own on the nearest free placement, with its spells and shots,
  and come under control; the rest stays on its side. The part comes from the same army slot (its starting count is moved over too), so the
  creatures go back together after the battle. It acts from the next round. With no room to split, or not one creature within the limit, the
  spell is refused.
- **AI** *(proposal)*: Martyrdom is valued like a buff on the guarded stack, with the other own stack with the most health as the martyr; Mirror
  Image at 10 % of the stack's value a round; Clockwork Precision like a buff on each machine; Raise Dead at the skeletons the enemy's slain
  would give now, plus the stack rising now (so not before something dies); Puppet Master III by the part it takes. It never casts Blood Price.
- **Code**: `SpellEffectKind.Copy`, `Raise`, `Sacrifice`, `SpellRises`, `SpellStatus.Twice`, `SpellFilter.Machine`, `SpellModifiers.MartyrPercent`;
  `BattleUnit.IsCopy` / `CopyHits` / `IsRaised` / `TookSecondTurn`, `InitialCount` settable for a split; `ActiveSpell.Martyr` / `IsMartyr`;
  `BattleState.Raising` / `Risen`; the engine's `Take` (blows, the martyr's share), `Mirror`, `Rise`, `SplitOff`; `BattleService`'s
  `JoinUndead` adds Raise Dead's skeletons. Events `BattleUnitCopied`, `BattleCopyVanished`, `BattleUnitShielded`, `BattleManaGained`,
  `BattleUnitActsAgain`, `BattleUnitRaised`, `BattleUnitSplit` (the 3D view adds the new stacks and narrates; the 2D board redraws). Smoke
  scenario `game-battle-mirror`. No save format change.

### ADR-177: The last rank changes: Heal, Purge, Meteor Shower, Summon Demons, Ashfall, Bind Elemental, the Familiars, Entangle, Unravel, Dispel, Hellfire, Inferno, Sanctuary, Unseen (ADR-122, ADR-123, 2026-11-11)
**Status**: accepted and implemented (2026-11-11), the owner's pick from the roadmap; builds the rank changes ADR-123 left "still not built",
as [spells.md](../../docs/content/spells.md) words them. Every one of the 91 battle spells now has its own rank III line in `spells.csv`.
- **New words in the effect grammar** (`SpellEffect.Parse`): for healing `cure` (Heal II, III) and `revive% N` (Heal III, 25); for a dispel
  `heal N` (Purge II, III: 20), `sap-power N` (Unravel III: 1) and `refund% N` (Dispel III: 50); for a curse `hit N` (Entangle III: 15); for
  damage `rubble` (Meteor Shower III), `spare-own-demons` (Ashfall III) and `own% N` (Hellfire III: `damage 160 all own% 50`); for a summon
  `stays` (Summon Demons III) and `kinds N` (Bind Elemental III: 2). Buffs gain the status `screened` (Sanctuary III) and `ambush% N`
  (Unseen III: 25). Inferno III needed no new word: `taken% 20` while it burns. Each word is refused on the wrong kind or reach.
- **The Familiars' mana theft is a creature capability**, `ManaThief 1` on `inferno.familiar` (so recruited Familiars steal too, not only
  Hellgate III's): each melee attack or shot, not a retaliation, takes 1 mana from the enemy hero; the Familiars' hero gains it when it has a
  spell book (`BattleManaStolen`).
- **Choices** *(proposals)*: Heal III revives for good, only with the health left after the top creature's wounds, up to a quarter of the
  creatures lost (rounded down). Purge II's healing grows with Spell Power and lets Purge aim at a wounded stack with no enemy spells. Rubble
  falls on the area's cells that no stack, obstacle or standing siege structure covers, as one-cell obstacles (`ObstacleClass.Rubble`, 1/8
  cover) for the rest of the battle (`BattleRubbleFell`; the 3D view and the 2D board draw it). Staying demons join the winner's army (its
  stack of them or a free slot, else they are lost); a lost or fled battle keeps none. Bind Elemental III calls the first two kinds not yet
  bound, each worth the full 280. Unravel III's −1 Spell Power is per cast and adds up for the battle (`BattleSpellPowerSapped`). Dispel
  III's refund is half its mana cost, rounded down, when every spell it took off was cast by the enemy, and not for a creature caster.
  Sanctuary III's clear line: no living stack, obstacle or standing siege structure on the line of fire. Unseen III's +25 % counts on every
  blow of the attack made while Unseen (Unseen ends as the stack's action ends).
- **The battle AI** values them with the existing rules: healing and revived health, the enemy spells taken off, Entangle's damage, both
  summoned kinds; rubble, the refund and the sapped Spell Power are not valued.
- **Not changed**: the map format, the save format and the state digest. Old saves and replays whose heroes cast these spells at rank III,
  or fought with Familiars against a hero, may now play differently.
