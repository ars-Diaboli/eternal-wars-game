# Decisions: Artifacts

**Covers**: Artifact slots, tiers and effects, assembled relics, the exchange screen.

**Current rules**: [docs/content/artifacts.md](../../docs/content/artifacts.md). The design and technical docs state the rules as they are now; the ADRs below are the reasons and the history. A later ADR can change an earlier one: check the status and the [index](README.md) before relying on one.

| ADR | Title | Status |
|---|---|---|
| [061](#adr-061-artifacts) | Artifacts | accepted |
| [108](#adr-108-assembled-relics-the-rules-and-six-more-relics-cont-7-2026-10-30) | Assembled relics: the rules, and six more relics (CONT-7, 2026-10-30) | accepted |
| [111](#adr-111-artifacts-the-re-draft-the-table-and-the-new-effect-kinds-adr-061-adr-099-adr-100-adr-106-adr-108-2026-10-30) | Artifacts, the re-draft: the table and the new effect kinds (ADR-061, ADR-099, ADR-100, ADR-106, ADR-108, 2026-10-30) | accepted |
| [121](#adr-121-assembled-relics-the-full-mana-and-no-faction-penalty-effects-war-machine-tuning-adr-056-adr-061-adr-068-adr-100-adr-108-2026-11-03) | Assembled relics, the full-mana and no-faction-penalty effects, war machine tuning (ADR-056, ADR-061, ADR-068, ADR-100, ADR-108, 2026-11-03) | implemented |
| [147](#adr-147-the-crown-of-the-waning-eyes-best-magic-skill-adr-099-adr-111-2026-11-06) | The Crown of the Waning Eye's best magic skill (ADR-099, ADR-111, 2026-11-06) | implemented |
| [153](#adr-153-the-artifact-exchange-screen-adr-061-adr-070-2026-11-09) | The artifact exchange screen (ADR-061, ADR-070, 2026-11-09) | implemented |
| [162](#adr-162-drag-and-drop-of-artifacts-on-the-hero-and-exchange-screens-adr-050-adr-153-2026-11-09) | Drag and drop of artifacts on the hero and exchange screens (ADR-050, ADR-153, 2026-11-09) | implemented |


### ADR-061: Artifacts
**Status**: accepted (2026-10-16), from [artifacts.md](../../docs/content/artifacts.md) and the roadmap; the numbers are a draft, and
the list was extended with HoMM3-style placeholder items; hero screen with paper doll and backpack built (`HeroPanel`, ADR-050)
- **Content is a table** (`data/artifacts.csv`, `data/artifact_sets.csv`, embedded in core, read by the apps at start-up,
  `.csv.import` "keep" like the other tables): `id, name, slot, tier, set, effects, notes`. **67 artifacts**: the
  36 lore artifacts of [artifacts.md](../../docs/content/artifacts.md) and 31 **HoMM3-style placeholders** (Centaur's Ax, Blackshard of the Dead
  Knight, Ogre's Club of Havoc, Sword of Hellfire, Titan's Gladius, the Dwarven Lords' and Gnoll King's shields, Sentinel's
  Shield, Skull Helmet, Crown of the Supreme Magi, the body armours from Petrified Wood to Titan's Cuirass, Pendant of
  Courage, Boots of Speed, Cape of Velocity, the Endless purses, bags and sack of gold, …), marked "HoMM3 style
  placeholder" in their notes, with our own flavour lines. Four **sets** (Pilgrim's Road, Armor of the Dawn Covenant, Ash Court
  Regalia, The Unbound's Trophies) add a bonus when every piece is worn.
- **Effects the game applies** (`ArtifactEffect`): Attack, Defense, Spell Power, Knowledge (only primary stats until
  magic), morale, luck, movement percent and flat, battle speed of every stack, melee and ranged damage percent, damage
  taken (all, and ranged only), daily income of a resource, native terrain (the hero's army walks it with no penalty),
  terrain penalty reduction (adds to Pathfinding, at most all of it), skill levels (one skill, or every started skill,
  capped at the skill's maximum). Effects the game cannot apply yet (mana, spell resistance, regeneration, re-rolls,
  initiative) are in the notes as "Not yet". Negative values are allowed (Titan's Gladius: +12 Attack, −3 Defense).
- **Paper doll** (`ArtifactSlot`, `HeroArtifacts`): head, neck, cloak, body, weapon, shield, 2 rings, boots, 3 miscellaneous
  and a **backpack of 50** (user decision; first 12). The rules read the sum of what is worn (`ArtifactBonuses`, kept on the hero by
  `GameState.RefreshArtifacts`; `Hero.TotalAttack`, `SkillLevelOf`, `Bonus`), never the artifacts one by one; base stats
  stay what they were, so taking an artifact off restores them.
- **Commands** (the owner's turn, no movement points): `EquipArtifactCommand` (from the backpack; the one worn in a full slot
  goes to the backpack), `UnequipArtifactCommand` (needs backpack room), `GiveArtifactCommand` (to another own hero, only
  while their exchange is open; the backpack copy goes first). Events: `ArtifactEquipped`, `ArtifactUnequipped`,
  `ArtifactGiven`, `ArtifactGained` (pickup or loot), `ArtifactLost`, `ArtifactBackpackFull`.
- **On the map**: `ArtifactPickup` objects (`artifact.minor`, `artifact.major`, `artifact.relic`); a hero stepping on one
  puts it in the backpack (a full backpack leaves it there). An open pickup **rolls its artifact when the game starts**
  from the definition's tier, from the game RNG, skipping artifacts that are already placed or carried, so relics stay
  unique while any are left (`ArtifactService.RollUnsetPickups`; no random numbers are drawn when nothing is open). Classic
  guard mode lets a pickup in a guarded tile be taken from outside, like piles and chests.
- **Loot** (user decision: all of them go to the winner): a hero that wins a battle against a hero (a defeat, not a retreat) gets all its artifacts, worn and carried, into
  its backpack; when they do not all fit, the **most valuable come first** (`ArtifactValue.BestFirst`: effects weighed, then rarer tier,
  then id; user decision) and the rest are lost. A hero that flees takes them off the map.
- **Map format v11**: heroes list `artifacts` (worn ids, each to the slot its definition names) and `backpack`; objects of kind
  `artifact` with an optional `artifact` id. The editor session has `SetHeroArtifacts` and `SetArtifactPickup` (undoable); the
  validator flags unknown artifacts. Games load their catalog like the other tables (`ArtifactCatalog` parameter).
- **AI**: it values artifacts by their effects (`AdventureAi.Score`: Attack and Defense most, battle speed, morale and luck
  high, income by gold), goes for pickups it can reach and win, and puts on what fills a free slot or beats the worst worn
  there by 20 %.
- **Not yet**: artifacts in treasure chests (ADR-039 stays without the artifact outcome), the editor inspector rows, a merchant, artifacts that give spells or war machines, the effects marked "Not yet".

### ADR-108: Assembled relics: the rules, and six more relics (CONT-7, 2026-10-30)
**Status**: accepted (2026-10-30), the owner's answer to CONT-7 (ADR-100): the proposals, except (f). **Nothing implemented**; the new
relics are a content draft in [artifacts.md](../../docs/content/artifacts.md#assembled-relics).

- **As proposed**: the game asks to join when the hero puts on the last piece, and the hero screen has an "Assemble" button too (a); the
  relic sits in the first piece's slot and the other pieces' slots stay blocked (b); it can be taken apart any time on the hero screen, the
  pieces going back to their slots (c); a defeated hero's relic goes to the winner whole when its slots are free, otherwise as pieces into
  the backpack, and the Artifact Merchant and the Sacrificial Altar take and sell pieces only (d); the AI always joins and values a piece
  more when it already has the others (e); the dropped effects stay dropped (g).
- **More assembled relics** (f; the proposal kept three): HoMM3's combinations named in the question, and the bow and Necromancy sets turned
  into relics. Drafted *(the pieces, effects and numbers are the agent's draft)*:
  - **Elixir of Life**: Ring of Vitality, Ring of Life (new) and Vial of Lifeblood; creature health, +30 % in all (HoMM3's regeneration was
    dropped by CONT-6).
  - **Ring of the Magi**: Collar, Ring and Cape of Conjuring (new, +1 Spell Power each); +3 Spell Power more. HoMM3's longer spells come
    from Spell Power, which already lengthens spells (ADR-072).
  - **Cornucopia**: Everflowing Crystal Cloak, Eversmoking Ring of Sulfur, Everpouring Vial of Mercury (new, +1 a day each); +2 of each more.
  - **Angelic Alliance**: Sword of Judgement, Lion's Shield of Courage, Celestial Necklace of Bliss (new); +2 to every primary stat and no
    morale loss for mixed factions (a new effect kind, `no-faction-penalty`; HoMM3's Prayer at the battle start is left out). Three pieces,
    not HoMM3's six.
  - **Regalia of the Undead King** and **Bow of the Sharpshooter**: no longer sets but assembled relics of the same pieces, the set bonus
    becoming the relic's added effect (+10 Necromancy points; +15 % ranged damage, +1 battle speed).
- **Counts** (with ADR-106's four orbs): **109 artifacts**: 29 Minor, 53 Major (the pieces included), 18 Relics, 9 assembled relics; **5
  sets**.

### ADR-111: Artifacts, the re-draft: the table and the new effect kinds (ADR-061, ADR-099, ADR-100, ADR-106, ADR-108, 2026-10-30)
**Status**: accepted (2026-10-30), built. The roadmap item "Artifact table: the re-draft": `data/artifacts.csv` now holds the catalog of
[artifacts.md](../../docs/content/artifacts.md), so the table is the truth for numbers again. Points marked *(assumed)* are the agent's choices.

- **Table**: 101 artifacts and 5 sets. Ids renamed: `ash-sigil` → `badge-of-courage`, `greater-gnolls-flail` → `lizard-warriors-flail`,
  `buckler-of-the-gnoll-king` → `buckler-of-the-lizard-king`, `scale-of-the-elder-dragon` → `dragon-scale-armor` (no shipped map used them;
  older saves and maps naming them fail to load them like any unknown artifact). New rows: every ✅ / 🆕 / ⏳ row of the draft, the
  assembled relics' pieces with their own effects (the relics themselves are the next roadmap item), the four orbs, the Spyglass and
  Speculum, the Seed of the World Tree, the Unwritten Codex and the Recanter's Cloak. Changed numbers: Dawn Banner +1 luck, Ember Crown +6 Spell
  Power, Bone Dragon's Fang +2 luck, Dragon Scale Armor +2 Attack, Mirror +3 Defense / +2 luck, Aegis −5 % damage taken, Amulet of the Deep
  +10 maximum mana; the dropped "Not yet" notes are gone. Sets: the Armor of the Dawn Covenant adds +2 Spell Power, "Ashen Court Regalia" is
  renamed (id kept), the Unbound's Trophies lose their terrain condition, and the **Regalia of the Firstborn** is new (+3 Attack, +3 Defense,
  +2 Spell Power, immune to `mind`).
- **New effect words** (`ArtifactEffect.Parse`, summed in `ArtifactBonuses`): `sight N` (scouting radius), `mana-a-day N`, `mana-max N`,
  `creature-health N` (percent), `no-retreat`, `necromancy N` (percent points), `growth N` (percent), `immune <tag>`, `element-power
  <air|earth|fire|water> N` (percent), `spells L1-L2`, `no-spells-above L`.
- **Rules**:
  - Mana: added to `ManaRules.MaxMana` / `DailyMana`. Sight: added to `Hero.ScoutingRadius` (seen on the next reveal, *(assumed)* not at once).
  - Creature health: the hero's creatures (not war machines) fight with health × (100 + N) / 100, rounded down, for the battle; Necromancy
    counts the slain with the table's health.
  - Growth: the sum over the owner's heroes on the map adds to the Monday growth of every town of the owner, like the fort bonus *(assumed:
    not to a new dwelling's first week)*.
  - Necromancy points add to the share only for a hero with the skill *(assumed)*.
  - Immunities: the enemy's hostile spells with the tag do not reach the army (own buffs with the tag still do: Steadfast is `mind`).
  - No retreat: either hero's artifact forbids retreat and surrender to both sides (`SurrenderRules.CheckRetreat`); the AI does not try.
  - No spells above L: neither side casts a spell above the lower cap of the two heroes (`BattleMagic.SpellLevelCap`).
  - Element power: multiplies the amount of the hero's damage and healing spells of that element (Magic Arrow's "all" gets none
    *(assumed)*).
  - Spells known while worn: with a spell book, every spell of those levels of every school is castable at rank 1 (`MagicRules.Known`);
    nothing is learned for good. The hero panel counts them.
- **AI**: the new kinds have weights in `ArtifactValue` (placeholders).
- **Not built**: the Crown of the Waning Eye's +1 to the best magic skill; the assembled relics (joining, blocked slots, `full-mana`,
  `no-faction-penalty`); creature casters' spells against artifact immunities (creature casters are not built).

### ADR-121: Assembled relics, the full-mana and no-faction-penalty effects, war machine tuning (ADR-056, ADR-061, ADR-068, ADR-100, ADR-108, 2026-11-03)
**Status**: implemented (2026-11-03), ADR-108's rules as written; *(proposal)* marks the agent's choices.

- **Treasure-chest artifacts** (roadmap, ADR-039, CONT-3) were already built in ADR-068 (5 % of chests hold a Minor artifact, no choice) and
  tested; the roadmap box was only left unticked.
- **The nine relics** are rows of `artifacts.csv` with the new `pieces` column (tier Relic): Wizard's Well, Statue of Legion, Shackles of
  War, Elixir of Life, Ring of the Magi, Cornucopia, Angelic Alliance, Regalia of the Undead King, Bow of the Sharpshooter. A relic's
  `effects` are what it **adds**; worn, it has its pieces' effects too (`ArtifactCatalog.EffectsOf`). It sits in its **first piece's slot**
  (the column order chooses it *(proposal)*: Misc for the well, statue, shackles, elixir and cornucopia, Ring for the Ring of the Magi, Weapon
  for the alliance and the bow, Body for the regalia) and **blocks the other pieces' slots**. The catalog refuses a relic in the wrong slot,
  with fewer than two pieces, a repeated or unknown piece, or a piece of two relics. Relics never drop (`OfTier` leaves them out), so map
  pickups, chests, banks, merchants and the generator give only pieces.
- **Joining**: equipping the last piece publishes `RelicReady` and the hero screen says so and shows an **Assemble** button
  (`AssembleRelicCommand`) *(the "ask" is this notice and button, not a blocking decision: a decision would break an open exchange)*.
  **Taking apart** any time (`TakeRelicApartCommand`): a worn relic into its pieces in their slots, a carried one into the backpack (needs
  the room). A carried relic is worn again only when the slots it blocks are free (the slot itself swaps as usual).
- **Loot**: a defeated hero's relic goes to the winner **whole when the winner can wear it** (its slot and the blocked ones free), else as
  its pieces into the backpack. The **Sacrificial Altar** refuses a whole relic (take it apart first); the **Artifact Merchant** sells pieces
  only (its stock is drawn by tier).
- **Effects**: `full-mana` (the hero's mana is full at the start of each of its turns) and `no-faction-penalty` (no mixed-factions morale
  for the hero's army, garrison included). Values for the AI: 40 and 15 *(proposal)*; a relic is worth its pieces plus its own effects.
- **AI**: always assembles what it can after putting artifacts on; a piece found is worth **50 % more for each other piece of its relic
  the hero has** *(proposal)*; it does not swap out a worn piece for an ordinary artifact.
- **War machine tuning** *(proposal)*: the four offensive machines now deal about the same expected damage per initiative (average damage ×
  shots × initiative / 10, `WarMachines.Throughput`): ballista 47.5, cannon 45.5, catapult 46; the glaivethrower was 72 and is now **20–26
  damage, initiative 11** (50.6), still the most, with the least health (200). Tent, cart, prices and the Ballistics table stay; machines
  already scale with the hero's Attack through the damage formula. A test keeps the four within 15 %.

### ADR-147: The Crown of the Waning Eye's best magic skill (ADR-099, ADR-111, 2026-11-06)
**Status**: implemented; rules only (the hero screen's artifact line names it).

- **Rule**: the Crown adds one level to the hero's best magic skill: the "magic" skill (which spell levels the hero can learn) of the school
  whose three skills have the most learned levels together. On a tie, the first school in the ring order (Sylvan, Haven, Academy, Dungeon,
  Necropolis, Inferno) counting from the hero's own town (its roster town, else its preferred creature's); a hero of a town without a school
  counts from Sylvan. Nothing while the hero has no school started; a skill at Master stays at Master. Only learned levels count, so other
  artifacts' skill levels do not move the choice. The level counts everywhere a skill level does (learning spells in a guild, the AI).
- **Code**: the effect word `best-magic-skill` (`ArtifactEffectKind.BestMagicSkill`); `Hero.BestMagicSkill()`; `Hero.SkillLevelOf` adds it.
  `ArtifactBonuses.HomeTown` keeps the hero's town for the tie, set by `GameState.RefreshArtifacts` (also refreshed when a placeholder
  becomes a roster hero). AI weight 8 (placeholder).

### ADR-153: The artifact exchange screen (ADR-061, ADR-070, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing.

- **Screen**: the exchange of two own heroes covers the map like the hero screen: the two heroes side by side, each with its total primary
  stats, seven army slots, war machines (a button gives one), paper doll (12 slots), assembled relics ready to join, and backpack (with a count;
  copies of one artifact show once with ×N). Escape drops what is picked, else closes the exchange.
- **Artifacts** *(proposal: clicks, not drag and drop, which stays open)*: click an artifact, worn or carried, of either hero, then where it
  goes: a slot of the other hero's paper doll (any slot; the artifact goes to its own kind) gives it and puts it on, the other's backpack
  ("Into the backpack" or any carried artifact) gives it to carry; the own paper doll puts a carried one on, the own backpack takes a worn one
  off. Clicking another artifact of the same hero picks that one instead.
- **Rule**: `GiveArtifactCommand` gains `Worn` (give the worn copy, not one carried with the same id) and `Wear` (the receiver puts it on, as
  `EquipArtifactCommand` would: the one worn in a full slot goes to its backpack, and an assembled relic needs the slots it blocks free). The
  receiver's backpack needs room only for what ends up in it. Everything is checked first, so a refused give changes nothing. Events: the
  receiver's `ArtifactUnequipped` (the swapped one) and `ArtifactEquipped`, then `ArtifactGiven`. Replays without the two fields load as before.
- **Code**: `ArtifactService.Give` and the shared `Fit` / `PutOn` (also used by `Equip`); `ExchangePanel` rebuilt, on the game app's screen
  layer. The smoke scenario `game-exchange-and-gate` gives Orrin's Rusted Blade onto Valeska's doll (her Centaur's Axe goes to her backpack),
  hands over the Iron Band to carry and moves a stack before closing. No save format change.

### ADR-162: Drag and drop of artifacts on the hero and exchange screens (ADR-050, ADR-153, 2026-11-09)
**Status**: implemented (2026-11-09); *(proposal)* marks the agent's choices, for play-testing.

- **Drag**: on the hero screen and the exchange screen a worn or carried artifact is pressed, dragged and let go where it goes; a label
  with its name follows the pointer. The places are the same as the clicks' (ADR-153): a slot of the other hero's paper doll gives it and
  puts it on, the other's backpack gives it to carry, the own doll puts a carried one on, the own backpack takes a worn one off. Godot's
  control drag: a press that moves a few pixels starts it, a press without moving stays a click; letting go over a place that would
  change nothing (back where it came from) or off the places does nothing, and the pointer shows "no drop" there.
- **Places** *(proposal)*: any slot of a paper doll, empty or not (the artifact goes to its own kind, as with clicks; not only the
  matching slot as in HoMM3), the doll's free space, every backpack button, "Into the backpack" and the backpack's free space. Clicks stay
  as they were; a drag drops what a click had picked.
- **Code**: Godot `ArtifactDrag` (`WithArtifactDrag` on a control, `Accepts`, `MoveCommand`: the give, equip or unequip command a move
  is); `HeroPanel` and `ExchangePanel` use it, and the exchange's clicks now share `MoveCommand`. No core change, no save format change.
  Smoke scenario `game-artifact-drag` (exchange: onto the other's empty slot, own backpack onto own doll, onto the other's backpack's free
  space; hero screen: onto the doll, into the backpack).
- **Not done**: dragging army stacks (still click, then slot); dropping on a slot of the wrong kind is not refused; touch drag.
