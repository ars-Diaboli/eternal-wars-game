using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Events;
using EternalWars.Core.Domain.Game;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Data;

/// <summary>Saved games (ADR-058): any moment of a game, a battle included, comes back exactly.</summary>
public sealed class SaveGameTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ew-saves-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static (GameSession Session, MapDocument Initial) NewGame()
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var initial = MapConverter.ToDocument(state);
        return (Start(state, revealAll: false), initial);
    }

    [Fact]
    public void ASavedGame_LoadsToTheSameStateOnTheAdventureMap()
    {
        var (session, initial) = NewGame();
        session.Step(A, Direction.East);
        session.EndTurn();
        session.EndTurn();

        var path = GameSaves.Save(_folder, "Day two", initial, session);
        var (loaded, record) = GameSaves.Load(path);

        Assert.Equal(GameStateDigest.Compute(session.State), GameStateDigest.Compute(loaded.State));
        Assert.Equal(session.CommandLog.Count, loaded.CommandLog.Count);
        Assert.Equal("Day two", record.Save!.Name);
        Assert.False(record.Save.InBattle);

        // The loaded game goes on like the original.
        loaded.Step(A, Direction.East);
        session.Step(A, Direction.East);
        Assert.Equal(GameStateDigest.Compute(session.State), GameStateDigest.Compute(loaded.State));
    }

    [Fact]
    public void AGameSavedInTheMiddleOfABattle_LoadsIntoThatBattle()
    {
        var (session, initial) = NewGame();
        session.MoveTo(A, new MapPosition(2, 0));
        Assert.IsType<BattleDecision>(session.State.PendingDecision);
        var player = session.ActivePlayer();
        Assert.True(session.Execute(new BattleDefendCommand(player)).IsAccepted);
        Assert.NotNull(session.State.Battle);

        var path = GameSaves.Save(_folder, GameSaves.QuickSaveName, initial, session);
        var (loaded, record) = GameSaves.Load(path);

        Assert.True(record.Save!.InBattle);
        Assert.NotNull(loaded.State.Battle);
        Assert.Equal(session.State.Battle!.Round, loaded.State.Battle!.Round);
        Assert.Equal(session.State.Battle.ActiveUnit!.Id, loaded.State.Battle.ActiveUnit!.Id);
        Assert.Equal(GameStateDigest.Compute(session.State), GameStateDigest.Compute(loaded.State));

        // Fighting on from the loaded game ends exactly like the original.
        session.AutoResolve();
        loaded.AutoResolve();
        Assert.Null(loaded.State.Battle);
        Assert.Equal(GameStateDigest.Compute(session.State), GameStateDigest.Compute(loaded.State));
    }

    [Fact]
    public void ASave_SurvivesTheFileFormat()
    {
        var (session, initial) = NewGame();
        session.MoveTo(A, new MapPosition(2, 0));
        var json = GameRecordSerializer.Serialize(GameReplay.Record(initial, session, "x"));

        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(json));

        Assert.Equal(GameStateDigest.Compute(session.State), GameStateDigest.Compute(replayed.State));
    }

    [Fact]
    public void ALoadWithChangedContent_IsRefusedWithAnError()
    {
        var (session, initial) = NewGame();
        session.EndTurn();
        var path = GameSaves.Save(_folder, "x", initial, session);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"finalDigest\": \"", "\"finalDigest\": \"0000"));

        Assert.Throws<InvalidOperationException>(() => GameSaves.Load(path));
    }

    [Fact]
    public void List_ShowsSavesNewestFirst_AndDamagedFilesWithTheirError()
    {
        var (session, initial) = NewGame();
        var first = GameSaves.Save(_folder, "first", initial, session);
        File.SetLastWriteTimeUtc(first, DateTime.UtcNow.AddMinutes(-5));
        session.EndTurn();
        GameSaves.Save(_folder, "second", initial, session);
        File.WriteAllText(Path.Combine(_folder, "broken" + GameRecordSerializer.SaveExtension), "{ not json");

        var entries = GameSaves.List(_folder);

        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, e => e.Info?.Name == "first");
        Assert.Contains(entries, e => e.Info is null && e.Error is not null);
        Assert.Equal("first", entries[^1].Info?.Name);
        Assert.Empty(GameSaves.List(Path.Combine(_folder, "missing")));
    }

    [Fact]
    public void SavingAgainUnderTheSameName_ReplacesTheOldSave()
    {
        var (session, initial) = NewGame();
        GameSaves.Save(_folder, GameSaves.QuickSaveName, initial, session);
        session.EndTurn();
        GameSaves.Save(_folder, GameSaves.QuickSaveName, initial, session);

        var only = Assert.Single(GameSaves.List(_folder));
        Assert.Single(GameRecordSerializer.Load(only.Path).Commands);
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Fact]
    public void Autosave_KeepsTheStartOfTheCurrentAndThePreviousTurn()
    {
        var (session, initial) = NewGame();
        GameSaves.Autosave(_folder, initial, session); // turn 1
        session.EndTurn();
        GameSaves.Autosave(_folder, initial, session); // turn 2
        session.EndTurn();
        GameSaves.Autosave(_folder, initial, session); // turn 3

        var current = GameRecordSerializer.Load(GameSaves.PathFor(_folder, GameSaves.AutosaveCurrentName));
        var previous = GameRecordSerializer.Load(GameSaves.PathFor(_folder, GameSaves.AutosavePreviousName));

        Assert.Equal(2, current.Commands.Count);
        Assert.Single(previous.Commands);
        Assert.Equal(2, GameSaves.List(_folder).Count);
        Assert.Equal(AutosaveSlot.Current, GameSaves.SlotOf(GameSaves.PathFor(_folder, GameSaves.AutosaveCurrentName)));
        Assert.Equal(AutosaveSlot.Previous, GameSaves.SlotOf(GameSaves.PathFor(_folder, GameSaves.AutosavePreviousName)));
        Assert.Null(GameSaves.SlotOf(GameSaves.PathFor(_folder, "my save")));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Fact]
    public void AutosavingWhenTheTurnBegins_AfterTheCommand_LoadsToTheStartOfThatTurn()
    {
        var (session, initial) = NewGame();
        var turnBegan = false;
        var saves = 0;
        session.Events.Subscribe<TurnStarted>(_ => turnBegan = true);
        session.Events.Subscribe<CommandExecuted>(_ =>
        {
            if (turnBegan)
            {
                turnBegan = false;
                saves++;
                GameSaves.Autosave(_folder, initial, session);
            }
        });

        session.Step(A, Direction.East); // not a turn start
        Assert.Equal(0, saves);
        session.EndTurn();
        var atRedStart = GameStateDigest.Compute(session.State);
        session.EndTurn();
        var atBlueStart = GameStateDigest.Compute(session.State);
        session.Step(A, Direction.East);

        Assert.Equal(2, saves);
        var (current, _) = GameSaves.Load(GameSaves.PathFor(_folder, GameSaves.AutosaveCurrentName));
        var (previous, _) = GameSaves.Load(GameSaves.PathFor(_folder, GameSaves.AutosavePreviousName));
        Assert.Equal(atBlueStart, GameStateDigest.Compute(current.State));
        Assert.Equal(atRedStart, GameStateDigest.Compute(previous.State));
        Assert.Equal(Red, previous.State.CurrentPlayer.Id);
    }

    [Fact]
    public void CommandExecuted_IsPublishedAfterTheCommandIsLogged_AndNotForRefusedCommands()
    {
        var (session, _) = NewGame();
        var logSizes = new List<int>();
        session.Events.Subscribe<CommandExecuted>(_ => logSizes.Add(session.CommandLog.Count));

        session.EndTurn();
        Assert.False(session.Execute(new EndTurnCommand(Blue)).IsAccepted); // not Blue's turn

        Assert.Equal([1], logSizes);
    }

    [Fact]
    public void PlayersCannotSaveOverAnAutosaveSlot()
    {
        var (session, initial) = NewGame();

        Assert.Throws<ArgumentException>(() => GameSaves.Save(_folder, "Autosave-Current", initial, session));
    }

    [Theory]
    [InlineData("My game", "My game")]
    [InlineData("a/b:c", "a_b_c")]
    [InlineData("  ", "save")]
    [InlineData("..", "save")]
    public void Sanitize_MakesAUsableFileName(string text, string expected) => Assert.Equal(expected, GameSaves.Sanitize(text));
}
