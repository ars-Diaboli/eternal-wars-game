using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Editing;

/// <summary>Places and removes heroes and edits their stats, type, army, war machines, artifacts and spells.</summary>
public sealed class HeroEditor
{
    private readonly EditorContext _context;

    internal HeroEditor(EditorContext context) => _context = context;

    private Game.GameState Scenario => _context.Scenario;

    public EditResult Place(PlayerId owner, MapPosition position, string name, out HeroId id)
    {
        id = default;
        if (!Scenario.HasPlayer(owner))
        {
            return EditResult.UnknownPlayer;
        }

        if (!Scenario.Map.Contains(position))
        {
            return EditResult.OutOfBounds;
        }

        if (Scenario.HeroAt(position) is not null || (Scenario.ObjectCellAt(position) is { } cell && !MapValidator.IsOwnTownEntrance(cell, owner)))
        {
            return EditResult.Overlaps;
        }

        if (!Scenario.Terrains.IsPassable(Scenario.Map.GetTerrain(position)))
        {
            return EditResult.ImpassableTerrain;
        }

        if (Scenario.HeroLimitReached(owner))
        {
            return EditResult.HeroLimit;
        }

        var hero = new Hero(Scenario.NextHeroId(), name, owner, position);
        id = hero.Id;
        _context.Run($"Place hero {name}", () => _context.AddHero(hero), () => _context.RemoveHero(hero));
        return EditResult.Ok;
    }

