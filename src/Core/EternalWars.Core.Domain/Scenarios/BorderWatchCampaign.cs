using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Campaigns;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Scenarios;

/// <summary>
/// The shipped sample campaign (ADR-241, ADR-242; placeholder lore): <b>Border Watch</b>, two small maps for one person, Haven (Blue) against a
/// Necropolis vanguard (Red, the computer). Maren Holt holds the Grey Ford, may tend wounded scouts (the kept variable <c>scouts</c>) and takes
/// the undead outpost; she is carried into the Ashen Gate, where the scouts she saved bring archers and taking the gate wins the campaign. Each map
/// also plays on its own (its hero placeholder is then a random Haven hero).
/// </summary>
public static class BorderWatchCampaign
{
    public const string Name = "Border Watch";

    public const string GreyFord = "Border Watch 1: The Grey Ford";

    public const string AshenGate = "Border Watch 2: The Ashen Gate";

    /// <summary>The campaign's hero, carried from the first map into the second.</summary>
    public const string Maren = "haven.maren";

    /// <summary>The campaign variable: 1 when Maren tended the scouts at the ford.</summary>
    public const string Scouts = "scouts";

    public static Campaign Campaign { get; } = new(
        Name,
        "A sample campaign of two maps: hold the Grey Ford against the Ashen Court's vanguard, then carry the fight to the Ashen Gate. Placeholder story.",
        [
            new CampaignMap(GreyFord, "The Grey Ford")
            {
                Intro = "The Ashen Court stirs beyond the Grey Ford. Haven sends Maren Holt to hold the crossing and burn the outpost the dead have raised on our side of the river.",
                Outro = "The outpost burns. Beyond the river the road climbs towards the Ashen Gate, where the vanguard gathers.",
            },
            new CampaignMap(AshenGate, "The Ashen Gate")
            {
                Intro = "Maren crosses the river with what is left of her company. The Ashen Gate must fall before the Court's main host arrives.",
                Outro = "The Ashen Gate is ours. The border holds, for now.",
                CarryHeroes = [Maren],
                KeepArmy = true,
            },
        ]);

    /// <summary>The campaign's maps by name (<see cref="CampaignMap.Map"/>).</summary>
    public static IReadOnlyList<string> Maps { get; } = [GreyFord, AshenGate];

    /// <summary>The map of that name.</summary>
    public static GameState CreateMap(string map, ContentCatalogs? content = null) => map switch
    {
        GreyFord => CreateGreyFord(content ?? ContentCatalogs.Default),
        AshenGate => CreateAshenGate(content ?? ContentCatalogs.Default),
        _ => throw new ArgumentOutOfRangeException(nameof(map), map, "No such map in Border Watch."),
    };

    private static readonly PlayerId Blue = new(0);
    private static readonly PlayerId Red = new(1);

    private static Player[] Players() => [new(Blue, "Blue", isHuman: true), new(Red, "Red", isHuman: false)];

