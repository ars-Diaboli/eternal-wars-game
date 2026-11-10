using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing;

/// <summary>Edits towns: owner, name, type, buildings, dwellings, fixed guild spells and garrisons (also of map garrisons).</summary>
public sealed class TownEditor
{
    private readonly EditorContext _context;

    internal TownEditor(EditorContext context) => _context = context;

    private Game.GameState Scenario => _context.Scenario;

    public EditResult SetOwner(MapObjectId id, PlayerId? owner)
    {
        if (_context.Object<Town>(id) is not { } town)
        {
            return EditResult.InvalidTarget;
        }

        if (owner is { } player && !Scenario.HasPlayer(player))
        {
            return EditResult.UnknownPlayer;
        }

        _context.Edit("Set town owner", () => town.Owner, value =>
        {
            var before = town.Owner;
            town.Owner = value;
            _context.Publish(new TownOwnerChanged(town.Id, before, value));
        }, owner);
        return EditResult.Ok;
    }

    public EditResult SetName(MapObjectId id, string name)
    {
        if (_context.Object<Town>(id) is not { } town)
        {
            return EditResult.InvalidTarget;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return EditResult.InvalidValue;
        }

        Edit(town, "Rename town", () => town.Name, value => town.Name = value, name.Trim());
        return EditResult.Ok;
    }

    /// <summary>
    /// Gives a town another playable town type, e.g. after generating a map: a town of the new type takes its id, place, owner, garrison and,
    /// unless it kept the old type's default name, its name. Buildings the new type also has stay (at most its top level); each dwelling, and the
    /// creatures waiting in it, moves to the new type's line at the same place in the same tier. Fixed guild spells (v30) are of the old school and
    /// are dropped (undo brings them back). Refused when the result lacks a requirement.
    /// </summary>
    public EditResult SetType(MapObjectId id, string townType)
    {
        if (_context.Object<Town>(id) is not { } town || !BuiltInMapObjects.PlayableTowns.Contains(townType) ||
            !_context.Catalog.TryGet(BuiltInMapObjects.TownId(townType), out var definition))
        {
            return EditResult.InvalidTarget;
        }

        if (townType == town.TownType)
        {
            return EditResult.Ok;
        }

        var replacement = TownRules.Retyped(Scenario, town, definition);
        replacement.IsRandom = town.IsRandom; // a random town's type only shows it in the editor
        if (TownRules.UnmetRequirement(Scenario, replacement) is not null)
        {
            return EditResult.RequirementMissing;
        }

        _context.Run($"Make {town.Name} a {townType} town", () => Swap(town, replacement), () => Swap(replacement, town));
        return EditResult.Ok;

        void Swap(Town from, Town to)
        {
            _context.RemoveObject(from);
            _context.AddObject(to);
        }
    }

    /// <summary>
    /// Makes a town random or fixed (ADR-126): a random town's faction is drawn when the game starts, an owned one taking its owner's chosen faction.
    /// Making it random drops its fixed guild spells, which belong to one school (undo brings them back).
    /// </summary>
    public EditResult SetRandom(MapObjectId id, bool random)
    {
        if (_context.Object<Town>(id) is not { } town)
        {
            return EditResult.InvalidTarget;
        }

        if (town.IsRandom == random)
        {
            return EditResult.Ok;
        }

        var description = random ? $"Make {town.Name} a random town" : $"Make {town.Name} a {town.TownType} town";
        Edit(town, description, () => (town.IsRandom, town.FixedGuildSpells), value => (town.IsRandom, town.FixedGuildSpells) = value,
            (random, random ? [] : town.FixedGuildSpells));
        return EditResult.Ok;
    }

    /// <summary>
    /// Pre-builds a town building up to <paramref name="level"/> (0 = not built). Requirements hold as in play: a level
    /// needs what it requires, and nothing built may lose a building it requires.
    /// </summary>
    public EditResult SetBuilding(MapObjectId id, string building, int level)
    {
        if (_context.Object<Town>(id) is not { } town ||
            BuiltInBuildings.DwellingTier(building) is not null || !TownRules.Buildings(Scenario, town).Contains(building))
        {
            return EditResult.InvalidTarget;
        }

        if (level < 0 || level > TownRules.Buildings(Scenario, town).MaxLevel(building))
        {
            return EditResult.InvalidValue;
        }

        if (TownRules.UnmetRequirement(Scenario, town, building, level) is not null)
        {
            return EditResult.RequirementMissing;
        }

        Edit(town, $"Set {building} of {town.Name}", () => town.Level(building), value => town.SetLevel(building, value), level);
        return EditResult.Ok;
    }