    public EditResult Remove(HeroId id)
    {
        if (_context.Hero(id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        _context.Run($"Remove hero {hero.Name}", () => _context.RemoveHero(hero), () => _context.AddHero(hero));
        return EditResult.Ok;
    }

    /// <summary>Sets primary stats and Tactics; Spell Power and Knowledge stay as they are when omitted.</summary>
    public EditResult SetStats(HeroId id, int attack, int defense, SkillLevel tactics, int? spellPower = null, int? knowledge = null)
    {
        if (_context.Hero(id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if (attack < 0 || defense < 0 || spellPower < 0 || knowledge < 0 || !Enum.IsDefined(tactics) || tactics > BuiltInSkills.Get(BuiltInSkills.Tactics).MaxLevel)
        {
            return EditResult.InvalidValue;
        }

        Edit(hero, $"Edit {hero.Name}", () => (hero.Attack, hero.Defense, hero.Tactics, hero.SpellPower, hero.Knowledge),
            value => (hero.Attack, hero.Defense, hero.Tactics, hero.SpellPower, hero.Knowledge) = value,
            (attack, defense, tactics, spellPower ?? hero.SpellPower, knowledge ?? hero.Knowledge));
        return EditResult.Ok;
    }

    /// <summary>Sets which war machines a hero owns (ADR-056).</summary>
    public EditResult SetWarMachines(HeroId id, IReadOnlySet<WarMachineSlot> machines)
    {
        if (_context.Hero(id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if (machines.Any(m => !Enum.IsDefined(m)))
        {
            return EditResult.InvalidValue;
        }

        IReadOnlyList<WarMachineSlot> next = machines.ToList();
        Edit(hero, $"Edit {hero.Name}'s war machines", () => hero.WarMachines.ToList(), value => hero.SetWarMachines(value), next,
            EditorContext.SameItems<WarMachineSlot>);
        return EditResult.Ok;
    }

    /// <summary>
    /// Sets what a hero wears (ids; each goes to the slot its definition names) and carries (ADR-061). Refused when an
    /// artifact is unknown, does not fit its slots, or the backpack would overflow.
    /// </summary>
    public EditResult SetArtifacts(HeroId id, IReadOnlyList<string> worn, IReadOnlyList<string> backpack)
    {
        if (_context.Hero(id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        var next = (Worn: worn.ToList(), Backpack: backpack.ToList());
        try
        {
            new Artifacts.HeroArtifacts().ReplaceWith(next.Worn, next.Backpack, Scenario.Artifacts); // validates without touching the hero
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException)
        {
            return EditResult.InvalidValue;
        }

        _context.Edit(
            $"Edit {hero.Name}'s artifacts",
            () => (Worn: hero.Artifacts.AllWorn.Select(w => w.Id).ToList(), Backpack: hero.Artifacts.Backpack.ToList()),
            set =>
            {
                hero.Artifacts.ReplaceWith(set.Worn, set.Backpack, Scenario.Artifacts);
                Scenario.RefreshArtifacts(hero);
                _context.Publish(new HeroArtifactsChanged(hero.Id));
            },
            next,
            (a, b) => a.Worn.SequenceEqual(b.Worn) && a.Backpack.SequenceEqual(b.Backpack));
        return EditResult.Ok;
    }

    /// <summary>Sets a hero's morale and luck (−5…+5, ADR-053).</summary>
    public EditResult SetMoraleAndLuck(HeroId id, int morale, int luck)
    {
        if (_context.Hero(id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if (Math.Abs(morale) > MoraleAndLuck.Limit || Math.Abs(luck) > MoraleAndLuck.Limit)
        {
            return EditResult.InvalidValue;
        }

        Edit(hero, $"Edit {hero.Name}'s morale and luck", () => (hero.Morale, hero.Luck), value => (hero.Morale, hero.Luck) = value, (morale, luck));
        return EditResult.Ok;
    }

    /// <summary>
    /// Makes a hero one of the roster (<see cref="Game.GameState.HeroRoster"/>), taking over its name, class, preferred
    /// creature, specialty, stats and starting skills; null turns it back into a custom hero (identity cleared, stats kept).
    /// </summary>
    public EditResult SetType(HeroId id, string? type)
    {
        if (_context.Hero(id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if (type is not null && !Scenario.HeroRoster.Contains(type))
        {
            return EditResult.InvalidValue;
        }

        var before = Snapshot(hero);
        _context.Run($"Make {hero.Name} {type ?? "custom"}", () => Apply(type), () => Restore(before));
        return EditResult.Ok;

        void Apply(string? newType)
        {
            if (newType is null)
            {
                (hero.Definition, hero.Class, hero.PreferredCreature, hero.Specialty) = (null, HeroClass.Warrior, null, null);
            }
            else
            {
                hero.Apply(Scenario.HeroRoster.Get(newType));
            }

            StatsChanged(hero);
        }

        void Restore((string? Definition, string Name, HeroClass Class, string? Preferred, HeroSpecialty? Specialty, int Attack, int Defense,
            int SpellPower, int Knowledge, Dictionary<string, SkillLevel> Skills) snapshot)
        {
            (hero.Definition, hero.Name, hero.Class, hero.PreferredCreature, hero.Specialty, hero.Attack, hero.Defense, hero.SpellPower, hero.Knowledge) =
                (snapshot.Definition, snapshot.Name, snapshot.Class, snapshot.Preferred, snapshot.Specialty, snapshot.Attack, snapshot.Defense,
                 snapshot.SpellPower, snapshot.Knowledge);
            hero.Skills.ReplaceWith(snapshot.Skills);
            StatsChanged(hero);
        }

        static (string?, string, HeroClass, string?, HeroSpecialty?, int, int, int, int, Dictionary<string, SkillLevel>) Snapshot(Hero h) =>
            (h.Definition, h.Name, h.Class, h.PreferredCreature, h.Specialty, h.Attack, h.Defense, h.SpellPower, h.Knowledge,
             h.Skills.Levels.ToDictionary(s => s.Key, s => s.Value));
    }

    /// <summary>
    /// Makes a hero a placeholder or a hero of its own again (ADR-126): a placeholder becomes a random roster hero of its owner's faction when the
    /// game starts, keeping its place, army and artifacts; what the editor sets on its type, stats, skills and spells is replaced then.
    /// </summary>
    public EditResult SetPlaceholder(HeroId id, bool placeholder)
    {
        if (_context.Hero(id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        var description = placeholder ? $"Make {hero.Name} a hero placeholder" : $"Make {hero.Name} a hero of its own";
        Edit(hero, description, () => hero.IsPlaceholder, value => hero.IsPlaceholder = value, placeholder);
        return EditResult.Ok;
    }

    /// <summary>Puts a stack into (or, with null, clears) one of a hero's army slots.</summary>
    public EditResult SetArmySlot(HeroId id, int slot, CreatureStack? stack)
    {
        if (_context.Hero(id) is not { } hero)
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

        _context.Edit($"Edit army of {hero.Name}", () => hero.Army[slot], value =>
        {
            hero.Army.Set(slot, value);
            _context.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        }, stack);
        return EditResult.Ok;
    }

    /// <summary>A hero's spell book and the spells in it (ADR-120): known spell ids, none without a book.</summary>
    public EditResult SetSpells(HeroId id, bool spellBook, IReadOnlyList<string> spells)
    {
        if (_context.Hero(id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if ((!spellBook && spells.Count > 0) || spells.Any(s => !Scenario.Spells.Contains(s)) || spells.Distinct(StringComparer.Ordinal).Count() != spells.Count)
        {
            return EditResult.InvalidValue;
        }

        _context.Edit(
            $"Edit {hero.Name}'s spells",
            () => (Book: hero.HasSpellBook, Spells: hero.Spells.Keys.ToList()),
            set =>
            {
                hero.SetSpellBook(set.Book, set.Spells);
                _context.Publish(new HeroSpellsChanged(hero.Id));
            },
            (Book: spellBook, Spells: spells.ToList()),
            (a, b) => a.Book == b.Book && a.Spells.SequenceEqual(b.Spells));
        return EditResult.Ok;
    }

    /// <summary>An undoable edit of one of the hero's values that publishes <see cref="HeroStatsChanged"/>.</summary>
    private void Edit<T>(Hero hero, string description, Func<T> get, Action<T> set, T next, Func<T, T, bool>? equals = null) =>
        _context.Edit(description, get, value =>
        {
            set(value);
            StatsChanged(hero);
        }, next, equals);

    private void StatsChanged(Hero hero) => _context.Publish(new HeroStatsChanged(hero.Id, hero.Attack, hero.Defense, hero.Tactics));
}