    /// <summary>
    /// 28 × 16: Blue's fort in the west, the undead outpost in the east, a river between them crossed at the ford (rows 8–10), where the wounded
    /// scouts lie and a vanguard of skeletons waits.
    /// </summary>
    private static GameState CreateGreyFord(ContentCatalogs content)
    {
        var rows = Enumerable.Range(0, 16)
            .Select(y => string.Concat(Enumerable.Range(0, 28).Select(x => x is 13 or 14 && y is not (8 or 9 or 10) ? '~' : x > 15 ? (y + x) % 7 == 0 ? 'w' : 'd' : (x * 3 + y) % 11 == 0 ? 'r' : 'g')))
            .ToList();
        var sketch = new MapSketch(content, GreyFord, "Hold the Grey Ford and take the undead outpost beyond it.", rows);
        Road(sketch.Map, 9, 2, 25);
        sketch.Map.SetRoad(new MapPosition(4, 8), RoadType.Dirt);
        sketch.Map.SetRoad(new MapPosition(23, 8), RoadType.Dirt);
        Settle(sketch.AddTown(TownIds.Haven, 1, 1, Blue, "Greywatch"), content);
        var outpost = Settle(sketch.AddTown(TownIds.Necropolis, 20, 1, Red, "Ashen Outpost"), content);
        outpost.Garrison.Set(0, new CreatureStack("necropolis.skeleton", 18));
        outpost.Garrison.Set(1, new CreatureStack("necropolis.ghoul", 4));
        sketch.Monster("necropolis.skeleton", 12, 17, 9);
        sketch.Pile(ResourceType.Gold, 6, 12, 750);
        sketch.Pile(ResourceType.Lumber, 9, 4, 6);
        sketch.Pile(ResourceType.Ore, 22, 12, 6);
        var maren = sketch.AddHero(1, Blue, 5, 9, Maren, Army.Of(new("haven.pikeman", 24), new("haven.archer", 10)));

        var state = sketch.Build(Players(), [maren]);
        state.SetMapEvents(new MapEventSet
        {
            Variables = new Dictionary<string, int> { [Scouts] = 0 },
            KeptVariables = [Scouts],
            Cells = [new EventCell(11, 9, 0, "scouts")],
            Events =
            [
                new MapEventDefinition("briefing", MapEventKind.Player,
                [
                    new IfAction(new AllCondition([new PlayerIsHumanCondition(), Day(1)]),
                    [
                        new DialogueAction(
                        [
                            new DialogueLine(Maren, "The river is low and the dead do not sleep. Hold the ford, then burn their outpost."),
                            new DialogueLine("", "Scouts sent ahead last night have not come back."),
                        ]),
                        new EnableEventAction(null, false),
                    ]),
                ]),
                new MapEventDefinition("scouts", MapEventKind.Hero,
                [
                    new IfAction(new PlayerIsHumanCondition(),
                    [
                        new QuestionAction("Wounded scouts lie in the reeds by the ford. Stop to tend them? The day is short.",
                        [
                            new EventAnswer("Tend them", [new SetVariableAction(Scouts, VariableOp.Set, new NumberExpr(1)), new MovementAction(new NumberExpr(-500)),
                                new MessageAction("The scouts will live. They promise to find you again beyond the river.")]),
                            new EventAnswer("Ride on", []),
                        ]),
                        new RemoveCellAction(),
                    ]),
                ]),
                new MapEventDefinition("outpost taken", MapEventKind.Captured,
                [
                    new IfAction(new PlayerIsCondition(Blue.Value),
                    [
                        new DialogueAction([new DialogueLine(Maren, "Burn it. Nothing of theirs stays on our side of the river.")]),
                        new EndGameAction(Won: true) { Text = "The Ashen Outpost burns: the Grey Ford is held." },
                    ]),
                ]) { Towns = [outpost.Id.Value] },
            ],
        });
        return state;
    }

