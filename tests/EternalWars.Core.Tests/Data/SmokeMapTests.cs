using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Data;

/// <summary>The maps under <c>tools/smoke/</c> load, and do what the scripted input of <c>tools/smoke-godot.ps1</c> expects.</summary>
public class SmokeMapTests
{
    [Fact]
    public void BattleMap_FirstStepEastStartsAFightTheHumanCommands()
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(SmokeMap("battle.ewmap.json"))));
        session.Start();

        session.Step(new HeroId(1), Direction.East);

        Assert.Equal(BattlePhase.Fighting, session.State.Battle?.Phase);
        Assert.NotNull(session.Battles.Commander(BattleSide.Attacker));
    }

    [Fact]
    public void SiegeMap_FirstStepNorthStartsASiegeOfTheCastleWithTheBallista()
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(SmokeMap("siege.ewmap.json"))));
        session.Start();

        session.Step(new HeroId(1), Direction.North);

        var battle = session.State.Battle;
        Assert.Equal(BattlePhase.Fighting, battle?.Phase);
        Assert.NotNull(battle!.Siege);
        Assert.Contains(battle.Siege!.Structures, s => s.Kind == SiegeStructureKind.Keep);
        Assert.Contains(battle.Siege.Structures, s => s.Kind == SiegeStructureKind.Tower);
        Assert.Contains(battle.LivingUnits, u => u.Side == BattleSide.Attacker && u.Machine is not null);
    }

    [Fact]
    public void TownsMap_FirstStepNorthVisitsTheOwnTownWhoseGarrisonTheHeroCanEnter()
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(SmokeMap("towns.ewmap.json"))));
        session.Start();

        session.Step(new HeroId(1), Direction.North);

        var town = session.State.ObjectsOf<Town>().Single(t => t.Name == "Castlebridge");
        Assert.Equal(new HeroId(1), TownRules.VisitingHero(session.State, town)?.Id);
        Assert.Null(TownRules.CheckEnterGarrison(session.State, town));
    }

    [Fact]
    public void CaravanMap_FirstStepNorthVisitsADwelling_AndTheTownCanHireFromTheOwnedOneByCaravan()
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(SmokeMap("caravan.ewmap.json"))));
        session.Start();

        session.Step(new HeroId(1), Direction.North);

        var visited = session.State.ObjectsOf<CreatureDwelling>().Single(d => d.Creature == "haven.pikeman");
        Assert.Equal(session.State.CurrentPlayer.Id, visited.Owner);
        var town = session.State.ObjectsOf<Town>().Single();
        var owned = session.State.ObjectsOf<CreatureDwelling>().Single(d => d.Creature == "haven.footman");
        Assert.Contains(owned, CaravanRules.Dwellings(session.State, town));
        Assert.Null(CaravanRules.CheckOrder(session.State, town, owned, owned.Available!.Value));
    }

    [Fact]
    public void ConnectionsTemplate_GeneratesEveryV7ConnectionObject()
    {
        var template = TemplateSerializer.Load(SmokeMap("connections.ewtemplate.json"));
        Assert.DoesNotContain(TemplateValidator.Validate(template), i => i.Severity == IssueSeverity.Error);

        var map = MapGenerator.Generate(template, 72, 1);

        Assert.Empty(map.Problems);
        Assert.Single(map.Objects.OfType<MapGarrison>());
        Assert.Equal(2, map.Objects.OfType<BorderPost>().Count());
        Assert.Equal(2, map.Objects.OfType<KeymasterTent>().Count());
        Assert.Equal(2, map.Objects.Count(o => o.Definition.Id == BuiltInMapObjects.TwoWayMonolith));
    }

    private static string SmokeMap(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EternalWars.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found."), "tools", "smoke", name);
    }
}
