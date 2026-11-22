# Town model prompts (for a 3D-generating model)

Prompts for the **adventure-map town models** of all ten town types, for a text-to-3D or image-to-3D model (or a concept-art pass
first, then image-to-3D). The designs are original: the prompts never name or imitate another game's towns.
Each town's look follows its key paintings in [faction-art.md](../faction-art.md) (ADR-284); the best start for image-to-3D is
the landmark cut out of that painting.

## Use a prompt

Each town section below ends with its own block, `TOWN-<id>`. A prompt is a lead block and the town's block, each copied exactly and
joined with a space:

```text
PROMPT   = TOWN-STYLE + TOWN-<id>          (the town)
         = TOWN-STAGE-<n> + TOWN-<id>      (a stage, from "Optional: stages by fort level"; TOWN-<id>-staged where there is one)
         = PIECE-<piece> + SIEGE-<id>      (a siege piece, from "Optional: siege pieces")
NEGATIVE = TOWN-NEG   (only where the tool takes a negative prompt)
OUTPUT   = town_<id>.glb (town_<id>_<n>.glb for a stage), to the footprint, size and triangle budget under "Output for every town";
           siege_<id>_<piece>.glb for a siege piece
```

The **Silhouette**, **Materials**, **Telling details** and **Avoid** notes in each section explain the block and are for checking the
result; they are not pasted. If a result shows something on the Avoid list, add those words to the negative prompt for that town. Do not
paraphrase the blocks.

## Blocks

**`TOWN-STYLE`** (style, first in every full-town prompt):

```text
Game-ready 3D model of a fortified fantasy town for a turn-based strategy game, seen from above at a steep angle. Grim dark fantasy,
realistic PBR materials, weathered and worn, desaturated earth tones. Compact, fits a square base with the corners cut off, main gate
centred on the front edge at ground level. Clear silhouette from above, chunky readable shapes, one tall landmark toward the back.
Flat bottom, no ground plane, no lights, no characters. Light-grey cloth banners for player colour.
```

**`TOWN-NEG`** (negative prompt, every town):

```text
photo of a real place, real-world landmark, modern buildings, text, logos, people, cartoon, toy, chibi, low-poly flat shading, pixel
art, bright saturated colours everywhere, floating debris, terrain around the base, thin spindly spires, cluttered tiny details,
another game's design.
```

## Output for every town

- **Footprint**: a town covers **7 × 7 tiles with the four corner tiles free** (ADR-046); 1 tile = 1 Godot unit (`MapSpace.TileSize`),
  so the model fits a 7 × 7 unit square with its corners cut, the blocked cells' shape below. The **entrance** is the bottom-middle tile:
  the gate stands there, facing the bottom of the map (toward the camera), at ground level, open and readable.
  ```
  .BBBBB.
  BBBBBBB
  BBBBBBB
  BBBBBBB
  BBBBBBB
  BBBBBBB
  .BBVBB.
  ```
