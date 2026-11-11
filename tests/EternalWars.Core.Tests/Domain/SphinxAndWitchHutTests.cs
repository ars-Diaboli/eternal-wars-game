using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.Domain.ObjectVisitWorld;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The sphinx (MAP-27) and the witch hut (MAP-34) of the third object catalogue (ADR-079), in play and in the editor (ADR-114); and the map format
/// of that catalogue's objects.
/// </summary>
public class SphinxAndWitchHutTests
{
    private static readonly Riddle Author = new("What has keys but opens no lock?", "A piano", "A jailer", "A chest");

    [Fact]
    public void TheSphinx_AsksItsAuthorsRiddle_AWrongAnswerCostsTheDayAndMorale_AndEachHeroTriesOnce()
    {
        var (session, sphinx) = World<Sphinx>(BuiltInMapObjects.Sphinx, (_, s) => s.Riddle = Author);
        session.Step(A, Direction.North);
        var decision = Assert.IsType<SphinxDecision>(session.State.PendingDecision);
        Assert.Equal(Author.Question, decision.Question);
        Assert.Equal("A piano", decision.Options[decision.Correct]);
        Assert.Equal(["A chest", "A jailer", "A piano"], decision.Options.Order(StringComparer.Ordinal));

        session.Execute(new AnswerSphinxCommand(Blue, (decision.Correct + 1) % 3)).ThrowIfRejected();

        var hero = HeroA(session);
        Assert.Equal(0, hero.MovementPoints);
        Assert.Contains(hero.Statuses, s => s.Kind == HeroStatusKind.Morale && s.Value == -3 && s.Expires == StatusExpiry.NextBattle);
        session.Interactions.Visit(hero, sphinx);
        Assert.Null(session.State.PendingDecision); // one try per hero
    }

    [Fact]
    public void TheRightAnswer_BringsAMajorArtifactOr5000Experience_AndRiddlesComeFromTheTable()
    {
        Assert.True(RiddleTable.Default.Count >= 10);
        var rewards = new List<SphinxAnswered>();
        for (var seed = 0; seed < 8; seed++)
        {
            var state = Create(new GameOptions(Seed: (ulong)seed), ".........", ".........", "...A.....", ".........", "C.......B");
            Add<Sphinx>(state, BuiltInMapObjects.Sphinx, 2, 0);
            var session = Start(state);
            session.Events.Subscribe<SphinxAnswered>(rewards.Add);
            session.Step(A, Direction.North);
            var decision = Assert.IsType<SphinxDecision>(state.PendingDecision);
            Assert.Contains(RiddleTable.Default, r => r.Question == decision.Question);
            session.Execute(new AnswerSphinxCommand(Blue, decision.Correct)).ThrowIfRejected();
        }

        Assert.All(rewards, r => Assert.True(r.Right && (r.Experience == 5000) != (r.Artifact is not null)));
        Assert.Contains(rewards, r => r.Artifact is { } a && ArtifactCatalog.Default.Get(a).Tier == ArtifactTier.Major);
        Assert.Contains(rewards, r => r.Experience == 5000);
    }

    [Fact]
    public void TheWitchHut_OffersItsTwoSkills_OrAnotherOfTheirGroup_OncePerLevel()
    {
        var (session, hut) = World<WitchHut>(BuiltInMapObjects.WitchHut, (state, h) =>
        {
            h.Skills = [BuiltInSkills.Pathfinding, BuiltInSkills.Stewardship];
            state.GetHero(A).Skills.Set(BuiltInSkills.Pathfinding, BuiltInSkills.Get(BuiltInSkills.Pathfinding).MaxLevel);
        });
        var hero = HeroA(session);
        session.Step(A, Direction.North);

        var decision = Assert.IsType<WitchHutDecision>(session.State.PendingDecision);
        Assert.Equal(2, decision.Options.Count);
        Assert.Equal("pathfinding", BuiltInSkills.Get(decision.Options[0]).Group);
        Assert.NotEqual(BuiltInSkills.Pathfinding, decision.Options[0]); // maxed: another of the group stands in
        Assert.Equal(BuiltInSkills.Stewardship, decision.Options[1]);
        session.Execute(new ChooseWitchHutSkillCommand(Blue, BuiltInSkills.Stewardship)).ThrowIfRejected();

        Assert.Equal(SkillLevel.Basic, hero.Skills.Level(BuiltInSkills.Stewardship));
        Assert.Equal(1, hero.WitchHutLevel);
        var refused = new List<MapObjectRefused>();
        session.Events.Subscribe<MapObjectRefused>(refused.Add);
        session.Interactions.Visit(hero, hut);
        Assert.Contains("at this level already", Assert.Single(refused).Reason, StringComparison.Ordinal);

        hero.Level = 2;
        session.Interactions.Visit(hero, hut);
        Assert.IsType<WitchHutDecision>(session.State.PendingDecision);
        session.Execute(new ChooseWitchHutSkillCommand(Blue, null)).ThrowIfRejected(); // leaving uses nothing
        Assert.Equal(1, hero.WitchHutLevel);
    }

