using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>Who may set off a simple event: player slots (none listed: any), and whether people and computer players may.</summary>
public sealed record EventTriggers(IReadOnlyList<int> Players, bool People = true, bool Computers = false)
{
    public static EventTriggers Anyone { get; } = new([], People: true, Computers: true);
}

/// <summary>What a simple event gives; negative resources and creatures take. A town event gives creatures to the garrison.</summary>
public sealed record SimpleRewards
{
    public IReadOnlyDictionary<ResourceType, int> Resources { get; init; } = new Dictionary<ResourceType, int>();

    public int Experience { get; init; }

    public int Attack { get; init; }

    public int Defense { get; init; }

    public int SpellPower { get; init; }

    public int Knowledge { get; init; }

    public int Morale { get; init; }

    public int Luck { get; init; }

    public IReadOnlyList<CreatureStack> Creatures { get; init; } = [];

    public IReadOnlyList<string> Artifacts { get; init; } = [];

    public static SimpleRewards None { get; } = new();

    /// <summary>The rewards as actions, in a fixed order.</summary>
    public IReadOnlyList<EventAction> Actions() =>
    [
        .. Resources.Where(r => r.Value != 0).OrderBy(r => r.Key).Select(r => new ResourceAction(r.Key, new NumberExpr(r.Value))),
        .. Experience > 0 ? [new ExperienceAction(new NumberExpr(Experience))] : Array.Empty<EventAction>(),
        .. new[] { (PrimaryStat.Attack, Attack), (PrimaryStat.Defense, Defense), (PrimaryStat.SpellPower, SpellPower), (PrimaryStat.Knowledge, Knowledge) }
            .Where(s => s.Item2 > 0).Select(s => new PrimaryStatAction(s.Item1, new NumberExpr(s.Item2))),
        .. Morale != 0 || Luck != 0 ? [new MoraleAction(Morale, Luck)] : Array.Empty<EventAction>(),
        .. Creatures.Where(s => s.Count != 0).Select(s => new CreaturesAction(s.Creature, new NumberExpr(s.Count))),
        .. Artifacts.Select(a => new ArtifactAction(a)),
    ];
}

/// <summary>
/// The "simple event" forms of the editor (ADR-115, EVT-1): HoMM3's timed events, event cells and town events, each written as the same tree an
/// author could build by hand: a condition for the day and who may set it off, then a message with the rewards (and, for a town, buildings and
/// creatures to hire). HoMM3's quests are <see cref="QuestForms"/>.
/// </summary>
public static class SimpleEventForms
{
    /// <summary>
    /// A timed event (a player event): on day <paramref name="firstDay"/> and then every <paramref name="every"/> days (0: once), the players it is
    /// for read the message and get the rewards.
    /// </summary>
    public static MapEventDefinition Timed(string name, string message, int firstDay, int every, EventTriggers who, SimpleRewards rewards) =>
        new(name, MapEventKind.Player, Guarded(All(OnDays(firstDay, every), Who(who)), Show(message, rewards.Actions())));

    /// <summary>
    /// An event cell's event (a hero event): a hero of the players it is for that steps onto one of its cells reads the message and gets the
    /// rewards; <paramref name="once"/> disables it afterwards (all its cells).
    /// </summary>
    public static MapEventDefinition Cell(string name, string message, EventTriggers who, bool once, SimpleRewards rewards) =>
        new(name, MapEventKind.Hero, Guarded(Who(who), [.. Show(message, rewards.Actions()), .. once ? [new EnableEventAction(null, false)] : Array.Empty<EventAction>()]));

    /// <summary>
    /// A town event: at the start of the owner's turn on the days given, in the <paramref name="towns"/> (object ids), the message, the rewards (the
    /// creatures join the garrison), the <paramref name="buildings"/> built and <paramref name="growth"/> more creatures to hire (by base creature).
    /// </summary>
    public static MapEventDefinition Town(
        string name, IReadOnlyList<int> towns, string message, int firstDay, int every, EventTriggers who, SimpleRewards rewards,
        IReadOnlyList<string> buildings, IReadOnlyDictionary<string, int> growth) =>
        new(name, MapEventKind.Town, Guarded(All(OnDays(firstDay, every), Who(who)),
        [
            .. Show(message, rewards.Actions()),
            .. buildings.Select(b => new BuildAction(b)),
            .. growth.Where(g => g.Value != 0).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new AvailableCreaturesAction(g.Key, new NumberExpr(g.Value))),
        ]))
        {
            Towns = towns,
        };

    /// <summary>Day <paramref name="first"/> (from 1), then every <paramref name="every"/> days; 0 or less: only that day.</summary>
    public static EventCondition OnDays(int first, int every)
    {
        var day = new QuantityExpr(Quantity.Day);
        if (every <= 0)
        {
            return new CompareCondition(day, CompareOp.Equal, new NumberExpr(first));
        }

        if (every == 1)
        {
            return new CompareCondition(day, CompareOp.GreaterOrEqual, new NumberExpr(first));
        }

        // (day − first) − (day − first) ÷ every × every = 0: the days since the first are a whole number of periods (there is no remainder operator).
        var since = new ArithmeticExpr(ArithmeticOp.Subtract, day, new NumberExpr(first));
        var periods = new ArithmeticExpr(ArithmeticOp.Multiply, new ArithmeticExpr(ArithmeticOp.Divide, since, new NumberExpr(every)), new NumberExpr(every));
        return new AllCondition(
        [
            new CompareCondition(day, CompareOp.GreaterOrEqual, new NumberExpr(first)),
            new CompareCondition(new ArithmeticExpr(ArithmeticOp.Subtract, since, periods), CompareOp.Equal, new NumberExpr(0)),
        ]);
    }

    /// <summary>The condition for who may set it off, or null for anyone.</summary>
    public static EventCondition? Who(EventTriggers who)
    {
        var parts = new List<EventCondition>();
        if (who.Players.Count > 0)
        {
            parts.Add(who.Players.Count == 1 ? new PlayerIsCondition(who.Players[0]) : new AnyCondition([.. who.Players.Select(p => new PlayerIsCondition(p))]));
        }

        switch (who.People, who.Computers)
        {
            case (true, false):
                parts.Add(new PlayerIsHumanCondition());
                break;
            case (false, true):
                parts.Add(new NotCondition(new PlayerIsHumanCondition()));
                break;
            case (false, false):
                parts.Add(new AnyCondition([])); // nobody
                break;
        }

        return All([.. parts]);
    }

    private static EventCondition? All(params EventCondition?[] conditions) =>
        conditions.OfType<EventCondition>().SelectMany(c => c is AllCondition { Conditions.Count: > 0 } all ? all.Conditions : [c]).ToList() switch
        {
            [] => null,
            [var one] => one,
            var many => new AllCondition(many),
        };

    private static IReadOnlyList<EventAction> Guarded(EventCondition? condition, IReadOnlyList<EventAction> actions) =>
        condition is null ? actions : [new IfAction(condition, actions)];

    /// <summary>The message with the rewards on it; without a text, the rewards alone.</summary>
    private static IReadOnlyList<EventAction> Show(string message, IReadOnlyList<EventAction> rewards) =>
        string.IsNullOrWhiteSpace(message) ? rewards : [new MessageAction(message) { Rewards = rewards }];
}