    /// <summary>
    /// Pre-builds a line's dwelling: 0 none, 1 built, 2 upgraded; the 7-of-9 rule (ADR-030) and the dwelling's
    /// requirements (the fort, ADR-066) apply.
    /// </summary>
    public EditResult SetDwelling(MapObjectId id, string line, int level)
    {
        if (_context.Object<Town>(id) is not { } town)
        {
            return EditResult.InvalidTarget;
        }

        if (TownRules.Lines(Scenario, town).FirstOrDefault(l => l.Id == line) is not { } definition ||
            TownRules.CheckDwellingChoice(Scenario, town, line, level) is not null)
        {
            return EditResult.DwellingRule;
        }

        if (level < 0 || level > (definition.UpgradesTo is null ? 1 : 2))
        {
            return EditResult.InvalidValue;
        }

        if (TownRules.UnmetDwellingRequirement(Scenario, town, definition, level) is not null)
        {
            return EditResult.RequirementMissing;
        }

        Edit(town, $"Set {definition.Name} dwelling of {town.Name}", () => town.DwellingLevel(line), value => town.SetDwelling(line, value), level);
        return EditResult.Ok;
    }

    /// <summary>Puts a stack into (or, with null, clears) one of the garrison slots of a town or a map garrison (ADR-070).</summary>
    public EditResult SetGarrisonSlot(MapObjectId id, int slot, CreatureStack? stack)
    {
        var (troops, name) = Scenario.Objects.FirstOrDefault(o => o.Id == id) switch
        {
            Town t => (t.Garrison, t.Name),
            MapGarrison g => (g.Troops, g.Definition.Name.ToLowerInvariant()),
            _ => (null, ""),
        };
        if (troops is null)
        {
            return EditResult.InvalidTarget;
        }

        if (!Army.IsValidSlot(slot))
        {
            return EditResult.InvalidValue;
        }

        if (stack is not null && !Scenario.Creatures.Contains(stack.Creature))
        {
            return EditResult.UnknownCreature;
        }

        _context.Edit($"Edit garrison of {name}", () => troops[slot], value =>
        {
            troops.Set(slot, value);
            _context.Publish(new GarrisonChanged(id, troops.Slots.ToList()));
        }, stack);
        return EditResult.Ok;
    }

    /// <summary>
    /// The spells a town's mage guild is fixed to teach (map v30): spells of the town's school, at most 3 / 3 / 2 / 2 per level, each once
    /// (<see cref="Magic.MagicRules.GuildSpellsProblem"/>); the game-start draw fills the rest. Empty = everything drawn. The order is kept as given.
    /// </summary>
    public EditResult SetGuildSpells(MapObjectId id, IReadOnlyList<string> spells)
    {
        if (_context.Object<Town>(id) is not { } town)
        {
            return EditResult.InvalidTarget;
        }

        if (Magic.MagicRules.GuildSpellsProblem(Scenario.Spells, town.TownType, spells) is not null)
        {
            return EditResult.InvalidValue;
        }

        IReadOnlyList<string> next = [.. spells];
        Edit(town, $"Set guild spells of {town.Name}", () => town.FixedGuildSpells, value => town.FixedGuildSpells = value, next,
            EditorContext.SameItems<string>);
        return EditResult.Ok;
    }

    /// <summary>An undoable edit of one of the town's values that publishes <see cref="MapObjectChanged"/>.</summary>
    private void Edit<T>(Town town, string description, Func<T> get, Action<T> set, T next, Func<T, T, bool>? equals = null) =>
        _context.Edit(description, get, value =>
        {
            set(value);
            _context.Publish(new MapObjectChanged(town.Id));
        }, next, equals);
}
