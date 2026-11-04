using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>
/// HoMM3's quest forms as ready forms (EVT-5): each writes a <see cref="QuestStage"/> whose requirement asks for something and whose actions take
/// what it asks (artifacts, creatures, resources), then give the <c>rewards</c>. The editor's forms and the tests use them; the result is the same
/// tree an author could write by hand.
/// </summary>
public static class QuestForms
{
    /// <summary>The hero reaches level <paramref name="level"/>.</summary>
    public static QuestStage Level(int level, string proposal, params EventAction[] rewards) =>
        new(AtLeast(Quantity.HeroLevel, null, level), proposal, rewards);

    /// <summary>The hero's primary stats reach the values (0 = no demand).</summary>
    public static QuestStage PrimaryStats(int attack, int defense, int spellPower, int knowledge, string proposal, params EventAction[] rewards) =>
        new(All(new[] { (Quantity.Attack, attack), (Quantity.Defense, defense), (Quantity.SpellPower, spellPower), (Quantity.Knowledge, knowledge) }
                .Where(d => d.Item2 > 0)
                .Select(d => AtLeast(d.Item1, null, d.Item2))),
            proposal, rewards);

    /// <summary>The hero brings the artifacts (one of each listed, as often as listed); the stage takes them.</summary>
    public static QuestStage Artifacts(IReadOnlyList<string> artifacts, string proposal, params EventAction[] rewards) =>
        new(All(artifacts.GroupBy(a => a, StringComparer.Ordinal).Select(g => AtLeast(Quantity.Artifacts, g.Key, g.Count()))), proposal,
            [.. artifacts.Select(a => new ArtifactAction(a, Take: true)), .. rewards]);

    /// <summary>The hero brings the creatures; the stage takes them.</summary>
    public static QuestStage Creatures(IReadOnlyList<CreatureStack> creatures, string proposal, params EventAction[] rewards)
    {
        var asked = creatures.GroupBy(s => s.Creature, StringComparer.Ordinal).Select(g => (Creature: g.Key, Count: g.Sum(s => s.Count))).ToList();
        return new(All(asked.Select(c => AtLeast(Quantity.Creatures, c.Creature, c.Count))), proposal,
            [.. asked.Select(c => new CreaturesAction(c.Creature, new NumberExpr(-c.Count))), .. rewards]);
    }

    /// <summary>The player has the resources; the stage takes them.</summary>
    public static QuestStage Resources(ResourceSet resources, string proposal, params EventAction[] rewards)
    {
        var asked = Enum.GetValues<ResourceType>().Where(r => resources[r] > 0).ToList();
        return new(All(asked.Select(r => AtLeast(Quantity.Resource, r.ToString(), resources[r]))), proposal,
            [.. asked.Select(r => new ResourceAction(r, new NumberExpr(-resources[r]))), .. rewards]);
    }

    /// <summary>The player beats the map monster (object id).</summary>
    public static QuestStage DefeatMonster(int monster, string proposal, params EventAction[] rewards) =>
        new(new MonsterDefeatedCondition(monster), proposal, rewards);

    /// <summary>The player beats the map hero (hero id).</summary>
    public static QuestStage DefeatHero(int hero, string proposal, params EventAction[] rewards) =>
        new(new HeroDefeatedCondition(hero), proposal, rewards);

    /// <summary>Only the roster hero <paramref name="hero"/> may complete it.</summary>
    public static QuestStage BeHero(string hero, string proposal, params EventAction[] rewards) =>
        new(new HeroIsCondition(hero), proposal, rewards);

    /// <summary>Only a hero of the class (and town, when set) may complete it.</summary>
    public static QuestStage BeClass(HeroClass heroClass, string? town, string proposal, params EventAction[] rewards) =>
        new(new HeroClassCondition(heroClass, town), proposal, rewards);

    /// <summary>Only a hero of slot <paramref name="player"/> may complete it.</summary>
    public static QuestStage BePlayer(int player, string proposal, params EventAction[] rewards) =>
        new(new PlayerIsCondition(player), proposal, rewards);

    private static CompareCondition AtLeast(Quantity quantity, string? id, int value) =>
        new(new QuantityExpr(quantity, id), CompareOp.GreaterOrEqual, new NumberExpr(value));

    private static EventCondition All(IEnumerable<EventCondition> conditions) =>
        conditions.ToList() is [var one] ? one : new AllCondition(conditions.ToList());
}
