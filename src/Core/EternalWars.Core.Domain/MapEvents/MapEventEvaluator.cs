using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>Who and what an event's expressions and conditions read: its player, hero and town (each may be missing).</summary>
internal readonly record struct EventScope(PlayerId? Player, Hero? Hero, MapObjectId? Town);

/// <summary>
/// Evaluates the expressions and conditions of the map events (EVT-3). Only a <see cref="RandomExpr"/> draws from <see cref="GameState.Random"/>; a
/// quest's requirement has none, so routes may test it (<see cref="Objects.QuestRules"/>).
/// </summary>
internal static class MapEventEvaluator
{
    public static int Evaluate(GameState state, EventScope scope, EventExpr expr) => expr switch
    {
        NumberExpr number => number.Value,
        VariableExpr variable => state.MapEvents.Variable(variable.Name),
        RandomExpr random => state.Random.NextInclusive(Math.Min(random.Min, random.Max), Math.Max(random.Min, random.Max)),
        ArithmeticExpr arithmetic => Combine(arithmetic.Op switch
        {
            ArithmeticOp.Add => VariableOp.Add,
            ArithmeticOp.Subtract => VariableOp.Subtract,
            ArithmeticOp.Multiply => VariableOp.Multiply,
            _ => VariableOp.Divide,
        }, Evaluate(state, scope, arithmetic.Left), Evaluate(state, scope, arithmetic.Right)),
        QuantityExpr quantity => Read(state, scope, quantity),
        _ => 0,
    };

    public static int Combine(VariableOp op, int left, int right) => (int)Math.Clamp(op switch
    {
        VariableOp.Set => right,
        VariableOp.Add => (long)left + right,
        VariableOp.Subtract => (long)left - right,
        VariableOp.Multiply => (long)left * right,
        _ => right == 0 ? 0 : (long)left / right,
    }, int.MinValue, int.MaxValue);

    public static bool Test(GameState state, EventScope scope, EventCondition condition)
    {
        var hero = scope.Hero;
        var player = scope.Player is { } id ? state.GetPlayer(id) : null;
        var events = state.MapEvents;
        return condition switch
        {
            AllCondition all => all.Conditions.All(c => Test(state, scope, c)),
            AnyCondition any => any.Conditions.Any(c => Test(state, scope, c)),
            NotCondition not => !Test(state, scope, not.Condition),
            CompareCondition compare => Compare(Evaluate(state, scope, compare.Left), compare.Op, Evaluate(state, scope, compare.Right)),
            HeroIsCondition named => hero?.Definition == named.Hero,
            HeroClassCondition kind => hero is not null && hero.Class == kind.Class && (kind.Town is null || ProgressionService.TownOf(state, hero) == kind.Town),
            PlayerIsCondition slot => player?.Id.Value == slot.Player,
            PlayerIsHumanCondition => player?.IsHuman == true,
            FactionCondition faction => player is not null && player.StartTown == faction.Town,
            TeamCondition team => player?.Team.Value == team.Team,
            AllyCondition ally => player is not null && state.HasPlayer(new PlayerId(ally.Player)) && state.AreAllies(player.Id, new PlayerId(ally.Player)),
            TownOwnerCondition owner => state.FindObject(new MapObjectId(owner.Town)) is Town town && town.Owner?.Value == owner.Owner,
            MonsterDefeatedCondition monster => events.MonstersDefeated.TryGetValue(new MapObjectId(monster.Monster), out var by) && by.Value == (monster.Player ?? player?.Id.Value),
            HeroDefeatedCondition beaten => events.HeroesDefeated.TryGetValue(new HeroId(beaten.Hero), out var winner) && winner.Value == (beaten.Player ?? player?.Id.Value),
            _ => false,
        };
    }

    private static bool Compare(int left, CompareOp op, int right) => op switch
    {
        CompareOp.Equal => left == right,
        CompareOp.NotEqual => left != right,
        CompareOp.Less => left < right,
        CompareOp.LessOrEqual => left <= right,
        CompareOp.Greater => left > right,
        _ => left >= right,
    };

    private static int Read(GameState state, EventScope scope, QuantityExpr quantity)
    {
        var hero = scope.Hero;
        var player = scope.Player is { } id ? state.GetPlayer(id) : null;
        return quantity.Quantity switch
        {
            Quantity.Resource => player is not null && Enum.TryParse<ResourceType>(quantity.Id, ignoreCase: true, out var resource) ? player.Resources[resource] : 0,
            Quantity.Creatures => (hero?.Army ?? (scope.Town is { } town && state.FindObject(town) is Town t ? t.Garrison : null))?.Stacks
                .Where(s => s.Stack.Creature == quantity.Id).Sum(s => s.Stack.Count) ?? 0,
            Quantity.HeroLevel => hero?.Level ?? 0,
            Quantity.Experience => hero?.Experience ?? 0,
            Quantity.Attack => hero?.Attack ?? 0,
            Quantity.Defense => hero?.Defense ?? 0,
            Quantity.SpellPower => hero?.SpellPower ?? 0,
            Quantity.Knowledge => hero?.Knowledge ?? 0,
            Quantity.Mana => hero?.Mana ?? 0,
            Quantity.MovementPoints => hero?.MovementPoints ?? 0,
            Quantity.SkillLevel => hero is not null && quantity.Id is { } skill ? (int)hero.Skills.Level(skill) : 0,
            Quantity.Artifacts => hero is null ? 0 : hero.Artifacts.AllWorn.Count(w => w.Id == quantity.Id) + hero.Artifacts.Backpack.Count(a => a == quantity.Id),
            Quantity.Day => state.Date.AbsoluteDay,
            Quantity.DayOfWeek => state.Date.Day,
            Quantity.Week => state.Date.Week,
            Quantity.Month => state.Date.Month,
            Quantity.Difficulty => (int)(state.Options.Complexity ?? Complexity.Normal),
            Quantity.Heroes => player is null ? 0 : state.HeroesOf(player.Id).Count(),
            Quantity.Towns => player is null ? 0 : state.ObjectsOf<Town>().Count(t => t.Owner == player.Id),
            _ => 0,
        };
    }
}
