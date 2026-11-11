using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Map bans and the random placeholders: random monsters, random towns and hero placeholders (ADR-126, map format v35).</summary>
public class MapBanAndPlaceholderTests
{
    private static readonly string[] WideMap = [.. Enumerable.Repeat("..............................", 14), "A............................B"];

    private static GameState World(MapBans? bans = null, GameOptions? options = null)
    {
        var state = Create(options ?? GameOptions.Default, WideMap);
        if (bans is not null)
        {
            state.SetRules(state.Rules with { Bans = bans });
        }

        return state;
    }

    private static MapBans BanAllBut(MapBanKind kind, IEnumerable<string> all, params string[] allowed) =>
        all.Where(id => !allowed.Contains(id)).Aggregate(MapBans.None, (bans, id) => bans.With(kind, id, banned: true));

    // Bans --------------------------------------------------------------------------------------------

    [Fact]
    public void Bans_StaySortedWithoutRepeats_AndAPoolTheyWouldEmptyStaysWhole()
    {
        var bans = MapBans.None.With(MapBanKind.Spell, "b", true).With(MapBanKind.Spell, "a", true).With(MapBanKind.Spell, "b", true);
        Assert.Equal(["a", "b"], bans.Spells);
        Assert.Equal(["a"], bans.With(MapBanKind.Spell, "b", false).Spells);
        Assert.Equal(bans, MapBans.None.With(MapBanKind.Spell, "a", true).With(MapBanKind.Spell, "b", true));

        Assert.Equal(["c"], MapBans.Allowed(["a", "b", "c"], bans.BansSpell));
        Assert.Equal(["a", "b"], MapBans.Allowed(["a", "b"], bans.BansSpell));
    }

