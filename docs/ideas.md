Raw design notes. Once a note is implemented or recorded (decisions.md, game-design.md), it is removed from here.

Heroes, skills and magic: moved to ADR-047 / ADR-048 (2026-10-16).

still to decide:
- offense group: third skill (placeholder `offense.third`)
- defense group: third skill (placeholder `defense.third`)

Roster notes and spell resistance: moved to ADR-049 (2026-10-16). No sea travel: ADR-051.

mechanics for morale 
calculated at the begining of every combat round
morale range is +/-5
2% chance per morale point that unit act twice during the turn(skip turn for negative morale)
if above mechanic is not triggered then
4% chance per morale point to get +/-50% boost to initiative

luck
range is +/-5 for unit/hero
applied during combat when one unit attacks another
if attacker rolls luck +75% damage
if deffender rolls luck 50% damage reduction
if both roll - nothing happens

caravan
creature dwellings accumulate weekly creature growth
caravan can be built in the town
it allows to hire creatures from nearby dwellings
creature do not arrive immediately to the town
dwelling cannot be too far from the town
path from controlled dwelling to town should not be blocked by enemy heroes or neutral creatures
imagine creatures move virtually through the map 
creature's caravan map speed affects caravan speed
if it would take more than 5 days for creature's caravan to reach the town - it is impossible to hire from specific dwelling
this should be checked before creatures are hired in the dwellin/caravan created
caravan system also allows to transfer troops between towns with the same rules