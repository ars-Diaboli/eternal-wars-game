using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>
/// The starting points the editor's "Events" tab offers (EVT-6): a blank event of each kind, an action of every type and the usual conditions, each
/// valid on the map it is made for (ids taken from its catalogs), to be changed afterwards.
/// </summary>
public static class MapEventTemplates
{
    /// <summary>A new event: a hero event shows a message, a quest has one stage (reach level 2; a Quest Gate's quest as it must be), the rest are empty.</summary>
    public static MapEventDefinition Blank(string name, MapEventKind kind, IReadOnlyList<int>? towns = null) => kind switch
    {
        MapEventKind.Hero => new(name, kind, [new MessageAction("Something happens here.")]),
        MapEventKind.Quest => new(name, kind, []) { Stages = [QuestForms.Level(2, "Come back when you have reached level 2.")] },
        MapEventKind.Town => new(name, kind, []) { Towns = towns ?? [] },
        _ => new(name, kind, []),
    };

    /// <summary>One action of every type, with a label for the menu.</summary>
    public static IReadOnlyList<(string Label, EventAction Action)> Actions(GameState state, string? otherEvent = null, string? variable = null)
    {
        var creature = state.Creatures.All.OrderBy(c => c.Tier).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? "creature";
        var line = state.Creatures.All.Where(c => !state.Creatures.IsUpgrade(c.Id)).OrderBy(c => c.Tier).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? creature;
        var artifact = state.Artifacts.All.OrderBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? "artifact";
        var spell = state.Spells.All.OrderBy(s => s.Level).ThenBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? "spell";
        var hero = state.HeroRoster.All.OrderBy(h => h.Id, StringComparer.Ordinal).FirstOrDefault()?.Id ?? "hero";
        var x = variable ?? "x";
        return
        [
            ("Message", new MessageAction("Write the message here.") { Rewards = [new ResourceAction(ResourceType.Gold, new NumberExpr(500))] }),
            ("Question", new QuestionAction("Write the question here?", [new EventAnswer("Yes", []), new EventAnswer("No", [])])),
            ("If", new IfAction(new PlayerIsHumanCondition(), [])),
            ("Repeat", new RepeatAction(new NumberExpr(2), [])),
            ("While", new WhileAction(new CompareCondition(new VariableExpr(x), CompareOp.Less, new NumberExpr(3)),
                [new SetVariableAction(x, VariableOp.Add, new NumberExpr(1))])),
            ("Run another event", new RunEventAction(otherEvent ?? "event")),
            ("Stop", new StopAction()),
            ("Set a variable", new SetVariableAction(x, VariableOp.Add, new NumberExpr(1))),
            ("Disable this event", new EnableEventAction(null, false)),
            ("Remove this cell", new RemoveCellAction()),
            ("Resources", new ResourceAction(ResourceType.Gold, new NumberExpr(500))),
            ("Experience", new ExperienceAction(new NumberExpr(500))),
            ("Primary stat", new PrimaryStatAction(PrimaryStat.Attack, new NumberExpr(1))),
            ("Secondary skill", new SkillAction(BuiltInSkills.Logistics, SkillLevel.Basic)),
            ("Artifact", new ArtifactAction(artifact)),
            ("War machine", new WarMachineAction(WarMachineSlot.Tent)),
            ("Spell book", new SpellBookAction()),
            ("Spell", new SpellAction(spell)),
            ("Mana", new ManaAction(new NumberExpr(10))),
            ("Movement", new MovementAction(new NumberExpr(500))),
            ("Morale and luck", new MoraleAction(1, 0)),
            ("Creatures", new CreaturesAction(creature, new NumberExpr(5))),
            ("Creatures to hire (town)", new AvailableCreaturesAction(line, new NumberExpr(5))),
            ("Build (town)", new BuildAction(BuiltInBuildings.Tavern)),
            ("Battle", new BattleAction([new CreatureStack(creature, 10)])),
            ("Reveal an area", new RevealAction(0, 0, 0, 5)),
            ("Give a hero", new GiveHeroAction(hero)),
        ];
    }

    /// <summary>The usual conditions, for the "insert a condition" menu.</summary>
    public static IReadOnlyList<(string Label, EventCondition Condition)> Conditions(string? variable = null) =>
    [
        ("Compare a variable", new CompareCondition(new VariableExpr(variable ?? "x"), CompareOp.GreaterOrEqual, new NumberExpr(1))),
        ("Hero level", new CompareCondition(new QuantityExpr(Quantity.HeroLevel), CompareOp.GreaterOrEqual, new NumberExpr(5))),
        ("Gold", new CompareCondition(new QuantityExpr(Quantity.Resource, nameof(ResourceType.Gold)), CompareOp.GreaterOrEqual, new NumberExpr(1000))),
        ("Day", new CompareCondition(new QuantityExpr(Quantity.Day), CompareOp.Equal, new NumberExpr(7))),
        ("Random chance (1 in 2)", new CompareCondition(new RandomExpr(1, 2), CompareOp.Equal, new NumberExpr(1))),
        ("The player is a person", new PlayerIsHumanCondition()),
        ("The player is slot 1", new PlayerIsCondition(0)),
        ("All of", new AllCondition([new PlayerIsHumanCondition(), new CompareCondition(new QuantityExpr(Quantity.Day), CompareOp.GreaterOrEqual, new NumberExpr(2))])),
        ("Any of", new AnyCondition([new PlayerIsCondition(0), new PlayerIsCondition(1)])),
        ("Not", new NotCondition(new PlayerIsHumanCondition())),
    ];
}
