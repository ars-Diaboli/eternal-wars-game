using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>
/// Dimension Door: once a day (twice at rank III), up to 6 tiles (9 from rank II) in a straight line on the same level to an explored, free,
/// passable and unguarded tile.
/// </summary>
internal sealed class DimensionDoorHandler : AdventureSpellHandler
{
    public override Common.SpellId Spell => AdventureMagic.DimensionDoor;

    public override bool NeedsTarget(GameState state, Hero hero) => true;

    public override string? Refusal(GameState state, Hero hero, int rank, MapPosition? target, bool aimed)
    {
        if (hero.Statuses.FirstOrDefault(s => s.Source == Spell && s.Kind == HeroStatusKind.Cast) is { } used
            && used.Value >= AdventureMagic.DimensionDoorCasts(rank))
        {
            return AdventureMagic.DimensionDoorCasts(rank) == 1 ? "Dimension Door works once a day." : $"Dimension Door works {AdventureMagic.DimensionDoorCasts(rank)} times a day.";
        }

        return aimed ? DoorRefusal(state, hero, target, AdventureMagic.DimensionDoorRange(rank)) : null;
    }

    public override string Aim(int rank) => $"Dimension Door: click a tile up to {AdventureMagic.DimensionDoorRange(rank)} tiles away in a straight line.";

    public override void Cast(AdventureCast cast)
    {
        var used = cast.Hero.Statuses.FirstOrDefault(s => s.Source == cast.Spell && s.Kind == HeroStatusKind.Cast)?.Value ?? 0;
        cast.Relocate(cast.Target!.Value);
        cast.Mark(new HeroStatus(cast.Spell.Value, HeroStatusKind.Cast, used + 1, StatusExpiry.EndOfDay));
    }

    private static string? DoorRefusal(GameState state, Hero hero, MapPosition? target, int range)
    {
        if (target is not { } to)
        {
            return "Dimension Door needs a tile to go to.";
        }

        var (dx, dy) = (to.X - hero.Position.X, to.Y - hero.Position.Y);
        var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (to.Level != hero.Position.Level || distance == 0 || distance > range || (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy)))
        {
            return $"Dimension Door goes up to {range} tiles in a straight line.";
        }

        if (!state.Map.Contains(to) || !state.Fog.IsExplored(hero.Owner, to))
        {
            return "Dimension Door lands only on explored tiles.";
        }

        return MovementRules.CheckTile(state, to) != MoveResult.Moved || state.HeroAt(to) is not null || state.ObjectCellAt(to) is not null || state.IsGuarded(to)
            ? "Dimension Door needs a free, passable and unguarded tile."
            : null;
    }
}
