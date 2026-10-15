# Open questions

Answers move into [game-design.md](game-design.md) / [decisions.md](decisions.md).

## Answered (2026-10-15)
- **Fidelity**: HoMM-inspired with our own twists (ADR-008, GDD §7).
- **Art**: realistic lighting and textures, dark and grim fantasy atmosphere, nods to the original (GDD §5).
- **Modes**: single-player vs AI → hot-seat → story campaign. No online multiplayer.
- **Battles**: separate battlefield, square grid, unit footprints 2×2 / 3×3 / 4×4 (ADR-005).
- **Turn order**: initiative decides order; one action per unit per round (ADR-009).
- **Size rules**: surround/flanking and body blocking/cover; no cleave, no trample.
- **Towns**: 8 playable in 3 groups + Conflux and Dragon Utopia as non-playable (ADR-010).
- **Terrain**: smooth terrain with grid overlay (ADR-011).

## Game direction
1. **Setting**: original world or lore? Name and theme of the world and its conflict ("Eternal Wars")?
   Group names (Light / Darkness / Wild / Primordial are placeholders)?
2. **Groups**: do they matter in gameplay (alliances, shared skills, diplomacy) or only in lore?
3. **Vertical slice**: Haven vs Necropolis (proposed)?
4. **Twists**: which systems do we deliberately change beyond combat (economy, hero skills, magic,
   town building, morale/luck)?

## Rules
5. **Combat details**: battlefield size; 4- vs 8-way movement; flanking model (facing or sides);
   cover threshold; initiative tie-breaks and *Wait*.
6. **Fog of war**: permanent reveal (HoMM3) or re-fogging?
7. **Map scale**: target map sizes (36×36 … 144×144?) and max players (8?).

## Presentation
8. **Camera**: fixed angle (HoMM-like) or free rotation?

## Tooling and process
9. **2D harness scope**: debug viewer only, or also scripted scenario runner / AI sandbox (headless)?
10. **Map editor**: separate standalone app (current) or in-game editor mode? Random map generator priority?
11. **Platforms**: Windows only at first, or also Linux/macOS/Steam Deck?
12. **Modding**: must content be moddable by players from 1.0 (affects data format choices now)?
