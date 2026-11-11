using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Spell scrolls (ADR-126, ADR-136): one blank Misc scroll per spell level; the spell is written on it when it is rolled, given or at game start
/// (the generator and the editor may write it first); worn, the hero casts that spell.
/// </summary>
public class SpellScrollTests
{
    private static readonly string[] Map = ["A.......", "........", ".......B"];

    private static Hero Outfit(GameState state, HeroId id, string[] worn, string[]? backpack = null)
    {
        var hero = state.GetHero(id);
        hero.Artifacts.ReplaceWith(worn, backpack ?? [], state.Artifacts);
        state.RefreshArtifacts(hero);
        return hero;
    }

    private static SpellDefinition Unknown(Hero hero, int level) =>
        SpellCatalog.Default.All.Where(s => s.Level == level && !hero.Spells.ContainsKey(s.Id)).OrderBy(s => s.Id, StringComparer.Ordinal).First();

    private static string Scroll(int level) => $"spell-scroll-{level}";

    private static void AssertWritten(ArtifactCatalog catalog, string id, int level)
    {
        var scroll = catalog.Get(id);
        Assert.Equal(Scroll(level), ArtifactCatalog.BaseOf(id));
        Assert.NotNull(scroll.Spell);
        Assert.Equal(level, SpellCatalog.Default.Get(scroll.Spell!).Level);
    }

    [Fact]
    public void TheCatalog_HasOneBlankMiscScrollPerSpellLevel()
    {
        var catalog = ArtifactCatalog.Default;

        Assert.Equal([Scroll(1), Scroll(2), Scroll(3), Scroll(4)], catalog.Scrolls.Select(s => s.Id));
        Assert.Equal([ArtifactTier.Minor, ArtifactTier.Major, ArtifactTier.Relic, ArtifactTier.Relic], catalog.Scrolls.Select(s => s.Tier));
        Assert.All(catalog.Scrolls, s => Assert.Equal(ArtifactSlot.Misc, s.Slot));
        Assert.All(catalog.Scrolls, s => Assert.Null(s.Spell));
        Assert.Equal([1, 2, 3, 4], catalog.Scrolls.Select(s => s.ScrollLevel!.Value));
        Assert.Equal(4, catalog.All.Count(a => a.IsScroll));
    }

    [Fact]
    public void AWrittenScroll_IsLookedUpByItsId_OnlyWithASpellOfItsLevel()
    {
        var catalog = ArtifactCatalog.Default;
        var bless = SpellCatalog.Default.Get("light.bless");
        var high = SpellCatalog.Default.All.First(s => s.Level == 3);

        var written = catalog.Get(ArtifactCatalog.WithSpell(Scroll(bless.Level), bless.Id));
        Assert.Equal($"spell-scroll-{bless.Level}:light.bless", written.Id);
        Assert.Equal("Scroll of Bless", written.Name);
        Assert.Equal(bless.Id, written.Spell);
        Assert.Equal(bless.Level, written.ScrollLevel);
        Assert.Same(written, catalog.Get(written.Id));
        Assert.DoesNotContain(written, catalog.All);

        Assert.False(catalog.Contains(ArtifactCatalog.WithSpell(Scroll(1), high.Id)));
        Assert.False(catalog.Contains(Scroll(1) + ":no-such-spell"));
        Assert.False(catalog.Contains("badge-of-courage:light.bless"));
        Assert.Equal("badge-of-courage", ArtifactCatalog.BaseOf("badge-of-courage"));
    }

    [Fact]
    public void ScrollRows_NeedOneLevelEffect_AndNoSetPiecesOrSeparator()
    {
        const string header = "id,name,slot,tier,set,effects\n";
        ArtifactCatalog Parse(string rows) => new(ArtifactTable.ParseArtifacts(header + rows));

        Assert.Equal("scroll 2", Parse("s,S,Misc,Major,,scroll 2\n").Get("s").Effects.Single().ToString());
        Assert.Throws<FormatException>(() => Parse("s,S,Misc,Minor,,scroll 5\n"));
        Assert.Throws<FormatException>(() => Parse("s,S,Misc,Minor,,scroll 1; attack 1\n"));
        Assert.Throws<FormatException>(() => Parse("a:b,S,Misc,Minor,,attack 1\n"));
    }

    [Fact]
    public void ScrollValues_RiseWithTheSpellLevel_BlankOrWritten()
    {
        var catalog = ArtifactCatalog.Default;
        var values = catalog.Scrolls.Select(s => ArtifactValue.Of(catalog, s)).ToList();

        Assert.Equal(values.Order(), values);
        Assert.Equal(values.Distinct().Count(), values.Count);
        Assert.Equal(values[0], ArtifactValue.Of(catalog, catalog.Get(ArtifactCatalog.WithSpell(Scroll(1), "light.bless"))));
        Assert.True(values[2] > ArtifactValue.Of(catalog, catalog.Get("badge-of-courage"))); // a level 3 scroll beats +1 morale
    }

