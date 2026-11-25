# Question picture prompts (for an image-generating model)

Prompts for the **pictures on the adventure map's question cards**: the card that asks a hero's player to choose (a treasure chest, a
training site, a sphinx, a town gate, ...) shows one painted picture above the question and its answers. There is one picture for each
kind of question the game asks (`PendingDecision`), and a few spares for messages. The battle and the hero exchange open screens of their
own and need none. The designs are original.

## Use a prompt

Every prompt below is **one ready block**: copy it whole into the model, as it is. Where the model takes a negative prompt, give it
`Q-NEG`. Generate at the model's largest 16:9 landscape size and save at 1024 × 576 as the file named above the prompt. The prompts
share their opening words on purpose: keep them as they are so the pictures match.

## Negative prompt

**`Q-NEG`** (every picture):

```text
text, letters, numbers, labels, frame, border, interface, logo, watermark, photograph, plastic CGI render, pixel art, cartoon, comic
book, anime, chibi, cute, flat colours, vector art, clip art, oversaturated colours, neon, cluttered background, several subjects
competing, hard edges at the picture's border, another game's art
```

## Output

- PNG, sRGB, **1024 × 576**; the faded edges are part of the picture (no alpha needed). The card draws it at about 480 × 270.
- **Names**: `q_<id>.png` as in the table.
- **Check**: the subject reads at 240 × 135, and the faded edges meet the card's slate (RGB 28, 31, 38) without a visible line.

## Pictures

**Use**: the file's prompt · negative `Q-NEG` · 1024 × 576

| File | Question |
|---|---|
| `q_treasure-chest` | a treasure chest: gold or experience |
| `q_arena` | the Arena: attack or defence |
| `q_colosseum` | the Colosseum: a stat for the gods' favour |
| `q_gazebo` | the Gazebo: experience for gold |
| `q_tree-of-knowledge` | the Tree of Knowledge: a level for a price |
| `q_deposit` | a deposit: build its mine for a price |
| `q_cartographer` | the cartographer: a level's map for gold |
| `q_sphinx` | a sphinx's riddle |
| `q_pandoras-box` | Pandora's Box: open it or not |
| `q_witch-hut` | a witch hut: learn a skill |
| `q_town-gate` | a town gate: travel to a town |
| `q_guard-join` | neutrals offer to join |
| `q_guard-flee` | neutrals try to flee |
| `q_level-up` | a hero reaches a level |
| `q_event-army` | a map event's creatures with no room in the army |
| `q_event-message` | a map event's message or question |
| `q_event-reward` | a map event's message that gives something |

**`q_treasure-chest`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. An old iron-bound wooden chest standing open on mossy ground,
gold coins spilling over its lip, a faint warm glow from inside.
```

**`q_arena`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A small round stone arena seen from its rim, a sand floor, a rack
of practice swords and a battered shield by the gate.
```

**`q_colosseum`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A tall ruined colosseum of pale stone at dusk, banners hanging
from its arches, a bronze statue of a champion at its gate.
```

**`q_gazebo`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A white stone gazebo with slender columns in a quiet garden, an
open book and a lit candle on a lectern inside.
```

**`q_tree-of-knowledge`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A huge ancient tree with silver bark and pale glowing leaves in a
clearing, small runes shining in the bark.
```

**`q_deposit`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A raw seam of ore in a rock face, a few loose crystals and
nuggets on the ground, a pick and an empty cart waiting beside it.
```

**`q_cartographer`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A cluttered map-maker's table in a tent, rolled charts, a brass
compass and dividers, a half-drawn map under a lantern.
```

**`q_sphinx`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A great stone sphinx lying on a sand-swept plinth between two
broken columns, its eyes glowing faintly gold.
```

**`q_pandoras-box`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A small ornate box of dark wood and gold on a stone altar, its
lid shut, thin wisps of violet light leaking from the seam.
```

**`q_witch-hut`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A crooked hut on stilts over a dark pond, green light in its
round window, herbs and bones hanging from the eaves.
```

**`q_town-gate`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A tall stone gateway standing alone in a field, a shimmering blue
veil of light filling the arch, a road leading through it.
```

**`q_guard-join`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A band of armed creatures lowering their weapons before a camp
fire, one holding out an open hand, a banner planted in the ground.
```

**`q_guard-flee`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A band of creatures running off along a forest road at dusk,
dropped spears and a fallen banner in the dust behind them.
```

**`q_level-up`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A raised sword held up in a beam of golden light breaking through
clouds, sparks rising around the blade.
```

**`q_event-army`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A crowd of mixed creatures waiting at a crossroads with their
gear, a signpost behind them, more of them than a camp could hold.
```

**`q_event-message`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A sealed letter with a red wax seal lying on a weathered table
beside a candle and a quill.
```

**`q_event-reward`**

```text
A small painted vignette for a question card in a dark-fantasy strategy game, hand-painted in a realistic painterly style like a
detailed oil study or book illustration, 16:9 landscape. One clear subject in the middle third, seen from slightly above at eye level,
lit warmly from the top left against a dark, quiet surround. The edges of the picture fade softly to a deep slate-blue darkness on all
four sides, so the picture sits in a dark card without a hard border. Muted natural colours with one warm accent. No text, no letters,
no numbers, no frame, no interface, no characters' faces in close-up. A small leather purse, a scroll and a gem laid out on a cloth in
candlelight.
```

## After generating

1. Look at each picture at the card's size beside its question: the subject is the thing the question is about.
2. Put the pictures side by side: the same light, the same faded edges, the same level of detail.
3. Scale to 1024 × 576, blend the outer 6 % into the card's slate (RGB 28, 31, 38), put the files in
   `src/Godot/EternalWars.Godot.Assets/textures/ui/questions/` and update the folder's `CREDITS.md`.
