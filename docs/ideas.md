Raw design notes. Once a note is implemented or recorded (decisions.md, game-design.md), it is removed from here.

Heroes, skills and magic: moved to ADR-047 / ADR-048 (2026-10-16).

still to decide:
- offense group: third skill (placeholder `offense.third`)
- defense group: third skill (placeholder `defense.third`)

Roster notes and spell resistance: moved to ADR-049 (2026-10-16). No sea travel: ADR-051.
Morale and luck: implemented, ADR-053 (2026-10-16). Caravans: designed in ADR-054, built with map dwellings (roadmap).
War machines: implemented, ADR-056 (2026-10-16).

Stewardship
when hero has appropriate skill hero can become a governor in the city
all Stewardship bonuses are only applied to a town with appointed governor
hero can only be a governor in one town
if town does not have a governor - visiting hero with appropriate skill is assigned as governor automatically
bonuses 5%,10%,15% discount for building cost; 10%, 15%, 20% for creatures
also applies for creature dwellings on adventure map if recruited by hero with Stewardship
applies for caravan recruitment if town has a governor
Stewardship also affects marketplace resource trade - town with governor has 150% value when calculating trading rates

Starting resources
affected by game complexity (selected by player during map/scenario configuration)
5 levels of scenario complexity
use homm3 for reference values
hardest difficulty 2500 gold, 10 ore, 10 lumber

Prospecting skill has another effect
10%,20%,30% chance to get 50% more resources when picking up resource piles

Treasury skill
following effect 250,500,750,1000 gold a day; 
level 3 and 4 also give 0.5% and 1% of total kingdom gold bonus every day

Ballistics
without Ballistics war machine fires automatically
by default war machine has 2/3 chance to hit towns defense structure
it can deal either 1 or 2 points of damage to the defense structure
max upgraded town fortifications give defense structures 3 hp
basic skill level gives control over war machine
advanced - fires twice same target if target is destroyed in 1 shot, target for 2nd shot is selected automatically
expert - chance to hit a taget fortification is 100%
master - damage is 2 to 3 hitpoints