    [Fact]
    public void ArtifactRolls_SkipBannedArtifacts_UnlessTheWholeTierIsBanned()
    {
        var minors = ArtifactCatalog.Default.OfTier(ArtifactTier.Minor).Select(a => a.Id).Order(StringComparer.Ordinal).ToList();
        var kept = minors[3];
        var state = World(BanAllBut(MapBanKind.Artifact, minors, kept));
        var pickups = Enumerable.Range(0, 4).Select(i => Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), 2 + (i * 2), 2)).ToList();
        Start(state);
        Assert.All(pickups, p => Assert.Equal(kept, p.Artifact));

        var everything = World(BanAllBut(MapBanKind.Artifact, minors));
        var pickup = Add<ArtifactPickup>(everything, BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), 2, 2);
        Start(everything);
        Assert.Contains(pickup.Artifact, minors);
    }

    [Fact]
    public void TavernsPrisonsAndMapTaverns_NeverOfferABannedHero()
    {
        var roster = HeroCatalog.Default.All.Select(h => h.Id).ToList();
        var kept = HeroCatalog.Default.All.First(h => h.Town == TownIds.Haven).Id;
        var state = World(BanAllBut(MapBanKind.Hero, roster, kept));
        var prison = Add<Prison>(state, BuiltInMapObjects.Prison, 4, 4);
        Start(state);

        Assert.Equal([kept], TavernRules.PoolOrder(state, Blue, TownIds.Inferno).Select(h => h.Id));
        Assert.Equal(kept, prison.Hero);

        var open = World(new MapBans { Heroes = [kept] });
        Assert.DoesNotContain(kept, TavernRules.PoolOrder(open, Blue, TownIds.Haven).Select(h => h.Id));
    }

    [Fact]
    public void GuildDraws_SkipBannedSpells_ButKeepTheMapsFixedOnes()
    {
        var one = SpellCatalog.Default.Of(TownIds.Haven, 1).Select(s => s.Id).ToList();
        Assert.True(one.Count >= 4);
        var bans = BanAllBut(MapBanKind.Spell, one, one[1], one[2]);
        foreach (var seed in new ulong[] { 1, 2, 3 })
        {
            var state = World(bans, GameOptions.Default with { Seed = seed });
            var town = AddTown(state, TownIds.Haven, 10, 0, Blue);
            town.FixedGuildSpells = [one[0]]; // banned, but placed by hand
            Start(state);
            Assert.Equal(new[] { one[0], one[1], one[2] }.Order(StringComparer.Ordinal), town.GuildSpells[1]);
        }
    }

    [Fact]
    public void ShrinesLevelUpsAndWitchHuts_SkipBannedSpellsAndSkills()
    {
        var levelOne = ShrineRules.Candidates(SpellCatalog.Default, 1).Select(s => s.Id).ToList();
        var spellBans = BanAllBut(MapBanKind.Spell, levelOne, levelOne[5]);
        var random = new GameRandom(7);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(levelOne[5], ShrineRules.Roll(random, SpellCatalog.Default, 1, spellBans));
        }

        var skills = BuiltInSkills.All.Select(s => s.Id).ToList();
        var first = BuiltInSkills.All.First();
        var second = BuiltInSkills.All.First(s => s.Group != first.Group);
        var skillBans = BanAllBut(MapBanKind.Skill, skills, first.Id, second.Id);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(new[] { first.Id, second.Id }.Order(StringComparer.Ordinal), WitchHutRules.Roll(random, skillBans).Order(StringComparer.Ordinal));
        }

        var hero = new Hero(A, "Alpha", Blue, new MapPosition(0, 0), DefaultArmy());
        var banned = new MapBans { Skills = [.. skills.Where((_, i) => i % 2 == 0).Order(StringComparer.Ordinal)] };
        for (var i = 0; i < 50; i++)
        {
            Assert.All(SkillOffers.Generate(hero, TownIds.Haven, random, banned), offer => Assert.False(banned.BansSkill(offer)));
        }
    }

    [Fact]
    public void Bans_RoundTripThroughTheMapFormat_AndAnOlderMapHasNone()
    {
        var bans = new MapBans { Heroes = [HeroCatalog.Default.All.First().Id], Skills = [BuiltInSkills.All.First().Id] };
        var state = World(bans);

        var json = TestFormat.Save(state);
        Assert.Contains("\"bans\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"artifacts\"", json, StringComparison.Ordinal); // empty lists stay out
        Assert.Equal(bans, TestFormat.Load(json).Rules.Bans);

        var plain = MapSerializer.Serialize(MapConverter.ToDocument(World()) with { FormatVersion = 34 });
        Assert.DoesNotContain("\"bans\"", plain, StringComparison.Ordinal);
        Assert.True(TestFormat.Load(plain).Rules.Bans.IsEmpty);
    }

    [Fact]
    public void TheValidator_FlagsUnknownBans_AndWarnsAboutWhatIsPlacedByHand()
    {
        var hero = HeroCatalog.Default.All.First();
        var state = World(new MapBans { Heroes = [hero.Id, "nobody"] });
        state.GetHero(A).Apply(hero);

        var issues = MapValidator.Validate(state);
        Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && i.Message.Contains("'nobody'", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.Severity == IssueSeverity.Warning && i.Message.Contains("banned type", StringComparison.Ordinal));
    }

    [Fact]
    public void TheEditor_BansThroughTheRules_Undoably()
    {
        var editor = TestEditor.Blank(20, 12);
        var rules = editor.Scenario.Rules;
        Assert.Equal(EditResult.Ok, editor.Map.SetRules(rules with { Bans = rules.Bans.With(MapBanKind.Artifact, "x", true) }));
        Assert.Equal(["x"], editor.Scenario.Rules.Bans.Artifacts);
        editor.Undo();
        Assert.True(editor.Scenario.Rules.Bans.IsEmpty);
    }

    // Random monsters -----------------------------------------------------------------------------------

    [Fact]
    public void ARandomMonster_BecomesACreatureOfItsTier_FromThePlayableAndNeutralLines_AndGetsACount()
    {
        foreach (var seed in new ulong[] { 1, 2, 3, 4, 5 })
        {
            var state = World(options: GameOptions.Default with { Seed = seed });
            var tierThree = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 5, 5);
            tierThree.RandomTier = 3;
            var any = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 8, 5);
            any.RandomTier = 0;
            var changed = new List<MapObjectChanged>();
            var session = new GameSession(state);
            session.Events.Subscribe<MapObjectChanged>(changed.Add);
            session.Start();

            var creature = state.Creatures.Get(tierThree.Creature);
            Assert.Equal(3, creature.Tier);
            Assert.True(creature.Town is null || BuiltInMapObjects.PlayableTowns.Contains(creature.Town));
            Assert.Contains(any.Creature, PlaceholderRules.MonsterCandidates(state.Creatures, 0).Select(c => c.Id));
            Assert.Null(tierThree.RandomTier);
            Assert.NotNull(tierThree.Count);
            Assert.Contains(changed, c => c.Object == tierThree.Id);
        }
    }

    // Random towns --------------------------------------------------------------------------------------

    [Fact]
    public void ARandomTown_TakesItsOwnersChosenFaction_KeepingItsIdNameAndDwellingTiers()
    {
        var state = World(options: GameOptions.Default with { StartTowns = new Dictionary<int, string> { [0] = TownIds.Inferno } });
        var town = AddTown(state, TownIds.Haven, 2, 2, Blue);
        town.IsRandom = true;
        var tierOne = state.Creatures.BaseCreaturesOf(TownIds.Haven).First(c => c.Tier == 1).Id;
        town.SetDwelling(tierOne, 1);
        var session = Start(state);

        var drawn = Assert.IsType<Town>(state.FindObject(town.Id));
        Assert.Equal(TownIds.Inferno, drawn.TownType);
        Assert.False(drawn.IsRandom);
        Assert.Equal(drawn.Definition.Name, drawn.Name);
        Assert.Equal(1, drawn.DwellingLevel(state.Creatures.BaseCreaturesOf(TownIds.Inferno).First(c => c.Tier == 1).Id));
        Assert.Equal(TownIds.Inferno, state.GetPlayer(Blue).StartTown);
        Assert.NotNull(session);
    }

    [Fact]
    public void WithoutAChoice_APlayersRandomTownsShareOneDrawnFaction_AndNeutralOnesDrawTheirOwn()
    {
        foreach (var seed in new ulong[] { 1, 2, 3, 4 })
        {
            var state = World(options: GameOptions.Default with { Seed = seed });
            var first = AddTown(state, TownIds.Haven, 2, 2, Red);
            var second = AddTown(state, TownIds.Haven, 12, 2, Red);
            var neutral = AddTown(state, TownIds.Haven, 22, 2);
            first.IsRandom = second.IsRandom = neutral.IsRandom = true;
            Start(state);

            var types = new[] { first, second, neutral }.Select(t => ((Town)state.FindObject(t.Id)!).TownType).ToList();
            Assert.Equal(types[0], types[1]);
            Assert.All(types, t => Assert.Contains(t, BuiltInMapObjects.PlayableTowns));
        }
    }

    // Hero placeholders ---------------------------------------------------------------------------------

    [Fact]
    public void AHeroPlaceholder_BecomesAFreeUnbannedHeroOfItsOwnersFaction_KeepingPlaceAndArmy()
    {
        var havens = HeroCatalog.Default.All.Where(h => h.Town == TownIds.Haven).Select(h => h.Id).ToList();
        var state = World(BanAllBut(MapBanKind.Hero, havens, havens[0], havens[1]));
        AddTown(state, TownIds.Haven, 10, 2, Blue);
        var alpha = state.GetHero(A);
        alpha.IsPlaceholder = true;
        var charlie = Hero.Create(C, Blue, new MapPosition(3, 10), new EternalWars.Core.Domain.Armies.Army(), null, "Charlie", placeholder: true);
        state.AddHero(charlie);
        var (position, army) = (alpha.Position, alpha.Army.Stacks.ToList());
        var placed = new List<HeroPlaced>();
        var session = new GameSession(state);
        session.Events.Subscribe<HeroPlaced>(placed.Add);
        session.Start();

        Assert.Equal(new[] { havens[0], havens[1] }.Order(StringComparer.Ordinal), new[] { alpha.Definition, charlie.Definition }.Order(StringComparer.Ordinal));
        Assert.False(alpha.IsPlaceholder);
        Assert.Equal(HeroCatalog.Default.Get(alpha.Definition!).Name, alpha.Name);
        Assert.Equal(position, alpha.Position);
        Assert.Equal(army, alpha.Army.Stacks.ToList());
        Assert.False(charlie.Army.IsEmpty); // an empty placeholder brings its hero's starter stack
        Assert.Equal(2, placed.Count);
    }

    [Fact]
    public void AHeroPlaceholderWithoutATown_TakesItsOwnersChosenFaction()
    {
        var state = World(options: GameOptions.Default with { StartTowns = new Dictionary<int, string> { [1] = TownIds.Necropolis } });
        state.GetHero(B).IsPlaceholder = true;
        Start(state);

        Assert.Equal(TownIds.Necropolis, HeroCatalog.Default.Get(state.GetHero(B).Definition!).Town);
    }

    // Map format and editor -----------------------------------------------------------------------------

    [Fact]
    public void Placeholders_RoundTripThroughTheMapFormat()
    {
        var state = World();
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 5, 5);
        monster.RandomTier = 4;
        var town = AddTown(state, TownIds.Sylvan, 10, 2, Blue);
        town.IsRandom = true;
        state.GetHero(B).IsPlaceholder = true;

        var json = TestFormat.Save(state);
        Assert.Contains("\"randomTier\": 4", json, StringComparison.Ordinal);
        Assert.Contains("\"random\": true", json, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"placeholder\": true"));
        var restored = TestFormat.Load(json);

        Assert.Equal(4, restored.ObjectsOf<MonsterGuard>().Single().RandomTier);
        Assert.True(restored.ObjectsOf<Town>().Single().IsRandom);
        Assert.True(restored.GetHero(B).IsPlaceholder);
        Assert.False(restored.GetHero(A).IsPlaceholder);

        var wrongTier = MapConverter.ToDocument(state) with
        {
            Objects = [.. MapConverter.ToDocument(state).Objects.Select(o => o is MonsterDocument m ? m with { RandomTier = 9 } : o)],
        };
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(wrongTier));
    }

    [Fact]
    public void TheEditor_MakesTownsMonstersAndHeroesRandom_Undoably()
    {
        var editor = TestEditor.Blank(20, 12);
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TownId(TownIds.Haven), new(2, 2), out var townId));
        var town = (Town)editor.Scenario.FindObject(townId)!;
        var one = SpellCatalog.Default.Of(TownIds.Haven, 1)[0].Id;
        Assert.Equal(EditResult.Ok, editor.Towns.SetGuildSpells(townId, [one]));
        Assert.Equal(EditResult.Ok, editor.Towns.SetRandom(townId, true));
        Assert.True(town.IsRandom);
        Assert.Empty(town.FixedGuildSpells); // a random town has no school yet
        Assert.Equal(EditResult.Ok, editor.Towns.SetType(townId, TownIds.Inferno)); // what it shows
        Assert.True(((Town)editor.Scenario.FindObject(townId)!).IsRandom);
        editor.Undo();
        editor.Undo();
        Assert.False(town.IsRandom);
        Assert.Equal([one], town.FixedGuildSpells);

        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(12, 2), BuiltInCreatures.Wolf, null, GuardDisposition.Aggressive, false, out var monsterId));
        var monster = (MonsterGuard)editor.Scenario.FindObject(monsterId)!;
        Assert.Equal(EditResult.InvalidValue, editor.Monsters.SetRandomTier(monsterId, 8));
        Assert.Equal(EditResult.Ok, editor.Monsters.SetRandomTier(monsterId, 5));
        Assert.Equal(5, monster.RandomTier);
        Assert.Equal(5, editor.Scenario.Creatures.Get(monster.Creature).Tier);
        editor.Undo();
        Assert.Null(monster.RandomTier);

        Assert.Equal(EditResult.Ok, editor.Heroes.Place(new PlayerId(0), new MapPosition(15, 8), "Hero", out var heroId));
        Assert.Equal(EditResult.Ok, editor.Heroes.SetPlaceholder(heroId, true));
        Assert.True(editor.Scenario.GetHero(heroId).IsPlaceholder);
        editor.Undo();
        Assert.False(editor.Scenario.GetHero(heroId).IsPlaceholder);
    }
}
