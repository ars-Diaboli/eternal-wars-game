using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

public class HeroProgressionTests
{
    [Fact]
    public void SkillGroups_HaveThreeSkills_WorthTenPoints_AndASchoolPerPlayableTown()
    {
        Assert.All(BuiltInSkills.Groups, group =>
        {
            var skills = BuiltInSkills.SkillsOf(group.Id).ToList();
            Assert.Equal(3, skills.Count);
            Assert.Equal(SkillGroup.Points, skills.Sum(s => (int)s.MaxLevel));
        });
        Assert.Equal(8, BuiltInSkills.Groups.Count(g => g.IsMagic));
        Assert.Equal(14, BuiltInSkills.Groups.Count);
    }

    [Fact]
    public void HeroSkills_AllowSixGroups_AndStopAtTheSkillsMaximum()
    {
        var hero = new Hero(A, "Alpha", Blue, default);
        string[] six = [BuiltInSkills.Melee, BuiltInSkills.Armor, BuiltInSkills.Treasury, BuiltInSkills.Logistics, BuiltInSkills.Tactics, "magic.haven.spells"];
        foreach (var skill in six)
        {
            hero.Skills.Raise(skill);
        }

        Assert.False(hero.Skills.CanRaise("magic.inferno.spells"));
        Assert.True(hero.Skills.CanRaise(BuiltInSkills.Archery)); // a group it already has
        hero.Skills.Set(BuiltInSkills.Pathfinding, SkillLevel.None);
        hero.Skills.Raise(BuiltInSkills.Tactics);
        hero.Skills.Raise(BuiltInSkills.Tactics);
        Assert.False(hero.Skills.CanRaise(BuiltInSkills.Tactics)); // Expert is its top
        Assert.Throws<ArgumentOutOfRangeException>(() => hero.Skills.Set("magic.haven.mastery", SkillLevel.Expert));
    }

    [Fact]
    public void Roster_HasAMageAndAWarriorForEveryTierOneToFourLine()
    {
        var roster = HeroCatalog.Default;

        Assert.Empty(roster.Check(CreatureCatalog.Default));
        Assert.Equal(128, roster.All.Count());
        Assert.All(roster.All.GroupBy(h => h.Town), town => Assert.Equal(8, town.Count(h => h.Class == HeroClass.Mage)));
        Assert.All(roster.All.Where(h => h.Class == HeroClass.Mage),
            mage => Assert.Equal(SkillLevel.Basic, mage.StartingSkills.GetValueOrDefault($"magic.{mage.Town}.spells")));
    }

    [Fact]
    public void HeroTable_RoundTrips_AndReportsBadCells()
    {
        var text = HeroTable.Format(HeroCatalog.Default.All);
        Assert.Equal(HeroCatalog.Default.All.Count(), HeroTable.Parse(text).Count);

        const string header = "id,name,town,class,preferred,specialty,skills\n";
        Assert.StartsWith("Line 2, column 'specialty'", Assert.Throws<FormatException>(() =>
            HeroTable.Parse(header + "x.y,Y,haven,Mage,haven.footman,resource gems 2,")).Message);
        Assert.StartsWith("Line 2, column 'skills'", Assert.Throws<FormatException>(() =>
            HeroTable.Parse(header + "x.y,Y,haven,Mage,haven.footman,spell light.heal,tactics.tactics Master")).Message);
    }

    [Fact]
    public void Offers_RarelyIncludeARivalSchool_AndOnlyRaisableSkills()
    {
        var mage = Hero.Create(A, Blue, default, null, HeroCatalog.Default.Get("haven.ansel"));
        var random = new GameRandom(11);
        var withRival = 0;
        const int levelUps = 4000;
        for (var i = 0; i < levelUps; i++)
        {
            var offers = SkillOffers.Generate(mage, TownIds.Haven, random);
            Assert.Equal(SkillOffers.OfferCount, offers.Distinct().Count());
            Assert.All(offers, o => Assert.True(mage.Skills.CanRaise(o)));
            withRival += offers.Any(o => o.StartsWith("magic.necropolis", StringComparison.Ordinal) || o.StartsWith("magic.inferno", StringComparison.Ordinal)) ? 1 : 0;
        }

        Assert.InRange(100.0 * withRival / levelUps, 3.5, 6.5);
    }

    [Fact]
    public void LevelUp_AsksTheOwner_RaisesTheChosenSkill_AndAsksAgainForTheNextLevel()
    {
        var state = Create("A..", "..B");
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 1, 0);
        chest.FixedValues = new ChestValues(0, 2000); // level 3: two choices
        var session = Start(state);
        var hero = state.GetHero(A);

        session.MoveTo(A, chest.VisitPosition);
        session.ResolveChest(TreasureChestReward.Experience);

        var first = Assert.IsType<LevelUpDecision>(state.PendingDecision);
        Assert.Equal(2, first.Level);
        Assert.False(session.Execute(new ChooseSkillCommand(Blue, "no.such-skill")).IsAccepted);
        Assert.True(session.Execute(new ChooseSkillCommand(Blue, first.Options[0])).IsAccepted);
        Assert.Equal(SkillLevel.Basic, hero.Skills.Level(first.Options[0]));

