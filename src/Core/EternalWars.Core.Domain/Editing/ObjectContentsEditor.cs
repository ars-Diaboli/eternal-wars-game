using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// What visitable objects hold, say or ask: chests, artifact pickups, sign posts, Pandora's boxes, quest sites, sphinxes, witch huts, shrines and
/// prisons. Each edit publishes <see cref="MapObjectChanged"/>.
/// </summary>
public sealed class ObjectContentsEditor
{
    private readonly EditorContext _context;

    internal ObjectContentsEditor(EditorContext context) => _context = context;

    private Game.GameState Scenario => _context.Scenario;

    /// <summary>Fixes what a chest offers (null = rolled when opened).</summary>
    public EditResult SetChestValues(MapObjectId id, ChestValues? values)
    {
        if (_context.Object<TreasureChest>(id) is not { } chest)
        {
            return EditResult.InvalidTarget;
        }

        if (values is { Gold: < 0 } or { Experience: < 0 })
        {
            return EditResult.InvalidValue;
        }

        Edit(chest, "Set chest values", () => chest.FixedValues, value => chest.FixedValues = value, values);
        return EditResult.Ok;
    }

    /// <summary>Fixes which artifact a pickup holds (null = rolled from its tier when the game starts).</summary>
    public EditResult SetArtifactPickup(MapObjectId id, string? artifact)
    {
        if (_context.Object<ArtifactPickup>(id) is not { } pickup)
        {
            return EditResult.InvalidTarget;
        }

        if (artifact is not null && !Scenario.Artifacts.Contains(artifact))
        {
            return EditResult.InvalidValue;
        }

        Edit(pickup, "Set artifact", () => pickup.Artifact, value => pickup.Artifact = value, artifact);
        return EditResult.Ok;
    }

    /// <summary>The message a sign post shows (ADR-068).</summary>
    public EditResult SetSignMessage(MapObjectId id, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (_context.Object<SignPost>(id) is not { } sign)
        {
            return EditResult.InvalidTarget;
        }

        Edit(sign, "Set sign message", () => sign.Message, value => sign.Message = value, message);
        return EditResult.Ok;
    }

    /// <summary>
    /// What a Pandora's Box holds, its guard (empty for none, at most seven stacks) and its message (ADR-100). Contents that are not gains are
    /// refused; an empty box is allowed while editing and is a validator error.
    /// </summary>
    public EditResult SetPandorasBox(MapObjectId id, BoxContents contents, IReadOnlyList<CreatureStack> guard, string? message)
    {
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(guard);
        if (_context.Object<PandorasBox>(id) is not { } box)
        {
            return EditResult.InvalidTarget;
        }

        if (contents.Problem() is not null || guard.Count > Army.SlotCount || guard.Any(s => s.Count < 1 || !Scenario.Creatures.Contains(s.Creature))
            || contents.Creatures.Any(s => !Scenario.Creatures.Contains(s.Creature)))
        {
            return EditResult.InvalidValue;
        }

        message = string.IsNullOrEmpty(message) ? null : message;
        Edit(box, "Set Pandora's box", () => (box.Contents, box.Guard, box.Message), value => (box.Contents, box.Guard, box.Message) = value,
            (contents, guard, message));
        return EditResult.Ok;
    }

    /// <summary>The hero event a Pandora's Box runs for its opener (MAP-35), or null for none; an unknown name is the validator's error.</summary>
    public EditResult SetPandorasBoxEvent(MapObjectId id, string? @event)
    {
        if (_context.Object<PandorasBox>(id) is not { } box)
        {
            return EditResult.InvalidTarget;
        }

        @event = string.IsNullOrWhiteSpace(@event) ? null : @event.Trim();
        Edit(box, "Set Pandora's box event", () => box.Event, value => box.Event = value, @event);
        return EditResult.Ok;
    }

    /// <summary>The quest a Seer's Hut, Quest Guard or Quest Gate runs (EVT-5), or null for none; an unknown name is refused when the game loads.</summary>
    public EditResult SetQuestSiteQuest(MapObjectId id, string? quest)
    {
        if (_context.Object<QuestSite>(id) is not { } site)
        {
            return EditResult.InvalidTarget;
        }

        quest = string.IsNullOrWhiteSpace(quest) ? null : quest.Trim();
        Edit(site, "Set quest", () => site.Quest, value => site.Quest = value, quest);
        return EditResult.Ok;
    }