    [Fact]
    public void AWornScroll_LetsTheHeroCastItsSpell_WithoutTheSchoolSkill_AtItsMasterysRank()
    {
        var state = Create(Map);
        var hero = state.GetHero(A);
        hero.HasSpellBook = true;
        var spell = Unknown(hero, 3);
        Assert.True(hero.SkillLevelOf(MagicRules.MagicSkill(spell.School)) < SkillLevel.Expert);

        Outfit(state, A, [ArtifactCatalog.WithSpell(Scroll(3), spell.Id)]);
        Assert.Contains(MagicRules.Known(hero, state.Spells), k => k.Spell.Id == spell.Id && k.Rank == 1);
        Assert.NotNull(BattleCaster.For(hero, state.Spells)!.Find(spell.Id));
        Assert.Contains(spell.Name, hero.Bonus.Describe(state.Spells), StringComparison.Ordinal);

        hero.Skills.Set(MagicRules.MasterySkill(spell.School), SkillLevel.Advanced);
        Assert.Contains(MagicRules.Known(hero, state.Spells), k => k.Spell.Id == spell.Id && k.Rank == 3);
    }

    [Fact]
    public void AScroll_InTheBackpack_Blank_OrWithoutASpellBook_TeachesNothing()
    {
        var state = Create(Map);
        var hero = state.GetHero(A);
        var spell = Unknown(hero, 1);
        var scroll = ArtifactCatalog.WithSpell(Scroll(1), spell.Id);
        var learned = hero.Spells.Count;

        hero.HasSpellBook = true;
        Outfit(state, A, [], [scroll]);
        Assert.DoesNotContain(MagicRules.Known(hero, state.Spells), k => k.Spell.Id == spell.Id);

        Outfit(state, A, [Scroll(1)]);
        Assert.Equal(learned, MagicRules.Known(hero, state.Spells).Count());

        Outfit(state, A, [scroll]);
        hero.HasSpellBook = false;
        Assert.Empty(MagicRules.Known(hero, state.Spells));
        Assert.False(hero.Spells.ContainsKey(spell.Id)); // nothing is learned from a scroll
    }

    [Fact]
    public void Draws_GiveTheTiersScrollInAFewPercent_WrittenWithAnUnbannedSpellOfItsLevel()
    {
        var levelOne = SpellCatalog.Default.All.Where(s => s.Level == 1).Select(s => s.Id).Order(StringComparer.Ordinal).ToList();
        var kept = levelOne[0];
        var state = Create(Map);
        state.SetRules(state.Rules with { Bans = levelOne.Skip(1).Aggregate(MapBans.None, (b, id) => b.With(MapBanKind.Spell, id, banned: true)) });
        var session = Start(state);

        var drawn = Enumerable.Range(0, 2000).Select(_ => session.Artifacts.Draw(ArtifactTier.Minor)!).ToList();
        var scrolls = drawn.Where(id => state.Artifacts.Get(id).IsScroll).ToList();

        Assert.InRange(scrolls.Count, 60, 140); // 5 % of 2000
        Assert.All(scrolls, id => Assert.Equal(ArtifactCatalog.WithSpell(Scroll(1), kept), id));
        Assert.DoesNotContain(Enumerable.Range(0, 300).Select(_ => session.Artifacts.Draw(ArtifactTier.Relic)!), id => ArtifactCatalog.BaseOf(id) == Scroll(4));
    }

    [Fact]
    public void AtGameStart_BlankScrollsOnPickupsAndHeroesGetASpell_WrittenOnesStay()
    {
        var state = Create(Map);
        var blank = Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Major), 3, 1);
        blank.Artifact = Scroll(2);
        var chosen = Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), 5, 1);
        chosen.Artifact = ArtifactCatalog.WithSpell(Scroll(1), "light.bless");
        Outfit(state, A, [Scroll(3)], [Scroll(1)]);

        Start(state);

        AssertWritten(state.Artifacts, blank.Artifact!, 2);
        Assert.Equal(ArtifactCatalog.WithSpell(Scroll(1), "light.bless"), chosen.Artifact);
        var hero = state.GetHero(A);
        AssertWritten(state.Artifacts, hero.Artifacts.Worn(ArtifactSlot.Misc).Single(), 3);
        AssertWritten(state.Artifacts, hero.Artifacts.Backpack.Single(), 1);
    }

    [Fact]
    public void AGivenBlankScroll_ArrivesWritten()
    {
        var state = Create(Map);
        state.SetMapEvents(new MapEventSet
        {
            Events = [new MapEventDefinition("gift", MapEventKind.Hero, [new ArtifactAction(Scroll(2))])],
            Cells = [new EventCell(1, 0, 0, "gift")],
        });
        var session = Start(state);
        var hero = state.GetHero(A);

        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.East)); // onto the event cell

        AssertWritten(state.Artifacts, hero.Artifacts.Backpack.Single(), 2);
    }

    [Fact]
    public void TheEditor_SetsABlankOrAWrittenScroll_OnAPickup()
    {
        var editor = TestEditor.Blank(12, 8);
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), new(2, 2), out var id));

        Assert.Equal(EditResult.Ok, editor.Contents.SetArtifactPickup(id, Scroll(1)));
        Assert.Equal(EditResult.Ok, editor.Contents.SetArtifactPickup(id, ArtifactCatalog.WithSpell(Scroll(1), "light.bless")));
        Assert.Equal(EditResult.InvalidValue, editor.Contents.SetArtifactPickup(id, ArtifactCatalog.WithSpell(Scroll(2), "light.bless")));
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error);
    }
}
