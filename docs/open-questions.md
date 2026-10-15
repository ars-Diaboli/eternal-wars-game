# Open questions

Answers move into [game-design.md](game-design.md) / [decisions.md](decisions.md).

## Answered (2026-10-15)
- **Fidelity**: HoMM-inspired with our own twists (ADR-008, GDD §8).
- **Art**: realistic lighting and textures, dark and grim fantasy atmosphere, nods to the original (GDD §5).
- **Modes**: single-player vs AI → hot-seat → story campaign. No online multiplayer.
- **Battles**: separate battlefield, square grid, unit footprints 2×2 / 3×3 / 4×4 (ADR-005).
- **Turn order**: initiative decides order; one action per unit per round (ADR-009).
- **Size rules**: surround/flanking and body blocking/cover; size gives no cleave/trample. Breath and
  cleave exist as **creature abilities** (dragon/phoenix, cerberus/hydra).
- **Towns**: 8 playable in 3 groups + Conflux and Dragon Utopia as non-playable (ADR-010).
- **Terrain**: smooth terrain with grid overlay (ADR-011).
- **Movement**: HoMM3 rules: pay for the tile being left, last step with any points left (ADR-018).
- **Movement points**: slowest creature speed + skills + artifacts (ADR-018).
- **Fog of war**: chosen per game: permanent reveal, or shroud + unit refog with hysteresis (ADR-017).
- **Resources**: gold, lumber, ore, crystal, mercury, sulfur (ADR-019).
- **Map scale**: 48, 72, 96, 128, 160, 192, 256; max 8 players (ADR-020).
- **Combat**: 24 × 28–36 battlefield (Tactics), no facing/generic flanking (ability hooks instead),
  HoMM3 wait and ties, breath friendly fire (ADR-021).
- **Camera**: 15–30° from vertical, ±45° rotation, zoom (ADR-022).
- **Roads / native terrain**: open / rough (fixed penalty for all) / native terrain classes; HoMM3-style
  roads (ADR-024, GDD §4.1).
- **Income**: no placeholder income; sources are towns, mines, hero skills, specialisations and artifacts (ADR-025).
- **Gates**: two-way gates and one-way gates (entrance → fixed exit); no random-exit portals for now (ADR-026).
- **Town groups**: lore and campaign only; no rule effect in standard games (ADR-028).
- **Town rivalries**: lore only; no hate bonuses or alignment effects in combat (ADR-029).
- **Tech tree**: 5 tiers; 9 dwellings (2 per tier 1–4, 1 at tier 5); build 7, at least one per tier;
  playable creatures all upgradeable; the choice is final per town; one upgrade per dwelling;
  locked creatures are still available from map dwellings and other towns; neutrals use tiers 1–5 (ADR-030).
- **Meeting heroes**: own → exchange; enemy → battle; allies → no exchange for now (ADR-027).
- **Map editor / random maps**: standalone editor; HotA-style templates authored in the editor;
  both game and editor generate maps from templates (ADR-023, GDD §4.9).
- **Refog**: auto-stop when an enemy comes into sight; bumping into an unseen enemy starts an ambush
  battle; out-of-sight objects show their last known state (ADR-031).
- **Teams**: map/template defaults, editable at setup unless locked (ADR-032).
- **Native terrain**: whole army must be native; Pathfinding skill 25/50/75 %; new terrains highlands
  (Haven), forest floor (Sylvan), wasteland (Necropolis) (ADR-032).
- **Roads**: road cost only road → road (ADR-032).
- **Tactics**: choose the width before battle (Basic 30/32, Advanced 28/30/32, Expert 28–34); enemy
  Tactics reduces yours; 36 only for sieges (ADR-033).
- **Damage**: HoMM3 formula as baseline (ADR-033).
- **Cover**: cumulative per blocker on the line of fire: 2×2/3×3 creature 1/8, 4×4 creature 1/4, small
  obstacle 1/8, big obstacle 1/4, city wall 1/2; above 7/8 no ranged attack (ADR-035).
- **Vertical slice**: Haven and Inferno (ADR-034).
- **Treasure**: rolled HoMM3-style tiers; map authors may fix values (ADR-034).
- **Mines**: optional guards; after that, re-flag by visiting (ADR-034).
- **Platforms**: Windows + Linux/Steam Deck for 1.0; modding not required for 1.0 (ADR-034).
- **Harness**: Godot 2D viewer + headless .NET simulator (ADR-034).
- **Random maps 1.0**: mirrored templates, save generated maps; no water/islands, no HotA import (ADR-034).
- **Lore**: Claude drafts a proposal for review → [lore.md](lore.md).

## Still open

### Content
1. **Lore review**: premise, world/group names in [lore.md](lore.md).
2. **Twists** beyond combat: which systems do we deliberately change (hero skills, magic, town building,
   morale/luck)?
3. **Creature rosters** for Haven and Inferno: 9 creatures each over 5 tiers (2+2+2+2+1), with
   upgrades, size class, speed, initiative and abilities. Should Claude draft them?

### Tuning (defaults chosen; confirm or change)
4. Ambush defender bonus (none?), non-native cost for new terrains (150), neutral creatures count as
   non-native on all terrains, combat movement 8-way.
   Cover details: how the line of fire is traced on the grid (centre-to-centre supercover?); can skills
   reduce cover penalties?
5. Treasure chest roll odds per tier (copy HoMM3's after checking them, or set our own).
