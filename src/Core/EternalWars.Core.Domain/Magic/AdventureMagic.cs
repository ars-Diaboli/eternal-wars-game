using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Magic;

/// <summary>
/// Adventure spells (ADR-071, ADR-073, ADR-120; [spells.md](../content/spells.md#adventure-spells)), rank I: cast from the spell book on the map for
/// the spell's mana and a fixed <see cref="MovementCost"/> movement points; a hero with fewer left cannot cast. Built: Town Portal, Dimension Door,
/// View Terrain, Pathfinder's Call and Ashwalk; the spells that only show something (Distant Vision, Detect Magic, Grave Whisper, Ledger of Ages,
/// Veil, Eavesdrop) wait for their screens.
/// </summary>
public static class AdventureMagic
{
    /// <summary>Movement points every adventure spell costs besides its mana (ADR-073, MAG-5).</summary>
    public const int MovementCost = 500;

    public const string TownPortal = "arcana.town-portal";
    public const string DimensionDoor = "shadow.dimension-door";
    public const string ViewTerrain = "nature.view-terrain";
    public const string PathfindersCall = "nature.pathfinders-call";
    public const string Ashwalk = "chaos.ashwalk";

    /// <summary>How far Dimension Door goes at rank I: up to 6 cells in a straight line (ADR-072).</summary>
    public const int DimensionDoorRange = 6;

    /// <summary>View Terrain's radius at rank I.</summary>
    public const int ViewTerrainRadius = 12;

    /// <summary>The adventure spells that can be cast now.</summary>
    public static IReadOnlySet<string> Built { get; } = new HashSet<string>(StringComparer.Ordinal) { TownPortal, DimensionDoor, ViewTerrain, PathfindersCall, Ashwalk };

    /// <summary>The terrains a spell makes native for the rest of the day (Pathfinder's Call: forest floor, grass, swamp; Ashwalk: lava, wasteland, cursed lands).</summary>
    public static IReadOnlySet<TerrainType> NativeTerrains(string spell) => spell switch
    {
        PathfindersCall => new HashSet<TerrainType> { TerrainType.ForestFloor, TerrainType.Grass, TerrainType.Swamp },
        Ashwalk => new HashSet<TerrainType> { TerrainType.Lava, TerrainType.Wasteland, TerrainType.CursedLands },
        _ => new HashSet<TerrainType>(),
    };

    /// <summary>A terrain set as a status value: one bit per <see cref="TerrainType"/>.</summary>
    public static int TerrainMask(IEnumerable<TerrainType> terrains) => terrains.Aggregate(0, (mask, t) => mask | (1 << (int)t));

    /// <summary>Whether a status makes <paramref name="terrain"/> native to the hero's army (Pathfinder's Call, Ashwalk).</summary>
    public static bool MakesNative(Hero hero, TerrainType terrain) =>
        hero.Statuses.Any(s => s.Kind == HeroStatusKind.NativeTerrain && (s.Value & (1 << (int)terrain)) != 0);

    /// <summary>
    /// Town Portal's town at rank I: the nearest own town whose entrance is free (the same level first, then by the distance in tiles, then by id);
    /// null when there is none other than where the hero stands.
    /// </summary>
    public static Town? PortalTown(GameState state, Hero hero) =>
        state.ObjectsOf<Town>()
            .Where(t => t.Owner == hero.Owner && t.VisitPosition != hero.Position && state.HeroAt(t.VisitPosition) is null)
            .OrderBy(t => t.VisitPosition.Level != hero.Position.Level)
            .ThenBy(t => Math.Max(Math.Abs(t.VisitPosition.X - hero.Position.X), Math.Abs(t.VisitPosition.Y - hero.Position.Y)))
            .ThenBy(t => t.Id.Value)
            .FirstOrDefault();

    /// <summary>The adventure spells the hero could cast (it knows them, from its book or its artifacts, and they are built), whether or not it can pay now.</summary>
    public static IEnumerable<SpellDefinition> Known(GameState state, Hero hero) =>
        MagicRules.Known(hero, state.Spells).Select(k => k.Spell).Where(s => s.IsAdventure && Built.Contains(s.Id))
            .OrderBy(s => s.Level).ThenBy(s => s.Id, StringComparer.Ordinal);

    /// <summary>What casting the spell costs the hero in mana (its attunement lowers it, <see cref="ManaRules.SpellCost"/>).</summary>
    public static int Cost(GameState state, Hero hero, string spell) => ManaRules.SpellCost(hero, state.Spells.Get(spell).Mana);

