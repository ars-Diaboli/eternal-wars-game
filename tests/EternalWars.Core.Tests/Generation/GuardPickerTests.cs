using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>Picking a guard stack for an army power (Phase 4, stage 3).</summary>
public class GuardPickerTests
{
    private static readonly CreatureCatalog Creatures = CreatureCatalog.Default;

    [Fact]
    public void TierRanges_Overlap_SoOnePowerCanBeSeveralTiers()
    {
        var low = GuardPicker.EligibleTiers(Creatures, 500);
        var high = GuardPicker.EligibleTiers(Creatures, 1000);

        Assert.True(low.Count >= 2, $"500: {string.Join(",", low)}");
        Assert.True(high.Count >= 2, $"1000: {string.Join(",", high)}");
        Assert.NotEmpty(low.Intersect(high)); // the same tier can guard both, in a different number
        Assert.True(GuardPicker.EligibleTiers(Creatures, 50_000).Max() > GuardPicker.EligibleTiers(Creatures, 300).Max());
    }

    [Fact]
    public void ThePowerOfThePick_IsCloseToTheValue()
    {
        foreach (var value in new[] { 400, 500, 1000, 2500, 6000, 15_000 })
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var (creature, count) = GuardPicker.Pick(Creatures, value, new GameRandom(seed));

                Assert.InRange((double)count * creature.Value, value * 0.7, value * 1.3);
                Assert.InRange(count, GuardPicker.CountsFor(creature.Tier).Min, GuardPicker.CountsFor(creature.Tier).Max);
            }
        }
    }

    [Fact]
    public void ASameValue_GivesVariety_ButTheSameSeedTheSamePick()
    {
        var picks = Enumerable.Range(1, 40).Select(s => GuardPicker.Pick(Creatures, 1000, new GameRandom((ulong)s)).Creature.Id).ToList();

        Assert.True(picks.Distinct().Count() >= 4);
        Assert.Equal(GuardPicker.Pick(Creatures, 1000, new GameRandom(7)), GuardPicker.Pick(Creatures, 1000, new GameRandom(7)));
        Assert.True(Enumerable.Range(1, 40).Select(s => Creatures.Get(GuardPicker.Pick(Creatures, 1000, new GameRandom((ulong)s)).Creature.Id).Tier).Distinct().Count() >= 2);
    }

    [Fact]
    public void OnlyPlayableTownsAndNeutralsGuard_AndNoUpgrades()
    {
        var upgrades = Creatures.All.Where(c => c.UpgradesTo is not null).Select(c => c.UpgradesTo).ToHashSet();
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var creature = GuardPicker.Pick(Creatures, 300 + (int)(seed * 400), new GameRandom(seed)).Creature;

            Assert.DoesNotContain(creature.Id, upgrades);
            Assert.True(creature.Town is null || BuiltInMapObjects.PlayableTowns.Contains(creature.Town), creature.Id);
        }
    }

    [Fact]
    public void ExtremeValues_StillGiveAStackInTheRangeOfItsTier()
    {
        foreach (var value in new[] { 1, 30, 5_000_000 })
        {
            var (creature, count) = GuardPicker.Pick(Creatures, value, new GameRandom(1));

            Assert.InRange(count, GuardPicker.CountsFor(creature.Tier).Min, GuardPicker.CountsFor(creature.Tier).Max);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => GuardPicker.Pick(Creatures, 0, new GameRandom(1)));
    }
}
