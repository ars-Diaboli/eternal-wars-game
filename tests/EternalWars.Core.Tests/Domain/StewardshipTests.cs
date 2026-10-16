using EternalWars.Core.Events;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Governors and Stewardship (ADR-063).</summary>
public class StewardshipTests
{
    private static readonly string[] Rows =
    [
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "...A................",
        "....................",
        "...................B",
    ];

    [Theory]
    [InlineData(SkillLevel.None, 0, 0)]
    [InlineData(SkillLevel.Basic, 5, 10)]
    [InlineData(SkillLevel.Advanced, 10, 15)]
    [InlineData(SkillLevel.Expert, 15, 20)]
    public void Discounts_FollowTheLevel(SkillLevel level, int building, int creatures)
    {
        Assert.Equal(building, StewardshipRules.BuildingDiscountPercent(level));
        Assert.Equal(creatures, StewardshipRules.CreatureDiscountPercent(level));
    }

    [Fact]
    public void VisitingHeroWithTheSkill_BecomesGovernorAutomatically_AndTheTownIsCheaper()
    {
        var (session, town) = Setup(SkillLevel.Advanced);
        var changes = new List<GovernorChanged>();
        session.Events.Subscribe<GovernorChanged>(changes.Add);
        var blue = session.State.GetPlayer(Blue);

        session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.Blacksmith)).ThrowIfRejected();
        Assert.Equal(new ResourceSet(Gold: 100_000 - 1000, Lumber: 100 - 5, Ore: 100 - 5, Crystal: 50, Mercury: 50, Sulfur: 50), blue.Resources); // no governor yet

        session.MoveTo(A, town.VisitPosition);
        Assert.Equal(A, town.Governor);
        Assert.Equal(new GovernorChanged(town.Id, null, A), Assert.Single(changes));

        session.EndTurn();
        session.EndTurn();
        var before = blue.Resources;
        session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.Marketplace)).ThrowIfRejected();
        var price = session.State.Buildings.Get(BuiltInBuildings.Marketplace, 1).Cost;
        Assert.Equal(before - StewardshipRules.DiscountBuilding(session.State, town, price), blue.Resources);
        Assert.True(StewardshipRules.DiscountBuilding(session.State, town, price).Gold < price.Gold);
    }

    [Fact]
    public void Governor_MakesRecruitingCheaper_ByTheCreatureDiscount()
    {
        var (session, town) = Setup(SkillLevel.Expert);
        town.SetDwelling(BuiltInCreatures.Footman, 1);
        town.SetAvailable(BuiltInCreatures.Footman, 10);
        session.MoveTo(A, town.VisitPosition);
        var footman = session.State.Creatures.Get(BuiltInCreatures.Footman);
        var before = session.State.GetPlayer(Blue).Resources;

        session.Execute(new RecruitCommand(Blue, town.Id, BuiltInCreatures.Footman, 10)).ThrowIfRejected();

        var paid = before - session.State.GetPlayer(Blue).Resources;
        Assert.Equal(10 * ((footman.Cost.Gold * 80 + 99) / 100), paid.Gold);
        Assert.True(paid.Gold < footman.Cost.Gold * 10);
    }

    [Fact]
    public void AHeroGovernsOneTown_AndDismissAndLostSkillEndTheBonus()
    {
        var (session, town) = Setup(SkillLevel.Basic);
        var second = AddTown(session.State, TownIds.Haven, 10, 0, Blue);
        session.MoveTo(A, town.VisitPosition);
        Assert.Equal(A, town.Governor);

        session.Execute(new AppointGovernorCommand(Blue, second.Id, A)).ThrowIfRejected();
        Assert.Equal(A, second.Governor);
        Assert.Null(StewardshipRules.GovernorOf(session.State, town)); // moved: the first town governs nothing
        Assert.Same(second, StewardshipRules.TownGovernedBy(session.State, session.State.GetHero(A)));

        session.Execute(new DismissGovernorCommand(Blue, second.Id)).ThrowIfRejected();
        Assert.Null(StewardshipRules.GovernorOf(session.State, second));
        Assert.Contains("no governor", session.Execute(new DismissGovernorCommand(Blue, second.Id)).Error);

        session.Execute(new AppointGovernorCommand(Blue, town.Id, A)).ThrowIfRejected();
        session.State.GetHero(A).Skills.Set(BuiltInSkills.Stewardship, SkillLevel.None);
        Assert.Equal(SkillLevel.None, StewardshipRules.LevelIn(session.State, town));
    }

    [Fact]
    public void OnlyHeroesWithTheSkill_AndOnlyForOwnTowns_CanBeAppointed()
    {
        var (session, town) = Setup(SkillLevel.None);

        Assert.Contains("no Stewardship", session.Execute(new AppointGovernorCommand(Blue, town.Id, A)).Error);
        session.MoveTo(A, town.VisitPosition);
        Assert.Null(town.Governor);
        Assert.False(session.Execute(new AppointGovernorCommand(Blue, town.Id, session.State.Heroes.First(h => h.Owner == Red).Id)).IsAccepted);
    }

    private static (GameSession Session, Town Town) Setup(SkillLevel stewardship)
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        state.GetHero(A).Skills.Set(BuiltInSkills.Stewardship, stewardship);
        var session = Start(state);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 100_000, Lumber: 100, Ore: 100, Crystal: 50, Mercury: 50, Sulfur: 50);
        return (session, town);
    }
}
