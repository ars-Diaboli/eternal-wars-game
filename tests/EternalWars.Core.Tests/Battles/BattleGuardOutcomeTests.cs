using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>
/// A guard only describes what the end of a battle does to it (ADR-216): asking changes nothing; <see cref="BattleGuardService"/> applies the
/// description (the battle and object tests check what each one does).
/// </summary>
public class BattleGuardOutcomeTests
{
    [Fact]
    public void EveryGuard_DescribesItsOutcome_WithoutChangingTheGame()
    {
        var rows = Enumerable.Repeat(new string('.', 40), 10).ToArray();
        rows[9] = "A" + new string('.', 38) + "B";
        var state = Create(rows);
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 1, 0);
        var mine = Add<Mine>(state, BuiltInMapObjects.AbandonedMineId(ResourceType.Gold), 3, 0);
        mine.Guard = new GuardStack(BuiltInCreatures.Wolf, 5);
        var dwelling = Add<CreatureDwelling>(state, BuiltInMapObjects.Dwelling, 6, 0);
        dwelling.Guard = new GuardStack(BuiltInCreatures.Wolf, 5);
        var bank = Add<CreatureBank>(state, BuiltInMapObjects.DragonCity, 9, 0);
        var box = Add<PandorasBox>(state, BuiltInMapObjects.PandorasBox, 14, 0);
        var garrison = Add<MapGarrison>(state, BuiltInMapObjects.Garrison, 16, 0);
        var town = AddTown(state, TownIds.Haven, 25, 0);
        Start(state);
        var before = GameStateDigest.Describe(state);
        var objects = state.Objects.Count;

        var outcomes = new IBattleGuard[] { monster, mine, dwelling, bank, box, garrison, town }
            .SelectMany(g => new[] { g.Survived(), g.Defeated() })
            .ToList();

        Assert.Equal(
            [
                new MonsterSurvives(monster), new MonsterBeaten(monster),
                new SiteGuardSurvives(mine), new MineGuardBeaten(mine),
                new SiteGuardSurvives(dwelling), new DwellingGuardBeaten(dwelling),
                new GuardUnchanged(bank), new BankPlundered(bank),
                new GuardUnchanged(box), new BoxWon(box),
                new TroopsSurvive(garrison, garrison.Troops), new GarrisonWon(garrison),
                new TroopsSurvive(town, town.Garrison), new TownWon(town),
            ],
            outcomes);
        Assert.Equal(before, GameStateDigest.Describe(state));
        Assert.Equal(objects, state.Objects.Count);
    }
}
