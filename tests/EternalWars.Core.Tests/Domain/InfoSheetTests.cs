using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Visibility;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The right-click information windows (ADR-118, UI-3; ADR-154): what each sheet shows, and only what the viewer may know.</summary>
public class InfoSheetTests
{
    // Blue's A at (0, 1) sees 5 cells; Red's B at (4, 1) is in its sight.
    private static GameState Strip() => Create(new string('.', 30), "A...B" + new string('.', 24) + "C", new string('.', 30));

    [Fact]
    public void ACreature_ShowsItsNumbersAndRules_AndDetailedAddsTheStrengthFormula()
    {
        var state = Strip();
        var footman = state.Creatures.Get(BuiltInCreatures.Footman);

        var plain = InfoSheets.Creature(state, BuiltInCreatures.Footman, detailed: false);
        var detailed = InfoSheets.Creature(state, BuiltInCreatures.Footman, detailed: true);

        Assert.Equal(footman.Name, plain.Title);
        Assert.StartsWith($"Creature, tier {footman.Tier}", plain.Kind, StringComparison.Ordinal);
        Assert.Contains(plain.Lines, l => l.StartsWith($"Attack {footman.Attack}   Defense {footman.Defense}", StringComparison.Ordinal));
        Assert.DoesNotContain(plain.Lines, l => l.StartsWith("Strength", StringComparison.Ordinal));
        Assert.Contains(detailed.Lines, l => l.StartsWith($"Strength {footman.Value} a creature", StringComparison.Ordinal));
    }

    [Fact]
    public void ACapability_IsSaidInWords()
    {
        Assert.Equal("Pack hunter: +10 % damage for every other own stack fighting the target.",
            InfoSheets.Capability(new CreatureCapability(CapabilityKind.PackHunter, 10)));
        Assert.Equal("Unaffected by spells of level 2 and below.", InfoSheets.Capability(new CreatureCapability(CapabilityKind.SpellResistance, 2)));
    }

    [Fact]
    public void AnEnemyHero_ShowsWhatScoutingShows_AnOwnHeroEverything()
    {
        var state = Strip();
        Start(state);

        var enemy = InfoSheets.MapCell(state, Blue, new MapPosition(4, 1), detailed: false)!;
        var own = InfoSheets.MapCell(state, Blue, new MapPosition(0, 1), detailed: false)!;

        Assert.Equal(state.GetHero(B).Name, enemy.Title);
        Assert.Contains("Scouting shows nothing more of this hero.", enemy.Lines);
        Assert.Contains(enemy.Lines, l => l.StartsWith("Army: a pack (10–19)", StringComparison.Ordinal));
        Assert.Contains("Artifacts: not seen.", enemy.Lines);

        Assert.Contains(own.Lines, l => l.StartsWith($"Attack {state.GetHero(A).TotalAttack}", StringComparison.Ordinal));
        Assert.Contains(own.Lines, l => l == $"Army: 10 {state.Creatures.Get(BuiltInCreatures.Footman).Name}");
        Assert.StartsWith($"Level {state.GetHero(A).Level}", own.Kind, StringComparison.Ordinal);
    }

    [Fact]
    public void ANeutralStack_ShowsItsAmount_AndItsCountFromBasicScouting()
    {
        var state = Strip();
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 2, 0);
        monster.Creature = BuiltInCreatures.Footman;
        monster.Count = 12;
        Start(state);

        var unseen = InfoSheets.MapCell(state, Blue, monster.Position, detailed: false)!;
        state.GetHero(A).Skills.Set(BuiltInSkills.Scouting, SkillLevel.Basic);
        var scouted = InfoSheets.MapCell(state, Blue, monster.Position, detailed: false)!;

