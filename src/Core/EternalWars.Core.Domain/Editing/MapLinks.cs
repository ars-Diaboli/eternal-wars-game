using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// Something a thing on the map is tied to, which the inspector offers as a jump link (EDIT-1, ADR-254): the tile to show, and the
/// object or hero to select there (neither for an event cell).
/// </summary>
public sealed record MapLink(string Text, MapPosition Position, MapObjectId? Object = null, HeroId? Hero = null);

/// <summary>
/// The links of a thing on the map (ADR-254), read from the scenario and never kept:
/// <list type="bullet">
/// <item>a gate's partner or exit, and the gates that lead to it;</item>
/// <item>what a quest object's quest names (the monster or hero to beat, the town to hold);</item>
/// <item>the cells that run the same hero event as a Pandora's box;</item>
/// <item>for a monster, a hero or a town an event names: what runs that event (a quest object, a box, a cell).</item>
/// </list>
/// A link whose target is not on the map is left out; the validator reports those.
/// </summary>
public static class MapLinks
{
    /// <summary>The links of <paramref name="mapObject"/>, the ones it holds first.</summary>
    public static IReadOnlyList<MapLink> Of(GameState scenario, MapObject mapObject)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(mapObject);
        var links = new List<MapLink>();
        if (mapObject.TeleportTarget is { } target && scenario.FindObject(target) is { } exit)
        {
            links.Add(To("Leads to", exit));
        }

        links.AddRange(scenario.Objects
            .Where(o => o.TeleportTarget == mapObject.Id && o.Id != mapObject.TeleportTarget && o.Id != mapObject.Id)
            .OrderBy(o => o.Id.Value)
            .Select(o => To("Entered from", o)));

        var events = scenario.MapEvents.Set;
        if (mapObject is QuestSite { Quest: { } quest } && events.Find(quest) is { } definition)
        {
            links.AddRange(Named(scenario, definition, "Quest names", mapObject.Id));
        }

        if (mapObject is PandorasBox { Event: { } run })
        {
            links.AddRange(events.Cells.Where(c => c.Event == run).Select(c => new MapLink($"Also run by the cell at {c.Position}", c.Position)));
        }

        links.AddRange(NamedBy(scenario, c => c switch
        {
            MonsterDefeatedCondition monster => mapObject is MonsterGuard && monster.Monster == mapObject.Id.Value,
            TownOwnerCondition town => mapObject is Town && town.Town == mapObject.Id.Value,
            _ => false,
        }, except: mapObject.Id));
        return links;
    }

    /// <summary>The links of <paramref name="hero"/>: what runs the events that name it.</summary>
    public static IReadOnlyList<MapLink> Of(GameState scenario, Hero hero)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(hero);
        return NamedBy(scenario, c => c is HeroDefeatedCondition beaten && beaten.Hero == hero.Id.Value, except: null);
    }

    private static MapLink To(string what, MapObject mapObject) =>
        new($"{what} {mapObject.Definition.Name}{(mapObject.GivenName is { } name ? $" \"{name}\"" : "")} #{mapObject.Id.Value} at {mapObject.VisitPosition}", mapObject.VisitPosition, mapObject.Id);

    /// <summary>
    /// The monsters, heroes and towns the event's conditions name, each once, in the order they are named; an id that is not a monster
    /// (or not a town) on the map, or is <paramref name="self"/>, gives no link.
    /// </summary>
    private static List<MapLink> Named(GameState scenario, MapEventDefinition definition, string what, MapObjectId self)
    {
        var links = new List<MapLink>();
        foreach (var condition in MapEventTree.Conditions(definition))
        {
            var link = condition switch
            {
                MonsterDefeatedCondition monster when monster.Monster != self.Value && scenario.FindObject(new MapObjectId(monster.Monster)) is MonsterGuard found =>
                    To(what + ":", found),
                TownOwnerCondition town when town.Town != self.Value && scenario.FindObject(new MapObjectId(town.Town)) is Town found => To(what + ":", found),
                HeroDefeatedCondition beaten when scenario.Heroes.FirstOrDefault(h => h.Id.Value == beaten.Hero) is { } hero =>
                    new MapLink($"{what}: {hero.Name} at {hero.Position}", hero.Position, Hero: hero.Id),
                _ => null,
            };
            if (link is not null && !links.Contains(link))
            {
                links.Add(link);
            }
        }

        return links;
    }

    /// <summary>What runs the events with a condition that <paramref name="names"/> the thing: quest objects, boxes and cells.</summary>
    private static List<MapLink> NamedBy(GameState scenario, Func<EventCondition, bool> names, MapObjectId? except)
    {
        var links = new List<MapLink>();
        foreach (var definition in scenario.MapEvents.Set.Events.Where(e => MapEventTree.Conditions(e).Any(names)))
        {
            var name = definition.Name;
            links.AddRange(scenario.Objects
                .Where(o => o.Id != except && (o is QuestSite site ? site.Quest == name : o is PandorasBox box && box.Event == name))
                .OrderBy(o => o.Id.Value)
                .Select(o => To($"Named by \"{name}\" of", o)));
            links.AddRange(scenario.MapEvents.Set.Cells.Where(c => c.Event == name)
                .Select(c => new MapLink($"Named by \"{name}\", run by the cell at {c.Position}", c.Position)));
        }

        return links;
    }
}
