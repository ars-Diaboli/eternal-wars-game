# Faction art direction

The owner's key paintings, one or more per town (ADR-284), set how each faction looks. Every faction asset follows them: the 3D town
scene and its models, the town models on the adventure map, the town-screen backgrounds, the creature and hero backdrops, the puzzle
pictures and the faction's corner of the UI. Where a prompt file or a model still disagrees with a painting, the painting is right and
the prompt is fixed. Where a town has more than one painting, they are variations of the same place: the first is the reference, the
others show the room the look allows.

The paintings are in [art/towns/](art/towns/), saved as `key_<town>[-n].jpg` (about 1670 × 940; Stronghold's two, Swamp's second and
Dragon Utopia's 1774 × 887).

## What all the paintings share

- **Painterly realism**: a concept painting, natural light and believable materials. Not a cartoon, a board-game diorama, a toy or an
  HDR render (ADR-248).
- **One camera**: a wide side view from a little above eye level, one horizon, sizes falling off with distance. The foreground is a low,
  calm band of ground (a path, rocks, roots, a gate in the middle distance at most); the town stands in the middle distance and the
  background.
- **A HoMM town screen's layout**: about seven to nine separate buildings, each readable on its own, linked by paths. The castle or
  keep stands back and to one side (most often the right), the landmark near the middle or the other side, never in a symmetric row.
- **Built by hand, worn by time**: finished surfaces against raw material (carved stone against cliff, cut timber against bark, slag
  against masonry), weathering, moss, soot and human-scale details (doors, steps, carts, fences, smoke from a chimney).
- **Light carries the mood**: a warm light against cool shadow, one bright focal point (the landmark), depth from haze in the
  distance. Magic glows only in small accents (a crystal, a window, a brazier), never over everything.
- **Each faction its own ground**: the terrain around the town is the faction's native ground, so the town reads before any building
  does.

## Group colours

The [faction groups](../design/factions.md) keep the colours already given to their icons and heraldry
([icon-prompts.md](prompts/icon-prompts.md)), and the paintings fill them in:

| Group | Towns | Colours in the paintings |
|---|---|---|
| Dawn Covenant | Haven, Sylvan, Academy | Daylight: sky blue, white and grey stone, green, gold trim; Sylvan's misty blue-teal with amber windows |
| Ashen Court | Necropolis, Inferno, Dungeon | Near black and ash grey; each with one accent glow: Necropolis cyan, Inferno orange fire and red lava, Dungeon violet |
| Unbound | Stronghold, Swamp Bestiary | Raw wood, hide, bone and dust under warm low sun; Swamp's dark pine green with cyan accents |
| Firstborn | Conflux, Dragon Utopia | Ancient stone and old bronze; Conflux's crystals in the four elements' colours, Dragon Utopia's dark granite |

## The towns

### Haven

![Haven, sunlit](art/towns/key_haven-1.jpg)
![Haven, overcast](art/towns/key_haven-2.jpg)

- **Setting**: a lakeside town on green grass below snowy mountains and pine forest; a HoMM human town in plain daylight.
- **Light**: a clear blue-sky day (1) or a grey overcast day with low cloud (2). Natural, never golden-hour kitsch.
- **Landmark**: the castle on a rocky rise at the back right: grey stone curtain walls, round towers with dark conical slate roofs,
  pennants and a white banner with the gold sun.
- **Buildings**: a stone church with a bell tower and a gold sun over its door; the mage guild as a tall tower crowned by a blue orb in
  a ring; a thatched half-timbered tavern; a smithy with a glowing forge and a tall chimney; stables with a paddock; an archery range
  with targets; a harbour with a crane and a sailing boat (the shipyard).
- **Colours**: grey stone, dark slate, terracotta roofs on the lesser towers, timber and thatch, green grass; gold sun and blue orb as
  the accents.
- **Avoid**: a fairy-tale white castle, clean gold, crowded streets.

### Sylvan

![Sylvan, gorge](art/towns/key_sylvan-1.jpg)
![Sylvan, gorge, warmer](art/towns/key_sylvan-2.jpg)
![Sylvan, glade](art/towns/key_sylvan-3.jpg)

- **Setting**: a deep misty gorge in an ancient forest with waterfalls, the main-menu background's atmosphere (1, 2); a forest glade
  under a dragon-shaped cliff is the third take.
- **Light**: cool blue-teal haze, light shafts from the canopy, warm amber windows; the forest edges dark and framing the scene.
- **Landmark**: the Elder Grove, a vast ancient tree with a half-bare crown, propped by timber braces and hung with pale banners.
- **Buildings**: a tall tree tower of stacked balconies; a long carved hall with round shields (the archers); a great hall grown into
  living trees at the back right; a gallery hall on a cliff ledge; a white gazebo in a hedged garden; a ring of carved standing stones;
  rope and plank suspension bridges; a gate grown from arching roots.
- **Colours**: dark moss green, grey-brown bark, blue-teal mist, pale carved wood, amber light, pale green lanterns.
- **Creatures**: leaves, moss and vines only as patterns on clothing and armour, never growing on bodies, fur or weapons.
  The timber wolf and the dire wolf are Sylvan creatures by lore, drawn in its style, though not hired in the
  town (neutral in the game).
- **Avoid**: candy fairy forest, mushrooms (Dungeon's), a lush healthy look.

### Academy

![Academy, cloud temple](art/towns/key_academy-1.jpg)
![Academy, bright](art/towns/key_academy-2.jpg)

- **Setting**: a snowy mountain valley by a frozen lake, pine trees and frozen waterfalls.
- **Light**: crisp winter day, from dramatic cloud and sun (1) to a bright clear sky (2).
- **Landmark**: the castle on a rock spur at the back right, verdigris domes and blue banners with a gold star, with the
  **observatory** (a great bronze ring orrery) on its main tower.
- **Buildings**: a white columned **cloud temple** on its own peak above the clouds (the Titans); a domed pavilion holding a golden lamp
  with a blue wisp rising from it, the **altar of wishes** (the Genies); a tall tower with blue-lit windows (the mage guild); a library
  with tall windows; a golem workshop with a crane, scaffold and a half-built golem; a forge; a gate guarded by two chained stone golems.
- **Colours**: snow white, blue-grey stone, verdigris green-blue domes, bronze and gold, blue rune light.
- **Creatures**: royal blue, bright steel, gold and ice-blue light; no verdigris, green or teal (that stays on the buildings).
  The thanes, the efreet and the golden golem are Academy creatures by lore, drawn in its style, though not hired in the town
  (neutral in the game).
- **Avoid**: a desert or Arabian palace, floating islands.

### Necropolis

![Necropolis, graveyard](art/towns/key_necropolis-1.jpg)
![Necropolis, plain](art/towns/key_necropolis-2.jpg)

- **Setting**: a grey dead plain under heavy dark cloud, gaunt bare trees, far ruins on the horizon.
- **Light**: a grey dusk with a faint purple-red band at the horizon; everything in near black to grey. **Cyan** is the only colour of
  magic: braziers, the spire's tip, a few windows.
- **Landmark**: the Necromancy Amplifier, a tall straight black obelisk spire with a cyan light at its tip, in the middle.
- **Buildings**: a dark castle keep at the back right with a skull over the gate and grey banners; a gothic manor with lit windows (the
  vampires); a bell tower; a mausoleum with statues by its door; a crypt mound with a skull on its gable; a graveyard ringed by an iron
  fence with an open grave; the bones of a great dragon lying on the plain (the bone dragons); a wall and an iron gate in front with
  cyan braziers on skull-carved posts.
- **Colours**: black, charcoal, ash grey, bone, rusted iron; cyan accents only.
- **Creatures**: the specter and the banshee are Necropolis creatures by lore, drawn in its style, though not hired in the town
  (neutral in the game).
- **Avoid**: cartoon skulls, green-and-purple Halloween colours, slime, skulls on every surface.

### Inferno

![Inferno, foundry](art/towns/key_inferno-1.jpg)
![Inferno, plain](art/towns/key_inferno-2.jpg)

- **Setting**: a field of black volcanic ash and obsidian under a smoke-black sky lit red from below, jagged far mountains.
- **Light**: the fire is natural **orange** (the altar, forges, braziers); the lava glows **red to infrared** in cracks, channels and
  pools, never over whole surfaces.
- **Landmark**: the Altar of Brimstone, a stepped furnace mound in the middle, belching flame and black smoke, crusted yellow with
  brimstone.
- **Buildings**: the citadel at the back right with curved sickle spires; a black tower with red-lit windows; a foundry with three
  chimneys, an open forge door and an ore cart; birthing pools of molten rock in carved basins; a chained pit glowing from below; a
  kennel yard with an iron fence; a gate with bronze doors in a wall of black stone.
- **Colours**: ash black, obsidian, charcoal, blackened bronze, dark red banners; orange fire and red lava as the accents.
- **Creatures**: the gog, magog, breeder and fel breeder are Inferno creatures by lore, drawn in its style, though not hired
  in the town (neutral in the game).
- **Avoid**: cartoon hell, flames on every surface, a pink or uniformly red scene.

### Dungeon

![Dungeon, cavern](art/towns/key_dungeon-1.jpg)
![Dungeon, cavern, violet fire](art/towns/key_dungeon-2.jpg)

- **Setting**: a vast enclosed cavern: no sky, stalactites hanging from the black ceiling, rock floor, no open water.
- **Light**: true blacks; light only from local sources: the violet crystal, violet glow in windows, warm orange torches and braziers
  (violet flames in the second take), bioluminescent mushrooms in violet and cyan.
- **Landmark**: the Mana Spring, a tall crystal spire glowing violet in the middle.
- **Buildings**: a gothic black citadel with many spires on a ledge at the back right; a slender tower of spires; a columned temple with
  a carved serpent on its roof and statues by its steps; an open labyrinth with horned skulls over its gate (the minotaurs); a lair
  carved into the rock as a huge beast's maw with bones before it; cave dwellings lit from within in the cavern wall; a spiked gothic
  gate in front.
- **Colours**: black, dark violet-grey stone, silver; violet glow, orange flame and a little cyan as accents.
- **Avoid**: sky, a sinkhole, water, green goblin caves, red lava (Inferno's), purple over everything.

### Stronghold

![Stronghold, warm](art/towns/key_stronghold-1.jpg)
![Stronghold, dusk](art/towns/key_stronghold-2.jpg)

- **Setting**: ochre badlands: mesas, canyon walls and dusty scrub, with broken columns and arches of an older age scattered about.
- **Light**: low warm sun with long shadows, from late afternoon (1) to a cooler dusk with cloud (2).
- **Landmark**: the great lodge on a rise at the back right, behind its own palisade, under a huge horned beast skull on a pole frame.
- **Buildings**: a log gate with two watchtowers hung with hides; hide yurts and tents; the Proving Ring, a fighting arena of stakes; a
  platform of great war drums with a red banner; a forge in a cave under the ruins; a tall nest tower of timber on a rock (the roc
  roost); a stockade pen; carts, barrels and smoke from the tents.
- **Colours**: ochre, dust, raw wood, tan hide, bone, rusted iron, red-ochre banners.
- **Avoid**: stone keeps, neat carpentry, motifs of real tribal cultures.

### Swamp Bestiary

![Swamp Bestiary, pine forest](art/towns/key_swamp-bestiary-1.jpg)
![Swamp Bestiary, open marsh](art/towns/key_swamp-bestiary-2.jpg)

- **Setting**: half-sunk ruins of an ancient stone city in a deep, dark pine forest, on mossy islands and narrow channels (1); an open
  marsh at sunset is the second take, which shows too much water to be the reference.
- **Light**: split: the left side and forest edges deep green to near black with **cyan** accents (glowing eggs, lanterns, the hydra
  lair), the right side caught by warm gold light through the canopy.
- **Landmark**: the Elder Totem, a towering carving of coiled serpents and toad-like beasts in the middle.
- **Buildings**: the stronghold at the back right on a ruined colonnade behind the Shell Wall of giant spiral shells, tusks and scales;
  a sunken overgrown dome glowing cyan inside (the hydra lair); a broken tower crowned by a huge nest (the wyverns); reed-thatched stilt
  huts with plank walks; spawning pools of glowing eggs; a toppled statue head sunk in moss.
- **Colours**: dark pine green, near-black green, pale algae-stained stone, wet wood, straw thatch, bone and pearly shell; cyan and warm
  gold as the accents.
- **Avoid**: a wide lake or a long boardwalk filling the foreground, jungle temples of a real culture, tropical colours, toxic green.

### Conflux (not playable)

![Conflux](art/towns/key_conflux.jpg)

- **Setting**: an ancient high plateau among snowy mountains, ruined walls and stones scattered across the grass.
- **Light**: a bright clear day, soft and calm.
- **Landmark**: the **Altar of Thought** in the middle: a stepped round platform ringed by pillars, a glowing violet orb above it with
  stones floating around it.
- **Buildings**: a weathered castle at the back right with green domes and violet crystals on its towers; a tall tiered tower with a
  ring and crystal at its top (the mage guild); four domed shrines, each its element's: ice and snow with a blue crystal, scorched rock
  and lava with an orange crystal, roots and vines with a green crystal, a wind shrine with streaming cloth and a yellow crystal; an
  arched gate with a bronze emblem in front.
- **Colours**: weathered pale stone, old green bronze; crystals in the four elements' colours and violet for thought.
- **Avoid**: a gear or cog (the Wheel stays a metaphor here).

### Dragon Utopia (not playable)

![Dragon Utopia](art/towns/key_dragon-utopia.jpg)

- **Setting**: a high crag of dark granite over a misty valley, sparse dry grass and wind-bent trees; the dragons are away.
- **Light**: plain late-afternoon daylight, no fires; the only warm glow is the hoard's gold behind bronze doors.
- **Landmark**: the eyrie itself, terraces and huge cave mouths cut into the granite, crowned by a natural outcrop shaped like a
  dragon's head; a broken colossal dragon statue on a pinnacle across the gorge.
- **Buildings**: a gate of two great dragon ribs; vault doors of green bronze over the hoard; the keepers' small stone village with a
  watchtower; a spring and pool in a sheltered hollow; a fallen dragon's spine in the foreground.
- **Colours**: dark grey and blue-grey granite with rusty ochre stains and pale lichen, bleached bone, green-tarnished bronze, a little
  gold.
- **Ground**: Dragon Utopia's native rocky ground is the same dark speckled granite ([terrain-texture-prompts.md](prompts/terrain-texture-prompts.md)).
- **Avoid**: red sandstone, a sun emblem, dragons in the sky, temples of a real culture.

## Painting more

New faction paintings (a building sheet, another season, the town scene's sky) follow the same recipe:

1. **Camera first**: "Fantasy strategy game town screen. A single wide view from one camera: eye level slightly above the ground, one
   consistent horizon, all structures on one continuous ground level that recedes into the distance, getting smaller with distance.
   Painterly realism, natural and atmospheric, like a concept painting. Solid, believable architecture with human-scale details."
2. **Setting and construction**: the faction's ground, and its finished surfaces against raw material.
3. **Seven or eight structures**, each with its place and distance (left background, middle distance, ...). Every listed thing gets
   drawn, so the list stays short.
4. **A low, calm foreground** with no large structures.
5. **Light and colour**: one focal point, warm against cool, the accent colours named and limited.
6. **A negative**: inconsistent perspective, multiple viewpoints, distorted scale, fisheye, isometric, large foreground structures,
   monotone, uniform materials, cartoon, stylized, board game, miniature, diorama, toy-like, bright saturated, HDR, oversharpened,
   hyperdetailed, symmetric composition, aerial view, bird's eye view, text, watermark, UI, frame; plus the faction's own "avoid" list.

Avoid the words "epic", "cinematic", "highly detailed" and "volumetric": they push the model toward slop. The faction lore and materials
are in [town-model-prompts.md](prompts/town-model-prompts.md); the buildings each town has are in [town-buildings.md](town-buildings.md).
