using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using EternalWars.Core.Generation;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Pandora's Box (ADR-100, MAP-35): "open?", an optional guard, the author's contents (gains only), then it vanishes.</summary>
public class PandorasBoxTests
{
    private static readonly string Minor = ArtifactCatalog.Default.OfTier(ArtifactTier.Minor).OrderBy(a => a.Id, StringComparer.Ordinal).First().Id;

    private static readonly BoxContents Everything = new()
    {
        Resources = new ResourceSet(Gold: 5000, Crystal: 3),
        Experience = 1000,
        Mana = 5,
        Morale = 2,
        Luck = 1,
        Attack = 2,
        Knowledge = 1,
        Skills = new Dictionary<string, SkillLevel> { [BuiltInSkills.Logistics] = SkillLevel.Advanced },
        Artifacts = [Minor],
        Creatures = [new CreatureStack(BuiltInCreatures.Wolf, 4)],
    };

    // The box stands at (3, 1), right above hero A.
    private static (GameSession Session, PandorasBox Box) World(BoxContents contents, IReadOnlyList<CreatureStack>? guard = null, string? message = null)
    {
        var state = Create(".........", ".........", "...A.....", ".........", "C.......B");
        var box = Add<PandorasBox>(state, BuiltInMapObjects.PandorasBox, 3, 1);
        (box.Contents, box.Guard, box.Message) = (contents, guard ?? [], message);
        return (Start(state), box);
    }

    [Fact]
    public void AVisit_AsksWhetherToOpen_LeavingKeepsTheBox_OpeningGivesEverythingAndTheBoxVanishes()
    {
        var (session, box) = World(Everything, message: "The lid creaks open.");
        var hero = session.State.GetHero(A);
        var (attack, knowledge, experience) = (hero.Attack, hero.Knowledge, hero.Experience);
        var opened = new List<PandorasBoxOpened>();
        session.Events.Subscribe<PandorasBoxOpened>(opened.Add);

        session.Step(A, Direction.North);
        Assert.Equal(new PandorasBoxDecision(A, box.Id), session.State.PendingDecision);
        session.Execute(new AnswerPandorasBoxCommand(Blue, Open: false)).ThrowIfRejected();
        Assert.Same(box, session.State.FindObject(box.Id));
        Assert.Empty(opened);

        var gold = session.State.GetPlayer(Blue).Resources.Gold;
        session.Interactions.Visit(hero, box);
        session.Execute(new AnswerPandorasBoxCommand(Blue, Open: true)).ThrowIfRejected();

        Assert.Null(session.State.FindObject(box.Id));
        Assert.Equal(gold + 5000, session.State.GetPlayer(Blue).Resources.Gold);
        Assert.Equal((attack + 2, knowledge + 1), (hero.Attack, hero.Knowledge));
        Assert.Equal(SkillLevel.Advanced, hero.Skills.Level(BuiltInSkills.Logistics));
        Assert.True(hero.Artifacts.Has(Minor));
        Assert.Contains(hero.Army.Stacks, s => s.Stack == new CreatureStack(BuiltInCreatures.Wolf, 4));
        Assert.Contains(hero.Statuses, s => s.Kind == HeroStatusKind.Morale && s.Value == 2 && s.Expires == StatusExpiry.NextBattle);
        Assert.Contains(hero.Statuses, s => s.Kind == HeroStatusKind.Luck && s.Value == 1 && s.Expires == StatusExpiry.NextBattle);
        Assert.Equal(experience + 1000, hero.Experience);
        var report = Assert.Single(opened);
        Assert.Equal(Everything, report.Received);
        Assert.Equal("The lid creaks open.", report.Message);
    }

