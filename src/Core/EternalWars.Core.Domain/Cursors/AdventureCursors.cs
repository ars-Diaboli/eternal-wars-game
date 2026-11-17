using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Cursors;

/// <summary>
/// Which cursor the adventure map shows (ADR-204, cursor-prompts.md): the horse for where the chosen hero can go and what it would do there,
/// the helmet and the keep to select, the spell cursor while an adventure spell waits for its cell. It reads only what
/// the viewing player may know: heroes it sees, objects as it last saw them.
/// </summary>
public static class AdventureCursors
{
    /// <summary>
    /// The cursor over <paramref name="cell"/> for <paramref name="viewer"/>, with <paramref name="selected"/> the chosen hero (null: none) and
    /// <paramref name="path"/> its route there (null: no route; the front-end finds it, <c>HeroMovementService.FindPath</c>).
    /// <paramref name="spellAimed"/>: an adventure spell waits for its cell.
    /// </summary>
    public static CursorChoice For(GameState state, PlayerId viewer, HeroId? selected, MapPosition cell, HeroPath? path, bool spellAimed)
    {
        if (spellAimed)
        {
            return GameCursor.AdventureSpell;
        }

        if (!state.Map.Contains(cell) || !state.Fog.IsExplored(viewer, cell))
        {
            return selected is null ? GameCursor.Pointer : GameCursor.Blocked;
        }

        var reach = selected is not null && path is not null;
        var days = path?.Turns ?? 1;
        if (state.HeroAt(cell) is { } hero && state.CanSee(viewer, hero))
        {
            if (hero.Owner == viewer || state.AreAllies(viewer, hero.Owner))
            {
                return reach && hero.Owner == viewer && hero.Id != selected ? new CursorChoice(GameCursor.Exchange, days) : GameCursor.SelectHero;
            }

            return reach ? new CursorChoice(GameCursor.Attack, days) : selected is null ? GameCursor.Pointer : GameCursor.Blocked;
        }

        if (state.Memory.Of(viewer).FirstOrDefault(k => k.Cells().Contains(cell) || IsVisitCell(k, cell)) is { } known)
        {
            var live = state.FindObject(known.Id);
            var own = known.Owner is { } owner && (owner == viewer || state.AreAllies(viewer, owner));
            if (live is Town)
            {
                return own && known.Owner == viewer && reach ? new CursorChoice(GameCursor.EnterTown, days)
                    : own ? GameCursor.SelectTown
                    : reach ? new CursorChoice(GameCursor.Attack, days)
                    : selected is null ? GameCursor.Pointer : GameCursor.Blocked;
            }

            if (!IsVisitCell(known, cell) && live is not MonsterGuard)
            {
                return selected is null ? GameCursor.Pointer : GameCursor.Blocked; // the object's body: no way in there
            }

            return !reach ? (selected is null ? GameCursor.Pointer : GameCursor.Blocked)
                : new CursorChoice(live is MonsterGuard ? GameCursor.Attack : GameCursor.Visit, days);
        }

        return selected is null ? GameCursor.Pointer : reach ? new CursorChoice(GameCursor.Move, days) : GameCursor.Blocked;
    }

    private static bool IsVisitCell(Visibility.KnownObject known, MapPosition cell) =>
        known.Definition.Footprint.HasVisit
        && known.Position.Offset(known.Definition.Footprint.VisitOffset.Dx, known.Definition.Footprint.VisitOffset.Dy) == cell;
}
