# Spell icon prompts (for an image-generating model)

Prompts for the **spell icons** of the most common spells: the spell book, the spell rows of the hero and town screens, the guild and the
battle's cast list. Six spells a school, chosen because they are the heroes' specialties, the creature casters' spells or the staple
adventure spells ([spells.md](../spells.md)); the other spells follow the same pattern later. Generated as **sheets of 6 (2 × 3)**, one
sheet a school, like the icons ([icon-prompts.md](icon-prompts.md)).

## Use a prompt

Copy the school's **Prompt** block whole; where the model takes a negative prompt, paste `NEG-SPELL` (from
[Negative prompt](#negative-prompt)). Generate at 1536 × 1024, cut the sheet and save each cell as its **File** (`<File>.png`). The
**Reads as** column is for checking the result; it is not pasted. Do not paraphrase the prompts.

**Shared wording**: the style paragraph ("Game spell icon sheet in a realistic painterly…") and the grid sentence ("6 separate
icons…") are repeated verbatim in each prompt so each is ready to paste; a change to one of them must be made in every prompt of this
file. Each school's palette paragraph ("Light school: …") appears only in that school's prompt.

## Negative prompt

**`NEG-SPELL`** (every spell sheet):

```text
text, letters, numbers, labels, frames, borders, grid lines, cell dividers, scenery, ground shadow, photograph, plastic CGI render,
pixel art, blurry, cropped subject, subjects touching or overlapping, two subjects in one cell, cartoon, comic book, anime, manga,
chibi, cute, toy-like, childish, cel shading, flat colours, flat shading, vector art, clip art, sticker, emoji, mobile game icon,
casual game art, thick black outlines, ink line art, exaggerated proportions, oversized head, big eyes, rounded bubbly shapes, glossy
plastic, smooth airbrushed gradients, oversaturated colours, candy colours, spell book, card frame, scroll, whole characters, people,
another school's colours, rainbow colours, cluttered background
```

## Output for every spell icon

- **Sheet**: 2 × 3 at 1536 × 1024, every cell 512 × 512, transparent background; cut as the icons.
- **File**: `icon_spell_<spell id>.png` with the spell's id from `spells.csv` (`icon_spell_light.bless.png`), 32-bit RGBA, 512 × 512.
  The game shows them at 48 and 32 px, and draws the school frame and the rank itself.
- **Shape**: one silhouette that reads at 32 px; the school's colour dominant, so a page of one school reads as one colour.
- **Telling apart**: the spells of one school must differ by shape, not only by detail: a buff, a curse, a bolt and an area spell each
  have their own outline.

## Light (Haven)

**Prompt** · 1536 × 1024 · negative `NEG-SPELL`

```text
Game spell icon sheet in a realistic painterly dark-fantasy style, like a detailed digital oil painting: each spell one strong image of
its effect at the moment it happens, a single central motif with believable form and real materials (fire, smoke, ice, stone, metal,
flesh), lit by its own magic in the school's colour so the glow falls naturally on the surfaces near it. Deep shadows, a full range of
values, fine detail and subtle colour variation instead of flat fills; edges made by light and shadow, not by drawn lines; front view
unless a cell says otherwise. Not a photograph, not a plastic CGI render, no pixel art. Light school: radiant white-gold and warm
sunrise light, pale cream rays, a touch of polished gold; holy, solemn and severe. 6 separate icons arranged in a grid of 3 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a sword pointing up, its blade lit white-gold from within, three short
rays of light falling onto it from above; 2 a kite shield seen from the front, outlined by a thin bright halo of white-gold light, a
small sun disc on its field; 3 an open hand palm up, a soft white-gold flame of light rising from it, a few motes drifting upward; 4 a
single heavy shaft of white-gold light striking straight down onto a cracked stone, sparks and a ring of light where it hits; 5 a
narrow pointed stone arch seen from the front, filled with a curtain of pale gold light, a small flame at its keystone; 6 a fallen
helmet on the ground, a column of white-gold light rising from it and a pale winged shape forming in the light
```

| # | File | Reads as |
|---|---|---|
| 1 | `icon_spell_light.bless` | a blessed weapon |
| 2 | `icon_spell_light.shield-of-faith` | a guarded stack |
| 3 | `icon_spell_light.heal` | healing |
| 4 | `icon_spell_light.smite` | a holy strike |
| 5 | `icon_spell_light.sanctuary` | a refuge no one can strike |
| 6 | `icon_spell_light.resurrection` | the dead rise again |

## Nature (Sylvan)

**Prompt** · 1536 × 1024 · negative `NEG-SPELL`

```text
Game spell icon sheet in a realistic painterly dark-fantasy style, like a detailed digital oil painting: each spell one strong image of
its effect at the moment it happens, a single central motif with believable form and real materials (fire, smoke, ice, stone, metal,
flesh), lit by its own magic in the school's colour so the glow falls naturally on the surfaces near it. Deep shadows, a full range of
values, fine detail and subtle colour variation instead of flat fills; edges made by light and shadow, not by drawn lines; front view
unless a cell says otherwise. Not a photograph, not a plastic CGI render, no pixel art. Nature school: living leaf green and gold-green
light, amber sap, pale bark and silver-white lightning; wild and old. 6 separate icons arranged in a grid of 3 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 thick green roots bursting from the ground and coiling tightly around an
armoured boot; 2 a young green sprout with two leaves growing from a cut tree stump, gold-green light spiralling up around it; 3 one
forked silver-white lightning bolt falling from a small dark green-grey storm cloud; 4 a silver-white lightning bolt jumping in three
bends between three small glowing points, each smaller than the last; 5 a dense dark cloud of tiny insects with amber glints, shaped
like a spiral; 6 a clenched fist seen from the front, covered in thick plates of grey-brown bark with green moss in the cracks
```

| # | File | Reads as |
|---|---|---|
| 1 | `icon_spell_nature.entangle` | held in place |
| 2 | `icon_spell_nature.regrowth` | healing over time |
| 3 | `icon_spell_nature.call-lightning` | one bolt |
| 4 | `icon_spell_nature.chain-lightning` | a bolt that jumps |
| 5 | `icon_spell_nature.swarm` | a cloud of insects |
| 6 | `icon_spell_nature.barkskin` | skin as hard as bark |

## Arcana (Academy)

**Prompt** · 1536 × 1024 · negative `NEG-SPELL`

```text
Game spell icon sheet in a realistic painterly dark-fantasy style, like a detailed digital oil painting: each spell one strong image of
its effect at the moment it happens, a single central motif with believable form and real materials (fire, smoke, ice, stone, metal,
flesh), lit by its own magic in the school's colour so the glow falls naturally on the surfaces near it. Deep shadows, a full range of
values, fine detail and subtle colour variation instead of flat fills; edges made by light and shadow, not by drawn lines; front view
unless a cell says otherwise. Not a photograph, not a plastic CGI render, no pixel art. Arcana school: cold pale sky-blue and silver
light, thin precise rune lines, frost white and faint violet; exact and scholarly. 6 separate icons arranged in a grid of 3 columns and
2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light;
nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG
with alpha. The icons, top row left to right, then bottom row: 1 a single arrow made of pale-blue light flying diagonally up to the
right, a thin trail of runes behind it; 2 a heavy iron chain with a pale-blue frost-covered padlock, a faint spiral of slowed motion
lines around it; 3 a winged silver boot with three bright pale-blue speed streaks behind it; 4 a long jagged shard of blue-white ice
flying diagonally down to the left, frost crystals breaking off it; 5 a ring of pale-blue runes shattering into fragments, flying
outward from a bright empty centre; 6 a tall pale-blue glowing doorway of light standing on its own, a tiny silhouette of a town's
towers visible through it
```

| # | File | Reads as |
|---|---|---|
| 1 | `icon_spell_arcana.magic-arrow` | a bolt that never misses |
| 2 | `icon_spell_arcana.slow` | slowed down |
| 3 | `icon_spell_arcana.haste` | sped up |
| 4 | `icon_spell_arcana.frost-bolt` | a bolt of ice |
| 5 | `icon_spell_arcana.dispel` | magic undone |
| 6 | `icon_spell_arcana.town-portal` | the way home |

## Death (Necropolis)

**Prompt** · 1536 × 1024 · negative `NEG-SPELL`

```text
Game spell icon sheet in a realistic painterly dark-fantasy style, like a detailed digital oil painting: each spell one strong image of
its effect at the moment it happens, a single central motif with believable form and real materials (fire, smoke, ice, stone, metal,
flesh), lit by its own magic in the school's colour so the glow falls naturally on the surfaces near it. Deep shadows, a full range of
values, fine detail and subtle colour variation instead of flat fills; edges made by light and shadow, not by drawn lines; front view
unless a cell says otherwise. Not a photograph, not a plastic CGI render, no pixel art. Death school: bone white and ash grey, a cold
sickly green glow, black smoke; still, patient and cold. 6 separate icons arranged in a grid of 3 columns and 2 rows, each alone in its
own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing crosses into
another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with alpha. The icons,
top row left to right, then bottom row: 1 a cracked sword blade, its edge notched and dull, wrapped in a thin coil of cold green smoke;
2 a skeletal hand loosely grasping a drooping arm of a living hand, pale green veins of cold spreading from the touch; 3 a thin stream
of red light flowing from a beating heart into an open bony hand that glows cold green; 4 a billowing cloud of green-grey poisonous fog
with a faint skull shape in its folds; 5 a skeletal arm bursting up out of dark earth, cold green light pouring from the grave around
it; 6 a black rat sitting on a pale bone, sickly green fumes rising from its fur
```

| # | File | Reads as |
|---|---|---|
| 1 | `icon_spell_death.curse` | a weakened blow |
| 2 | `icon_spell_death.weakness` | strength drained |
| 3 | `icon_spell_death.drain-life` | life stolen |
| 4 | `icon_spell_death.death-cloud` | a poison cloud |
| 5 | `icon_spell_death.raise-dead` | the slain rise to serve |
| 6 | `icon_spell_death.plague` | a plague that spreads |

## Chaos (Inferno)

**Prompt** · 1536 × 1024 · negative `NEG-SPELL`

```text
Game spell icon sheet in a realistic painterly dark-fantasy style, like a detailed digital oil painting: each spell one strong image of
its effect at the moment it happens, a single central motif with believable form and real materials (fire, smoke, ice, stone, metal,
flesh), lit by its own magic in the school's colour so the glow falls naturally on the surfaces near it. Deep shadows, a full range of
values, fine detail and subtle colour variation instead of flat fills; edges made by light and shadow, not by drawn lines; front view
unless a cell says otherwise. Not a photograph, not a plastic CGI render, no pixel art. Chaos school: ember red and molten orange fire
with black cores, yellow-white heat at the centre, charcoal smoke; violent and hungry. 6 separate icons arranged in a grid of 3 columns
and 2 rows, each alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same
light; nothing crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background,
PNG with alpha. The icons, top row left to right, then bottom row: 1 a round ball of fire with a white-hot core, bursting outward in a
ring of flame and sparks; 2 a horned helmet seen from the front, its eye slits glowing ember red, red mist rising from it; 3 a single
spear-shaped bolt of fire flying diagonally up to the right, a short trail of embers behind it; 4 a wide-open screaming mouth of a pale
mask, red light inside, cracks running across the mask; 5 a column of roaring fire twisting upward from a cracked black ground, a dark
smoke crown above it; 6 a wall of fire sweeping across the cell from top to bottom, a band of black ash in its wake
```

| # | File | Reads as |
|---|---|---|
| 1 | `icon_spell_chaos.fireball` | an area of fire |
| 2 | `icon_spell_chaos.bloodlust` | battle fury |
| 3 | `icon_spell_chaos.firebolt` | one bolt of fire |
| 4 | `icon_spell_chaos.terror` | fear and flight |
| 5 | `icon_spell_chaos.inferno` | a burning pillar |
| 6 | `icon_spell_chaos.hellfire` | a burning line across the field |

## Shadow (Dungeon)

**Prompt** · 1536 × 1024 · negative `NEG-SPELL`

```text
Game spell icon sheet in a realistic painterly dark-fantasy style, like a detailed digital oil painting: each spell one strong image of
its effect at the moment it happens, a single central motif with believable form and real materials (fire, smoke, ice, stone, metal,
flesh), lit by its own magic in the school's colour so the glow falls naturally on the surfaces near it. Deep shadows, a full range of
values, fine detail and subtle colour variation instead of flat fills; edges made by light and shadow, not by drawn lines; front view
unless a cell says otherwise. Not a photograph, not a plastic CGI render, no pixel art. Shadow school: deep violet and black, a thin
silver-violet rim light, curling dark smoke; quiet, sly and cold. 6 separate icons arranged in a grid of 3 columns and 2 rows, each
alone in its own equal square cell, centred with about 10 % empty margin, all at the same scale and with the same light; nothing
crosses into another cell. No grid lines, no borders, no frames, no text, no labels, no numbers. Transparent background, PNG with
alpha. The icons, top row left to right, then bottom row: 1 an open eye seen front-on, a band of black smoke drawn across it like a
blindfold, a violet glint on its edge; 2 three small violet sparks circling in tangled loops around an empty centre, the loops crossing
each other; 3 a dark footprint on the left and a matching footprint on the right, a curling ribbon of violet smoke joining them; 4 a
lowered war banner on a broken pole, its cloth hanging limp and torn, violet mist pooling at its foot; 5 a jagged black bolt with a
violet rim light striking diagonally down, a crack of violet light where it lands; 6 a narrow dark violet rift standing upright, its
edges lit silver-violet, a faint second rift far behind it
```

| # | File | Reads as |
|---|---|---|
| 1 | `icon_spell_shadow.blind` | cannot see |
| 2 | `icon_spell_shadow.confusion` | lost and confused |
| 3 | `icon_spell_shadow.shadow-step` | a step through shadow |
| 4 | `icon_spell_shadow.despair` | hope lost |
| 5 | `icon_spell_shadow.dark-bolt` | a bolt of darkness |
| 6 | `icon_spell_shadow.dimension-door` | a leap across the map |

## After generating

1. Put each school's six icons side by side at 32 px: each is named at a glance, and the school's colour carries the page.
2. Put two schools side by side: no icon of one could pass for the other's.
3. Check for a cartoon look (thick outlines, flat colour fills, bubbly shapes, plastic shine, doll-like faces) and regenerate such a
   sheet.
4. Put the files in `src/Godot/EternalWars.Godot.Assets/textures/ui/spells/` *(proposal)* and add a line to its `CREDITS.md` with the
   tool and its licence terms.
