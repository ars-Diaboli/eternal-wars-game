using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>What a running event waits for.</summary>
internal enum MapEventWaiting
{
    None,
    Decision,
    Battle,
}

/// <summary>A list of actions being taken: a branch, an answer, a loop's body, or a whole event's actions (<see cref="Event"/> set).</summary>
internal sealed class MapEventFrame(IReadOnlyList<EventAction> actions)
{
    public IReadOnlyList<EventAction> Actions { get; } = actions;

    public int Next { get; set; }

    /// <summary>The event these are the actions of (the run's own or a run event); a stop ends the frames up to it.</summary>
    public MapEventDefinition? Event { get; init; }

    /// <summary>A repeat's rounds still to come after this one.</summary>
    public int RoundsLeft { get; set; }

    /// <summary>A while loop's condition, tested again when a round ends.</summary>
    public EventCondition? While { get; init; }

    /// <summary>The rounds a while loop has begun.</summary>
    public int Rounds { get; set; }

    /// <summary>The digest's words for the frame: whose actions, where it is in them, its loop counts.</summary>
    public string Describe() => $"{Event?.Name ?? "-"}@{Next}/{Actions.Count} left {RoundsLeft} rounds {Rounds}";
}

/// <summary>One event running for a player, hero, town and cell; a quest's run carries the actions of its visit instead of the event's.</summary>
internal sealed class MapEventRun(
    MapEventDefinition definition, PlayerId? player, HeroId? hero, MapObjectId? town, MapPosition? cell, bool startOfTurn, IReadOnlyList<EventAction>? actions = null)
{
    public MapEventDefinition Event { get; } = definition;

    public PlayerId? Player { get; } = player;

    public HeroId? Hero { get; } = hero;

    public MapObjectId? Town { get; } = town;

    public MapPosition? Cell { get; } = cell;

    public bool StartOfTurn { get; } = startOfTurn;

    /// <summary>The last place the run touched (an area it revealed, a hero it gave): what a later turn-start message concerns.</summary>
    public MapPosition? Place { get; set; }

    public Stack<MapEventFrame> Frames { get; } = new([new MapEventFrame(actions ?? definition.Actions) { Event = definition }]);

    /// <summary>The actions taken so far (<see cref="MapEventService.MaxActionsPerRun"/>).</summary>
    public int Steps { get; set; }

    public MapEventWaiting Waiting { get; set; }

    public IReadOnlyList<EventAnswer>? Answers { get; set; }

    /// <summary>The digest's words for the run: its event and for whom, what it waits for, and its frames innermost first.</summary>
    public string Describe() =>
        $"{Event.Name} player {Player?.Value} hero {Hero?.Value} town {Town?.Value} cell {Cell} place {Place} steps {Steps} {Waiting}"
        + $"{(Answers is null ? "" : $" answers {Answers.Count}")} frames {string.Join(';', Frames.Select(f => f.Describe()))}";
}

/// <summary>The battle being fought, as it began: who fought it for whom, for the "defeated by" records (EVT-3 e).</summary>
internal sealed record MapEventFight(HeroId? Attacker, HeroId? Defender, MapObjectId? Guard, PlayerId? AttackerOwner, PlayerId? DefenderOwner);
