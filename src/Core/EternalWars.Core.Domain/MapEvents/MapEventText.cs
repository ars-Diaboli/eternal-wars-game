using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents.Actions;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>
/// One readable line for each node of an event tree (the editor's "Events" tab, EVT-6): "If hero level ≥ 5", "Give 500 Gold", "Message: …". With a
/// game state the ids of creatures, artifacts, spells and objects become their names.
/// </summary>
public static class MapEventText
{
    public static string Describe(MapEventDefinition definition) =>
        $"{definition.Name} ({definition.Kind.ToString().ToLowerInvariant()}{(definition.Enabled ? "" : ", disabled")})";

    /// <summary>The action's line, its handler's (ADR-237).</summary>
    public static string Describe(EventAction action, GameState? state = null) => MapEventActionHandlers.For(action).Line(action, state);

    public static string Describe(EventCondition condition, GameState? state = null) => condition switch
    {
        AllCondition { Conditions.Count: 0 } => "always",
        AnyCondition { Conditions.Count: 0 } => "never",
        AllCondition all => string.Join(" and ", all.Conditions.Select(c => Grouped(c, state))),
        AnyCondition any => string.Join(" or ", any.Conditions.Select(c => Grouped(c, state))),
        NotCondition not => $"not {Grouped(not.Condition, state)}",
        CompareCondition compare => $"{Describe(compare.Left)} {Symbol(compare.Op)} {Describe(compare.Right)}",
        HeroIsCondition hero => $"the hero is {(state is not null && state.HeroRoster.Contains(hero.Hero) ? state.HeroRoster.Get(hero.Hero).Name : hero.Hero)}",
        HeroClassCondition kind => $"the hero is a {kind.Class}{(kind.Town is null ? "" : $" of {kind.Town}")}",
        PlayerIsCondition player => $"the player is player {player.Player + 1}",
        PlayerIsHumanCondition => "the player is a person",
        FactionCondition faction => $"the player started as {faction.Town}",
        TeamCondition team => $"the player is on team {team.Team + 1}",
        AllyCondition ally => $"the player is an ally of player {ally.Player + 1}",
        TownOwnerCondition owner => $"town {Object(owner.Town, state)} {(owner.Owner is { } slot ? $"belongs to player {slot + 1}" : "is neutral")}",
        MonsterDefeatedCondition monster => $"monster {Object(monster.Monster, state)} was beaten by {(monster.Player is { } slot ? $"player {slot + 1}" : "the player")}",
        HeroDefeatedCondition hero => $"hero {hero.Hero} was beaten by {(hero.Player is { } slot ? $"player {slot + 1}" : "the player")}",
        _ => condition.GetType().Name,
    };

    /// <summary>An object a condition names by id: with a game state, its name and id when it has a name (ADR-257), else the id.</summary>
    private static string Object(int id, GameState? state) =>
        state?.FindObject(new Common.MapObjectId(id))?.Name is { } name ? $"{name} (#{id})" : id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string Describe(EventExpr expr) => expr switch
    {
        NumberExpr number => number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        VariableExpr variable => variable.Name,
        RandomExpr random => $"random {random.Min}…{random.Max}",
        ArithmeticExpr arithmetic => $"({Describe(arithmetic.Left)} {arithmetic.Op switch
        {
            ArithmeticOp.Add => "+",
            ArithmeticOp.Subtract => "−",
            ArithmeticOp.Multiply => "×",
            _ => "÷",
        }} {Describe(arithmetic.Right)})",
        QuantityExpr quantity => quantity.Quantity switch
        {
            Quantity.Resource => $"the player's {quantity.Id}",
            Quantity.Creatures => $"{quantity.Id} in the army",
            Quantity.SkillLevel => $"{quantity.Id} level",
            Quantity.Artifacts => $"{quantity.Id} carried",
            Quantity.HeroLevel => "hero level",
            Quantity.DayOfWeek => "day of the week",
            Quantity.MovementPoints => "movement",
            Quantity.SpellPower => "spell power",
            var other => other.ToString().ToLowerInvariant(),
        },
        _ => expr.GetType().Name,
    };

    private static string Grouped(EventCondition condition, GameState? state) =>
        condition is AllCondition { Conditions.Count: > 1 } or AnyCondition { Conditions.Count: > 1 } ? $"({Describe(condition, state)})" : Describe(condition, state);

    private static string Symbol(CompareOp op) => op switch
    {
        CompareOp.Equal => "=",
        CompareOp.NotEqual => "≠",
        CompareOp.Less => "<",
        CompareOp.LessOrEqual => "≤",
        CompareOp.Greater => ">",
        _ => "≥",
    };

    /// <summary>A text on one line, cut to 48 characters.</summary>
    internal static string Short(string text)
    {
        var line = text.ReplaceLineEndings(" ");
        return line.Length <= 48 ? line : line[..47] + "…";
    }
}
