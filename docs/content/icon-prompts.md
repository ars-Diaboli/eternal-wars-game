# Icon prompts (for an image-generating model)

Prompts for the UI icons (owner's note, ADR-116): resources, primary stats, skills, magic schools, war machines, adventure-map and battle actions,
and plain icon buttons. Paste the **style block** in front of every prompt so the set matches; change only the subject line.

## Format for every icon

- **Size**: generate at **512 × 512**, square; the game scales them to 64, 48 and 32 px, so the shape must read at 32 px (one clear
  silhouette, no thin lines under 6 % of the width, no text, no letters, no numbers).
- **Background**: **transparent** (alpha), no frame, no circle behind the subject, no drop shadow outside the subject; 8 % empty margin on
  every side.
- **File**: PNG, 32-bit RGBA, sRGB. Name `icon_<group>_<id>.png` as in the tables below (e.g. `icon_resource_gold.png`).
- **Light**: from the top left; one soft highlight, no rim light. **View**: three-quarter front, slightly from above, for objects; flat
  front view for symbols.
- **Look**: hand-painted fantasy, like a classic strategy game's interface (rich but simple shapes, soft gradients, a thin dark outline to
  separate the subject from any background). No photo realism, no pixel art, no 3D render look.

**Style block** (start of every prompt):

> Game UI icon, hand-painted fantasy style, single centred subject on a transparent background, clear silhouette readable at 32 pixels,
> soft painted shading, light from the top left, thin dark outline, no text, no frame, no background, 512x512 PNG with alpha.

**Lore** (docs/content/lore.md): the world turns on the *Wheel of Ash*. The **Dawn Covenant** (Haven, Sylvan, Academy) wants to stop it:
gold, white, green, sky blue, sunrise light. The **Ashen Court** (Necropolis, Inferno, Dungeon) wants to hasten it: ash grey, ember red,
violet, bone. The **Unbound** (Stronghold, Swamp Bestiary): raw wood, leather, bone, moss green. The **Firstborn** (Conflux, Dragon Utopia):
stone, crystal, old bronze. Neutral icons (resources, actions, buttons) use none of the faction colours strongly.

## Resources (`icon_resource_<id>`)

| Id | Subject line |
|---|---|
| gold | a small heap of thick gold coins with one coin standing upright, warm yellow shine |
| lumber | three cut logs tied with a rope, pale wood ends showing the rings |
| ore | a lump of grey iron ore with dark metallic streaks, a few small pieces beside it |
| crystal | a cluster of three pale pink-red crystals growing from a rock base |
| mercury | a round glass flask with a silver liquid that reflects light, cork stopper |
| sulfur | a pile of yellow sulfur crystals with a thin wisp of yellow smoke |

## Primary stats (`icon_stat_<id>`)

| Id | Subject line |
|---|---|
| attack | a sword pointing up, crossed over nothing, steel blade with a red grip |
| defense | a kite shield seen from the front, steel rim, plain blue field |
| spell-power | an open hand with a bright violet spark above the palm |
| knowledge | a closed leather book with a small glowing rune on the cover |
| morale | a raised banner on a short pole, the cloth waving, warm red and gold |
| luck | a four-leaf clover with a faint golden glow |
| mana | a deep blue drop of liquid light |
| movement | a leather boot with a small wing at the heel |
| experience | a five-pointed gold star with a soft glow |

## Skill groups and skills (`icon_skill_<id>`)

One icon per skill; the level (Basic … Master) is shown by the game with a frame, not in the icon.

