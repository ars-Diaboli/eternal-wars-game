# Icon prompts (for an image-generating model)

Prompts for the UI icons (owner's note, ADR-116): resources, primary stats, skills, magic schools, war machines, adventure-map and battle actions,
and plain icon buttons, and the five difficulty levels (ADR-204). The mouse cursors are in [cursor-prompts.md](cursor-prompts.md). The icons are generated as **sheets of 4 (2 × 2) or 6 (2 × 3)**, the shapes the image model handles best, and cut into
one file per icon. A sheet groups icons of the **same level of detail and the same use**, so they come out matching. Each prompt is: the
tier's **style block** + the **sheet block** + the sheet's numbered cells, as written below.

## Format

- **Sheet**: 2 × 2 at **1024 × 1024**, 2 × 3 (3 columns, 2 rows) at **1536 × 1024**; every cell is 512 × 512. The cells are listed in reading
  order: top row left to right, then the bottom row.
- **Icon**: each cell becomes one PNG, 32-bit RGBA, sRGB, **512 × 512**, transparent background, named `icon_<group>_<id>.png` as in the tables
  (e.g. `icon_resource_gold.png`). The game scales them to 64, 48 and 32 px, so every shape must read at 32 px: one clear silhouette, no
  line thinner than 6 % of the cell, no text, letters or numbers.
- **Cutting**: split the sheet into equal tiles (ImageMagick: `magick sheet.png -crop 3x2@ +repage icon_%d.png`, `2x2@` for a 2 × 2 sheet),
  each tile keeps the sheet's transparency. Keep the sheets (`sheet_<name>.png`) as the masters.
- **Background**: **transparent** (alpha) on every sheet, crests and charges included; no colour behind the subjects.
- **Spare cell**: where a sheet has fewer icons than cells, the free cell is a second take of the hardest icon on it. Keep the better one.
- **Light**: from the top left; one soft highlight, no rim light, no cast shadow outside the subject.

**Lore** (docs/content/lore.md): the world turns on the *Wheel of Ash*; the sun is dimming and everything is old, worn and repaired. The
**Dawn Covenant** (Haven, Sylvan, Academy) wants to stop the Wheel: gold, white, green, sky blue, sunrise light. The **Ashen Court**
(Necropolis, Inferno, Dungeon) wants to hasten it: ash grey, ember red, violet, bone. The **Unbound** (Stronghold, Swamp Bestiary): raw wood,
leather, bone, moss green. The **Firstborn** (Conflux, Dragon Utopia): stone, crystal, old bronze. Neutral icons (resources, actions,
buttons) use none of the faction colours strongly.

### Detail tiers and style blocks

| Tier | What | Where it is shown | Sheets |
|---|---|---|---|
| **A · Objects** | painted objects with materials | hero screen, level-up and trade windows, the resource bar, the difficulty row; 48–64 px | 1–7, 29 |
| **B · Emblems** | symbols with two or three colours and a glow | skill and spell rows, the action panel, the battle bar; 32–48 px | 8–18 |
| **C · Plain** | one-colour symbols the theme tints | buttons; 24–32 px | 19–22 |
| **Heraldry** | faction crests in colour, and their one-colour charges | town and player panels, flags and banners; 24–128 px | 23–28 |

**Style block A** (objects):

> Game UI icon sheet, hand-painted dark-fantasy style, rich painted detail on simple bold shapes, worn realistic materials (steel, wood,
> leather, stone, glass), soft painted shading, light from the top left, a thin dark outline around each subject, three-quarter view
> slightly from above unless a cell says otherwise, desaturated earthy colours with one accent colour per icon. No photo realism, no 3D
> render look, no pixel art.

**Style block B** (emblems):

> Game UI icon sheet, hand-painted dark-fantasy emblems, flat front view, one bold symbolic shape per icon, medium detail, two or three
> colours plus a soft glow where named, soft painted gradients, light from the top left, a thin dark outline around each subject. No photo
> realism, no 3D render look, no pixel art.

**Style block C** (plain):

> Game UI button symbol sheet, flat front view, each symbol a single light-grey shape with a subtle bevel and a thick dark outline, no
> other colour, no glow, bold and simple, equal stroke weight on every symbol.

**Sheet block** (after the style block; fill in the count and grid):

> {4 | 6} separate icons arranged in a grid of {2 | 3} columns and 2 rows, each alone in its own equal square cell, centred with about 10 %
> empty margin, all at the same scale and with the same light; nothing crosses into another cell. No grid lines, no borders, no frames, no
> text, no labels, no numbers. Transparent background, PNG with alpha. The icons, top row left to right, then bottom row:
> 1 ... , 2 ... , ...

**Negative prompt** (where the model takes one):

> text, letters, numbers, labels, frames, borders, grid lines, cell dividers, scenery, ground shadow, photo, 3D render, pixel art, blurry,
> cropped subject, subjects touching or overlapping, two subjects in one cell.

## Tier A · Objects

### 1 · Resources (2 × 3) — the resource bar, trade and cost lists

| # | File | Subject |
|---|---|---|
| 1 | `icon_resource_gold` | a small heap of thick, worn gold coins stamped with a sun, one coin standing upright in front; warm yellow metal, brown shadows, one bright glint |
| 2 | `icon_resource_lumber` | three thick cut logs stacked as a pyramid (two below, one on top), tied with a coarse rope; dark bark, pale end grain with rings facing the viewer |
| 3 | `icon_resource_ore` | a fist-sized lump of dark grey iron ore with rust-brown streaks and metallic flecks, two small broken pieces beside it |
| 4 | `icon_resource_crystal` | a cluster of three pale rose-red crystals (one tall, two short) growing from a grey rock base; sharp facets, faint inner glow |
| 5 | `icon_resource_mercury` | a round-bellied glass flask with a short neck and a cork stopper, half full of liquid silver that mirrors the light, one silver droplet beside it |
| 6 | `icon_resource_sulfur` | a small pile of rough, powdery bright-yellow sulfur crystals, one thin curl of pale yellow smoke rising from it |

### 2 · Hero stats (2 × 3) — the hero screen's header, level-up

| # | File | Subject |
|---|---|---|
| 1 | `icon_stat_attack` | a straight longsword pointing up, filling the cell's height: steel blade with a central groove, brass cross-guard, red leather grip, round pommel |
| 2 | `icon_stat_defense` | a kite shield seen from the front: steel rim with rivets, plain deep-blue field with a few dents and scratches |
| 3 | `icon_stat_spell-power` | an open hand, palm up, seen from the front; a bright violet four-rayed spark floats above the palm and lights the fingertips violet |
| 4 | `icon_stat_knowledge` | a closed thick tome in dark brown leather with brass corners, a small pale-blue rune glowing on the cover |
| 5 | `icon_stat_mana` | one large drop of deep-blue liquid light, glowing from within, a soft white highlight at the top left |
| 6 | `icon_stat_experience` | a five-pointed gold star with bevelled faces, a soft golden glow around it |

### 3 · Combat skills (2 × 3) — skill rows, level-up offers

The level (Basic … Master) is shown by the game with a frame, not in the icon.

| # | File | Subject |
|---|---|---|
| 1 | `icon_skill_offense.melee` | two broadswords crossed in an X, blades up, steel with red grips; a small spark where the blades cross |
| 2 | `icon_skill_offense.archery` | a drawn longbow held upright, an arrow nocked and the string pulled back in a clean V; dark wood, leather grip; both limbs the same length and curve (mirror-symmetric) |
| 3 | `icon_skill_offense.marksmanship` | a round straw archery target seen from the front, red and white rings, one arrow buried in the exact centre, its feathers toward the viewer |
| 4 | `icon_skill_defense.armor` | a steel breastplate on its own, seen from the front: rounded shoulder plates, leather straps, rivets, a few scratches |
| 5 | `icon_skill_defense.resistance` | a round shield seen from the front; a violet lightning bolt strikes it from the upper right and shatters into violet sparks on its face |
| 6 | `icon_skill_defense.leadership` | a commander's closed helmet in three-quarter view, gold trim on the brow, a tall red horsehair plume |

### 4 · Kingdom skills (2 × 3)

| # | File | Subject |
|---|---|---|
| 1 | `icon_skill_estates.treasury` | an iron-bound wooden chest, lid open, heaped with gold coins that spill over its front edge |
| 2 | `icon_skill_estates.prospecting` | a miner's pickaxe leaning on a small heap of dark ore with two pink crystals in it |
| 3 | `icon_skill_estates.stewardship` | an iron ring with three large old keys, lying across a rolled scroll sealed with red wax |
| 4 | `icon_skill_pathfinding.pathfinding` | a dirt path winding from the bottom of the cell between two small green hills into the distance, a tiny wooden signpost at its side |
| 5 | `icon_skill_pathfinding.logistics` | a pair of packed leather saddlebags joined by a broad strap, a rolled blanket tied on top |
| 6 | `icon_skill_pathfinding.scouting` | a brass spyglass, extended in three sections, lying diagonally; leather-wrapped grip, a glint on the lens |

### 5 · Battlecraft skills (2 × 3)

| # | File | Subject |
|---|---|---|
| 1 | `icon_skill_tactics.tactics` | a small parchment battle map seen from above at an angle, three tiny banner pins (red, blue, gold) stuck in it, a curved ink arrow between them |
| 2 | `icon_skill_tactics.alert` | an open eye drawn front-on: sharp black pupil, gold iris, short pale rays around it |
| 3 | `icon_skill_tactics.wartempo` | a round war drum with a pale leather head and zig-zag rope cords on its side, two drumsticks crossed above it |
| 4 | `icon_skill_rage.first` (Blood Rage) | a clenched fist seen from the front, wrapped in strips of red cloth, small drops of red light around it |
| 5 | `icon_skill_rage.second` (Warcry) | the head of a tusked orc warrior seen exactly from the side, mouth wide open in a shout, three curved sound waves in front of the mouth |
| 6 | `icon_skill_rage.third` (Interference) | a wooden mage staff snapped in two in the middle, splinters and a fizzling blue spark at the break |

### 6 · War-machine skills (2 × 2)

These show what the skill does, not the machine (the machines have their own sheet).

| # | File | Subject |
|---|---|---|
| 1 | `icon_skill_machines.ballistics` | a round stone boulder smashing into a block of grey masonry wall: cracks spreading from the hit, stone chips flying |
| 2 | `icon_skill_machines.healing` | a rolled linen bandage, a small clay jar of salve with a wooden lid, and a sprig of green herb, grouped together |
| 3 | `icon_skill_machines.ammo` | a full leather quiver standing upright, packed with arrows and crossbow bolts, their feathers fanned at the top |
| 4 | spare | a second take of cell 1 |

### 7 · War machines (2 × 2) — the hero's army row, the blacksmith

Image models draw machines badly (wheels at odd angles, uneven bow arms). This sheet follows the **rules for mechanical subjects** below;
generate it on its own and expect to roll it several times.

| # | File | Subject |
|---|---|---|
| 1 | `icon_machine_ballista` | a heavy siege crossbow seen **from directly above**: a long wooden stock running from the bottom of the cell to the top, the bow across it near the top as one wide arc, **mirror-symmetric, both arms the same length and curve**, the string a straight line from tip to tip pulled back into a V to the trigger, one thick bolt lying along the stock; iron bands on the wood; **no wheels** |
| 2 | `icon_machine_catapult` | a wooden catapult in **pure side profile**, facing right: a low rectangular frame on **two solid plank wheels, each a perfect circle**, one throwing arm angled up at 45° with a cup holding a round stone, a crossbar where the arm stops; the far wheels are **hidden exactly behind** the near ones |
| 3 | `icon_machine_healing-tent` | a small white canvas field tent in three-quarter view, its flap tied open, a pole at the front with a red **flame-shaped** pennant (no red cross: it is a protected emblem) |
| 4 | `icon_machine_ammo-cart` | a two-wheeled wooden handcart in **pure side profile**, facing right: **one visible wheel, a perfect circle with six straight spokes**, two long handles reaching to the left, the box full of bundled arrows standing upright; the far wheel **hidden exactly behind** the near one |

**Rules for mechanical subjects** (machines, bows, scales, carts):

- **Choose the view that removes perspective**: pure side profile for anything with wheels; straight above or straight front for a bow.
  Three-quarter views are where wheels go oval and tilted.
- **Say the counts and shapes**: "one visible wheel, a perfect circle, six spokes", "two wheels", "one throwing arm".
- **Say the symmetry**: "mirror-symmetric, both arms the same length and curve, the string straight from tip to tip".
- **Fewer parts**: no ropes, winches, gears or crew unless the subject needs them; solid plank wheels are safer than spoked ones.
- **Add to the negative prompt**: "warped or oval wheels, wheels at different angles, wheels tilted off the axle, extra wheels, asymmetric
  bow arms, bent or doubled string, extra arms, tangled ropes, floating or disconnected parts".
- **If it still fails**: generate the one icon alone (1 × 1 at 1024 px); or sketch it (a few boxes and cylinders in Blender or Godot, a
  screenshot is enough) and give it to the model as the image input at medium strength; or fix the wheel by hand with a circle.

### 29 · Difficulty (2 × 3) — the new-game screen's difficulty row, the victory screen, the leaderboard

Five steps of one subject, from humble to grand, so the row reads as a climb at a glance: the **same helmet shape and size** in every cell,
**three-quarter view facing left**, each richer than the last. Generate the five on one sheet so they match.

| # | File | Subject |
|---|---|---|
| 1 | `icon_difficulty_beginner` | a plain brown leather cap with ear flaps and a chin strap, worn and patched |
| 2 | `icon_difficulty_easy` | a round iron kettle helmet with a wide brim, dull grey, a few dents |
| 3 | `icon_difficulty_normal` | a polished steel helm with a closed visor and a narrow eye slit, plain, rivets along the rim |
| 4 | `icon_difficulty_hard` | the same closed steel helm with bronze trim round the eye slit and a tall dark-red plume |
| 5 | `icon_difficulty_expert` | the same closed helm in blackened steel, a gold crown band round the brow with one ember-red gem, a dark-red plume |
| 6 | — | spare: a second take of the expert helm |

## Tier B · Emblems

### 8 · Hero condition (2 × 2) — the hero panel, creature windows

| # | File | Subject |
|---|---|---|
| 1 | `icon_stat_morale` | a tall banner on a short pole, the red cloth with gold trim streaming to the right |
| 2 | `icon_stat_luck` | a four-leaf clover seen from above, deep-green leaves with pale veins, a faint golden glow |
| 3 | `icon_stat_movement` | a leather traveller's boot seen from the side, a small white feathered wing at the heel |
| 4 | `icon_action_sleep` | a closed eye drawn as a curved lid with lashes, a small pale crescent moon above it; night-blue tones |

### 9–14 · Magic schools (2 × 2 each, one sheet per school)

Every school sheet has the same four cells; only the emblem and its colour change.

| # | File | Subject |
|---|---|---|
| 1 | `icon_school_<town>` | the school's emblem (below), alone and large |
| 2 | `icon_skill_magic.<town>.spells` | the emblem glowing above an open spell book with worn leather covers, its pages tinted in the school's colour |
| 3 | `icon_skill_magic.<town>.attunement` | the emblem, smaller, inside a thin ring of small runes in the school's colour |
| 4 | `icon_skill_magic.<town>.mastery` | the emblem, smaller, above a small three-pointed crown of old gold |

| Sheet | Town | School | Emblem |
|---|---|---|---|
| 9 | `haven` | Light | a radiant sun disc of white gold with twelve straight rays of alternating length, a pale warm inner glow |
| 10 | `sylvan` | Nature | a single green leaf with pale veins, a thin vine curling into a spiral around its stem, one small bud |
| 11 | `academy` | Arcana | an eight-pointed star drawn in thin lines of pale-blue light over a faint circle, a bright point at its centre |
| 12 | `necropolis` | Death | a pale bone skull seen from the front over an upturned crescent, cold green glow in the eye sockets |
| 13 | `inferno` | Chaos | a twisting ember-red flame with a black core and orange edges, a few sparks flying off it |
| 14 | `dungeon` | Shadow | a violet crescent moon half covered by curling black smoke, a faint violet rim light |

### 15 · Adventure map: hero actions (2 × 3) — the action panel

| # | File | Subject |
|---|---|---|
| 1 | `icon_action_move` | an old iron horseshoe with a small glowing gold rune on it, and a short trail of three hoof prints curving ahead of it toward the upper right |
| 2 | `icon_action_next-hero` | a knight's helmet in profile facing right, a small gold arrow pointing right in front of it |
| 3 | `icon_action_end-turn` | an hourglass in a dark wooden frame, the top bulb almost empty, the last grains of pale sand falling |
| 4 | `icon_action_spell-book` | a closed thick spell book in dark-violet leather, gold corners and a gold clasp, a faint rune glowing on the cover |
| 5 | `icon_action_dig` | an iron shovel with a wooden handle stuck upright in a small mound of dark earth, a few clods around it |
| 6 | `icon_action_level` | a dark cave mouth in a grey rock face, one small gold arrow above it pointing up and one beside it pointing down (surface and underground) |

### 16 · Adventure map: kingdom (2 × 2) — the action panel, kingdom overview

| # | File | Subject |
|---|---|---|
| 1 | `icon_action_kingdom` | a small castle seen from the front: three grey stone towers with dark-red conical roofs, a tiny gold flag on the middle one |
| 2 | `icon_action_marketplace` | brass balance scales seen from the front, **mirror-symmetric** frame, the beam level, gold coins in the left pan and a pink crystal in the right |
| 3 | `icon_action_thieves-guild` | a black hooded mask with empty eye holes, a curved dagger crossed behind it |
| 4 | `icon_action_quest-log` | a rolled parchment scroll tied with a cord, a red wax seal hanging from the cord |

### 17 · Battle: unit actions (2 × 3) — the battle bar, the cursor

| # | File | Subject |
|---|---|---|
| 1 | `icon_battle_melee` | a sword striking down diagonally from the top right, a short pale motion arc behind the blade |
| 2 | `icon_battle_shoot` | one arrow in flight from left to right, rising slightly, three speed lines behind its feathers |
| 3 | `icon_battle_cast` | an open hand palm up under a small floating open spell book, a violet spark rising from the pages |
| 4 | `icon_battle_defend` | a raised round shield seen from the front, a bright spark on its upper rim |
| 5 | `icon_battle_wait` | an hourglass lying on its side, sand resting in both bulbs |
| 6 | `icon_battle_warcry` | a curved war horn of yellowed bone with a leather strap, three curved sound waves coming from its mouth |

### 18 · Battle: commands (2 × 2)

| # | File | Subject |
|---|---|---|
| 1 | `icon_battle_retreat` | a tattered white flag on a broken spear shaft, leaning to the left |
| 2 | `icon_battle_surrender` | a sword laid flat across two open, upturned hands, offered forward |
| 3 | `icon_battle_quick-combat` | two swords crossed in an X, a yellow lightning bolt running down between them |
| 4 | `icon_battle_auto-combat` | two swords crossed in an X, a small bronze cogwheel where they cross |

## Tier C · Plain

The symbols are light grey; the theme tints them.

### 19 · Confirm (2 × 2)

| # | File | Subject |
|---|---|---|
| 1 | `icon_button_ok` | a tick mark, the short stroke and the long stroke the same thickness |
| 2 | `icon_button_cancel` | a circle with one diagonal stroke from top left to bottom right |
| 3 | `icon_button_close` | an X of two short, equal strokes |
| 4 | `icon_button_info` | a small open scroll, rolled at both ends |

### 20 · Navigation (2 × 2)

| # | File | Subject |
|---|---|---|
| 1 | `icon_button_back` | a thick arrow pointing left: a short shaft and a wide triangular head |
| 2 | `icon_button_forward` | the same arrow pointing right |
| 3 | `icon_button_up` | the same arrow pointing up |
| 4 | `icon_button_down` | the same arrow pointing down |

### 21 · Editing (2 × 3)

| # | File | Subject |
|---|---|---|
| 1 | `icon_button_add` | a plus sign with equal arms |
| 2 | `icon_button_remove` | a minus sign, as long as the plus sign's arms |
| 3 | `icon_button_undo` | a thick arrow curving up and back to the left, like a hook |
| 4 | `icon_button_redo` | the same arrow mirrored, curving to the right |
| 5 | `icon_button_zoom-in` | a magnifying glass with a round lens and a short handle to the lower right, a plus sign in the lens |
| 6 | `icon_button_zoom-out` | the same magnifying glass with a minus sign in the lens |

### 22 · System (2 × 3)

| # | File | Subject |
|---|---|---|
| 1 | `icon_button_save` | a closed book seen from the front with a ribbon bookmark hanging below it |
| 2 | `icon_button_load` | an open book seen from the front, pages fanned |
| 3 | `icon_button_settings` | a cogwheel with eight square teeth and a round hole in the middle |
| 4 | `icon_button_lock` | a closed padlock, the shackle down in the body |
| 5 | `icon_button_unlock` | the same padlock open, the shackle raised and turned aside |
| 6 | `icon_action_menu` | three thick horizontal bars of equal length, evenly spaced |

## Heraldry

Every town type and every group has a **crest** and a **charge**, one design in two forms:

- **Crest** (`crest_<id>.png`): the charge on a shield in the faction's own colours, painted. For the UI: the town screen, the
  new-game screen, the kingdom overview, information windows, loading and result screens.
- **Charge** (`charge_<id>.png`): the same figure alone, as a flat white shape on a transparent background, used as a mask (its
  alpha). The game paints it on cloth and
  panels in any colour: town banners and hero flags in the **player colour** (the banner meshes are light grey for this, see
  [town-model-prompts.md](town-model-prompts.md)), small markers on the map and the minimap. It must read at **24 px**: one silhouette, no
  inner detail thinner than 8 % of the cell, no lettering.

Generate the crests first, then the charges with the same subject lines, so both keep one design. The figures come from the lore and
the town models: each repeats its town's landmark.

**Style block for crests** (with the sheet block):

> Fantasy heraldry crest sheet, hand-painted dark-fantasy style, each crest a heater-shaped shield of worn painted metal with a thin dark
> iron rim, every shield the same shape, size and angle, seen exactly from the front; one bold figure centred on the field in the colours
> named, flat heraldic colours with light wear and scratches, soft painted shading, light from the top left, no mantling, no helmet, no
> scroll, no motto, no text.

**Style block for charges** (with the sheet block):

> Heraldic charge sheet, each figure a single flat pure-white silhouette, no shading, no gradient, no outline, no shield,
> bold and simple like a stencil, readable at 24 pixels, every figure filling about 75 % of its cell.

### Figures

| Id | Figure (the charge) | Crest colours (field / figure) | Why |
|---|---|---|---|
| `haven` | a sun with straight rays held inside an iron cage of four curved bars | white field / gold sun, black cage | the last lit cities, the sun-brazier that "feeds the light" |
| `sylvan` | a tree whose crown is leafy on one side and bare branches on the other, its roots forming a circle below | deep green field / silver tree | the old forest dying from the root |
| `academy` | three nested rings at different tilts (an orrery) around a small eight-pointed star | sky-blue field / bronze rings, white star | the mages who study, and may want to control, the Wheel |
| `necropolis` | a skeletal hand gripping the rim of a spoked wheel | ash-grey field / bone hand, black wheel, a cold-green glint | the Wheel cannot be stopped, only ruled |
| `inferno` | a curved harvest sickle whose blade is a flame | black field / ember-red flame-blade, dark iron handle | every war is harvest |
| `dungeon` | a spider with eight legs whose body is a cut gem | dark-violet field / black spider, violet gem | the lightless ones waiting to inherit |
| `stronghold` | a horned beast skull, horns sweeping up and out | red-ochre field (like painted hide) / bone skull | the war camp under the beast skull; nothing worth burning |
| `swamp-bestiary` | a coiled serpent with three heads rising from the coil | moss-green field / dull-gold serpent | the beast cults of the mire, the hydra |
| `conflux` | a cogged wheel split into four quarters by a cross | stone-grey field / old-bronze wheel, each quarter tinted faintly (ice blue, ember, pale air, moss) | the wardens of the Wheel's mechanism |
| `dragon-utopia` | a dragon coiled around a rayed sun, wings closed | dark-bronze field / gold dragon, white sun | the dragons who remember the world before the Wheel |
| `group_dawn-covenant` | a sun rising half above a straight horizon line, three long rays | sunrise-gold field / white sun | stop the Wheel; three towns |
| `group_ashen-court` | a spoked wheel wreathed in flame | ash-grey field / ember-red burning wheel | hasten the turning |
| `group_unbound` | a broken chain: two links pulled apart, the loose ends hanging | raw-leather brown field / iron chain | bound to neither side |
| `group_firstborn` | a whole spoked wheel of stone, complete and unbroken | stone field / old-bronze wheel | the Wheel's keepers |

### 23 · Crests: Dawn Covenant and Ashen Court (2 × 3)

Cells: 1 `crest_haven`, 2 `crest_sylvan`, 3 `crest_academy`, 4 `crest_necropolis`, 5 `crest_inferno`, 6 `crest_dungeon`; each cell's
subject is "a crest with" + the figure + "in" + the colours, from the table.

### 24 · Crests: Unbound and Firstborn (2 × 2)

Cells: 1 `crest_stronghold`, 2 `crest_swamp-bestiary`, 3 `crest_conflux`, 4 `crest_dragon-utopia`.

### 25 · Crests: the four groups (2 × 2)

Cells: 1 `crest_group_dawn-covenant`, 2 `crest_group_ashen-court`, 3 `crest_group_unbound`, 4 `crest_group_firstborn`.

### 26–28 · Charges (same grids)

Sheets 26, 27 and 28 repeat sheets 23, 24 and 25 with the charge style block, the figure only (no colours) and the `charge_` names
(`charge_haven`, ..., `charge_group_firstborn`). Cut them as the other sheets; the game uses the alpha as the mask and
paints the white in the colour it needs.
