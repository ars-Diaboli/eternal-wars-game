# UI element prompts (for an image-generating model)

Prompts for the **painted pictures behind the interface**: the screen backgrounds. The interface's own pieces (window and panel frames,
buttons, slots, bars, tabs, check boxes, scroll bars, sliders and the adventure HUD's frames) are drawn in code in the adventure map's
look (ADR-310, ADR-311): a near-black face, a thin silver-grey bevelled rim and 6 px corners, the player's or the faction's colour on the
rim where it means something. They need no art. The icons and the cursors are pale satin aluminium ([icon-prompts.md](icon-prompts.md),
[cursor-prompts.md](cursor-prompts.md), ADR-312); the main menu's picture already exists.

## Use a prompt

Every prompt below is **one ready block**: copy it whole into the model, as it is. Where the model takes a negative prompt, give it
`UI-NEG`. Generate at the size given and save as the file named above the prompt. The prompts share their opening words on purpose: keep
them as they are so the backgrounds match.

## Negative prompt

**`UI-NEG`** (every background):

```text
text, letters, numbers, icons, logos, interface elements, frames, borders, characters in the foreground, bright saturated colours,
neon glow, photo, 3D render, pixel art, another game's interface
```

## Output for every background

- **Size**: 2560 × 1440 (16 : 9), generated at the model's largest landscape size and upscaled; JPEG at quality 90. The game crops
  them to fit, never stretches them, and darkens them a little.
- **Calm**: the panels over them are near-black with silver-grey rims, so the middle and the lower half stay dark and quiet.
- **Names**: `bg_<id>.jpg` as in the table.

## Screen backgrounds

**Use**: the file's prompt · negative `UI-NEG` · 2560 × 1440

| File | Screen |
|---|---|
| `bg_hero` | the hero screen |
| `bg_kingdom` | the kingdom overview |
| `bg_tavern` | the tavern |
| `bg_marketplace` | the marketplace and trade windows |
| `bg_loading` | loading a game |
| `bg_victory` | the victory screen |
| `bg_defeat` | the defeat screen |
| `bg_battle-result` | a battle's result window |
| `bg_town_haven` | Haven's town screen |
| `bg_town_sylvan` | Sylvan's town screen |
| `bg_town_academy` | Academy's town screen |
| `bg_town_necropolis` | Necropolis' town screen |
| `bg_town_inferno` | Inferno's town screen |
| `bg_town_dungeon` | Dungeon's town screen |
| `bg_town_stronghold` | Stronghold's town screen |
| `bg_town_swamp-bestiary` | Swamp Bestiary's town screen |

Each town screen's background leaves the **lower two thirds calm**: the game places the buildings, the garrison and the panels over it.

**`bg_hero`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A war tent's interior at night, a worn map table, an armour stand and a
weapon rack in the shadows, lantern light.
```

**`bg_kingdom`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A council chamber, a great table with a painted map of the realm,
banners hanging in the dark.
```

**`bg_tavern`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A smoky tavern's common room, long tables, a fire in a stone hearth,
empty benches.
```

**`bg_marketplace`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A covered market at dusk, crates, sacks and scales, lanterns on poles.
```

**`bg_loading`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A road winding toward a distant ruined city under a dim sun, banners of
many factions on the roadside.
```

**`bg_victory`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A battlefield at dawn, a lone banner standing on a hill, the sun
breaking through clouds.
```

**`bg_defeat`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A battlefield at dusk, a broken banner in the mud, smoke drifting,
ravens in the sky.
```

**`bg_battle-result`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A quiet field after a battle, scattered shields and spears, low mist.
```

**`bg_town_haven`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A lakeside town under snowy mountains, a grey castle with conical slate
roofs on a rocky rise, a church with a gold sun.
```

**`bg_town_sylvan`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A forest town in a misty gorge among colossal living trees, a vast
half-bare ancient tree, blue-teal haze, amber windows.
```

**`bg_town_academy`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A mage citadel on a frozen peak, verdigris domes, a great observatory
with bronze rings.
```

**`bg_town_necropolis`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A dark keep and gothic manors on a grey dead plain under heavy cloud, a
straight black obelisk spire with a cyan light.
```

**`bg_town_inferno`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A fortress of black slag and fused rubble over rivers of lava,
sickle-shaped spires, red smoke.
```

**`bg_town_dungeon`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. A town in a vast enclosed cavern under stalactites, a gothic black
citadel, a violet crystal spire, glowing mushrooms.
```

**`bg_town_stronghold`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. An orc war camp on ochre badlands, log palisade, hide tents, a great
beast skull over a lodge.
```

**`bg_town_swamp-bestiary`**

```text
Full-screen painted background for a dark-fantasy strategy game screen, hand-painted, atmospheric and muted, deep shadows and soft
light, the middle and the lower half calm and dark so panels and text read over it, no characters in the foreground, no text, no
interface elements. The world is old and dying under a dim sun. Half-sunken ruins in a dark pine forest, stilt huts, a carved totem of
coiled beasts, cyan-glowing eggs.
```

## After generating

1. Look at each background behind a near-black panel with its text: the panel and its text still read.
2. Put the files in `src/Godot/EternalWars.Godot.Assets/textures/ui/backgrounds/` and add a line to its `CREDITS.md` with the tool and
   its licence terms.