| Id | Subject line |
|---|---|
| offense.melee | two swords crossed, blades up |
| offense.archery | a drawn longbow with an arrow on the string |
| offense.marksmanship | an arrow hitting the centre of a round target |
| defense.armor | a steel breastplate on its own |
| defense.resistance | a shield with a violet spell bolt breaking on it |
| defense.leadership | a commander's helmet with a plume |
| estates.treasury | an iron-bound chest, lid open, coins inside |
| estates.prospecting | a pickaxe resting on a small heap of ore and crystal |
| estates.stewardship | a ring of keys on an iron loop beside a scroll with a seal |
| pathfinding.pathfinding | a dirt path winding between two small hills |
| pathfinding.logistics | a laden pack mule's saddlebags |
| pathfinding.scouting | a spyglass, brass and leather |
| tactics.tactics | a battle map with three small banner pins |
| tactics.alert | an open eye with a sharp pupil, gold iris |
| tactics.wartempo | a war drum with two sticks |
| machines.ballistics | a wooden catapult throwing a stone |
| machines.healing | a white field tent with a small red flame-shaped banner (no red cross: it is a protected emblem) |
| machines.ammo | a wooden cart full of arrows in bundles |
| rage.first (Blood Rage) | a clenched fist wrapped in red cloth, drops of red light around it |
| rage.second (Warcry) | an open shouting mouth of a tusked warrior seen from the side, sound waves |
| rage.third (Interference) | a broken staff with a fizzling spark |
| magic.<school>.spells | the school's symbol (below) on an open spell book |
| magic.<school>.attunement | the school's symbol inside a ring of small runes |
| magic.<school>.mastery | the school's symbol above a small crown |

## Magic schools (`icon_school_<id>`)

| Id | School | Subject line |
|---|---|---|
| haven | Light | a radiant sun disc with straight rays, white and gold |
| sylvan | Nature | a green leaf with a spiral vine |
| academy | Arcana | a blue eight-pointed star made of thin lines of light |
| necropolis | Death | a pale skull over a crescent, cold green glow |
| inferno | Chaos | a twisting ember-red flame with a black core |
| dungeon | Shadow | a violet crescent moon half covered by black smoke |

## War machines (`icon_machine_<id>`)

| Id | Subject line |
|---|---|
| ballista | a large crossbow on a wooden stand with wheels |
| catapult | a wooden catapult with its arm raised |
| healing-tent | a white field tent with a red flame banner |
| ammo-cart | a small two-wheeled cart full of arrows |

## Adventure-map actions (`icon_action_<id>`)

| Id | Subject line |
|---|---|
| end-turn | an hourglass with the last sand falling |
| next-hero | a hero's helmet with a small arrow pointing right |
| sleep | a closed eye with a small crescent moon |
| move | a boot print with an arrow ahead of it |
| spell-book | a closed spell book with a gold clasp |
| kingdom | a small castle with three towers |
| marketplace | a pair of scales, one pan with coins, one with a crystal |
| thieves-guild | a hooded mask with a dagger behind it |
| level | two arrows, one up one down, over a cave mouth (surface and underground) |
| dig | a shovel in a mound of earth |
| quest-log | a rolled scroll with a red wax seal |
| menu | a cogwheel |

## Battle actions (`icon_battle_<id>`)

| Id | Subject line |
|---|---|
| wait | an hourglass lying on its side |
| defend | a raised shield with a small spark on the rim |
| retreat | a white flag on a broken spear |
| surrender | a sword laid flat on two open hands |
| quick-combat | two crossed swords with a lightning bolt between them |
| auto-combat | two crossed swords with a small cogwheel |
| cast | an open hand under a spell book with a spark |
| shoot | an arrow in flight with a motion trail |
| melee | a sword striking down with a short arc |
| warcry | the tusked shouting face (as Warcry above), smaller sound waves |

## Buttons (`icon_button_<id>`)

Plain symbols, light grey with a dark outline, so the theme can tint them.

| Id | Subject line |
|---|---|
| close | an X made of two short strokes |
| ok | a tick mark |
| cancel | a circle with a diagonal stroke |
| back | an arrow pointing left |
| forward | an arrow pointing right |
| up / down | an arrow pointing up / down |
| add | a plus sign |
| remove | a minus sign |
| save | a closed book with a ribbon bookmark |
| load | an open book |
| settings | a cogwheel |
| info | a small open scroll |
| zoom-in / zoom-out | a magnifying glass with a plus / minus |
| undo / redo | a curved arrow turning left / right |
| lock / unlock | a closed / open padlock |