    [Fact]
    public void TheGuard_FightsAfterOpen_TheBoxOpensOnlyWhenTheHeroWins()
    {
        var (session, box) = World(new BoxContents { Experience = 500 }, [new CreatureStack(BuiltInCreatures.Wolf, 2)]);
        session.Step(A, Direction.North);
        session.Execute(new AnswerPandorasBoxCommand(Blue, Open: true)).ThrowIfRejected();
        Assert.IsType<BattleDecision>(session.State.PendingDecision);
        Assert.Equal(box.Id, session.State.Battle!.Defender.Guard);

        var opened = new List<PandorasBoxOpened>();
        session.Events.Subscribe<PandorasBoxOpened>(opened.Add);
        session.AutoResolve();

        Assert.Null(session.State.FindObject(box.Id));
        Assert.Equal(500, Assert.Single(opened).Received.Experience);
    }

    [Fact]
    public void ALostBattle_LeavesTheGuardWhole_AndAHeroWithoutAnArmyIsTurnedAway()
    {
        var guard = new[] { new CreatureStack(BuiltInCreatures.Wolf, 300) };
        var (session, box) = World(new BoxContents { Experience = 500 }, guard);
        session.Step(A, Direction.North);
        session.Execute(new AnswerPandorasBoxCommand(Blue, Open: true)).ThrowIfRejected();
        session.AutoResolve();

        Assert.DoesNotContain(session.State.Heroes, h => h.Id == A);
        Assert.Equal(guard, session.State.ObjectsOf<PandorasBox>().Single().Guard);

        var (other, guarded) = World(new BoxContents { Experience = 500 }, guard);
        var hero = other.State.GetHero(A);
        hero.Army.Set(hero.Army.Stacks.Single().Slot, null);
        var refused = new List<MapObjectRefused>();
        other.Events.Subscribe<MapObjectRefused>(refused.Add);
        other.Interactions.Visit(hero, guarded);
        Assert.Null(other.State.PendingDecision);
        Assert.Single(refused);
    }

    [Fact]
    public void WhatTheHeroHasNoRoomFor_IsLost_AndTheReportLeavesItOut()
    {
        var (session, box) = World(Everything with { Experience = 0 });
        var hero = session.State.GetHero(A);
        var fillers = CreatureCatalog.Default.All.Where(c => c.Id != BuiltInCreatures.Wolf && c.Id != BuiltInCreatures.Footman).Take(Army.SlotCount - 1).ToList();
        foreach (var filler in fillers)
        {
            hero.Army.TryAdd(new CreatureStack(filler.Id, 1));
        }

        foreach (var skill in BuiltInSkills.All.GroupBy(s => s.Group).Where(g => g.Key != BuiltInSkills.Get(BuiltInSkills.Logistics).Group).Take(HeroSkills.MaxGroups))
        {
            hero.Skills.Set(skill.First().Id, SkillLevel.Basic);
        }

        var opened = new List<PandorasBoxOpened>();
        session.Events.Subscribe<PandorasBoxOpened>(opened.Add);
        session.Step(A, Direction.North);
        session.Execute(new AnswerPandorasBoxCommand(Blue, Open: true)).ThrowIfRejected();

        var received = Assert.Single(opened).Received;
        Assert.Empty(received.Creatures);
        Assert.Empty(received.Skills);
        Assert.Equal(SkillLevel.None, hero.Skills.Level(BuiltInSkills.Logistics));
        Assert.DoesNotContain(hero.Army.Stacks, s => s.Stack.Creature == BuiltInCreatures.Wolf);
        Assert.Equal(Everything.Resources, received.Resources);
    }

