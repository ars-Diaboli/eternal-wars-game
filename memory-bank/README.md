# Memory bank

The project's memory for LLM agents: what the project is, where it stands, what was decided and why. Human-facing documents (the
game design, content catalogs, technical guides, papers) are in [docs/](../docs/README.md); this folder points into them and never
restates them.

## Read order
1. [project-brief.md](project-brief.md): what the game is, the code layout, the rules every change follows (one screen).
2. [active-context.md](active-context.md): what was done last, what is next, known problems.
3. Then only what the task needs:
   - the design doc of the area (`docs/design/*.md`) for the rules as they are now;
   - [decisions/README.md](decisions/README.md) for an ADR by number or category, then that one entry, not the whole file;
   - [roadmap.md](roadmap.md) for an item's checkbox, [open-questions.md](open-questions.md) for an unanswered design point.

## Files

| File | Holds | Who updates it |
|---|---|---|
| [project-brief.md](project-brief.md) | Vision, stack, tiers, hard rules | Rarely: when the stack or a rule changes |
| [active-context.md](active-context.md) | Current focus, the last ADRs, next steps, known problems | At the end of every piece of work |
| [roadmap.md](roadmap.md) | Phases and their checkboxes (progress) | When code lands: tick or add the item with its ADR |
| [open-questions.md](open-questions.md) | Design questions with stable ids (`RMG-8`, `TOWN-4`, …) and a proposal each | When a question is asked or answered |
| [ideas.md](ideas.md) | The owner's raw notes | The owner adds; a note leaves once it is an ADR or a question |
| [decisions/](decisions/README.md) | Every ADR, split by category, with one index | A new ADR per significant decision |

## Rules for keeping it true
- **A decision** gets the next free ADR number (in [decisions/README.md](decisions/README.md)), goes at the bottom of its category
  file with a row in that file's table and in the index, and the matching design doc states the new rule. Accepted ADRs are never
  rewritten; a later one supersedes them and the old one's status says so.
- **A finished piece of work** ticks its roadmap item (with the ADR number) and refreshes [active-context.md](active-context.md).
- **An answered question** leaves [open-questions.md](open-questions.md) with one line under its "Answered" list.
- Code refers to decisions by number only (`ADR-024`), never by file, so ADRs can move between files.
- No change history in the design docs or here outside the ADRs; git and the ADRs keep it.
