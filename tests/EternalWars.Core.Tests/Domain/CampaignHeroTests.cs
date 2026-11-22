using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Campaign heroes (ADR-298): on top of the standard roster, never drawn at random.</summary>
public class CampaignHeroTests
{
    private static readonly string[] Map = [.. Enumerable.Repeat("..........", 9), "A........B"];

    [Fact]
    public void TheRoster_HasFourCampaignHeroesPerPlayableTown_AndEightForConfluxAndDragonUtopia()
    {
        var campaign = HeroCatalog.Default.All.Where(h => h.Campaign).ToList();

        Assert.Equal(48, campaign.Count);
        Assert.All(BuiltInMapObjects.PlayableTowns, town => Assert.Equal(4, campaign.Count(h => h.Town == town)));
        Assert.Equal(8, campaign.Count(h => h.Town == TownIds.Conflux));
        Assert.Equal(8, campaign.Count(h => h.Town == TownIds.DragonUtopia));
        Assert.All(campaign, h => Assert.Equal(5, h.Attack + h.Defense + h.SpellPower + h.Knowledge));
    }

    [Fact]
    public void TavernsAndRandomPrisons_NeverDrawACampaignHero_EvenWhenEveryStandardHeroIsBanned()
    {
        var bans = HeroCatalog.Default.Standard.Aggregate(MapBans.None, (b, h) => b.With(MapBanKind.Hero, h.Id, banned: true));
        var state = Create(GameOptions.Default, Map);
        state.SetRules(state.Rules with { Bans = bans });
        var prison = Add<Prison>(state, BuiltInMapObjects.Prison, 4, 4);
        Start(state);

        var pool = TavernRules.PoolOrder(state, Blue, TownIds.Haven);
        Assert.Equal(HeroCatalog.Default.Standard.Count(), pool.Count); // a ban that empties a pool leaves it whole (ADR-126)
        Assert.DoesNotContain(pool, h => h.Campaign);
        Assert.False(HeroCatalog.Default.Get(prison.Hero!).Campaign);
    }

    [Fact]
    public void TheCampaignColumn_TakesYesOrNothing_AndRoundTrips()
    {
        const string header = "id,name,town,class,preferred,specialty,skills,attack,defense,spell_power,knowledge,campaign,notes\n";
        var heroes = HeroTable.Parse(header
            + "haven.a,A,haven,Warrior,haven.footman,skill offense.melee,offense.melee Basic,2,1,1,1,yes,\n"
            + "haven.b,B,haven,Mage,haven.footman,spell light.bless,magic.haven.spells Basic,0,0,3,2,,\n");

        Assert.Equal([true, false], heroes.Select(h => h.Campaign));
        Assert.Equal([true, false], HeroTable.Parse(HeroTable.Format(heroes)).Select(h => h.Campaign));
        Assert.Throws<FormatException>(() => HeroTable.Parse(header + "haven.c,C,haven,Warrior,haven.footman,skill offense.melee,,2,1,1,1,no,\n"));
    }
}
