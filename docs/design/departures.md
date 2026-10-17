# Deliberate departures from HoMM3

> Part of the [game design](overview.md) (ADR-008). New twists are proposed in
> [open-questions.md](../project/open-questions.md).

| # | Area | HoMM3 | Eternal Wars | Status |
|---|---|---|---|---|
| 1 | Combat grid | 15×11 hexes; units occupy 1 or 2 hexes | Square cell grid; units occupy 2×2 / 3×3 / 4×4 | built |
| 2 | Tone | Bright high fantasy | Dark, grim fantasy, realistic lighting | accepted |
| 3 | Turn order | Speed decides order | Separate Initiative stat decides order (HoMM5 timeline UI); one action per round (good morale may add one) | built |
| 4 | Unit size | Mostly cosmetic (1 vs 2 hexes) | Size drives flanking exposure, body blocking and ranged cover | built |
| 5 | Towns | 9 towns, all playable | 8 playable in 3 groups + 2 non-playable towns for campaign/neutrals | accepted |
| 6 | Terrain | Painted tiles | Smooth 3D terrain with on-demand grid overlay | accepted |
| 7 | Resources | 7 (incl. gems) | 6: gold, lumber, ore, crystal, mercury, sulfur | built |
| 8 | Fog of war | Permanent reveal only | Choice per game: permanent reveal, or shroud + unit refog with hysteresis | built |
| 9 | Map scale | Up to 144×144 | Presets up to 256×256, 8 players | built |
| 10 | Random maps | Fixed templates (HotA adds a template editor) | Template editor in our map editor; game and editor both generate | in progress |
| 11 | Terrain penalties | One native terrain per town; grass, dirt and roads free for everyone | Every terrain penalises non-natives (125–200); towns may have several native terrains; only roads, Pathfinding and artifacts help | built |
| 12 | Creature tiers | 7 tiers, 1 dwelling per tier, all buildable | 5 tiers, 9 dwellings, build 7 of 9 (final per town) | built |
| 13 | Tactics | Larger deployment zone | Choose battlefield width (28–34) before battle; enemy Tactics reduces yours | built |
| 14 | Ranged | Range and wall penalties only | Cumulative cover from units (1/8, 4×4: 1/4), obstacles (1/8, 1/4) and walls (1/2); above 7/8 no shot | built |
| 15 | Fog ambush | — | Under refog, bumping into an unseen enemy starts an ambush battle | built |
| 16 | Neutral guards | Zone of control (pickups can be stolen) | Map setting: classic zone of control, or a fully guarded 3×3 area | built |
| 17 | Speed | One speed for battle and map | Battle Speed in cells, separate adventure map speed | built |
| 18 | Morale | Extra turn or freeze only | Also ±50 % initiative for the round when no extra action / freeze triggers | built |
| 19 | Luck | Attacker only, double damage | Both sides roll: +75 % (attacker) or −50 % (defender); opposite rolls cancel | built |
| 20 | External dwellings | Visit them with a hero | Caravans hire from owned dwellings within 5 days; creatures travel the map to the town | built (ADR-054) |
| 21 | War machines | Ballista, tent and cart bought at blacksmiths (one kind per town) or the War Machine Factory; catapult in every siege | Offensive machine per town, owned by every hero; it needs Advanced Ballistics outside sieges; tent heals every stack a little | built (ADR-056) |
| 22 | Victory | Standard victory (defeat everyone) plus one special condition, 7 days without a town | Default: no towns and no heroes means defeat, 7 days without a town too, last team wins; a map may switch these off and add several victory and defeat conditions | built (ADR-057) |
| 23 | Skills | 28 secondary skills, 8 slots, 3 levels | HoMM4-like groups of three skills, 4 levels, 10 points per group, at most 6 groups | built (ADR-048) |
| 24 | Magic | Four elemental schools, every town's guild mixes them | Six schools tied to towns; Stronghold and Swamp Bestiary get faction abilities instead | accepted (ADR-047, ADR-063) |
| 25 | Hiring heroes | Two random heroes per player | Offers ranked by the town's place on the ring; the upgraded tavern offers four | built (ADR-060) |
| 26 | Town governors | — | A hero with Stewardship governs a town and makes it cheaper | built (ADR-063) |
| … | | | *more twists to be proposed* ❓ | |
