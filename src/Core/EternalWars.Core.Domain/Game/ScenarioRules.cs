using System.Globalization;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game;

/// <summary>What a <see cref="ScenarioCondition"/> asks for (ADR-057).</summary>
public enum ConditionKind
{
    /// <summary>Victory: the player's team owns <see cref="ScenarioCondition.Town"/>.</summary>
    CaptureTown,

    /// <summary>Victory: <see cref="ScenarioCondition.Hero"/> is gone, and the player is not on its owner's team.</summary>
    DefeatHero,

    /// <summary>Victory: the player holds at least <see cref="ScenarioCondition.Amount"/> of <see cref="ScenarioCondition.Resource"/>.</summary>
    GatherResources,

    /// <summary>Victory: the calendar reached absolute day <see cref="ScenarioCondition.Day"/> with the player still in the game.</summary>
    SurviveUntilDay,

    /// <summary>Defeat: the player no longer owns <see cref="ScenarioCondition.Town"/>.</summary>
    LoseTown,

    /// <summary>Defeat: <see cref="ScenarioCondition.Hero"/> is gone.</summary>
    LoseHero,

    /// <summary>Defeat: the calendar passed absolute day <see cref="ScenarioCondition.Day"/> without the player winning.</summary>
    TimeLimit,
}

/// <summary>
/// One victory or defeat condition of a scenario (ADR-057). Which fields matter depends on <see cref="Kind"/>.
/// <see cref="Player"/> restricts the condition to one player; without it the condition applies to every player
/// (defeat conditions that name a town or hero then apply to its starting owner: the editor fills it in).
/// </summary>
public sealed record ScenarioCondition(
    ConditionKind Kind,
    PlayerId? Player = null,
    MapObjectId? Town = null,
    HeroId? Hero = null,
    ResourceType Resource = ResourceType.Gold,
    int Amount = 0,
    int Day = 0)
{
    public bool IsVictory => Kind is ConditionKind.CaptureTown or ConditionKind.DefeatHero or ConditionKind.GatherResources or ConditionKind.SurviveUntilDay;

    public static ScenarioCondition CaptureTown(MapObjectId town, PlayerId? player = null) => new(ConditionKind.CaptureTown, player, Town: town);

    public static ScenarioCondition DefeatHero(HeroId hero, PlayerId? player = null) => new(ConditionKind.DefeatHero, player, Hero: hero);

    public static ScenarioCondition GatherResources(ResourceType resource, int amount, PlayerId? player = null) =>
        new(ConditionKind.GatherResources, player, Resource: resource, Amount: amount);

    public static ScenarioCondition SurviveUntilDay(int day, PlayerId? player = null) => new(ConditionKind.SurviveUntilDay, player, Day: day);

    public static ScenarioCondition LoseTown(MapObjectId town, PlayerId player) => new(ConditionKind.LoseTown, player, Town: town);

    public static ScenarioCondition LoseHero(HeroId hero, PlayerId player) => new(ConditionKind.LoseHero, player, Hero: hero);

    public static ScenarioCondition TimeLimit(int day, PlayerId? player = null) => new(ConditionKind.TimeLimit, player, Day: day);
}

