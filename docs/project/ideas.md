# Ideas

Raw design notes from the owner, added at the bottom. Once a note is recorded (an ADR in [decisions.md](decisions.md), a question
in [open-questions.md](open-questions.md), or a design doc) it is removed from here; the ADR or question keeps the trail.

Nothing waiting: the last notes (cursors, icon-only buttons, sorted map lists, difficulty icons) are ADR-204; the cursor prompts are in
[cursor-prompts.md](../content/cursor-prompts.md), the difficulty icons in [icon-prompts.md](../content/icon-prompts.md) (sheet 29).


new game screen:

seed field (together with button) - make it visible in non release build;

remove refog margin field group;

timer - replace with checkbox, when enabled - numeric up/down appears with timer values;

complexity/game difficulty: 
rename to game difficulty;
complexity field - make it same way as in homm3 - square buttons with chess icons(same radio button behavior) map default could be dropped - it either defined in the map(or in editor) - so apropriate difficulty option could be selected, for random map selected option becomes map complexity;
complexity labels (easy, normal, hard, expert, master) default selected option is normal if no restrictions were defined by scenario; remove starting gold description, instead show description 
in bottom status panel when mouse hovers difficulty button;

fog of war: rename options - permanent reveal and refog (remove redundant homm3 labels);

move hero limit section close to fog of war and other settings;

players number selector -remove it; 
instead add "+" icon immediately below players list and "-" icon near each player;

remove back button - instead add "x" icon-button at the top-right corner;

on all screens across the game where "x" is present - align it at the top-right corner;

add ability to select player color;

faction selector - replace names with icons(show name on hover in status bar) - show 2x4 icon panel;

adventure map view:
put calendar label on the status panel - to re right of the resources;
remove turn <color> label;