    [Fact]
    public void TheBox_IsInTheMapFormat_TheValidatorWantsContents_TheEditorSetsItUndoably_AndAGameWithItReplays()
    {
        var editor = new MapEditorSession(Create(".........", ".........", "...A.....", ".........", "C.......B"));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.PandorasBox, new(3, 1), out var placed));
        var box = (PandorasBox)editor.Scenario.GetObject(placed);
        Assert.Contains(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error && i.Message.Contains("is empty", StringComparison.Ordinal));

        var guard = new[] { new CreatureStack(BuiltInCreatures.Wolf, 2) };
        Assert.Equal(EditResult.InvalidValue, editor.SetPandorasBox(box.Id, new BoxContents { Experience = -5 }, [], null));
        Assert.Equal(EditResult.InvalidValue, editor.SetPandorasBox(box.Id, Everything, [new CreatureStack("no-such-creature", 1)], null));
        Assert.Equal(EditResult.Ok, editor.SetPandorasBox(box.Id, Everything, guard, "Beware."));
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error);
        Assert.Equal(EditResult.Ok, editor.SetPandorasBox(box.Id, Everything with { Artifacts = ["no-such-artifact"] }, guard, null));
        Assert.Contains(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("no-such-artifact", StringComparison.Ordinal));
        editor.Undo();
        Assert.Equal((Everything, "Beware."), (box.Contents, box.Message));

        var map = MapConverter.ToDocument(editor.Scenario);
        var json = MapSerializer.Serialize(map);
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json)).ObjectsOf<PandorasBox>().Single();
        Assert.Contains("\"kind\": \"pandorasBox\"", json, StringComparison.Ordinal);
        Assert.Equal(27, MapDocument.CurrentFormatVersion);
        Assert.Equal((Everything, "Beware."), (restored.Contents, restored.Message));
        Assert.Equal(guard, restored.Guard);
        Assert.Throws<FormatException>(() => MapSerializer.Deserialize(json.Replace("\"experience\": 1000", "\"experience\": -1", StringComparison.Ordinal)) is var bad
            ? MapConverter.ToGameState(bad) : null);

        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 3)));
        session.Start();
        session.Step(A, Direction.North);
        session.Execute(new AnswerPandorasBoxCommand(Blue, Open: true)).ThrowIfRejected();
        session.AutoResolve();
        var record = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(record));
        Assert.Contains("\"pandorasBox\"", record, StringComparison.Ordinal);
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }

    [Fact]
    public void TheAi_OpensABoxWorthIt_AndLeavesOneWhoseGuardItCannotBeat()
    {
        var state = Create("A.......B");
        var rich = Add<PandorasBox>(state, BuiltInMapObjects.PandorasBox, 3, 0);
        rich.Contents = new BoxContents { Resources = new ResourceSet(Gold: 8000) };
        var session = Start(state);
        var gold = state.GetPlayer(Blue).Resources.Gold;

        new AdventureAi(session, controlHumanSlots: true).PlayTurn();

        Assert.Null(state.FindObject(rich.Id));
        Assert.Equal(gold + 8000, state.GetPlayer(Blue).Resources.Gold);

        var guarded = Create("A.......B");
        var deadly = Add<PandorasBox>(guarded, BuiltInMapObjects.PandorasBox, 3, 0);
        (deadly.Contents, deadly.Guard) = (new BoxContents { Resources = new ResourceSet(Gold: 8000) }, [new CreatureStack(BuiltInCreatures.Wolf, 500)]);
        var other = Start(guarded);
        var ai = new AdventureAi(other, controlHumanSlots: true);
        ai.PlayTurn();

        Assert.NotNull(guarded.FindObject(deadly.Id));
        Assert.Contains(guarded.Heroes, h => h.Id == A);
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void TheGenerator_PlacesBoxesOfGoldExperienceOrCreatures_WorthTheirBand()
    {
        var boxes = Enumerable.Range(1, 6)
            .SelectMany(seed => MapGenerator.Generate(BuiltInTemplates.All[0], 72, (ulong)seed).Objects.OfType<PandorasBox>())
            .ToList();

        Assert.NotEmpty(boxes);
        Assert.All(boxes, b =>
        {
            Assert.False(b.Contents.IsEmpty);
            Assert.Null(b.Contents.Problem());
            Assert.Empty(b.Guard); // the generator guards it with a monster next to it
            Assert.Equal(1, new[] { !b.Contents.Resources.IsEmpty, b.Contents.Experience > 0, b.Contents.Creatures.Count > 0 }.Count(x => x));
        });
    }
}
