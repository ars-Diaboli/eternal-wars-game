using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents.Actions;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>
/// Runs the map events (ADR-115, step 1: EVT-1 … EVT-4). Triggers queue runs: a hero stepping onto an event cell, a player's turn start (its player
/// events, then the town events of its towns, in list order), the start of a day (neutral towns' events), a town changing hands and a battle won
/// (ADR-242). Runs go one at a time; a run pauses on
/// a message, a question, a full army or a battle, or when something else (a level-up) is pending, and <see cref="Pump"/> carries on once nothing
/// is (the session calls it after every command). A disabled event is skipped when its turn comes.
/// </summary>
public sealed class MapEventService(
    GameState state, IEventPublisher events, Visibility.IFogRevealer fog, EconomyService economy, ProgressionService progression, ArtifactService artifacts, BattleService battles,
    TownVisitService towns, HeroRecruitment recruits)
    : IEventHandler<BattleStarted>, IEventHandler<BattleEnded>, IEventHandler<TownOwnerChanged>, Turns.ITurnPhaseParticipant
{
    /// <summary>The town's buildings an event builds (<see cref="BuildAction"/>).</summary>
    internal TownConstruction Construction { get; } = new(state, events, towns);

    /// <summary>The messages, questions and army choices an event asks, and the objects it removes.</summary>
    internal DecisionSteps Steps { get; } = new(state, events);

    /// <summary>The most morale or luck an event gives or takes (HoMM3's ±3).</summary>
    public const int MaxMoraleOrLuck = 3;

    /// <summary>The most rounds a loop runs (<see cref="RepeatAction"/>, <see cref="WhileAction"/>).</summary>
    public const int MaxLoopRounds = 100;

    /// <summary>How deep events may run one another (<see cref="RunEventAction"/>); a deeper run is skipped, so an event running itself ends.</summary>
    public const int MaxRunDepth = 16;

    /// <summary>The most actions one run takes; past it the run ends (a guard against loops in loops in events running themselves).</summary>
    public const int MaxActionsPerRun = 100_000;

    /// <summary>The events' state, the running ones too (<see cref="MapEventState.Runs"/>): the service keeps none of its own.</summary>
    internal MapEventState Data => state.MapEvents;

    /// <summary>The game the events run in; it and the services below are what the action handlers work with (<see cref="MapEventActionContext"/>).</summary>
    internal GameState State => state;

    internal IEventPublisher Events => events;

    internal Visibility.IFogRevealer Fog => fog;

    internal EconomyService Economy => economy;

    internal ProgressionService Progression => progression;

    internal ArtifactService Artifacts => artifacts;

    internal BattleService Battles => battles;

    internal HeroRecruitment Recruits => recruits;

    /// <summary>Whether an event is running or waiting to run.</summary>
    public bool IsBusy => Data.Current is not null || Data.Runs.Count > 0;

    /// <summary>
    /// The question waiting for an answer with what each answer does, so a computer player can weigh them (EVT-6 a); null when no question waits.
    /// </summary>
    public MapEventQuestion? PendingQuestion =>
        Data.Current is { Waiting: MapEventWaiting.Decision, Answers: { } answers } run && state.PendingDecision is MapEventDecision { IsQuestion: true }
            ? new MapEventQuestion(answers, run.Player, run.Hero, run.Town)
            : null;

    /// <summary>The quest object whose stage the answer completes (a quest's "Complete"), or null.</summary>
    public static MapObjectId? CompletedSite(EventAnswer answer) =>
        MapEventTree.Actions(answer.Actions).OfType<AdvanceQuestAction>().FirstOrDefault()?.Site;

    /// <summary>Subscribes to the battles ("defeated by" records, EVT-3 e; battle events) and to towns changing hands (captured events, ADR-242).</summary>
    internal void SubscribeTo(IEventSubscriber bus)
    {
        bus.Subscribe<BattleStarted>(this);
        bus.Subscribe<BattleEnded>(this);
        bus.Subscribe<TownOwnerChanged>(this);
    }

    // Triggers ------------------------------------------------------------------------------------------------------

    /// <summary>A hero stepped onto <paramref name="cell"/>: its hero event runs, on every step until it disables or removes itself (EVT-2 b).</summary>
    internal void OnHeroEntered(Hero hero, MapPosition cell)
    {
        if (Data.Cells.TryGetValue(cell, out var name) && Data.QueueHeroEvent(name, hero, cell))
        {
            Pump();
        }
    }

    /// <summary>The player's turn begins (after income, growth and the week's event): its player events and its towns' events, in list order.</summary>
    internal void StartTurn(PlayerId player)
    {
        foreach (var definition in Data.Set.Events)
        {
            if (definition.Kind == MapEventKind.Player)
            {
                Data.Runs.Enqueue(new MapEventRun(definition, player, null, null, null, startOfTurn: true));
            }
            else if (definition.Kind == MapEventKind.Town)
            {
                foreach (var town in TownsOf(definition).Where(t => t.Owner == player))
                {
                    Data.Runs.Enqueue(new MapEventRun(definition, player, null, town.Id, null, startOfTurn: true));
                }
            }
        }

        Pump();
    }

    /// <summary>A day begins: the events of neutral towns run before the first player's turn (EVT-2 d).</summary>
    internal void StartDay()
    {
        foreach (var definition in Data.Set.Events.Where(e => e.Kind == MapEventKind.Town))
        {
            foreach (var town in TownsOf(definition).Where(t => t.Owner is null))
            {
                Data.Runs.Enqueue(new MapEventRun(definition, null, null, town.Id, null, startOfTurn: true));
            }
        }

        Pump();
    }

    void Turns.ITurnPhaseParticipant.OpenDay() => StartDay();

    // The player's and its towns' map events, after income, growth and the week's event (ADR-115, EVT-2 d).
    void Turns.ITurnPhaseParticipant.OpenTurn(PlayerId player) => StartTurn(player);

    /// <summary>
    /// A hero visits a quest object (EVT-5): a Seer's Hut it entered, a Quest Guard it bumped into, a Quest Gate it may not pass, or a gate it
    /// walks through. Not meeting the stage shows its proposal (later visits: its progress) and writes the player's quest log; meeting it asks
    /// whether to complete it (a computer player takes the first answer). A gate's passage only marks the quest done in the log.
    /// </summary>
    internal void VisitQuest(Hero hero, QuestSite site)
    {
        events.Publish(new MapObjectVisited(hero.Id, site.Id));
        if (QuestRules.Current(state, site) is not var (quest, stage))
        {
            events.Publish(new MapObjectFoundEmpty(hero.Id, site.Id));
            return;
        }

        var seen = Data.QuestLog(hero.Owner).Any(e => e.Site == site.Id && e.Stage == site.Stage);
        var met = MapEventEvaluator.Test(state, new EventScope(hero.Owner, hero, null), stage.Requirement);
        if (site.Kind == MapObjectKind.QuestGate && met)
        {
            WriteLog(hero.Owner, new QuestLogEntry(site.Id, quest.Name, site.Stage, Done: true));
            return;
        }

        WriteLog(hero.Owner, new QuestLogEntry(site.Id, quest.Name, site.Stage, Done: false));
        IReadOnlyList<EventAction> actions = met
            ? [new QuestionAction(
                string.IsNullOrWhiteSpace(stage.Completion) ? $"{stage.Proposal}\n\nComplete the quest?" : stage.Completion,
                [new EventAnswer("Complete", [.. stage.Actions, new AdvanceQuestAction(site.Id, site.Stage)]), new EventAnswer("Not now", [])])]
            : [new MessageAction(seen && !string.IsNullOrWhiteSpace(stage.Progress) ? stage.Progress : stage.Proposal)];
        Data.Runs.Enqueue(new MapEventRun(quest, hero.Owner, hero.Id, null, null, startOfTurn: false, actions));
        Pump();
    }

    internal void WriteLog(PlayerId player, QuestLogEntry entry)
    {
        if (Data.SetQuestLog(player, entry))
        {
            events.Publish(new QuestLogChanged(player));
        }
    }

    private IEnumerable<Town> TownsOf(MapEventDefinition definition) =>
        definition.Towns.Select(id => state.FindObject(new MapObjectId(id))).OfType<Town>();

    // Running -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Runs queued events until one waits for something or none is left. When all are done and nothing is pending, any level-up an event caused
    /// is offered.
    /// </summary>
    internal void Pump()
    {
        // Level-ups are offered here only after an event ran, so games without events take their turns exactly as before.
        var ran = false;
        while (state.PendingDecision is null && state.Battle is null && !state.IsOver && state.ScriptedEnd is null)
        {
            ran |= Data.Current is not null || Data.Runs.Count > 0;
            if (Data.Current is null)
            {
                if (!Data.Runs.TryDequeue(out var next))
                {
                    break;
                }

                if (!Data.IsEnabled(next.Event.Name))
                {
                    continue;
                }

                Data.Current = next;
                events.Publish(new MapEventRan(next.Event.Name, next.Event.Kind, next.Player, next.Hero, next.Town));
            }

            var run = Data.Current;
            switch (run.Waiting)
            {
                // The answer went away unanswered (its hero left the map): the event ends.
                case MapEventWaiting.Decision:
                    Data.Current = null;
                    continue;

                // The battle is over: a hero still on the map won it; lost, fled or surrendered, the event stops (EVT-4 d).
                case MapEventWaiting.Battle when HeroOf(run) is null:
                    Data.Current = null;
                    continue;
                case MapEventWaiting.Battle:
                    run.Waiting = MapEventWaiting.None;
                    break;
            }

            Continue(run);
            if (run.Waiting == MapEventWaiting.None && run.Frames.Count == 0)
            {
                Data.Current = null;
            }
        }

        if (ran && !IsBusy && state.PendingDecision is null && state.Battle is null)
        {
            progression.OfferNext();
        }
    }

    /// <summary>Runs the actions until the event ends, waits, or something else becomes pending.</summary>
    private void Continue(MapEventRun run)
    {
        while (run.Waiting == MapEventWaiting.None && run.Frames.Count > 0 && state.PendingDecision is null && state.Battle is null && !state.IsOver
               && state.ScriptedEnd is null)
        {
            var frame = run.Frames.Peek();
            if (frame.Next >= frame.Actions.Count)
            {
                if (frame.While is { } condition && frame.Rounds < MaxLoopRounds && Test(run, condition))
                {
                    frame.Rounds++;
                    frame.Next = 0;
                }
                else if (frame.RoundsLeft > 0)
                {
                    frame.RoundsLeft--;
                    frame.Next = 0;
                }
                else
                {
                    run.Frames.Pop();
                }

                continue;
            }

            if (++run.Steps > MaxActionsPerRun)
            {
                run.Frames.Clear();
                break;
            }

            if (!Apply(run, frame.Actions[frame.Next++]))
            {
                Stop(run);
            }
        }
    }

    /// <summary>Ends the event the run is in: its frames go, up to and with the one that started it (a run event returns to the one that ran it).</summary>
    private static void Stop(MapEventRun run)
    {
        while (run.Frames.TryPop(out var frame) && frame.Event is null)
        {
        }
    }

    /// <summary>The event whose actions the run is taking now: the innermost run event, else the run's own.</summary>
    internal static MapEventDefinition EventOf(MapEventRun run) => run.Frames.FirstOrDefault(f => f.Event is not null)?.Event ?? run.Event;

    /// <summary>Answers the pending message or question (<see cref="MapEventDecision"/>) and goes on (the steps of ADR-215).</summary>
    internal void Answer(int option)
    {
        const string refusal = "No map event is waiting for an answer.";
        var decision = Steps.TakeDecision<MapEventDecision>(refusal);
        var run = WaitingRun(refusal);

        if (option < 0 || option >= Math.Max(1, decision.Answers.Count))
        {
            throw new CommandRefusedException(decision.IsQuestion ? $"Answer 0 to {decision.Answers.Count - 1}." : "A message is closed with 0.");
        }

        run.Waiting = MapEventWaiting.None;
        if (run.Answers is { } answers)
        {
            run.Frames.Push(new MapEventFrame(answers[option].Actions));
            run.Answers = null;
        }

        Steps.Resolve(decision);
        Pump();
    }

    /// <summary>Keeps the chosen stacks of a full army and its newcomers (<see cref="MapEventArmyDecision"/>), then goes on (the steps of ADR-215).</summary>
    internal void ChooseArmy(IReadOnlyList<int> keep)
    {
        const string refusal = "No map event is waiting for an army choice.";
        var decision = Steps.TakeDecision<MapEventArmyDecision>(refusal);
        var run = WaitingRun(refusal);

        if (keep.Count is < 1 or > Army.SlotCount || keep.Distinct().Count() != keep.Count || keep.Any(i => i < 0 || i >= decision.Stacks.Count))
        {
            throw new CommandRefusedException($"Keep one to {Army.SlotCount} different stacks of the {decision.Stacks.Count}.");
        }

        var hero = state.GetHero(decision.Hero);
        run.Waiting = MapEventWaiting.None;
        Steps.Resolve(decision);
        Refill(hero.Army, keep.Select(i => decision.Stacks[i]));
        events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        Pump();
    }

    /// <summary>The run waiting for the pending decision's answer; otherwise the answer is refused with <paramref name="refusal"/>.</summary>
    private MapEventRun WaitingRun(string refusal) =>
        Data.Current is { Waiting: MapEventWaiting.Decision } run ? run : throw new CommandRefusedException(refusal);

    // Actions -------------------------------------------------------------------------------------------------------

    /// <summary>Applies one action through its handler (<see cref="MapEventActionHandlers"/>); false stops the event.</summary>
    private bool Apply(MapEventRun run, EventAction action)
    {
        var hero = HeroOf(run);
        var town = run.Town is { } townId ? state.FindObject(townId) as Town : null;
        return MapEventActionHandlers.For(action).Apply(new MapEventActionContext(this, run, hero, town), action);
    }

    /// <summary>
    /// What a turn-start message or question concerns (EVT-2 f), for the camera: a place its own rewards name (a revealed area's centre, a
    /// hero given at a cell), else the last place the run touched (an area it revealed, a hero it gave), else the town of a town event. Null
    /// for runs that are not a turn start: a hero event's hero is already in view.
    /// </summary>
    internal MapPosition? PlaceOf(MapEventRun run, IReadOnlyList<EventAction> rewards)
    {
        if (!run.StartOfTurn)
        {
            return null;
        }

        foreach (var reward in rewards)
        {
            if (MapEventActionHandlers.For(reward).Place(reward) is { } place)
            {
                return place;
            }
        }

        return run.Place ?? (run.Town is { } id && state.FindObject(id) is Town town ? town.VisitPosition : null);
    }

    internal void Ask(MapEventRun run, PendingDecision decision)
    {
        run.Waiting = MapEventWaiting.Decision;
        Steps.Ask(decision);
    }

    /// <summary>Empties the army and fills it with the stacks, in order.</summary>
    internal static void Refill(Army army, IEnumerable<CreatureStack> stacks)
    {
        var kept = stacks.ToList();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            army.Set(slot, null);
        }

        foreach (var stack in kept)
        {
            army.TryAdd(stack);
        }
    }

    // Expressions and conditions ------------------------------------------------------------------------------------

    internal Hero? HeroOf(MapEventRun run) => run.Hero is { } id ? state.Heroes.FirstOrDefault(h => h.Id == id) : null;

    internal int Evaluate(MapEventRun run, EventExpr expr) => MapEventEvaluator.Evaluate(state, Scope(run), expr);

    internal bool Test(MapEventRun run, EventCondition condition) => MapEventEvaluator.Test(state, Scope(run), condition);

    private EventScope Scope(MapEventRun run) => new(run.Player, HeroOf(run), run.Town);

    // Defeat records (EVT-3 e) --------------------------------------------------------------------------------------

    public void Handle(BattleStarted @event) =>
        Data.Fight = new(@event.AttackerHero, @event.DefenderHero, @event.Guard, OwnerOf(@event.AttackerHero), OwnerOf(@event.DefenderHero));

    public void Handle(BattleEnded @event)
    {
        if (Data.Fight is not { } battle)
        {
            return;
        }

        Data.Fight = null;
        var (beatenHero, beatenGuard, winner, winnerHero) = @event.Winner == BattleSide.Attacker
            ? (battle.Defender, battle.Guard, battle.AttackerOwner, battle.Attacker)
            : (battle.Attacker, (MapObjectId?)null, battle.DefenderOwner, battle.Defender);
        if (winner is { } victor && Data.Current is not { Waiting: MapEventWaiting.Battle })
        {
            // A battle won (ADR-242): the battle events run for the winner once the battle is over; an event's own battle runs none, so an event
            // that fights cannot run itself.
            foreach (var definition in Data.Set.Events.Where(e => e.Kind == MapEventKind.Battle))
            {
                Data.Runs.Enqueue(new MapEventRun(definition, victor, winnerHero, null, null, startOfTurn: false));
            }
        }

        if (winner is not { } by || @event.Reason != BattleEndReason.Defeated)
        {
            return;
        }

        if (beatenGuard is { } monster && Data.WatchedMonsters.Contains(monster))
        {
            Data.RecordMonsterDefeat(monster, by);
        }

        if (beatenHero is { } heroId && Data.WatchedHeroes.Contains(heroId))
        {
            Data.RecordHeroDefeat(heroId, by);
        }
    }

    /// <summary>
    /// A town fell to a player (ADR-242): the captured events that name it run once the command that took it is done (the session pumps after every
    /// command), for the new owner and the hero standing at its gate.
    /// </summary>
    public void Handle(TownOwnerChanged @event)
    {
        if (!state.IsStarted || @event.Current is not { } owner || state.FindObject(@event.Town) is not Town town)
        {
            return;
        }

        var hero = state.Heroes.FirstOrDefault(h => h.Owner == owner && h.Position == town.VisitPosition)?.Id;
        foreach (var definition in Data.Set.Events.Where(e => e.Kind == MapEventKind.Captured && e.Towns.Contains(town.Id.Value)))
        {
            Data.Runs.Enqueue(new MapEventRun(definition, owner, hero, town.Id, null, startOfTurn: false));
        }
    }

    private PlayerId? OwnerOf(HeroId? hero) =>
        hero is { } id && state.Heroes.Concat(state.GarrisonHeroes).FirstOrDefault(h => h.Id == id) is { } found ? found.Owner : null;

    /// <summary>The last step of a quest stage's completion: the quest object moves on (not map data, so no map can hold it).</summary>
    internal sealed record AdvanceQuestAction(MapObjectId Site, int Stage) : EventAction;
}
