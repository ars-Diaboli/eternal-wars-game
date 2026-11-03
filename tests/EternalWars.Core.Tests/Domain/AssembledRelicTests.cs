using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Assembled relics (ADR-100, ADR-108, ADR-121): joining, blocked slots, taking apart, loot, the altar, the AI, full-mana and no-faction-penalty.</summary>
public class AssembledRelicTests
{
    private const string Magi = "ring-of-the-magi";
    private static readonly string[] MagiPieces = ["ring-of-conjuring", "collar-of-conjuring", "cape-of-conjuring"];
    private const string Well = "wizards-well";
    private static readonly string[] WellPieces = ["talisman-of-mana", "mystic-orb-of-mana", "charm-of-mana"];
    private const string Alliance = "angelic-alliance";
    private static readonly string[] AlliancePieces = ["sword-of-judgement", "lions-shield-of-courage", "celestial-necklace-of-bliss"];

    private static Hero Outfit(GameState state, HeroId id, string[]? worn = null, string[]? backpack = null)
    {
        var hero = state.GetHero(id);
        hero.Artifacts.ReplaceWith(worn ?? [], backpack ?? [], state.Artifacts);
        state.RefreshArtifacts(hero);
        return hero;
    }

    [Fact]
    public void TheNineRelics_AreInTheCatalog_NeverDrop_AndSitInTheirFirstPiecesSlot()
    {
        var catalog = ArtifactCatalog.Default;

        Assert.Equal(9, catalog.Relics.Count());
        Assert.All(catalog.Relics, r =>
        {
            Assert.Equal(ArtifactTier.Relic, r.Tier);
            Assert.Equal(catalog.Get(r.Pieces[0]).Slot, r.Slot);
            Assert.All(r.Pieces, p => Assert.Equal(r.Id, catalog.RelicOf(p)));
        });
        Assert.DoesNotContain(catalog.OfTier(ArtifactTier.Relic), a => a.IsAssembled);
        Assert.Equal([ArtifactSlot.Neck, ArtifactSlot.Cloak], catalog.BlockedSlots(catalog.Get(Magi)));
        Assert.Contains(ArtifactEffect.Parse("full-mana"), catalog.Get(Well).Effects);
        Assert.Contains(ArtifactEffect.Parse("no-faction-penalty"), catalog.Get(Alliance).Effects);
        Assert.Equal("no-faction-penalty", ArtifactEffect.Parse("no-faction-penalty").ToString());
    }

    [Fact]
    public void TheCatalog_RefusesBadRelics()
    {
        const string Header = "id,name,slot,tier,set,effects,notes,pieces\na,A,Head,Minor,,attack 1,,\nb,B,Neck,Minor,,attack 1,,\n";
        ArtifactCatalog Load(string row) => new(ArtifactTable.ParseArtifacts(Header + row));

        Load("r,R,Head,Relic,,attack 1,,a; b"); // fine
        Assert.Throws<FormatException>(() => Load("r,R,Neck,Relic,,attack 1,,a; b")); // not the first piece's slot
        Assert.Throws<FormatException>(() => Load("r,R,Head,Relic,,attack 1,,a")); // one piece
        Assert.Throws<FormatException>(() => Load("r,R,Head,Relic,,attack 1,,a; a"));
        Assert.Throws<FormatException>(() => Load("r,R,Head,Relic,,attack 1,,a; nope"));
        Assert.Throws<FormatException>(() => Load("r,R,Head,Relic,,attack 1,,a; b\ns,S,Head,Relic,,attack 1,,a; b")); // a piece of two
    }

    [Fact]
    public void WearingTheLastPiece_SaysTheRelicIsReady_AndAssemblingKeepsEveryEffect_AndBlocksTheOtherSlots()
    {
        var state = Create("A..", "..B");
        var hero = Outfit(state, A, MagiPieces[..2], [MagiPieces[2]]);
        var session = Start(state);
        var ready = new List<RelicReady>();
        session.Events.Subscribe<RelicReady>(ready.Add);
        var power = hero.Bonus.SpellPower;
        Assert.Contains("must wear", session.Execute(new AssembleRelicCommand(Blue, A, Magi)).Error);

        session.Execute(new EquipArtifactCommand(Blue, A, MagiPieces[2])).ThrowIfRejected();
        Assert.Equal(new RelicReady(A, Magi), Assert.Single(ready));
        session.Execute(new AssembleRelicCommand(Blue, A, Magi)).ThrowIfRejected();

        Assert.Equal([(ArtifactSlot.Ring, Magi)], hero.Artifacts.AllWorn);
        Assert.Equal([(ArtifactSlot.Neck, Magi), (ArtifactSlot.Cloak, Magi)], hero.Artifacts.AllBlocked);
        Assert.Equal(power + 1 + 3, hero.Bonus.SpellPower); // the three pieces' +1 each and the relic's +3
        Assert.True(hero.Artifacts.HasRoomToWear(ArtifactSlot.Ring)); // the second ring place is free

        Outfit(state, A, [Magi], ["badge-of-courage"]); // a neck artifact
        Assert.Contains("blocked", session.Execute(new EquipArtifactCommand(Blue, A, "badge-of-courage")).Error);
        Assert.Contains("is not an assembled relic", session.Execute(new AssembleRelicCommand(Blue, A, "badge-of-courage")).Error);
    }

