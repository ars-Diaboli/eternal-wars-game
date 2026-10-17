using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>Stage 4 of the generator: towns, mines and resource piles (Phase 4).</summary>
public class SettlementPlacerTests
{
    private static MapTemplate Duel() => BuiltInTemplates.Find("duel")!;

    private static GeneratedMap Generate(MapTemplate template, int size = 96, ulong seed = 3)
    {
        var layout = ZoneLayouter.Layout(template, size, seed);
        var map = GeneratedMap.From(TerrainPainter.Paint(template, layout, seed));
        ConnectionPlacer.Place(template, map, seed);
        SettlementPlacer.Place(template, map, seed);
        return map;
    }

    private static int ZoneOf(GeneratedMap map, MapObject o) => map.Layout.ZoneAt(o.Position.X, o.Position.Y, o.Position.Level);

    private static int Index(MapTemplate template, string id) => template.Zones.Select((z, i) => (z.Id, i)).First(p => p.Id == id).i;

    [Fact]
    public void TheSameInputs_GiveTheSameSettlements()
    {
        string Dump(GeneratedMap m) => string.Join(";", m.Objects.Select(o => $"{o.Definition.Id}@{o.Position}"));

        Assert.Equal(Dump(Generate(Duel())), Dump(Generate(Duel())));
        Assert.NotEqual(Dump(Generate(Duel())), Dump(Generate(Duel(), seed: 4)));
    }

    [Fact]
    public void EveryStartZone_GetsItsPlayerTownInTheMiddle_Unowned_WithTheSlotRecorded()
    {
        foreach (var seed in new ulong[] { 1, 2, 3 })
        {
            var map = Generate(Duel(), 96, seed);
            var starts = map.Towns.Where(t => t.Slot is not null).OrderBy(t => t.Slot).ToList();

            Assert.Empty(map.Problems);
            Assert.Equal([0, 1], starts.Select(t => t.Slot));
            Assert.Equal([Index(Duel(), "start-1"), Index(Duel(), "start-2")], starts.Select(t => t.Zone));
            Assert.All(starts, t =>
            {
                Assert.Null(t.Town.Owner);
                Assert.True(t.Town.Garrison.IsEmpty);
                Assert.Equal(t.Zone, ZoneOf(map, t.Town));
                var centre = map.Layout.Tiles(t.Zone).Average(p => p.X);
                Assert.InRange(Math.Abs(t.Town.VisitPosition.X - centre), 0, 25);
            });
        }
    }

    [Fact]
    public void AStartTown_CanTakeTheTypeAPlayerPicks()
    {
        var map = Generate(Duel());
        var start = map.Towns.First(t => t.Slot == 1);

        var changed = map.SetTownType(start.Town, TownIds.Inferno);

        Assert.Equal(TownIds.Inferno, changed.TownType);
        Assert.Equal((start.Town.Id, start.Town.Position), (changed.Id, changed.Position));
        Assert.Same(changed, map.Towns.First(t => t.Slot == 1).Town);
        Assert.Contains(changed, map.Objects);
        Assert.DoesNotContain(start.Town, map.Objects);
        Assert.Throws<ArgumentException>(() => map.SetTownType(changed, "atlantis"));
    }

    [Fact]
    public void ARequestedTownType_IsUsedForTheStartTown_AndNeutralTownsFollowTheZoneList()
    {
        var template = Duel() with
        {
            Zones = [.. Duel().Zones.Select(z => z.Id == "start-1" ? z with { TownTypes = [TownIds.Necropolis] } : z.Type == ZoneType.Treasure ? z with { TownTypes = [TownIds.Academy, TownIds.Dungeon], NeutralTowns = CountRange.Exactly(1) } : z)],
        };

        var map = Generate(template);

        Assert.Equal(TownIds.Necropolis, map.Towns.First(t => t.Slot == 0).Town.TownType);
        Assert.Equal(TownIds.Haven, map.Towns.First(t => t.Slot == 1).Town.TownType); // no list: the placeholder
        var neutrals = map.Towns.Where(t => t.Slot is null && template.Zones[t.Zone].Type == ZoneType.Treasure).ToList();
        Assert.Equal(2, neutrals.Count);
        Assert.All(neutrals, t => Assert.Contains(t.Town.TownType, new[] { TownIds.Academy, TownIds.Dungeon }));
    }

