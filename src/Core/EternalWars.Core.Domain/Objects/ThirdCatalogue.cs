using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// A cartographer (ADR-079, MAP-31): for <see cref="Price"/> gold the visitor's player sees the whole level the definition names
/// (<see cref="MapObjectDefinition.RevealsLevel"/>: the surface or the underground). Each player buys once.
/// </summary>
public sealed class Cartographer(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.Cartographer)
{
    public const int Price = 3000;

    /// <summary>The players who bought the map here.</summary>
    public HashSet<PlayerId> Buyers { get; } = [];

    public int Level => Definition.RevealsLevel;
}

/// <summary>
/// An artifact merchant (ADR-079, MAP-33): <see cref="StockSize"/> Minor and Major artifacts, rolled when the game starts and again on the
/// first day of every month. The hero on its entrance buys one for <see cref="ArtifactMerchantRules.Price"/>; a sold slot stays empty until then.
/// </summary>
public sealed class ArtifactMerchant(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.ArtifactMerchant)
{
    public const int StockSize = 7;

    /// <summary>What is for sale, slot by slot (null: sold).</summary>
    public List<string?> Stock { get; } = [];
}

/// <summary>Prices of the artifact merchant.</summary>
public static class ArtifactMerchantRules
{
    /// <summary>What an artifact of a tier is worth in gold (ADR-079 drafts, shared with the Sacrificial Altar's experience).</summary>
    public static int Value(ArtifactTier tier) => tier switch
    {
        ArtifactTier.Minor => 2000,
        ArtifactTier.Major => 5000,
        _ => 10000,
    };

    /// <summary>Twice the artifact's value (MAP-33).</summary>
    public static ResourceSet Price(ArtifactDefinition artifact) => new(Gold: 2 * Value(artifact.Tier));
}

/// <summary>
/// A refugee camp (ADR-079, MAP-30): each week a random base creature of tiers 3–5 from the playable factions, one week's growth of it to buy at
/// the normal price by the hero on its entrance. Not flaggable; unsold creatures leave when the next week's arrive.
/// </summary>
public sealed class RefugeeCamp(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.RefugeeCamp)
{
    public string? Creature { get; internal set; }

    public int Available { get; internal set; }
}

/// <summary>Which creatures come to refugee camps.</summary>
public static class RefugeeCampRules
{
    public const int MinTier = 3;

    public const int MaxTier = 5;

    /// <summary>Base creatures of tiers 3–5 of the playable towns, by id.</summary>
    public static IReadOnlyList<CreatureDefinition> Candidates(CreatureCatalog creatures) =>
        creatures.All.Where(c => c.Tier is >= MinTier and <= MaxTier && c.Town is { } town && BuiltInMapObjects.PlayableTowns.Contains(town)
                                 && creatures.BaseOf(c.Id) == c.Id)
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
}

/// <summary>What a hero may lay on a Sacrificial Altar (ADR-079, MAP-29), by its town's alignment.</summary>
[Flags]
public enum SacrificeKind
{
    None = 0,
    Creatures = 1,
    Artifacts = 2,
}

/// <summary>
/// A sacrificial altar (ADR-079, MAP-29): Darkness heroes (Necropolis, Inferno, Dungeon) sacrifice creatures for twice their hit points in
/// experience; Light heroes (Haven, Sylvan, Academy) artifacts for 2000 / 5000 / 10 000 by tier; Wild heroes (Stronghold, Swamp Bestiary)
/// either. Any number of times.
/// </summary>
public sealed class SacrificialAltar(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.SacrificialAltar);

public static class SacrificeRules
{
    public const int ExperiencePerHitPoint = 2;

    public static SacrificeKind For(string? town) => town switch
    {
        TownIds.Necropolis or TownIds.Inferno or TownIds.Dungeon => SacrificeKind.Creatures,
        TownIds.Haven or TownIds.Sylvan or TownIds.Academy => SacrificeKind.Artifacts,
        TownIds.Stronghold or TownIds.SwampBestiary => SacrificeKind.Creatures | SacrificeKind.Artifacts,
        _ => SacrificeKind.None,
    };

    public static int Experience(CreatureDefinition creature, int count) => count * creature.Health * ExperiencePerHitPoint;