    /// <summary>
    /// A sphinx's riddle (ADR-079, MAP-27): the author's question with its answer and two wrong ones, or null for one drawn from <c>riddles.csv</c>
    /// when the game starts. Refused when a text is empty or two answers are the same (ignoring case and spaces at the ends).
    /// </summary>
    public EditResult SetSphinxRiddle(MapObjectId id, Riddle? riddle)
    {
        if (_context.Object<Sphinx>(id) is not { } sphinx)
        {
            return EditResult.InvalidTarget;
        }

        if (riddle is not null)
        {
            riddle = new Riddle(riddle.Question.Trim(), riddle.Answer.Trim(), riddle.Wrong1.Trim(), riddle.Wrong2.Trim());
            string[] answers = [riddle.Answer, riddle.Wrong1, riddle.Wrong2];
            if (riddle.Question.Length == 0 || answers.Any(a => a.Length == 0) || answers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != answers.Length)
            {
                return EditResult.InvalidValue;
            }
        }

        Edit(sphinx, "Set riddle", () => sphinx.Riddle, value => sphinx.Riddle = value, riddle);
        return EditResult.Ok;
    }

    /// <summary>
    /// A witch hut's two skills (ADR-079, MAP-34): two different known skills of any group, or none for two rolled when the game starts. Old skill ids
    /// are stored as their successors (ADR-072).
    /// </summary>
    public EditResult SetWitchHutSkills(MapObjectId id, IReadOnlyList<string> skills)
    {
        if (_context.Object<WitchHut>(id) is not { } hut)
        {
            return EditResult.InvalidTarget;
        }

        if ((skills.Count != 0 && skills.Count != WitchHut.SkillCount) || skills.Any(s => !BuiltInSkills.Contains(s)))
        {
            return EditResult.InvalidValue;
        }

        IReadOnlyList<string> resolved = skills.Select(BuiltInSkills.Resolve).ToList();
        if (resolved.Distinct(StringComparer.Ordinal).Count() != resolved.Count)
        {
            return EditResult.InvalidValue;
        }

        Edit(hut, "Set witch hut skills", () => hut.Skills, value => hut.Skills = value, resolved, EditorContext.SameItems<string>);
        return EditResult.Ok;
    }

    /// <summary>A shrine's spell (ADR-120): a spell of the shrine's level, or null for one drawn when the game starts.</summary>
    public EditResult SetShrineSpell(MapObjectId id, string? spell)
    {
        if (_context.Object<Shrine>(id) is not { } shrine)
        {
            return EditResult.InvalidTarget;
        }

        if (ShrineRules.SpellRefusal(Scenario.Spells, shrine.SpellLevel, spell) is not null)
        {
            return EditResult.InvalidValue;
        }

        Edit(shrine, "Set shrine spell", () => shrine.Spell, value => shrine.Spell = value, spell);
        return EditResult.Ok;
    }

    /// <summary>Who sits in a prison and at what level (ADR-068); null = drawn when the game starts.</summary>
    public EditResult SetPrison(MapObjectId id, string? hero, int? level)
    {
        if (_context.Object<Prison>(id) is not { } prison)
        {
            return EditResult.InvalidTarget;
        }

        if (hero is not null && !Scenario.HeroRoster.Contains(hero))
        {
            return EditResult.InvalidValue;
        }

        if (level is < 1 or > 75)
        {
            return EditResult.InvalidValue;
        }

        Edit(prison, "Set prisoner", () => (prison.Hero, prison.Level), value => (prison.Hero, prison.Level) = value, (hero, level));
        return EditResult.Ok;
    }

    /// <summary>An undoable edit of one of the object's values that publishes <see cref="MapObjectChanged"/>.</summary>
    private void Edit<T>(MapObject mapObject, string description, Func<T> get, Action<T> set, T next, Func<T, T, bool>? equals = null) =>
        _context.Edit(description, get, value =>
        {
            set(value);
            _context.Publish(new MapObjectChanged(mapObject.Id));
        }, next, equals);
}
