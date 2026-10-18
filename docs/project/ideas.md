Raw design notes from the owner. Once a note is implemented or recorded ([decisions.md](decisions.md), the
[design docs](../design/overview.md)), it is removed from here; open points move to [open-questions.md](open-questions.md).

Recorded so far:
- Heroes, skills and magic: ADR-047 / ADR-048 (2026-10-16).
- Roster notes and spell resistance: ADR-049 (2026-10-16). No sea travel: ADR-051.
- Morale and luck: ADR-053 (2026-10-16). War machines: ADR-056 (2026-10-16).
- Treasury and Prospecting: ADR-062 (2026-10-17). Caravans and map dwellings: ADR-054 (2026-10-17).
- Stewardship and governors, starting resources (complexity), Ballistics against fortifications, six magic schools with
  attunement and mana, faction groups for Stronghold and Swamp Bestiary: ADR-063 (2026-10-17).

Still to decide (now in open-questions.md):
- Offense and Defense groups: the third skills (placeholders `offense.third`, `defense.third`): HERO-1.
- Stronghold and Swamp Bestiary: what Rage and Adrenaline do (placeholder groups, ADR-063): HERO-2.

-----------------------

Stronghold : introduce primal fury(still placeholder name) skill group special to this faction
skills in the group : 
Blood rage(mechanics very close to homm5 stronghold faction), 
warcry(instead of spells in othe magic schools from other factions) - various bufs/debufs in combat
spell resistance(different to one in defensive group) - reduces enemy hero spell power
level distribution in the group 4,3,3

-----------------------
offense.third - leadership (morale bonus)
defense.third - initiative bonus for troops during combat (put placeholder name for now)
no luck related skill for now
-----------------------
marketplace :

basic resources - lumber/ore
magic resources - crystal/sulfur/mercury
initial cost value :
when buying basic resources - 5000 gold
when buying basic magic resources - 10000 gold
formula for trade rate : (initial cost value) / min(X + 1, 10)
X means current number of towns, town with governor counted as 1.5;

when selling resources for gold - 12.5 basic resources 
when selling resources for gold - 25 magic resources
formula for trade rate : (initial cost value) * min(X + 1, 10) rounded down
X means current number of towns, town with governor counted as 1.5;