        var second = Assert.IsType<LevelUpDecision>(state.PendingDecision);
        Assert.Equal(3, second.Level);
        session.Execute(new ChooseSkillCommand(Blue, second.Options[0])).ThrowIfRejected();
        Assert.Equal((3, 0), (hero.Level, hero.PendingLevelUps));
        Assert.Null(state.PendingDecision);
    }

    [Fact]
    public void LevelUp_OfAnotherPlayersHero_WaitsForItsTurn()
    {
        var state = Create("A..", "..B");
        var session = Start(state);
        var bravo = state.GetHero(B);

        session.State.GetHero(B).Experience = 1000; // e.g. won a battle as defender during Blue's turn
        session.Progression.OfferNext();
        Assert.Null(state.PendingDecision);

        session.EndTurn();

        Assert.Equal(B, Assert.IsType<LevelUpDecision>(state.PendingDecision).Hero);
        Assert.Equal(1, bravo.Level);
    }

    [Fact]
    public void ComputerHeroes_LevelUpOnTheirOwn()
    {
        var map = new AdventureMap(4, 1);
        var hero = new Hero(A, "Bot", Blue, new MapPosition(0, 0), DefaultArmy());
        var state = new GameState(map, [new Player(Blue, "Blue", isHuman: false)], [hero]);
        var session = Start(state);

        hero.Experience = ExperienceTable.For(4);
        session.Progression.OfferNext();

        Assert.Equal(4, hero.Level);
        Assert.Equal(3, hero.Skills.Levels.Values.Sum(l => (int)l));
        Assert.Null(state.PendingDecision);
    }

    [Fact]
    public void PreferredCreature_AndOffenseDefenseSkills_ChangeDamageAndSpeed()
    {
        var footman = TestBattle.Fixed("haven.footman-test", damage: 10, attack: 10, defense: 10);
        var plain = TestBattle.Create();
        var preferred = new HeroBattleBonuses(0, 0, 0, 0, new HashSet<string> { footman.Id });
        var skilled = TestBattle.Create(
            attackerBonuses: preferred with { MeleePercent = 20 },
            defenderBonuses: HeroBattleBonuses.None with { DamageTakenReductionPercent = 10, SpeedBonus = 3 });

        int Hit(TestBattle battle, out BattleUnit attacker, out BattleUnit defender)
        {
            attacker = battle.Add(BattleSide.Attacker, footman, 10, 0, 0);
            defender = battle.Add(BattleSide.Defender, footman, 10, 2, 0);
            return DamageCalculator.Damage(battle.State, attacker, defender, new HitModifiers(10, IsRanged: false));
        }

        var baseline = Hit(plain, out _, out _);
        var boosted = Hit(skilled, out var attacker, out var defender);

        // +2 Attack (preferred) = +10 %, then Melee +20 %, then Armor −10 %: 100 → 110 → 132 → 118.
        Assert.Equal(100, baseline);
        Assert.Equal(118, boosted);
        Assert.Equal(footman.Speed + SkillEffects.PreferredSpeed, skilled.State.SpeedOf(attacker));
        Assert.Equal(footman.Speed + 3, skilled.State.SpeedOf(defender));
    }

    [Fact]
    public void Logistics_TroopManagement_AndPathfinding_ChangeMovement()
    {
        var state = Create("Ass..", ".....", "....B");
        var hero = state.GetHero(A);
        var before = MovementAllowance.Daily(state, hero);

        hero.Skills.Set(BuiltInSkills.TroopManagement, SkillLevel.Basic); // footman map speed 3 → 4: 1500 → 1560
        hero.Skills.Set(BuiltInSkills.Logistics, SkillLevel.Advanced);     // +20 %
        Assert.Equal((1500, 1560 * 120 / 100), (before, MovementAllowance.Daily(state, hero)));

        hero.Skills.Set(BuiltInSkills.Pathfinding, SkillLevel.Advanced);  // swamp 200: penalty 100 halved
        var session = Start(state);
        session.Step(A, Direction.East);
        var afterGrass = hero.MovementPoints;
        session.Step(A, Direction.East);
        Assert.Equal(150, afterGrass - hero.MovementPoints);
    }

    [Fact]
    public void HeroIncome_PaysTreasuryProspectingAndResourceSpecialties()
    {
        var hero = Hero.Create(A, Blue, default, null, HeroCatalog.Default.Get("haven.maren")); // resource gold 350
        hero.Skills.Set(BuiltInSkills.Treasury, SkillLevel.Basic);
        hero.Skills.Set(BuiltInSkills.Prospecting, SkillLevel.Basic);

        Assert.Equal(new ResourceSet(Gold: 450, Lumber: 1, Ore: 1), HeroIncome.Of(hero));
    }

    [Fact]
    public void MapHeroes_KeepTheirRosterType_AndSkills_AndOldTacticsStillLoads()
    {
        var state = BlankScenario.Create(10, 8);
        var editor = new MapEditorSession(state);
        Assert.Equal(EditResult.Ok, editor.SetHeroType(A, "inferno.azgaroth"));
        Assert.Equal(EditResult.InvalidValue, editor.SetHeroType(A, "inferno.nobody"));

        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(MapSerializer.Serialize(MapConverter.ToDocument(state)))).GetHero(A);
        Assert.Equal(("inferno.azgaroth", "Azgaroth", HeroClass.Warrior, "inferno.imp"), (restored.Definition, restored.Name, restored.Class, restored.PreferredCreature));
        Assert.Equal(SkillLevel.Basic, restored.Skills.Level(BuiltInSkills.Melee));

        editor.Undo();
        Assert.Null(state.GetHero(A).Definition);

        var v4 = """
            { "formatVersion": 4, "name": "Old", "width": 2, "height": 1, "terrain": [["gg"]],
              "players": [{ "name": "Blue", "isHuman": true }],
              "heroes": [{ "id": 1, "name": "Old", "owner": 0, "x": 0, "y": 0, "tactics": "Advanced" }] }
            """;
        Assert.Equal(SkillLevel.Advanced, MapConverter.ToGameState(MapSerializer.Deserialize(v4)).GetHero(A).Tactics);
    }
}
