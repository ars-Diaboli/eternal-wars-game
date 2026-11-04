using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>
/// One readable line for each node of an event tree (the editor's "Events" tab, EVT-6): "If hero level ≥ 5", "Give 500 Gold", "Message: …". With a
/// game state the ids of creatures, artifacts, spells and objects become their names.
/// </summary>
public static class MapEventText
{
    public static string Describe(MapEventDefinition definition) =>
        $"{definition.Name} ({definition.Kind.ToString().ToLowerInvariant()}{(definition.Enabled ? "" : ", disabled")})";

    public static string Describe(EventAction action, GameState? state = null)
    {
        string Expr(EventExpr expr) => Describe(expr);
        string Creature(string id) => state is not null && state.Creatures.TryGet(id, out var creature) ? creature.Name : id;
        string Artifact(string id) => state is not null && state.Artifacts.TryGet(id, out var artifact) ? artifact.Name : id;
        string Gain(EventExpr amount, string what) => amount is NumberExpr { Value: < 0 } taken ? $"Take {-taken.Value} {what}" : $"Give {Expr(amount)} {what}";
        return action switch
        {
            IfAction branch => $"If {Describe(branch.Condition, state)}",
            StopAction => "Stop this event",
            RepeatAction repeat => $"Repeat {Expr(repeat.Times)} times",
            WhileAction loop => $"While {Describe(loop.Condition, state)}",
            RunEventAction run => $"Run the event '{run.Event}'",
            SetVariableAction set => set.Op switch
            {
                VariableOp.Set => $"{set.Variable} = {Expr(set.Value)}",
                VariableOp.Add => $"{set.Variable} += {Expr(set.Value)}",
                VariableOp.Subtract => $"{set.Variable} −= {Expr(set.Value)}",
                VariableOp.Multiply => $"{set.Variable} ×= {Expr(set.Value)}",
                _ => $"{set.Variable} ÷= {Expr(set.Value)}",
            },
            EnableEventAction enable => $"{(enable.Enabled ? "Enable" : "Disable")} {(enable.Event is null ? "this event" : $"the event '{enable.Event}'")}",
            RemoveCellAction => "Remove this cell",
            MessageAction message => $"Message: \"{Short(message.Text)}\"",
            QuestionAction question => $"Ask: \"{Short(question.Text)}\"",
            ResourceAction resource => Gain(resource.Amount, resource.Resource.ToString()),
            ExperienceAction experience => $"Give {Expr(experience.Amount)} experience",
            PrimaryStatAction stat => $"Give {Expr(stat.Amount)} {stat.Stat}",
            SkillAction skill => $"Raise {(BuiltInSkills.Contains(skill.Skill) ? BuiltInSkills.Get(skill.Skill).Name : skill.Skill)} to {skill.Level}",
            ArtifactAction artifact => $"{(artifact.Take ? "Take" : "Give")} {Artifact(artifact.Artifact)}",
            WarMachineAction machine => $"{(machine.Take ? "Take" : "Give")} the {machine.Slot}",
            SpellBookAction => "Give a spell book",
            SpellAction spell => $"Teach {(state is not null && state.Spells.TryGet(spell.Spell, out var taught) ? taught.Name : spell.Spell)}",
            ManaAction mana => Gain(mana.Amount, "mana"),
            MovementAction movement => Gain(movement.Amount, "movement"),
            MoraleAction morale => $"Morale {morale.Morale:+0;−0;0}, luck {morale.Luck:+0;−0;0} until the next battle",
            CreaturesAction creatures => Gain(creatures.Count, Creature(creatures.Creature)),
            AvailableCreaturesAction available => $"{Expr(available.Count)} more {Creature(available.Line)} to hire",
            BuildAction build => $"Build {build.Building}{(build.Level > 1 ? $" up to level {build.Level}" : "")}",
            BattleAction battle => $"Fight {string.Join(", ", battle.Army.Select(s => $"{s.Count} {Creature(s.Creature)}"))}",
            RevealAction reveal => $"Reveal radius {reveal.Radius} around ({reveal.X}, {reveal.Y}{(reveal.Level > 0 ? $", level {reveal.Level + 1}" : "")})",
            GiveHeroAction give => $"Give the hero {(state is not null && state.HeroRoster.Contains(give.Hero) ? state.HeroRoster.Get(give.Hero).Name : give.Hero)}",
            _ => action.GetType().Name,
        };
    }

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
        TownOwnerCondition owner => $"town {owner.Town} {(owner.Owner is { } slot ? $"belongs to player {slot + 1}" : "is neutral")}",
        MonsterDefeatedCondition monster => $"monster {monster.Monster} was beaten by {(monster.Player is { } slot ? $"player {slot + 1}" : "the player")}",
        HeroDefeatedCondition hero => $"hero {hero.Hero} was beaten by {(hero.Player is { } slot ? $"player {slot + 1}" : "the player")}",
        _ => condition.GetType().Name,
    };

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

    private static string Short(string text)
    {
        var line = text.ReplaceLineEndings(" ");
        return line.Length <= 48 ? line : line[..47] + "…";
    }
}