    [Fact]
    public void NeutralTowns_AreGarrisonedByTheZonesStrength_WithTwoStacks()
    {
        var map = Generate(Duel() with { Zones = [.. Duel().Zones.Select(z => z.Type == ZoneType.Junction ? z with { Monsters = MonsterStrength.Strong, NeutralTowns = CountRange.Exactly(1) } : z)] });
        var town = map.Towns.Single(t => t.Slot is null && Duel().Zones[t.Zone].Id == "centre").Town;

        Assert.Equal(2, town.Garrison.StackCount);
        var power = town.Garrison.Stacks.Sum(s => (double)s.Stack.Count * CreatureCatalog.Default.Get(s.Stack.Creature).Value);
        Assert.InRange(power, ZoneGuards.TownGarrisonValue(MonsterStrength.Strong) * 0.6, ZoneGuards.TownGarrisonValue(MonsterStrength.Strong) * 1.4);

        // A zone without monsters still garrisons its neutral town (weakly).
        var calm = Generate(Duel() with { Zones = [.. Duel().Zones.Select(z => z.Type == ZoneType.Junction ? z with { Monsters = MonsterStrength.None, NeutralTowns = CountRange.Exactly(1) } : z)] });
        Assert.False(calm.Towns.Single(t => t.Slot is null && Duel().Zones[t.Zone].Id == "centre").Town.Garrison.IsEmpty);
    }

    [Fact]
    public void MinesAreAsManyAsAsked_StartZoneMinesStandNearTheTown_AndAreUnguarded()
    {
        var map = Generate(Duel());
        var mines = map.Objects.OfType<Mine>().ToList();
        var start = map.Towns.First(t => t.Slot == 0);

        foreach (var zone in Duel().Zones.Select((z, i) => (z, i)))
        {
            foreach (var (resource, count) in zone.z.Mines)
            {
                Assert.Equal(count, mines.Count(m => ZoneOf(map, m) == zone.i && m.Resource == resource));
            }
        }

        var near = mines.Where(m => ZoneOf(map, m) == start.Zone).ToList();
        Assert.Equal(3, near.Count);
        Assert.All(near, m =>
        {
            Assert.Null(m.Guard);
            Assert.InRange(Math.Max(Math.Abs(m.VisitPosition.X - start.Town.VisitPosition.X), Math.Abs(m.VisitPosition.Y - start.Town.VisitPosition.Y)), 0, SettlementPlacer.StartMineRadius + 2);
        });
    }

    [Fact]
    public void MinesOutsideStartZones_AreGuardedByTheZonesStrength_AndTheTemplateCanChangeThat()
    {
        var generated = Generate(Duel());
        var guarded = generated.Objects.OfType<Mine>().Where(m => ZoneOf(generated, m) >= 2).ToList();
        Assert.NotEmpty(guarded);
        Assert.All(guarded, m => Assert.NotNull(m.Guard));

        var never = Generate(Duel() with { Zones = [.. Duel().Zones.Select(z => z with { MineGuards = MineGuarding.Never })] });
        Assert.All(never.Objects.OfType<Mine>(), m => Assert.Null(m.Guard));

        var always = Generate(Duel() with { Zones = [.. Duel().Zones.Select(z => z with { MineGuards = MineGuarding.Always })] });
        Assert.All(always.Objects.OfType<Mine>(), m => Assert.NotNull(m.Guard)); // start zones too; "no monsters" counts as weak

        var calm = Generate(Duel() with { Zones = [.. Duel().Zones.Select(z => z with { Monsters = MonsterStrength.None })] });
        Assert.All(calm.Objects.OfType<Mine>(), m => Assert.Null(m.Guard)); // auto: no monsters, no guards
    }

