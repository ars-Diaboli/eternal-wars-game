# Deliberate departures from HoMM3

> Part of the [game design](overview.md) (ADR-008). Rules live in the linked docs; new twists are proposed in
> [open-questions.md](../project/open-questions.md). All built unless marked *accepted* (decided, not yet visible in play).

| # | Area | HoMM3 | Eternal Wars |
|---|---|---|---|
| 1 | Combat grid | 15×11 hexes; units 1 or 2 hexes | Square cell grid; units 2×2 / 3×3 / 4×4 ([combat](combat.md#battlefield)) |
| 2 | Tone | Bright high fantasy | Dark, grim fantasy, realistic lighting (*accepted*) |
| 3 | Turn order | Speed decides order | Separate Initiative stat, one action per round (good morale may add one), HoMM5 timeline |
| 4 | Unit size | Mostly cosmetic | Size drives flanking exposure, body blocking and ranged cover |
| 5 | Towns | 9 towns, all playable | 8 playable in 3 groups + 2 non-playable ([factions](factions.md)) (*accepted*) |
| 6 | Terrain | Painted tiles | Smooth 3D terrain with on-demand grid overlay (*accepted*) |
| 7 | Resources | 7 (incl. gems) | 6: no gems |
| 8 | Fog of war | Permanent reveal only | Per game: permanent reveal, or shroud + unit refog with hysteresis |
| 9 | Map scale | Up to 144×144 | Presets up to 256×256, 8 players |
| 10 | Random maps | Fixed templates (HotA adds a template editor) | Template editor in our map editor; game and editor both generate |
| 11 | Terrain penalties | One native terrain per town; grass, dirt, roads free | Every terrain penalises non-natives; several native terrains per town |
| 12 | Creature tiers | 7 tiers, all buildable | 5 tiers, build 7 of 9 dwellings |
| 13 | Tactics | Larger deployment zone | Also choose battlefield width; enemy Tactics reduces yours |
| 14 | Ranged | Range and wall penalties | One cumulative cover + distance penalty; above 7/8 no shot |
| 15 | Fog ambush | — | Under refog, bumping into an unseen enemy starts a battle |
| 16 | Neutral guards | Zone of control | Per stack: zone of control, or a fully guarded 3×3 area (ADR-065) |
| 17 | Speed | One speed for battle and map | Battle Speed in cells, separate map speed |
| 18 | Morale | Extra turn or freeze only | Also ±50 % initiative for the round |
| 19 | Luck | Attacker only, double damage | Both sides roll: +75 % or −50 %; opposite rolls cancel |
| 20 | External dwellings | Visit with a hero | Caravans hire from owned dwellings (ADR-054) |
| 21 | War machines | Bought per kind; catapult in every siege | Every hero owns its town's machine, also its siege engine (ADR-056) |
| 22 | Victory | One special condition | Several victory and defeat conditions per map (ADR-057) |
| 23 | Skills | 28 skills, 8 slots, 3 levels | HoMM4-like groups of three, 4 levels (ADR-048) |
| 24 | Magic | Four schools mixed in every guild | Six schools tied to towns; warcries for Stronghold, Swamp Bestiary (ADR-047) |
| 25 | Hiring heroes | Two random heroes | Pool per player and faction, ranked by the ring; starter stack (ADR-060, ADR-081) |
| 26 | Town governors | — | A Stewardship hero makes a town cheaper (ADR-063) |
| … | | | *more twists to be proposed* ❓ |