    [Fact]
    public void TheNewObjects_AreInTheMapFormat_WithTheirRiddleAndSkills_AndAGameWithThemReplays()
    {
        var state = Create(".............", ".............", "...A.........", ".............", "C...........B");
        Add<Sphinx>(state, BuiltInMapObjects.Sphinx, 2, 0).Riddle = Author;
        Add<WitchHut>(state, BuiltInMapObjects.WitchHut, 6, 0).Skills = [BuiltInSkills.Logistics, BuiltInSkills.Archery];
        Add<Cartographer>(state, BuiltInMapObjects.SurfaceCartographer, 9, 0);
        Add<ArtifactMerchant>(state, BuiltInMapObjects.ArtifactMerchant, 6, 3);
        Add<RefugeeCamp>(state, BuiltInMapObjects.RefugeeCamp, 2, 3);
        Add<SacrificialAltar>(state, BuiltInMapObjects.SacrificialAltar, 9, 3);
        var map = MapConverter.ToDocument(state);
        var json = MapSerializer.Serialize(map);
        var restored = TestFormat.Load(json);

        Assert.Equal(Author, restored.ObjectsOf<Sphinx>().Single().Riddle);
        Assert.Equal([BuiltInSkills.Logistics, BuiltInSkills.Archery], restored.ObjectsOf<WitchHut>().Single().Skills);
        Assert.Equal(6, restored.Objects.Count);

        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 4)));
        session.Start();
        session.Step(A, Direction.North);
        session.Execute(new AnswerSphinxCommand(Blue, 0)).ThrowIfRejected();
        var record = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(record));

        Assert.Contains("\"sphinx\"", record, StringComparison.Ordinal);
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }

    // The editor (ADR-114) ------------------------------------------------------------------------

    [Fact]
    public void TheEditor_SetsASphinxsRiddle_RefusesBadOnes_AndUndoes()
    {
        var editor = TestEditor.Blank(10, 6);
        editor.Objects.Place(BuiltInMapObjects.Sphinx, new(2, 2), out var id);
        var sphinx = (Sphinx)editor.Scenario.FindObject(id)!;

        Assert.Equal(EditResult.InvalidValue, editor.Contents.SetSphinxRiddle(id, new Riddle("Q?", "a", "A ", "b"))); // two answers the same
        Assert.Equal(EditResult.InvalidValue, editor.Contents.SetSphinxRiddle(id, new Riddle(" ", "a", "b", "c")));
        Assert.Equal(EditResult.Ok, editor.Contents.SetSphinxRiddle(id, new Riddle(" What walks? ", "A man", "A dog", "A fish")));
        Assert.Equal(new Riddle("What walks?", "A man", "A dog", "A fish"), sphinx.Riddle);

        editor.Undo();
        Assert.Null(sphinx.Riddle);
        Assert.Equal(EditResult.InvalidTarget, editor.Contents.SetSphinxRiddle(new MapObjectId(999), null));
    }

    [Fact]
    public void TheEditor_SetsAWitchHutsTwoSkills_OrNone_AndUndoes()
    {
        var editor = TestEditor.Blank(10, 6);
        editor.Objects.Place(BuiltInMapObjects.WitchHut, new(2, 2), out var id);
        var hut = (WitchHut)editor.Scenario.FindObject(id)!;

        Assert.Equal(EditResult.InvalidValue, editor.Contents.SetWitchHutSkills(id, [BuiltInSkills.Archery]));
        Assert.Equal(EditResult.InvalidValue, editor.Contents.SetWitchHutSkills(id, [BuiltInSkills.Archery, BuiltInSkills.Archery]));
        Assert.Equal(EditResult.InvalidValue, editor.Contents.SetWitchHutSkills(id, [BuiltInSkills.Archery, "no.such-skill"]));
        Assert.Equal(EditResult.Ok, editor.Contents.SetWitchHutSkills(id, [BuiltInSkills.Archery, BuiltInSkills.Logistics]));
        Assert.Equal([BuiltInSkills.Archery, BuiltInSkills.Logistics], hut.Skills);
        Assert.Equal(EditResult.Ok, editor.Contents.SetWitchHutSkills(id, []));
        Assert.Empty(hut.Skills);

        editor.Undo();
        Assert.Equal([BuiltInSkills.Archery, BuiltInSkills.Logistics], hut.Skills);
    }
}
