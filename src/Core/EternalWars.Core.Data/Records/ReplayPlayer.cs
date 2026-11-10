using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Data.Records;

/// <summary>A turn of a recorded game: the number of commands played before it starts, its date and whose turn it is.</summary>
public sealed record ReplayTurn(int Position, GameDate Date, PlayerId Player);

/// <summary>
/// Plays a game record (a replay or a save, ADR-101) one command at a time for the replay viewer. <see cref="Open"/> replays the whole record
/// once to check it (a refused command or another final digest fails like a load: "made with other content") and to note where every turn
/// and day starts. <see cref="Session"/> is the game being watched; <see cref="Step"/> runs its next command, so the views see the events.
/// <see cref="Seek"/> puts a new session at any point (replayed unheard from the start, ADR-058), which is how the viewer jumps to a turn or a
/// day, back or forward. <see cref="TakeOver"/> gives a separate session at the current point, for "Play from here" in a battle; the record
/// and the watched session do not change.
/// </summary>
public sealed class ReplayPlayer
{
    private readonly Func<GameSession> _create;
    private readonly bool[] _inBattle;

    private ReplayPlayer(GameRecordDocument record, Func<GameSession> create, IReadOnlyList<ReplayTurn> turns, bool[] inBattle)
    {
        Record = record;
        _create = create;
        Turns = turns;
        _inBattle = inBattle;
        Session = Build(0);
    }

    public GameRecordDocument Record { get; }

    /// <summary>The session being watched; replaced by <see cref="Seek"/>.</summary>
    public GameSession Session { get; private set; }

    /// <summary>How many of the record's commands the watched session has played.</summary>
    public int Position { get; private set; }

    /// <summary>The record's command count.</summary>
    public int Count => Record.Commands.Count;

    public bool IsAtEnd => Position >= Count;

    /// <summary>Every turn of the record in order, the first at position 0.</summary>
    public IReadOnlyList<ReplayTurn> Turns { get; }

    /// <summary>The last day the record reaches.</summary>
    public int LastDay => Turns[^1].Date.AbsoluteDay;

    /// <summary>The turn being watched: the last one that started at or before <see cref="Position"/>.</summary>
    public ReplayTurn CurrentTurn => Turns.Last(t => t.Position <= Position);

    /// <summary>
    /// Checks the record by replaying it to its end and indexes its turns. Throws <see cref="InvalidOperationException"/> when a command is
    /// refused or the final digest differs (other content, or another version of the rules).
    /// </summary>
    public static ReplayPlayer Open(
        GameRecordDocument record,
        MapObjectCatalog? catalog = null,
        CreatureCatalog? creatures = null,
        HeroCatalog? heroRoster = null,
        BuildingCatalog? buildings = null,
        TerrainCatalog? terrains = null,
        ArtifactCatalog? artifacts = null)
    {
        GameSession Create() =>
            new(MapConverter.ToGameState(record.Map, catalog, record.Options, terrains, creatures, heroRoster, buildings, artifacts));

        var session = Create();
        var turns = new List<ReplayTurn>();
        var position = 0;
        using (session.Events.Subscribe<TurnStarted>(e => turns.Add(new ReplayTurn(position, e.Date, e.Player))))
        {
            session.Start();
            var inBattle = new bool[record.Commands.Count + 1];
            inBattle[0] = session.State.Battle is not null;
            for (var i = 0; i < record.Commands.Count; i++)
            {
                position = i + 1; // a turn that starts during this command starts after it
                var result = session.Execute(record.Commands[i]);
                if (!result.IsAccepted)
                {
                    throw new InvalidOperationException($"Replay diverged at command {i} ({record.Commands[i]}): {result.Error}");
                }

                inBattle[i + 1] = session.State.Battle is not null;
            }

            if (record.FinalDigest is { } expected && GameStateDigest.Compute(session.State) is var actual && actual != expected)
            {
                throw new InvalidOperationException($"Replay ended in state {actual}, the record says {expected}.");
            }

            return new ReplayPlayer(record, Create, turns, inBattle);
        }
    }

    /// <summary>Runs the next command on the watched session; false at the end of the record.</summary>
    public bool Step()
    {
        if (IsAtEnd)
        {
            return false;
        }

        var result = Session.Execute(Record.Commands[Position]);
        if (!result.IsAccepted)
        {
            // Only a command from outside the viewer could make the watched game differ from its record.
            throw new InvalidOperationException($"Replay diverged at command {Position}: {result.Error}");
        }

        Position++;
        return true;
    }

    /// <summary>Puts a new watched session after <paramref name="position"/> commands (clamped to the record); the old one is dropped.</summary>
    public void Seek(int position)
    {
        position = Math.Clamp(position, 0, Count);
        Session = Build(position);
        Position = position;
    }

    /// <summary>Where the next turn starts (the end of the record after the last turn).</summary>
    public int NextTurnPosition() => Turns.FirstOrDefault(t => t.Position > Position)?.Position ?? Count;

    /// <summary>Where the next day's first turn starts (the end of the record on the last day).</summary>
    public int NextDayPosition() =>
        Turns.FirstOrDefault(t => t.Date.AbsoluteDay > CurrentTurn.Date.AbsoluteDay)?.Position ?? Count;

    /// <summary>Where <paramref name="day"/>'s first turn starts (clamped to the record's first and last days).</summary>
    public int DayPosition(int day)
    {
        day = Math.Clamp(day, 1, LastDay);
        return Turns.First(t => t.Date.AbsoluteDay >= day).Position;
    }

    /// <summary>Whether a battle is being fought after <paramref name="position"/> commands (one played out in a single command never is).</summary>
    public bool IsInBattle(int position) => _inBattle[Math.Clamp(position, 0, Count)];

    /// <summary>Where the battle being watched has ended: the first position after <see cref="Position"/> without a battle; null outside one.</summary>
    public int? BattleEndPosition()
    {
        if (!IsInBattle(Position))
        {
            return null;
        }

        for (var p = Position + 1; p <= Count; p++)
        {
            if (!_inBattle[p])
            {
                return p;
            }
        }

        return Count; // the record stops in the battle (a save made there)
    }

    /// <summary>
    /// Whether "Play from here" is offered (ADR-101, UI-2 f): only in a battle being fought, and only when a person commands a side of it (the
    /// computer's sides are played by the core).
    /// </summary>
    public bool CanTakeOver =>
        Session.State.Battle is { Phase: BattlePhase.Fighting or BattlePhase.Deploying }
        && (Session.Battles.Commander(BattleSide.Attacker) ?? Session.Battles.Commander(BattleSide.Defender)) is not null;

    /// <summary>
    /// A separate game at the current point, to play the battle on by hand: replayed from the record to <see cref="Position"/>. Playing it
    /// changes neither the record nor the watched session. Throws when <see cref="CanTakeOver"/> is false.
    /// </summary>
    public GameSession TakeOver()
    {
        if (!CanTakeOver)
        {
            throw new InvalidOperationException("Play from here works only in a battle a person commands.");
        }

        return Build(Position);
    }

    private GameSession Build(int position)
    {
        var session = _create();
        session.Start();
        for (var i = 0; i < position; i++)
        {
            var result = session.Execute(Record.Commands[i]);
            if (!result.IsAccepted)
            {
                throw new InvalidOperationException($"Replay diverged at command {i}: {result.Error}");
            }
        }

        return session;
    }
}