/// <summary>
/// How a scenario is won and lost (ADR-057), set by the map author in the editor and stored in the map. A game ends
/// when one team wins; the first victory condition that holds decides, in turn order.
/// </summary>
/// <param name="Victory">Special victory conditions: any one of them wins the game for the player's team.</param>
/// <param name="Defeat">Special defeat conditions: any one of them eliminates the player.</param>
/// <param name="StandardVictory">The last team standing wins (the default victory: defeat every enemy).</param>
/// <param name="Elimination">A player with no towns and no heroes is eliminated (the default defeat).</param>
/// <param name="GraceDays">
/// HoMM3's rule: a player that owned a town and has none any more is eliminated after this many days unless it owns a
/// town again; 0 turns the countdown off. Needs <see cref="Elimination"/>.
/// </param>
/// <param name="MaxHeroes">
/// How many heroes a player may have on the map at once, 1 to <see cref="HeroLimit"/> (ADR-068); hiring and prisons respect it.
/// </param>
public sealed record ScenarioRules(
    IReadOnlyList<ScenarioCondition> Victory,
    IReadOnlyList<ScenarioCondition> Defeat,
    bool StandardVictory = true,
    bool Elimination = true,
    int GraceDays = ScenarioRules.DefaultGraceDays,
    int MaxHeroes = ScenarioRules.HeroLimit)
{
    public const int DefaultGraceDays = 7;

    /// <summary>The most heroes a map may allow a player (and the default).</summary>
    public const int HeroLimit = 8;

    /// <summary>The default: defeat every enemy, no towns and no heroes means defeat.</summary>
    public static ScenarioRules Default { get; } = new([], []);

    /// <summary>A sandbox: nobody wins or is eliminated, the game never ends (tests, experiments).</summary>
    public static ScenarioRules Endless { get; } = new([], [], StandardVictory: false, Elimination: false);

    /// <summary>The week and month events (ADR-073, ADR-075): on by default, from the second week, creatures of the playable towns.</summary>
    public Turns.WeekEventSettings WeekEvents { get; init; } = Turns.WeekEventSettings.Default;

    public bool IsDefault => Victory.Count == 0 && Defeat.Count == 0 && StandardVictory && Elimination && GraceDays == DefaultGraceDays
        && MaxHeroes == HeroLimit && WeekEvents.IsDefault;

    /// <summary>Whether a scenario with these rules can be won at all.</summary>
    public bool CanBeWon => StandardVictory || Victory.Count > 0;

    /// <summary>A short sentence for a condition, for the editor and the game's result screen.</summary>
    public static string Describe(GameState state, ScenarioCondition condition)
    {
        var what = condition.Kind switch
        {
            ConditionKind.CaptureTown => $"Capture {TownName(state, condition.Town)}",
            ConditionKind.DefeatHero => $"Defeat {HeroName(state, condition.Hero)}",
            ConditionKind.GatherResources => $"Gather {condition.Amount.ToString(CultureInfo.InvariantCulture)} {condition.Resource.ToString().ToLowerInvariant()}",
            ConditionKind.SurviveUntilDay => $"Survive until day {condition.Day.ToString(CultureInfo.InvariantCulture)}",
            ConditionKind.LoseTown => $"Lose {TownName(state, condition.Town)}",
            ConditionKind.LoseHero => $"Lose {HeroName(state, condition.Hero)}",
            ConditionKind.TimeLimit => $"Fail to win by day {condition.Day.ToString(CultureInfo.InvariantCulture)}",
            _ => condition.Kind.ToString(),
        };
        return condition.Player is { } player && state.HasPlayer(player) ? $"{state.GetPlayer(player).Name}: {what}" : what;
    }

    /// <summary>What a player achieved with a victory condition: the reason a game ended.</summary>
    internal static string Achieved(GameState state, Player player, ScenarioCondition condition, string? heroName) => condition.Kind switch
    {
        ConditionKind.CaptureTown => $"{player.Name} captured {TownName(state, condition.Town)}.",
        ConditionKind.DefeatHero => $"{player.Name} defeated {heroName ?? HeroName(state, condition.Hero)}.",
        ConditionKind.GatherResources => $"{player.Name} gathered {condition.Amount.ToString(CultureInfo.InvariantCulture)} {condition.Resource.ToString().ToLowerInvariant()}.",
        _ => $"{player.Name} survived until day {condition.Day.ToString(CultureInfo.InvariantCulture)}.",
    };

    /// <summary>Why a defeat condition eliminated its player.</summary>
    internal static string Lost(GameState state, ScenarioCondition condition, string? heroName) => condition.Kind switch
    {
        ConditionKind.LoseTown => $"Lost {TownName(state, condition.Town)}.",
        ConditionKind.LoseHero => $"Lost {heroName ?? HeroName(state, condition.Hero)}.",
        _ => $"Ran out of time (day {condition.Day.ToString(CultureInfo.InvariantCulture)}).",
    };

    private static string TownName(GameState state, MapObjectId? id) =>
        id is { } town && state.FindObject(town) is Towns.Town found ? found.Name : "a town";

    private static string HeroName(GameState state, HeroId? id) =>
        id is { } hero && state.Heroes.FirstOrDefault(h => h.Id == hero) is { } found ? found.Name : "a hero";
}

/// <summary>How a game ended (<see cref="GameState.Outcome"/>).</summary>
/// <param name="Winners">The surviving players of the winning team; empty when nobody won.</param>
/// <param name="Reason">What decided it, in words.</param>
/// <param name="Date">The day it ended.</param>
public sealed record GameOutcome(IReadOnlyList<PlayerId> Winners, string Reason, GameDate Date)
{
    public bool HasWinner => Winners.Count > 0;

    public bool Won(PlayerId player) => Winners.Contains(player);
}