    [Fact]
    public void ARelic_ComesApartAnyTime_IntoWornPieces_OrIntoTheBackpack()
    {
        var state = Create("A..", "..B");
        var hero = Outfit(state, A, [Magi]);
        var session = Start(state);

        session.Execute(new TakeRelicApartCommand(Blue, A, Magi)).ThrowIfRejected();
        Assert.Equal(MagiPieces.Order(StringComparer.Ordinal), hero.Artifacts.AllWorn.Select(w => w.Id).Order(StringComparer.Ordinal));
        Assert.Empty(hero.Artifacts.AllBlocked);

        session.Execute(new AssembleRelicCommand(Blue, A, Magi)).ThrowIfRejected();
        session.Execute(new UnequipArtifactCommand(Blue, A, Magi)).ThrowIfRejected();
        Assert.Empty(hero.Artifacts.AllBlocked);
        session.Execute(new TakeRelicApartCommand(Blue, A, Magi)).ThrowIfRejected();
        Assert.Equal(MagiPieces, hero.Artifacts.Backpack);

        Outfit(state, A, [], [.. Enumerable.Repeat("badge-of-courage", ArtifactSlots.BackpackSize - 1), Magi]);
        Assert.Contains("no room", session.Execute(new TakeRelicApartCommand(Blue, A, Magi)).Error);
    }

    [Fact]
    public void ACarriedRelic_IsWornAgain_OnlyWithItsBlockedSlotsFree()
    {
        var state = Create("A..", "..B");
        var hero = Outfit(state, A, ["badge-of-courage"], [Magi]);
        var session = Start(state);

        Assert.Contains("free Neck slot", session.Execute(new EquipArtifactCommand(Blue, A, Magi)).Error);
        Assert.Contains(Magi, hero.Artifacts.Backpack);

        session.Execute(new UnequipArtifactCommand(Blue, A, "badge-of-courage")).ThrowIfRejected();
        session.Execute(new EquipArtifactCommand(Blue, A, Magi)).ThrowIfRejected();
        Assert.True(hero.Artifacts.IsWorn(Magi));
    }

    [Fact]
    public void ALootedRelic_GoesOverWhole_WhenItsSlotsAreFree_ElseAsPieces()
    {
        var state = Create("A..", "..B");
        Outfit(state, B, [Magi]);
        var winner = Outfit(state, A);
        var session = Start(state);

        session.Artifacts.Loot(state.GetHero(B), winner);
        Assert.True(winner.Artifacts.IsWorn(Magi));

        Outfit(state, B, [Magi]);
        var full = Outfit(state, A, ["badge-of-courage"]);
        session.Artifacts.Loot(state.GetHero(B), full);
        Assert.Equal(MagiPieces, full.Artifacts.Backpack);
    }

    [Fact]
    public void TheAltar_TakesPiecesOnly()
    {
        var state = Create(".............................", ".............................", "...A.........................", ".............................", "C...........................B");
        var altar = Add<SacrificialAltar>(state, BuiltInMapObjects.SacrificialAltar, 2, 0);
        state.GetHero(A).PreferredCreature = "haven.footman"; // a Light hero may sacrifice artifacts
        Outfit(state, A, [], [Magi]);
        var session = Start(state);
        session.Step(A, Direction.North);

        Assert.Contains("apart first", session.Execute(new SacrificeArtifactCommand(Blue, altar.Id, Magi)).Error);
        Assert.Contains(Magi, state.GetHero(A).Artifacts.Backpack);
    }

    [Fact]
    public void TheWizardsWell_FillsTheManaEveryTurn()
    {
        var state = Create("A..", "..B");
        var hero = Outfit(state, A, [Well]);
        hero.Knowledge = 10;
        var session = Start(state);
        hero.Mana = 0;

        session.EndTurn();
        session.EndTurn();

        Assert.Equal(hero.MaxMana, hero.Mana);
        Assert.True(hero.Bonus.FullMana);
    }

    [Fact]
    public void TheAngelicAlliance_RemovesTheMixedFactionsMorale()
    {
        foreach (var wearsIt in new[] { false, true })
        {
            var state = Create("AB.", "...");
            var hero = Outfit(state, A, wearsIt ? [Alliance] : []);
            hero.Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 10));
            hero.Army.Set(1, new CreatureStack(BuiltInCreatures.Wolf, 10));
            var session = Start(state);

            session.Step(A, Direction.East);

            var battle = Assert.IsType<BattleState>(session.State.Battle);
            Assert.Equal(wearsIt ? 0 : -1, battle.Attacker.ArmyMorale);
        }
    }

    [Fact]
    public void TheAi_JoinsARelic_AndWantsThePiecesItLacks()
    {
        var state = Create("A.......", ".......B");
        Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Major), 3, 0).Artifact = AlliancePieces[2];
        Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Major), 3, 1).Artifact = "helm-of-chaos";
        var hero = Outfit(state, A, [], AlliancePieces[..2]);
        var document = MapConverter.ToDocument(state);
        var session = Start(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);

        ai.PlayTurn();

        Assert.True(hero.Artifacts.IsWorn(Alliance));
        Assert.Contains(session.CommandLog, c => c is AssembleRelicCommand);
        Assert.Equal(0, ai.Refused);
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(document, session))));
        Assert.Equal(GameStateDigest.Compute(session.State), GameStateDigest.Compute(replayed.State));
    }
}