    public static int Experience(ArtifactDefinition artifact) => ArtifactMerchantRules.Value(artifact.Tier);
}

/// <summary>A riddle (ADR-079, MAP-27): the right answer and two wrong ones.</summary>
public sealed record Riddle(string Question, string Answer, string Wrong1, string Wrong2);

/// <summary>
/// A sphinx (ADR-079, MAP-27) asks every hero one riddle, once: its author's <see cref="Riddle"/>, or one drawn from <c>riddles.csv</c>.
/// Right: a Major artifact or 5000 experience (rolled). Wrong: the hero loses its remaining movement for the day and −3 morale in its next battle.
/// </summary>
public sealed class Sphinx(MapObjectId id, MapObjectDefinition definition, MapPosition position, Riddle? riddle = null)
    : MapObject(id, definition, position, MapObjectKind.Sphinx)
{
    public const int Experience = 5000;

    public const int WrongMorale = -3;

    public Riddle? Riddle { get; internal set; } = riddle;
}

/// <summary>
/// A witch hut (ADR-079, MAP-34) offers two skills (any group: general, school or faction), set by the map or rolled when the game starts. A
/// visit raises one of them by a level (or learns it at Basic); where the hero cannot, another skill of the same group stands in. One witch hut
/// upgrade per hero level.
/// </summary>
public sealed class WitchHut(MapObjectId id, MapObjectDefinition definition, MapPosition position, IReadOnlyList<string>? skills = null)
    : MapObject(id, definition, position, MapObjectKind.WitchHut)
{
    public const int SkillCount = 2;

    /// <summary>The hut's two skills; empty until rolled when the map leaves them open.</summary>
    public IReadOnlyList<string> Skills { get; internal set; } = skills ?? [];
}

public static class WitchHutRules
{
    /// <summary>
    /// What the hut offers the hero: each of its skills the hero can raise, else another skill of that group it can (the first by id); never the
    /// same skill twice. Empty when the hero used a witch hut at this level already.
    /// </summary>
    public static IReadOnlyList<string> Options(Hero hero, WitchHut hut)
    {
        if (hero.WitchHutLevel == hero.Level)
        {
            return [];
        }

        var options = new List<string>();
        foreach (var skill in hut.Skills)
        {
            var group = BuiltInSkills.Get(skill).Group;
            var pick = hero.Skills.CanRaise(skill) && !options.Contains(skill) ? skill
                : BuiltInSkills.All.Where(s => s.Group == group && hero.Skills.CanRaise(s.Id) && !options.Contains(s.Id))
                    .OrderBy(s => s.Id, StringComparer.Ordinal).Select(s => s.Id).FirstOrDefault();
            if (pick is not null)
            {
                options.Add(pick);
            }
        }

        return options;
    }

    /// <summary>Two different skills of two different groups, for a hut the map left open.</summary>
    public static IReadOnlyList<string> Roll(GameRandom random)
    {
        var all = BuiltInSkills.All.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
        var first = all[random.Next(all.Count)];
        var rest = all.Where(s => s.Group != first.Group).ToList();
        return [first.Id, rest[random.Next(rest.Count)].Id];
    }
}

/// <summary>The riddle table (<c>src/Core/data/riddles.csv</c>): <c>id, question, answer, wrong1, wrong2, notes</c>.</summary>
public static class RiddleTable
{
    public const string ResourceName = "EternalWars.Riddles.csv";

    public static IReadOnlyList<string> Columns { get; } = ["id", "question", "answer", "wrong1", "wrong2", "notes"];

    public static IReadOnlyList<Riddle> Default { get; } = Parse(EmbeddedTable());

    public static IReadOnlyList<Riddle> Parse(string text) =>
        CsvTable.Parse(text, "Riddle", ["id", "question", "answer", "wrong1", "wrong2"])
            .Read(row => row.Text("id") is { Length: > 0 } ? new Riddle(row.Text("question"), row.Text("answer"), row.Text("wrong1"), row.Text("wrong2")) : null)
            .OfType<Riddle>()
            .ToList();

    public static string EmbeddedTable()
    {
        using var stream = typeof(RiddleTable).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"The riddle table '{ResourceName}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