- **Origin**: the centre of the footprint, on the ground; Y up; the bottom is flat at Y = 0 (the game levels the ground under the
  footprint to the entrance's height). No ground slab reaching past the footprint, no terrain skirt; a thin base of the town's own ground
  (paving, roots, slag, mud) inside the footprint is fine.
- **Height**: the tallest point at most **6 units** *(proposal)*, the bulk under 3.5, so the town does not hide heroes behind it. The
  camera looks down **15–30° from vertical** (ADR-022) and rotates ±45°: the **roofs and the top-down silhouette** carry the identity;
  walls seen from the front matter less. One tall landmark per town, off-centre toward the back.
- **Readability**: one unmistakable outline per town from above, readable when the town is 60 px wide on screen. Big shapes, few
  small parts; no thin spires under 0.08 units wide.
- **Player colour**: two to four **banners or flags** as a separate mesh named `team_flag`, with a plain light-grey albedo, so the game
  tints them in the owner's colour and lays the town's charge on them ([icon-prompts.md](icon-prompts.md#heraldry)). No other
  strongly saturated cloth.
- **Light**: the model has none of its own baked in; glowing parts (braziers, lava, runes, crystals) go in an **emissive** map, kept small:
  bloom is for magic only.
- **Output**: glTF binary, `town_<id>.glb` (`town_haven.glb`, `town_swamp-bestiary.glb`, ...); **20 000–40 000 triangles**
  *(proposal)*; PBR metal-roughness, one 2048 px texture set (albedo, normal, ORM, emissive), real-world scale as above; no animation
  (flags, smoke and fire are added by the game).

- **Look**: every town is **old, patched and scarred**, built on the ruins of earlier ages and repaired in a hurry, never pristine. Group
  colours sparingly on top of the earth tones: Dawn Covenant gold, white, green, sky blue; Ashen Court ash grey, ember red, violet, bone;
  Unbound raw wood, leather, bone, moss green; Firstborn stone, crystal, old bronze.

## Haven (`town_haven`)

The last lit cities of a crumbling human theocracy. Iron discipline, flagellant zeal, war machines; they burn heretics to "feed the
light". Native ground: highlands and grass.

- **Silhouette**: a castle on a rocky rise (the landmark): grey curtain walls, round towers with dark **conical slate roofs**, a tall
  keep with pennants and a white banner with the **gold sun**; below it a stone church with a bell tower and a mage tower crowned by a
  blue orb in a ring.
- **Materials**: grey stone, dark slate, terracotta tiles on the lesser towers, half-timbering and thatch, worn brass trim.
- **Telling details**: the gold sun over the church door and on the banners, a smithy's glowing forge and tall chimney, a stable yard
  with a paddock, an archery range with targets, a jetty with a crane (the shipyard).
- **Avoid**: a fairy-tale white castle; clean gold; blue-and-white heraldry of a real order.

**`TOWN-haven`** (subject):

```text
Haven: a human castle town on a rocky rise, grey stone curtain walls and round towers with dark conical slate roofs, a tall keep
flying pennants and a white banner with a gold sun, a stone church with a bell tower and a gold sun over its door, a slender mage
tower crowned by a blue orb inside a bronze ring, a thatched half-timbered tavern, a smithy with a glowing forge and a tall chimney, a
stable yard with a paddock, an archery range with targets. Grey stone, slate and timber; disciplined, devout, the last lit cities.
```

## Sylvan (`town_sylvan`)

Elves of the last old-growth forest, which is dying from the root. Archers and wardens trade their memories to keep the trees alive.
Native ground: forest floor and grass.

- **Silhouette**: no wall of stone; a ring of **living giant trees** grown into a palisade, halls on platforms in their crowns, one
  vast ancient tree at the back (the Elder Grove, the landmark) whose crown is half bare.
- **Materials**: grey-brown bark, pale carved wood, moss, woven living branches, green-gold leaves; a creeping **grey rot** climbing
  from the roots.
- **Telling details**: lanterns of pale green light and warm amber windows, archery galleries between trunks, a ring of carved
  standing stones, rope and plank bridges, a gate grown from arching roots, memory offerings (ribbons, carved tablets) tied to the
  roots, dead branches propped and braced with timber; blue-teal mist around it all.
- **Avoid**: candy-coloured fairy forest; mushrooms everywhere (those are Dungeon's); a lush healthy look.

**`TOWN-sylvan`** (subject):

```text
Sylvan: an elven forest town grown from a ring of colossal living trees whose trunks form the walls, graceful halls and archery
galleries on platforms among the branches, a vast ancient tree at the back with a half-bare dying crown, grey rot creeping up from
the roots, pale green lanterns, ribbons and carved memory tablets tied around the roots, a moonlit glade with standing stones,
braced and propped dead limbs. Beautiful and failing.
```

## Academy (`town_academy`)

Mage-cities on frozen peaks that study the Wheel itself; constructs and bound elementals, and rumours that they mean to *control* the
turning. Native ground: snow and ice.

- **Silhouette**: a clustered citadel on a rock spur, slender towers with **domes**, one domed **observatory** at the back holding a
  great bronze **orrery of nested rings** (a model of the Wheel; the landmark).
- **Materials**: blue-grey granite, frost and snow on every ledge, verdigris bronze, dark glass, pale sky-blue rune light (emissive).
- **Telling details**: chained stone golems standing as gate guards, a workshop annex with cranes and a half-built construct, an ice
  crust on the walls, steam vents, a hall of records with tall narrow windows, a white columned cloud temple (the Titans) and a domed
  altar of wishes holding a golden lamp (the Genies).
- **Avoid**: a desert or Arabian palace; bright sapphire everywhere; floating islands.

**`TOWN-academy`** (subject):

```text
Academy: a mage citadel on a frozen rock spur, blue-grey granite with snow on every ledge and icicles under the eaves, slender towers
with verdigris bronze domes, a large domed observatory at the back holding a giant bronze orrery of nested rotating rings, dark glass
windows with faint pale-blue rune light, two chained stone golems flanking the gate, a workshop yard with cranes and a half-assembled
construct, steam rising from vents. Cold, scholarly, ambitious.
```

## Necropolis (`town_necropolis`)

The dead who remember every earlier age and know the Wheel cannot be stopped, only ruled. Native ground: cursed lands.

- **Silhouette**: a dark keep and gothic manors among **ruins of many ages**, with a tall straight black **obelisk spire** (the
  Necromancy Amplifier, the landmark) with a cold **cyan** light at its tip.
- **Materials**: grey-black basalt, bleached bone set into walls, rusted iron, crumbling older stone below newer; ash on everything.
- **Telling details**: a graveyard behind an iron fence with an open grave, a mausoleum and a crypt mound, a vaulted transformer
  building with a stone vat, grey tattered banners, the bones of a great dragon, gaunt dead trees, cyan fire in braziers on
  skull-carved gate posts, a faint cyan glow (emissive) in a few windows; everything near black to ash grey.
- **Avoid**: cartoon skulls, skulls on every surface; purple-and-green Halloween colours; green glow; slime.

**`TOWN-necropolis`** (subject):

```text
Necropolis: an undead town among the ruins of many fallen ages, a dark castle keep of grey-black basalt with a skull over its gate,
a tall straight black obelisk spire with a cold cyan light at its tip, gothic manors and a bell tower, a graveyard behind a rusted
iron fence with an open grave, a mausoleum and a vaulted crypt with a great stone vat, the bones of a great dragon, dead trees, ash
covering everything, cyan fire in braziers at the gate and faint cyan light in a few windows. Near black to ash grey. Ancient,
patient, inevitable.
```

## Inferno (`town_inferno`)

Demons of the ash beneath the world, born from each burned age; they see every war as harvest. Native ground: lava.

- **Silhouette**: a jagged fortress grown out of **cooled slag and fused rubble of burned cities**, curved **sickle-shaped** spires
  (harvest), one great altar-furnace at the back (the Altar of Brimstone, the landmark) venting smoke.
- **Materials**: black obsidian and slag, glowing **lava cracks** and channels (emissive, ember red), blackened bronze, charred
  timber and half-melted human masonry caught in the walls.
- **Telling details**: birthing pools of molten rock, a chained pit glowing from below, a foundry with chimneys and an ore cart, a moat
  of lava at the gate, brimstone-yellow crust around vents. Fire is natural orange; lava glows red to infrared.
- **Avoid**: a cartoon hell with pitchforks and flames on every surface; bright orange or red everywhere (keep the glow to the altar,
  forges, cracks and pools).

**`TOWN-inferno`** (subject):

```text
Inferno: a demonic fortress risen from cooled slag and fused rubble of burned human cities, black obsidian and charred stone with
half-melted towers and arches caught inside the walls, curved sickle-shaped spires, glowing ember-red lava cracks and channels, a
narrow lava moat before the gate, birthing pools of molten rock inside, iron cages hanging from chains, a great altar-furnace at the
back venting black smoke, yellow brimstone crust around the vents. A place that harvests the dead world.
```

## Dungeon (`town_dungeon`)

Dark elves and the lightless things they serve, untouched by the dying sun and ready to inherit. Native ground: subterranean.

- **Silhouette**: a town in an **enclosed cavern** (no sky, stalactites overhead): a gothic black citadel of many spires, slender
  towers, a columned temple, and a tall spire of **violet crystal** (the Mana Spring, the landmark).
- **Materials**: black and dark-violet stone, polished and angular, slick wet rock, pale fungus, **violet bioluminescence** (emissive),
  silver inlay.
- **Telling details**: clusters of glowing mushrooms in violet and cyan, the walls of a labyrinth in the open with horned skulls over
  its gate, a lair carved as a huge beast's maw, cave dwellings lit from within, warm orange braziers, spiked gothic gates.
- **Avoid**: sky; water; green goblin caves; red lava (that is Inferno); a bright purple glow over everything.

**`TOWN-dungeon`** (subject):

```text
Dungeon: a dark-elf town in an enclosed cavern under hanging stalactites, a gothic citadel of black and dark-violet stone with many
sharp spires and violet-lit windows, a tall spire of glowing violet crystal, a columned temple with a carved serpent on its roof, open
labyrinth walls with horned skulls over the gate, a lair carved as a giant beast's maw, clusters of glowing violet and cyan mushrooms,
warm orange braziers, polished stone with thin silver inlay, slick wet rock. Cold, elegant, patient, lightless.
```

## Stronghold (`town_stronghold`)

Orc clans who have survived several turnings by **never building anything worth burning**. Native ground: rough and wasteland.

- **Silhouette**: a **camp, not a castle**: a log palisade with battlements, hide yurts and long tents, a huge beast skull on a pole
  frame over the main lodge (the landmark), a fighting ring in the open.
- **Materials**: raw split logs, rope, hide and leather, bone, rusted scrap iron, dust; red-ochre paint on hides.
- **Telling details**: the Proving Ring (a dirt arena of stakes), war drums on a raised platform (Drums of the Horde), a mess hall
  with smoke holes, carts and sledges ready to move, scavenged ruin stones used as anvils.
- **Avoid**: stone keeps; neat carpentry; tribal stereotypes of real peoples (no real-world motifs, no war bonnets, no totem poles of a
  real culture).

**`TOWN-stronghold`** (subject):

```text
Stronghold: a fortified orc war camp that could be packed up and moved, a log palisade with crude battlements, hide yurts and long
leather tents with smoke holes, a great lodge under a huge horned beast skull on a pole frame, a dirt fighting arena ringed by stakes,
giant war drums on a raised platform, carts and sledges by the gate, rope, rusted scrap iron, bones, red-ochre paint, dust. Built
to survive, never to last.
```

## Swamp Bestiary (`town_swamp-bestiary`)

Beast-cults of the drowned lowlands, where ruins of an old age sink into the mire. Native ground: swamp and shallow water.

- **Silhouette**: half-sunk ruins of an older stone city, tilted and broken, with **stilt huts** and reed lodges built on and between
  them, a tall carved **totem of coiled beasts** at the back (Elder Totem, the landmark).
- **Materials**: algae-green stone, black water (a few shallow pools inside the footprint), reeds, rope, giant shells and scales,
  driftwood, bone; moss green and muddy brown.
- **Telling details**: the Shell Wall (a rampart of giant spiral shells, tusks and bones), spawning pools with eggs glowing cyan, a
  sunken dome glowing cyan inside (the hydra lair), a broken tower crowned by a great nest, short plank walks over channels, a toppled
  statue's head sinking in the moss, dark pines around it.
- **Avoid**: jungle temple of a real culture; bright tropical colours; toxic green glow.

**`TOWN-swamp-bestiary`** (subject):

```text
Swamp Bestiary: a beast-cult town in a drowned marsh, half-sunken tilted ruins of an ancient stone city green with algae, stilt huts
and reed lodges built on and between the ruins, boardwalks over black shallow water, a rampart of giant shells, scales and bones
instead of a wall, spawning pools with clusters of eggs, a tall carved totem of coiled beasts at the back, a toppled statue head
sinking in the mud, driftwood, rope, moss and mist-grey reeds.
```

## Conflux (`town_conflux`, not playable)

Elemental wardens of the Wheel's mechanism; neutral guardians. Campaign and neutral only.

- **Silhouette**: a sanctuary around the **Altar of Thought** (the landmark): a stepped round platform ringed by pillars, a glowing
  violet orb above it with stones floating around it; a weathered castle with green domes, a tall tiered mage tower, four domed
  shrines, each touched by one element; domes as on its siege field (ADR-144).
- **Materials**: ancient weathered stone, old green bronze, floating crystal shards close to the stone (not far off), a soft shimmer
  (emissive) in the crystals only.
- **Telling details**: one shrine iced with a blue crystal, one scorched with an orange crystal, one wind-swept with a yellow crystal,
  one overgrown with a green crystal; violet crystals on the castle's towers.
- **Avoid**: a gear or cog.

**`TOWN-conflux`** (subject):

```text
Conflux: an ancient elemental sanctuary of weathered stone and old green bronze around a stepped round altar ringed by pillars, a
glowing violet orb hovering above it with stones floating around it, a weathered castle with green domes and violet crystals on its
towers, a tall tiered mage tower, four small domed shrines, one frosted with a blue crystal, one scorched black with an orange
crystal, one wind-swept with a yellow crystal, one overgrown with roots and a green crystal. Older than any kingdom, silent,
watchful.
```

## Dragon Utopia (`town_dragon-utopia`, not playable)

The last dragons, who remember the world *before* the Wheel. Campaign and neutral only.

- **Silhouette**: a **terraced eyrie** (as on its siege field, ADR-144) cut into a dark granite crag, huge cave mouths and perches,
  crowned by a natural outcrop shaped like a dragon's head (the landmark); a broken colossal dragon statue on a far pinnacle.
- **Materials**: dark grey and blue-grey granite with rusty ochre stains and pale lichen, old green-tarnished bronze, gold hoard
  glints behind half-open vault doors, giant claw marks in the rock.
- **Telling details**: scorched ledges, enormous dragon ribs used as a gate arch, the keepers' small stone village; no fires.
- **Avoid**: red sandstone, a sun emblem, temples of a real culture.

**`TOWN-dragon-utopia`** (subject):

```text
Dragon Utopia: an ancient dragon eyrie cut into a dark granite crag in broad terraces, huge cave mouths and ledges scorched black,
a natural rock outcrop at the top shaped like a dragon's head, dark grey granite with rusty ochre stains and old green bronze, deep
claw marks in the rock, a gate arch of two enormous dragon ribs, glints of a gold hoard behind half-open bronze vault doors, a broken
colossal dragon statue on a pinnacle at the back.
```

## Optional: stages by fort level

If the map is to show a town's growth (not decided ❓), make three variants from the same base: **Fort** (`town_<id>_1`), **Citadel**
(`_2`), **Castle** (`_3`), same footprint and gate. Use the stage's lead instead of `TOWN-STYLE`.

**`TOWN-STAGE-1`** (style and stage, first in every Fort prompt):

```text
Game-ready 3D model of a fortified fantasy town for a turn-based strategy game, seen from above at a steep angle. Grim dark fantasy,
realistic PBR materials, weathered and worn, desaturated earth tones. Compact, fits a square base with the corners cut off, main gate
centred on the front edge at ground level. Clear silhouette from above, chunky readable shapes, one tall landmark toward the back. Flat
bottom, no ground plane, no lights, no characters. Light-grey cloth banners for player colour. Early stage: a low wall, few buildings,
the landmark not built yet, open ground inside.
```

**`TOWN-STAGE-2`** (style and stage, first in every Citadel prompt):

```text
Game-ready 3D model of a fortified fantasy town for a turn-based strategy game, seen from above at a steep angle. Grim dark fantasy,
realistic PBR materials, weathered and worn, desaturated earth tones. Compact, fits a square base with the corners cut off, main gate
centred on the front edge at ground level. Clear silhouette from above, chunky readable shapes, one tall landmark toward the back. Flat
bottom, no ground plane, no lights, no characters. Light-grey cloth banners for player colour. Middle stage: a full wall, a central
keep, the landmark half built under scaffolding.
```

**`TOWN-STAGE-3`** (style and stage, first in every Castle prompt):

```text
Game-ready 3D model of a fortified fantasy town for a turn-based strategy game, seen from above at a steep angle. Grim dark fantasy,
realistic PBR materials, weathered and worn, desaturated earth tones. Compact, fits a square base with the corners cut off, main gate
centred on the front edge at ground level. Clear silhouette from above, chunky readable shapes, one tall landmark toward the back. Flat
bottom, no ground plane, no lights, no characters. Light-grey cloth banners for player colour. Final stage: high walls with towers,
dense buildings, the landmark complete.
```

Stronghold and Swamp Bestiary grow outward and taller, not into stone: their stages take `TOWN-<id>-staged` instead of
`TOWN-<id>`.

**`TOWN-stronghold-staged`** (subject, stages only):

```text
Stronghold: a fortified orc war camp that could be packed up and moved, a log palisade with crude battlements, hide yurts and long
leather tents with smoke holes, a great lodge under a huge horned beast skull on a pole frame, a dirt fighting arena ringed by stakes,
giant war drums on a raised platform, carts and sledges by the gate, rope, rusted scrap iron, bones, red-ochre paint, dust. Built to
survive, never to last. Grows outward and taller, not into stone: more lodges and a higher palisade.
```

**`TOWN-swamp-bestiary-staged`** (subject, stages only):

```text
Swamp Bestiary: a beast-cult town in a drowned marsh, half-sunken tilted ruins of an ancient stone city green with algae, stilt huts
and reed lodges built on and between the ruins, boardwalks over black shallow water, a rampart of giant shells, scales and bones
instead of a wall, spawning pools with clusters of eggs, a tall carved totem of coiled beasts at the back, a toppled statue head
sinking in the mud, driftwood, rope, moss and mist-grey reeds. Grows outward and taller, not into stone: more stilt huts and a higher
shell wall.
```

## Optional: siege pieces

The siege field uses placeholder shapes in each town's look (ADR-144) until there are models. For a modular kit in the same style, a
prompt is the piece's block and the town's material line (each piece on its own, flat bottom; a battle cell is 0.5 units,
`BattleSpace.CellSize`). Name them `siege_<id>_<piece>.glb`.

| Piece | Block | Text |
|---|---|---|
| `wall` | `PIECE-wall` | `A straight wall segment, 1 unit long.` |
| `tower` | `PIECE-tower` | `A wall corner tower.` |
| `gate` | `PIECE-gate` | `The main gate with its doors closed.` |
| `keep` | `PIECE-keep` | `The central keep.` |
| `breach` | `PIECE-breach` | `A broken wall segment, rubble.` |

| Town | Block | Text (the town's **Materials** line) |
|---|---|---|
| Haven | `SIEGE-haven` | `Pale limestone gone grey with soot, black iron, dark slate, gilded trim worn to brass; whitewash peeling.` |
| Sylvan | `SIEGE-sylvan` | `Grey-brown bark, pale carved wood, moss, woven living branches, green-gold leaves; a creeping grey rot climbing from the roots.` |
| Academy | `SIEGE-academy` | `Blue-grey granite, frost and snow on every ledge, verdigris bronze, dark glass, pale sky-blue rune light (emissive).` |
| Necropolis | `SIEGE-necropolis` | `Grey-black basalt, bleached bone set into walls, rusted iron, crumbling older stone below newer; ash on everything.` |
| Inferno | `SIEGE-inferno` | `Black obsidian and slag, glowing lava cracks and channels (emissive, ember red), blackened bronze, charred timber and half-melted human masonry caught in the walls.` |
| Dungeon | `SIEGE-dungeon` | `Black and dark-violet stone, polished and angular, slick wet rock, pale fungus, violet bioluminescence (emissive), silver inlay.` |
| Stronghold | `SIEGE-stronghold` | `Raw split logs, rope, hide and leather, bone, rusted scrap iron, dust; red-ochre paint on hides.` |
| Swamp Bestiary | `SIEGE-swamp-bestiary` | `Algae-green stone, black water (a few shallow pools inside the footprint), reeds, rope, giant shells and scales, driftwood, bone; moss green and muddy brown.` |
| Conflux | `SIEGE-conflux` | `Ancient weathered stone, old green bronze, floating crystal shards close to the stone (not far off), a soft shimmer (emissive) in the crystals only.` |
| Dragon Utopia | `SIEGE-dragon-utopia` | `Dark weathered granite, old bronze, gold hoard glints in the openings, giant claw marks in the rock.` |

## After generating

1. Look at the model from above at the game's angle (15–30° from vertical) at 60 px wide: the outline must name the town.
2. Check the footprint (7 × 7 units, corners free), the gate on the bottom-middle tile, the height limit and the triangle budget.
3. Check that `team_flag` is a separate light-grey mesh and that only braziers, lava, runes and crystals glow.
4. Put the file in `src/Godot/EternalWars.Godot.Assets/models/towns/` *(proposal)* and add a line to its `CREDITS.md` with the tool and its
   licence terms.
