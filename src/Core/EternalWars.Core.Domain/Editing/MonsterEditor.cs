using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>Places neutral monsters and edits their stack, disposition, guard mode and random tier.</summary>
public sealed class MonsterEditor
{
    private readonly EditorContext _context;
    private readonly ObjectEditor _objects;

    internal MonsterEditor(EditorContext context, ObjectEditor objects) => (_context, _objects) = (context, objects);

    private Game.GameState Scenario => _context.Scenario;

    /// <summary>Places a neutral monster; a null <paramref name="count"/> is rolled from its tier at game start.</summary>
    public EditResult Place(
        MapPosition position,
        string creature,
        int? count,
        GuardDisposition disposition,
        bool neverFlees,
        out MapObjectId id)
    {
        id = default;
        if (!Scenario.Creatures.Contains(creature))
        {
            return EditResult.UnknownCreature;
        }

        if (count < 1)
        {
            return EditResult.InvalidValue;
        }

        var definition = _context.Catalog.OfKind(MapObjectKind.Monster).FirstOrDefault();
        if (definition is null)
        {
            return EditResult.InvalidTarget;
        }

        var check = _objects.CanPlace(definition, position);
        if (check != EditResult.Ok)
        {
            return check;
        }

        var monster = new MonsterGuard(Scenario.NextObjectId(), definition, position, creature, count, disposition, neverFlees);
        id = monster.Id;
        _context.Run($"Place {Scenario.Creatures.Get(creature).Name}", () => _context.AddObject(monster), () => _context.RemoveObject(monster));
        return EditResult.Ok;
    }

    public EditResult Set(MapObjectId id, string creature, int? count, GuardDisposition disposition, bool neverFlees)
    {
        if (_context.Object<MonsterGuard>(id) is not { } monster)
        {
            return EditResult.InvalidTarget;
        }

        if (!Scenario.Creatures.Contains(creature))
        {
            return EditResult.UnknownCreature;
        }

        if (count < 1)
        {
            return EditResult.InvalidValue;
        }

        _context.Edit("Edit monster", () => (monster.Creature, monster.Count, monster.Disposition, monster.NeverFlees), value =>
        {
            (monster.Creature, monster.Count, monster.Disposition, monster.NeverFlees) = value;
            _context.Publish(new MapObjectChanged(monster.Id));
        }, (creature, count, disposition, neverFlees));
        return EditResult.Ok;
    }

    /// <summary>
    /// Makes a monster random (ADR-126): <paramref name="tier"/> 0 = any tier, 1–7 = that tier, null = the creature as placed. A tier shows the first
    /// creature of that tier the draw could pick, so the editor has something to show; the creature is drawn when the game starts.
    /// </summary>
    public EditResult SetRandomTier(MapObjectId id, int? tier)
    {
        if (_context.Object<MonsterGuard>(id) is not { } monster)
        {
            return EditResult.InvalidTarget;
        }

        if (tier is < 0 or > Creatures.CreatureDefinition.MaxTier)
        {
            return EditResult.InvalidValue;
        }

        var shown = tier is > 0 and var t ? Game.PlaceholderRules.MonsterCandidates(Scenario.Creatures, t).FirstOrDefault()?.Id ?? monster.Creature : monster.Creature;
        _context.Edit<(int?, string)>(tier is null ? "Make the monster fixed" : "Make the monster random", () => (monster.RandomTier, monster.Creature), value =>
        {
            (monster.RandomTier, monster.Creature) = value;
            _context.Publish(new MapObjectChanged(monster.Id));
        }, (tier, shown));
        return EditResult.Ok;
    }

    /// <summary>
    /// How one monster stack guards its surroundings (ADR-065): Classic (HoMM3 zone of control, pickups can be taken
    /// from outside) or Area (treasure guard: touching anything in the 3×3 area starts the fight).
    /// </summary>
    public EditResult SetGuardMode(MapObjectId id, GuardMode mode)
    {
        if (_context.Object<MonsterGuard>(id) is not { } monster)
        {
            return EditResult.InvalidTarget;
        }

        if (!Enum.IsDefined(mode))
        {
            return EditResult.InvalidValue;
        }

        _context.Edit($"Guard mode {mode}", () => monster.Mode, value =>
        {
            monster.Mode = value;
            _context.Publish(new MapObjectChanged(monster.Id));
        }, mode);
        return EditResult.Ok;
    }
}