    /// <summary>
    /// Why the hero cannot cast the adventure spell now (aimed at <paramref name="target"/> for Dimension Door), or null: a spell book that holds
    /// it, a built adventure spell, its mana, 500 movement points; Dimension Door once a day, up to 6 tiles in a straight line on the same level to
    /// an explored, free, passable and unguarded tile (not checked when <paramref name="aimed"/> is false: the tile is still to be picked); Town
    /// Portal needs an own town with a free entrance.
    /// </summary>
    public static string? Refusal(GameState state, Hero hero, string spell, MapPosition? target, bool aimed = true)
    {
        if (!hero.HasSpellBook)
        {
            return $"{hero.Name} has no spell book.";
        }

        if (!state.Spells.TryGet(spell, out var definition) || MagicRules.Known(hero, state.Spells).All(k => k.Spell.Id != spell))
        {
            return $"{hero.Name} does not know that spell.";
        }

        if (!definition.IsAdventure || !Built.Contains(spell))
        {
            return $"{definition.Name} cannot be cast on the map{(definition.IsAdventure ? " yet" : string.Empty)}.";
        }

        if (hero.Mana < Cost(state, hero, spell))
        {
            return $"Not enough mana for {definition.Name}.";
        }

        if (hero.MovementPoints < MovementCost)
        {
            return $"Casting {definition.Name} needs {MovementCost} movement points.";
        }

        switch (spell)
        {
            case TownPortal when PortalTown(state, hero) is null:
                return "There is no own town to return to.";
            case DimensionDoor when hero.Statuses.Any(s => s.Source == DimensionDoor):
                return "Dimension Door works once a day.";
            case DimensionDoor:
                return aimed ? DoorRefusal(state, hero, target) : null;
            default:
                return null;
        }
    }

    private static string? DoorRefusal(GameState state, Hero hero, MapPosition? target)
    {
        if (target is not { } to)
        {
            return "Dimension Door needs a tile to go to.";
        }

        var (dx, dy) = (to.X - hero.Position.X, to.Y - hero.Position.Y);
        var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (to.Level != hero.Position.Level || distance == 0 || distance > DimensionDoorRange || (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy)))
        {
            return $"Dimension Door goes up to {DimensionDoorRange} tiles in a straight line.";
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

/// <summary>Casts adventure spells (ADR-120): validates, pays the mana and the movement points, then does what the spell does and publishes it.</summary>
internal sealed class AdventureMagicService(GameState state, IEventPublisher events)
{
    internal void Cast(PlayerId player, HeroId heroId, string spell, MapPosition? target)
    {
        var hero = state.Heroes.FirstOrDefault(h => h.Id == heroId && h.Owner == player) ?? throw new InvalidOperationException($"{heroId} is not an own hero on the map.");
        if (AdventureMagic.Refusal(state, hero, spell, target) is { } refusal)
        {
            throw new InvalidOperationException(refusal);
        }

        var cost = AdventureMagic.Cost(state, hero, spell);
        hero.Mana -= cost;
        hero.MovementPoints -= AdventureMagic.MovementCost;
        events.Publish(new AdventureSpellCast(hero.Id, spell, target, cost, hero.Mana, hero.MovementPoints));
        switch (spell)
        {
            case AdventureMagic.TownPortal:
                var town = AdventureMagic.PortalTown(state, hero)!;
                Relocate(hero, town.VisitPosition);
                TownService.AutoAppoint(state, events, town, hero);
                MagicRules.Visit(state, events, hero, town);
                break;
            case AdventureMagic.DimensionDoor:
                Relocate(hero, target!.Value);
                Mark(hero, new HeroStatus(spell, HeroStatusKind.Cast, 1, StatusExpiry.EndOfDay));
                break;
            case AdventureMagic.ViewTerrain:
                events.Publish(new AreaRevealed(hero.Owner, hero.Position, AdventureMagic.ViewTerrainRadius));
                break;
            default:
                Mark(hero, new HeroStatus(spell, HeroStatusKind.NativeTerrain, AdventureMagic.TerrainMask(AdventureMagic.NativeTerrains(spell)), StatusExpiry.EndOfDay));
                break;
        }
    }

    private void Relocate(Hero hero, MapPosition to)
    {
        var from = hero.Position;
        hero.Position = to;
        events.Publish(new HeroTeleported(hero.Id, from, to));
    }

    private void Mark(Hero hero, HeroStatus status)
    {
        hero.AddStatus(status);
        events.Publish(new HeroStatusGained(hero.Id, status));
    }
}