    /// <summary>
    /// 28 × 16: Blue's camp in the west with a hero placeholder (Maren, carried in), the Ashen Gate in the south-east and its keeper Morthal in the
    /// north; the scouts saved at the ford bring archers to the camp on day 1.
    /// </summary>
    private static GameState CreateAshenGate(ContentCatalogs content)
    {
        var rows = Enumerable.Range(0, 16)
            .Select(y => string.Concat(Enumerable.Range(0, 28).Select(x => x < 10 ? (x + y) % 9 == 0 ? 'r' : 'g' : x < 18 ? 'd' : (x * 5 + y) % 6 == 0 ? 'l' : 'w')))
            .ToList();
        var sketch = new MapSketch(content, AshenGate, "Take the Ashen Gate before the Court's host arrives.", rows);
        Road(sketch.Map, 15, 4, 23);
        for (var y = 8; y < 15; y++)
        {
            sketch.Map.SetRoad(new MapPosition(4, y), RoadType.Dirt);
        }
        var camp = Settle(sketch.AddTown(TownIds.Haven, 1, 1, Blue, "Ford Camp"), content);
        var gate = Settle(sketch.AddTown(TownIds.Necropolis, 20, 8, Red, "Ashen Gate"), content);
        gate.Garrison.Set(0, new CreatureStack("necropolis.skeleton", 30));
        gate.Garrison.Set(1, new CreatureStack("necropolis.ghoul", 8));
        sketch.Monster("necropolis.ghoul", 6, 14, 12);
        sketch.Pile(ResourceType.Gold, 8, 13, 1000);
        sketch.Pile(ResourceType.Sulfur, 17, 2, 4);
        var maren = sketch.AddRandomHero(1, Blue, 5, 9, Army.Of(new CreatureStack("haven.pikeman", 12)));
        var morthal = sketch.AddHero(2, Red, 21, 4, "necropolis.morthal", Army.Of(new("necropolis.skeleton", 26), new("necropolis.ghoul", 6)));

        var state = sketch.Build(Players(), [maren, morthal]);
        state.SetMapEvents(new MapEventSet
        {
            Variables = new Dictionary<string, int> { [Scouts] = 0 },
            TakenVariables = [Scouts],
            Events =
            [
                new MapEventDefinition("arrival", MapEventKind.Player,
                [
                    new IfAction(new AllCondition([new PlayerIsHumanCondition(), Day(1)]),
                    [
                        new DialogueAction([new DialogueLine("", "Ash falls like snow on the road to the gate."), new DialogueLine(Maren, "Make camp. We strike before their host arrives.")]),
                        new EnableEventAction(null, false),
                    ]),
                ]),
                new MapEventDefinition("scouts return", MapEventKind.Town,
                [
                    new IfAction(new AllCondition([Day(1), new CompareCondition(new VariableExpr(Scouts), CompareOp.GreaterOrEqual, new NumberExpr(1))]),
                    [
                        new DialogueAction([new DialogueLine("Scout", "You stopped for us at the ford. We brought friends.")]),
                        new CreaturesAction("haven.archer", new NumberExpr(12)),
                    ]),
                    new EnableEventAction(null, false),
                ]) { Towns = [camp.Id.Value] },
                new MapEventDefinition("morthal falls", MapEventKind.Battle,
                [
                    new IfAction(new HeroDefeatedCondition(morthal.Id.Value, Blue.Value),
                    [
                        new DialogueAction([new DialogueLine(Maren, "Their keeper is dust. Now the gate.")]),
                        new EnableEventAction(null, false),
                    ]),
                ]),
                new MapEventDefinition("gate taken", MapEventKind.Captured,
                [
                    new IfAction(new PlayerIsCondition(Blue.Value),
                    [
                        new DialogueAction([new DialogueLine(Maren, "Raise our banner over the gate. Let the Court see it.")]),
                        new EndGameAction(Won: true) { Text = "The Ashen Gate has fallen." },
                    ]),
                ]) { Towns = [gate.Id.Value] },
            ],
        });
        return state;
    }

    private static CompareCondition Day(int day) => new(new QuantityExpr(Quantity.Day), CompareOp.Equal, new NumberExpr(day));

    private static void Road(AdventureMap map, int y, int from, int to)
    {
        for (var x = from; x <= to; x++)
        {
            map.SetRoad(new MapPosition(x, y), RoadType.Dirt);
        }
    }

    /// <summary>A town with its tier-1 dwellings built and a week's creatures in them.</summary>
    private static Town Settle(Town town, ContentCatalogs content)
    {
        foreach (var line in content.Creatures.BaseCreaturesOf(town.TownType).Where(l => l.Tier == 1))
        {
            town.SetDwelling(line.Id, 1);
            town.SetAvailable(line.Id, TownRules.WeeklyGrowth(content.Buildings, town, line));
        }

        return town;
    }
}