    [Fact]
    public void AMinesGuard_FollowsTheStrength_AndTheResource()
    {
        Assert.Equal(0, ZoneGuards.MineGuardValue(MonsterStrength.None, ResourceType.Gold));
        Assert.True(ZoneGuards.MineGuardValue(MonsterStrength.Weak, ResourceType.Ore) < ZoneGuards.MineGuardValue(MonsterStrength.Average, ResourceType.Ore));
        Assert.True(ZoneGuards.MineGuardValue(MonsterStrength.Average, ResourceType.Ore) < ZoneGuards.MineGuardValue(MonsterStrength.Strong, ResourceType.Ore));
        Assert.True(ZoneGuards.MineGuardValue(MonsterStrength.Average, ResourceType.Lumber) < ZoneGuards.MineGuardValue(MonsterStrength.Average, ResourceType.Crystal));
        Assert.True(ZoneGuards.MineGuardValue(MonsterStrength.Average, ResourceType.Crystal) < ZoneGuards.MineGuardValue(MonsterStrength.Average, ResourceType.Gold));
        Assert.True(ZoneGuards.TownGarrisonValue(MonsterStrength.None) > 0);
        Assert.True(ZoneGuards.TownGarrisonValue(MonsterStrength.Weak) < ZoneGuards.TownGarrisonValue(MonsterStrength.Strong));
    }

    [Fact]
    public void ExtraPiles_ArePlacedUnguarded_ByTheZone_MostlyGold()
    {
        var template = Duel() with { Zones = [.. Duel().Zones.Select(z => z with { ExtraPiles = 20 })] };

        var map = Generate(template);
        var piles = map.Objects.OfType<ResourcePile>().ToList();

        Assert.Equal(20 * template.Zones.Count, piles.Count);
        Assert.True(piles.Count(p => p.Resource == ResourceType.Gold) > piles.Count(p => p.Resource == ResourceType.Crystal));
        Assert.Equal(piles.Count, piles.Select(p => p.Position).Distinct().Count());
    }

    [Fact]
    public void NothingOverlaps_AndEverythingIsOnFreeGround()
    {
        var map = Generate(BuiltInTemplates.Find("Free for all")!, 96, 2);
        var cells = map.Objects.SelectMany(o => o.OccupiedCells().Select(c => c.Position)).ToList();

        Assert.Equal(cells.Count, cells.Distinct().Count());
        Assert.All(cells, p => Assert.NotEqual(TerrainPainter.WallTerrain, map.Map.GetTerrain(p)));
    }

    [Fact]
    public void ARoomLessZone_ReportsWhatItCouldNotPlace()
    {
        var crowded = Duel() with { Zones = [.. Duel().Zones.Select(z => z.Id == "reserve-1" ? z with { ExtraPiles = 5000 } : z)] };

        var map = Generate(crowded, 72, 3);

        Assert.Contains(map.Problems, p => p.Contains("reserve-1") && p.Contains("resource pile"));
    }

    [Fact]
    public void ATemplateFromAnotherMap_IsRefused_AndMineGuardingSurvivesTheFileFormat()
    {
        var map = GeneratedMap.From(TerrainPainter.Paint(Duel(), ZoneLayouter.Layout(Duel(), 72, 1), 1));
        Assert.Throws<ArgumentException>(() => SettlementPlacer.Place(BuiltInTemplates.Find("Free for all")!, map, 1));

        var loaded = TemplateSerializer.Deserialize(TemplateSerializer.Serialize(Duel() with { Zones = [.. Duel().Zones.Select(z => z with { MineGuards = MineGuarding.Always })] }));
        Assert.All(loaded.Zones, z => Assert.Equal(MineGuarding.Always, z.MineGuards));
        Assert.All(Duel().Zones, z => Assert.Equal(MineGuarding.Auto, z.MineGuards));
    }
}
