using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// Draws the map's random placeholders when the game starts (ADR-126), before anything else; the rules are those of <see cref="PlaceholderRules"/>.
/// </summary>
public sealed class PlaceholderService(GameState state, IEventPublisher events)
{
    /// <summary>Draws every placeholder the map has; each changed town, monster and hero is reported so views already built redraw it.</summary>
    internal void Resolve()
    {
        var drawn = new Dictionary<PlayerId, string>();
        var towns = BuiltInMapObjects.PlayableTowns;
        foreach (var town in state.ObjectsOf<Town>().Where(t => t.IsRandom).OrderBy(t => t.Id.Value).ToList())
        {
            var faction = town.Owner is { } owner ? FactionOf(owner, drawn, fromTowns: false) : towns[state.Random.Next(towns.Count)];
            town.IsRandom = false;
            if (faction != town.TownType && state.ObjectCatalog.TryGet(BuiltInMapObjects.TownId(faction), out var definition))
            {
                var replacement = TownRules.Retyped(state, town, definition);
                state.RemoveObject(town);
                state.AddObject(replacement);
                events.Publish(new MapObjectChanged(replacement.Id));
            }
        }

        foreach (var monster in state.ObjectsOf<MonsterGuard>().Where(m => m.RandomTier is not null).OrderBy(m => m.Id.Value).ToList())
        {
            var candidates = PlaceholderRules.MonsterCandidates(state.Creatures, monster.RandomTier!.Value);
            monster.RandomTier = null;
            if (candidates.Count > 0)
            {
                monster.Creature = candidates[state.Random.Next(candidates.Count)].Id;
                events.Publish(new MapObjectChanged(monster.Id));
            }
        }

        var taken = state.Heroes.Where(h => !h.IsPlaceholder).Select(h => h.Definition)
            .Concat(state.ObjectsOf<Prison>().Select(p => p.Hero))
            .Concat(state.RetiredHeroes.Select(h => h.Definition))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        foreach (var hero in state.Heroes.Where(h => h.IsPlaceholder).OrderBy(h => h.Id.Value).ToList())
        {
            hero.IsPlaceholder = false;
            var faction = FactionOf(hero.Owner, drawn, fromTowns: true);
            var free = MapBans.Allowed(
                state.HeroRoster.All.Where(h => !taken.Contains(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList(), h => state.Rules.Bans.BansHero(h.Id));
            var native = free.Where(h => h.Town == faction).ToList();
            var pool = native.Count > 0 ? native : [.. free];
            if (pool.Count == 0)
            {
                continue;
            }

            var definition = pool[state.Random.Next(pool.Count)];
            hero.Apply(definition);
            state.RefreshArtifacts(hero); // the hero's own town changed (the Crown of the Waning Eye's tie)
            if (hero.Army.IsEmpty && TavernRules.StarterStack(state, definition) is { } stack)
            {
                hero.Army.TryAdd(stack);
            }

            taken.Add(definition.Id);
            events.Publish(new HeroRemoved(hero.Id));
            events.Publish(new HeroPlaced(hero.Id, hero.Owner, hero.Position));
        }
    }

    /// <summary>
    /// The faction of a player for its random towns and hero placeholders: its first town's type once the towns are drawn (by id), else its choice,
    /// else <paramref name="drawn"/>'s faction for it (drawn on first use).
    /// </summary>
    private string FactionOf(PlayerId player, Dictionary<PlayerId, string> drawn, bool fromTowns)
    {
        if (fromTowns && state.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value).FirstOrDefault() is { } town)
        {
            return town.TownType;
        }

        if (drawn.TryGetValue(player, out var faction))
        {
            return faction;
        }

        var towns = BuiltInMapObjects.PlayableTowns;
        faction = state.Options.StartTowns?.GetValueOrDefault(player.Value) is { } chosen && towns.Contains(chosen)
            ? chosen
            : towns[state.Random.Next(towns.Count)];
        drawn[player] = faction;
        return faction;
    }
}