        Assert.Equal("Neutral stack", unseen.Kind);
        Assert.Equal("Count: a pack (10–19)", unseen.Lines[0]);
        Assert.Equal($"Disposition: {monster.Disposition}", unseen.Lines[1]);
        Assert.Equal("Count: 12", scouted.Lines[0]);
    }

    [Fact]
    public void ACellNotExplored_HasNoWindow_OpenGroundSaysItsTerrain()
    {
        var state = Strip();
        Start(state, revealAll: false);

        Assert.Null(InfoSheets.MapCell(state, Blue, new MapPosition(20, 1), detailed: false));
        var ground = InfoSheets.MapCell(state, Blue, new MapPosition(1, 0), detailed: false)!;
        Assert.Equal("Terrain", ground.Kind);
        Assert.Equal("Grass", ground.Title);
    }

    [Fact]
    public void AnObjectUnderTheFog_ShowsAsLastSeen()
    {
        var state = Create(new GameOptions(FogMode.ShroudWithRefog, RefogMargin: 2), new string('.', 20), new string('.', 20), "A" + new string('.', 19), new string('.', 20));
        var mine = Add<Mine>(state, BuiltInMapObjects.MineId(Core.Domain.Economy.ResourceType.Gold), 8, 0);
        var session = Start(state, revealAll: false);
        Assert.True(session.MoveTo(A, new MapPosition(5, 2)).Arrived);
        var seen = InfoSheets.MapCell(state, Blue, mine.Position, detailed: false)!;
        session.MoveTo(A, new MapPosition(0, 2)); // walked away: the mine is under the fog now

        var remembered = InfoSheets.MapCell(state, Blue, mine.Position, detailed: false)!;

        Assert.Contains("Gives 1000 gold a day.", seen.Lines);
        Assert.Equal(mine.Definition.Name, remembered.Title);
        Assert.Contains("Last seen here; it may have changed since.", remembered.Lines);
    }

    [Fact]
    public void ABuilding_ListsItsLevels_WithWhatIsBuilt()
    {
        var state = Create(new string('.', 20), new string('.', 20), new string('.', 20), new string('.', 20), new string('.', 20), new string('.', 20), new string('.', 20), new string('.', 20));
        var town = AddTown(state, TownIds.Haven, 10, 0, Blue);
        town.SetLevel(BuiltInBuildings.Fort, 1);
        var catalog = TownRules.Buildings(state, town);

        var sheet = InfoSheets.Building(state, town, BuiltInBuildings.Fort, detailed: false);

        Assert.Equal(catalog.Get(BuiltInBuildings.Fort, 1).Name, sheet.Title);
        Assert.Equal($"Built: {catalog.Get(BuiltInBuildings.Fort, 1).Name} (1 of {catalog.MaxLevel(BuiltInBuildings.Fort)}).", sheet.Lines[0]);
        Assert.StartsWith($"✓ {catalog.Get(BuiltInBuildings.Fort, 1).Name}", sheet.Lines[1], StringComparison.Ordinal);
        Assert.StartsWith($"· {catalog.Get(BuiltInBuildings.Fort, 2).Name}", sheet.Lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void ASpell_Detailed_ShowsEachRanksNumbersAtTheHerosSpellPower()
    {
        var state = Strip();
        var hero = state.GetHero(A);
        hero.Learn("arcana.magic-arrow");

        var plain = InfoSheets.Spell(state, "arcana.magic-arrow", 1, detailed: false, hero);
        var detailed = InfoSheets.Spell(state, "arcana.magic-arrow", 1, detailed: true, hero);

        Assert.Equal("Spell, academy", plain.Kind);
        Assert.DoesNotContain(plain.Lines, l => l.StartsWith("Rank I:", StringComparison.Ordinal));
        var power = hero.TotalSpellPower;
        Assert.Contains(detailed.Lines, l => l.StartsWith($"Rank I: Damage, amount 40 (×(10 + {power})/10 = {40 * (10 + power) / 10})", StringComparison.Ordinal));
        Assert.Contains(detailed.Lines, l => l.StartsWith("Rank II: Damage, amount 60", StringComparison.Ordinal));
    }

    [Fact]
    public void ASkill_ShowsItsLevelAndWhatItDoes()
    {
        var sheet = InfoSheets.Skill(BuiltInSkills.Scouting, SkillLevel.Advanced, detailed: false, learned: SkillLevel.Basic);

        Assert.Equal(BuiltInSkills.Get(BuiltInSkills.Scouting).Name, sheet.Title);
        Assert.StartsWith("Level: Advanced of", sheet.Lines[0], StringComparison.Ordinal);
        Assert.Contains("(learned Basic, the rest from artifacts)", sheet.Lines[0], StringComparison.Ordinal);
        Assert.Contains(BuiltInSkills.Get(BuiltInSkills.Scouting).Description, sheet.Lines);
    }

    [Fact]
    public void ABattleStack_ShowsItsAttackWithWhatIsAdded()
    {
        var battle = TestBattle.Create(nativeTowns: ["haven"]);
        var footman = battle.Add(BattleSide.Attacker, "haven.footman", 7, 0, 0);
        var creature = footman.Creature;

        var sheet = InfoSheets.BattleStack(battle.State, footman, detailed: false);

        Assert.Equal(creature.Attack + BattleState.NativeAttack, DamageCalculator.AttackOf(battle.State, footman));
        Assert.Equal($"Count: 7   Health of the top creature {creature.Health}/{creature.Health}", sheet.Lines[0]);
        Assert.StartsWith($"Attack {creature.Attack + 1} ({creature.Attack} + 1)", sheet.Lines[1], StringComparison.Ordinal);
        Assert.Contains("On its native terrain: +1 Attack, Defense and Speed.", sheet.Lines);
    }
}
