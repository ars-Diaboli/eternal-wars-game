Raw design notes from the owner. Once a note is implemented or recorded ([decisions.md](decisions.md), the
[design docs](../design/overview.md)), it is removed from here; open points move to [open-questions.md](open-questions.md).

Recorded so far:
- Heroes, skills and magic: ADR-047 / ADR-048 (2026-10-16).
- Roster notes and spell resistance: ADR-049 (2026-10-16). No sea travel: ADR-051.
- Morale and luck: ADR-053 (2026-10-16). War machines: ADR-056 (2026-10-16).
- Treasury and Prospecting: ADR-062 (2026-10-17). Caravans and map dwellings: ADR-054 (2026-10-17).
- Stewardship and governors, starting resources (complexity), Ballistics against fortifications, six magic schools with
  attunement and mana, faction groups for Stronghold and Swamp Bestiary: ADR-063 (2026-10-17).
- Marketplace rates, Leadership, the defense initiative skill, the Stronghold Primal Fury group: ADR-067 (2026-10-18).
- Marketplace rounding, artifact tiers, and the whole adventure-map object catalogue (resource providers, loot, dwelling
  guards, creature banks, power-ups, buffs, utilities): ADR-068 (2026-10-19), table in
  [adventure-map.md](../design/adventure-map.md#planned-objects-adr-068).

Still to decide (now in open-questions.md):
- Swamp Bestiary: what Adrenaline does (placeholder group, ADR-063): HERO-2.
- Marketplace details: the map marketplace's treasure threshold (ADR-067): TOWN-2.
- (MAP-1 … MAP-14 are all answered, ADR-068.)


changes to :
Attunement (ADR-063; several add up):
new table
| Schools | Max mana | Mana a day | Spell cost |
|---|---|---|---|
| Arcana, Chaos | +10 / 20 / 30 / 40 | +1 / 2 / 3 / 4 | – |
| Light | – | +2 / 4 / 6 / 8 | −1 / 2 / 3 / 4 |
| Death | – | +2 / 4 / 6 / 8 | – |
| Nature, Shadow | +5 / 10 / 15 / 20 | +2 / 4 / 6 / 8 | – |

additionally for
Death Attunement gives Necromancy at 5% / 10% / 15% / 20%
Necromancy mechanics 5% / 10% / 15% / 20% (depending on level) 
of creatures value slain during combat can join the necromancer's army as undead - after victory
level 1 - can raise only tier 1 undead creatures in random proportion
level 2 - can raise tier 1 and 2 undead creatures, 
- enemy creatures of level 1 to 3 are raised as tier 1 undead creatures
- enemy creatures of level 4 and above are raised as tier 2 undead creatures
level 3 - can raise tier 1, 2 and 3 undead creatures
- enemy creatures of level 1 to 2 are raised as tier 1 undead creatures
- enemy creatures of level 3 to 4 are raised as tier 2 undead creatures
- enemy creatures of level 5 are raised as tier 3 undead creatures
level 4 - can raise tier 1, 2, 3 and 4 undead creatures
- enemy creatures of level 1 to 2 are raised as tier 1 undead creatures
- enemy creatures of level 3 are raised as tier 2 undead creatures
- enemy creatures of level 4 are raised as tier 3 undead creatures
- enemy creatures of level 5 are raised as tier 4 undead creatures

Necropolis should have Necromancy amplifier 
gives 5% additional Necromancy bonus to all players heroes with that skill
eg with one Necromancy amplifier necromancer hero has following table 10% / 15% / 20% / 25%
if player owns more than one Necromancy amplifier necromancer hero gets no additional bonuses

currently :
The mana pool is 10 × Knowledge plus attunement; a hero regains ⌈Knowledge / 10⌉ mana a day plus attunement; a spell costs
at least 25 % of its base, rounded up.

additionally hero should get 10 * roundUp( (current level) / 10) of mana
meaning on levels 1,11,21,31 and so on, hero gets 10 additional points of mana
 
rework pathfinding
new table
| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Pathfinding | `pathfinding.pathfinding` | Master | −25 % of the non-native terrain penalty | −50 % | −75 % | -100% | ✅ |
| Logistics | `pathfinding.logistics` | Expert | +10 % movement points | +20 % | +30 % | – | ✅ |
| Scouting | `pathfinding.scouting` | Expert | +2 map visibility radius | +4 | +6 | – | ✅ |

rework tactics
new table
| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| War Tempo | `tactics.wartempo` | Master | +1 battlefield speed for every stack | +2 | +3 | +4 | ✅ |
| Tactics | `tactics.tactics` | Expert | rearrange troops before the battle | rearrange troops before the battle, choose battlefield width 30 or 32 | rearrange troops before the battle, choose battlefield width 28–34 | – | ✅ (ADR-033), ⏳ deployment step  |
// troops rearrangement buffer/width 4 /5 /6 cells
| Alertness (working title) | `tactics.alert` | Expert | +1 initiative to all own stacks in battle | +2 | +3 | – | ✅ (ADR-067) |

rework defense
new table
| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Armor | `defense.armor` | Master | −5 % damage taken | −10 % | −15 % | −20 % | ✅ |
| Spell resistance | `defense.resistance` | Expert | −10 % | −15 % | −20 % | – | ⏳ magic | // reduces damage or duration
| Leadership | `defense.leadership` | Expert | +1 morale to all own stacks | +2 | +3 | – | ✅ (ADR-067; the id still reads "third") |

rework offense
new table
| Skill | Id | Max | Basic | Advanced | Expert | Master | Status |
|---|---|---|---|---|---|---|---|
| Melee | `offense.melee` | Master | +9 % melee damage | +16 % | +23 % | +30 % | ✅ |
| Archery | `offense.archery` | Expert | +15 % ranged damage | +25 % | +35 % | – | ✅ |
| Marksmanship | `offense.marksmanship` | Expert | -1/4 to ranged penalty | -1/2 | -3/4 | // reduces ranged penalty
also need to introduce distance penalty for ranged attacks - every 10 cells 1/8 penalty 