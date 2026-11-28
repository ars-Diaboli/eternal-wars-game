using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>Edits what belongs to the map as a whole: its name and description, the scenario rules and the Grail.</summary>
public sealed class MapPropertiesEditor
{
    private readonly EditorContext _context;

    internal MapPropertiesEditor(EditorContext context) => _context = context;

    private GameState Scenario => _context.Scenario;

    public void SetProperties(string name, string description)
    {
        var map = Scenario.Map;
        _context.Edit("Edit map properties", () => (map.Name, map.Description), value =>
        {
            (map.Name, map.Description) = value;
            _context.Publish(new MapPropertiesChanged(value.Item1, value.Item2));
        }, (name, description));
    }

    /// <summary>
    /// Sets how the scenario is won and lost (ADR-057) and the map's hero limit (ADR-068); problems with the conditions are reported by
    /// <see cref="MapValidator"/>. Refused when the limit is out of range or below the heroes a player (without a limit of its own) already has.
    /// </summary>
    public EditResult SetRules(ScenarioRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.MaxHeroes is < 1 or > ScenarioRules.HeroLimit || rules.WeekEvents.Problem() is not null
            || rules.Rumours.Count > ScenarioRules.MaxRumours || rules.Rumours.Any(r => !ScenarioRules.IsFitRumour(r)))
        {
            return EditResult.InvalidValue;
        }

        if (Scenario.Players.Any(p => p.MaxHeroes is null && Scenario.HeroesOf(p.Id).Count() > rules.MaxHeroes))
        {
            return EditResult.HeroLimit;
        }

        var what = Scenario.Rules with { Bans = rules.Bans } == rules ? "Edit the bans"
            : Scenario.Rules with { Rumours = rules.Rumours } == rules ? "Edit the rumours"
            : "Edit victory and defeat conditions";
        _context.Edit(what, () => Scenario.Rules, value =>
        {
            Scenario.SetRules(value);
            _context.Publish(new ScenarioRulesChanged());
        }, rules);
        return EditResult.Ok;
    }

    /// <summary>
    /// The tournament switch (ADR-366, <see cref="TournamentRules.Switched"/>), as one undo step. Turned on, the hero limit becomes 1 for the map
    /// and for every player (the players' own limits go) and the arena goes; refused while a player has more than one hero. Turned off, only the
    /// switch goes.
    /// </summary>
    public EditResult SetTournament(bool on)
    {
        var result = EditResult.Ok;
        if (!on)
        {
            _context.AsOneStep("Make the map no tournament", () => result = SetRules(TournamentRules.Switched(Scenario.Rules, on: false)));
            return result;
        }

        if (Scenario.Players.Any(p => Scenario.HeroesOf(p.Id).Count() > 1))
        {
            return EditResult.HeroLimit;
        }

        _context.AsOneStep("Make the map a tournament", () =>
        {
            result = SetRules(TournamentRules.Switched(Scenario.Rules, on: true));
            if (result != EditResult.Ok)
            {
                return;
            }

            foreach (var player in Scenario.Players.Where(p => p.MaxHeroes is not null))
            {
                var id = player.Id;
                _context.Edit($"Set hero limit of {player.Name}", () => Scenario.GetPlayer(id).MaxHeroes, value =>
                {
                    Scenario.SetHeroLimit(id, value);
                    _context.Publish(new PlayerSlotsChanged());
                }, null);
            }
        });
        return result;
    }

    /// <summary>
    /// Buries the Grail at <paramref name="position"/> (ADR-140), or takes it away (null: drawn when the game starts on a map with obelisks). Refused
    /// off the map, nearer the edge than <see cref="GrailRules.EdgeDistance"/> (or on a map too small for a Grail) and where a hero could not
    /// dig (impassable ground, a road, an object).
    /// </summary>
    public EditResult SetGrail(MapPosition? position)
    {
        if (position is { } at)
        {
            if (!Scenario.Map.Contains(at))
            {
                return EditResult.OutOfBounds;
            }

            if (!GrailRules.FarFromEdge(Scenario.Map, at))
            {
                return EditResult.TooNearEdge;
            }

            if (GrailRules.CheckGround(Scenario, at) is not null)
            {
                return Scenario.ObjectCellAt(at) is not null ? EditResult.Overlaps
                    : Scenario.Terrains.IsPassable(Scenario.Map.GetTerrain(at)) ? EditResult.InvalidTarget
                    : EditResult.ImpassableTerrain;
            }
        }

        _context.Edit(position is null ? "Remove the Grail" : "Bury the Grail", () => Scenario.Grail.Position, value =>
        {
            Scenario.SetGrail(value);
            _context.Publish(new GrailMoved(value));
        }, position);
        return EditResult.Ok;
    }
}
