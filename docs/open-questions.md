# Open questions

Answers move into [game-design.md](game-design.md) / [decisions.md](decisions.md).

## Answered (2026-10-15)
- **Fidelity**: HoMM-inspired with our own twists (ADR-008, GDD §7).
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
- **Map editor / random maps**: standalone editor; HotA-style templates authored in the editor;
  both game and editor generate maps from templates (ADR-023, GDD §4.9).

## Game direction
1. **Setting**: original world or lore? Name and theme of the world and its conflict ("Eternal Wars")?
   Group names (Light / Darkness / Wild / Primordial are placeholders)?
2. **Groups**: do they matter in gameplay (alliances, shared skills, diplomacy) or only in lore?
3. **Vertical slice**: Haven vs Necropolis (proposed)?
4. **Twists**: which systems do we deliberately change beyond combat (economy, hero skills, magic,
   town building, morale/luck)?

## Combat (answered: ADR-021; remaining details)
5. **Tactics → width**: exact width per Tactics level (30 default, up to 36, down to 28 when?). Movement 8-way?
6. **Cover**: what counts as cover for ranged attacks (any unit in the line of fire, or only larger ones)?
7. **Damage formula**: HoMM3 attack/defense formula as a baseline, or our own?
8. **Creature roster** for the first two towns (7 tiers + upgrades, sizes, abilities).

## Adventure map
- **Refog**: should a moving hero stop automatically when an enemy unit comes into sight?
  Should mine ownership flags also show "last known" state under refog?
- **Treasure rewards**: fixed (1000 gold / 500 XP now) or rolled from tiers (HoMM3: 1000/1500/2000 gold or 500/1000/1500 XP)?
- **Mines**: guarded by neutral stacks when placed? Re-flagging rules (any hero vs. needs to defeat a guard)?
- **Meeting heroes**: own hero on target tile → exchange screen; enemy → battle (Phase 2). Allies?
- **Terrain assignment**:
  - Confirm the proposed classes and native owners: open grass/dirt; rough +25 %; native sand
    (Stronghold), snow (Academy), swamp (Swamp Bestiary), lava (Inferno), subterranean (Dungeon).
  - Do Haven, Sylvan and Necropolis get new native terrains?
  - Is "native" decided by the whole army (HoMM3) or by the hero's town group?
  - Is there a Pathfinding skill?
- **Roads**: does the road cost apply only road → road (HoMM3), or whenever the tile being left has a road?

## Tooling and process
9. **2D harness scope**: debug viewer only, or also scripted scenario runner / AI sandbox (headless)?
10. **Random maps**:
    - Should competitive templates support **mirrored/symmetric** generation?
    - Water maps and islands (boats) in 1.0?
    - Should players be able to save a generated map, or share its seed only?
    - Import HotA templates? Their format could be converted, but we need to check licensing.
11. **Platforms**: Windows only at first, or also Linux/macOS/Steam Deck?
12. **Modding**: must content be moddable by players from 1.0 (affects data format choices now)?
