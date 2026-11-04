using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>
/// Runs the map events (ADR-115, step 1: EVT-1 … EVT-4). Triggers queue runs: a hero stepping onto an event cell, a player's turn start (its player
/// events, then the town events of its towns, in list order) and the start of a day (neutral towns' events). Runs go one at a time; a run pauses on
/// a message, a question, a full army or a battle, or when something else (a level-up) is pending, and <see cref="Pump"/> carries on once nothing
/// is (the session calls it after every command). A disabled event is skipped when its turn comes.
/// </summary>
public sealed class MapEventService(
    GameState state, IEventPublisher events, EconomyService economy, ProgressionService progression, ArtifactService artifacts, BattleService battles)
    : IEventHandler<BattleStarted>, IEventHandler<BattleEnded>
{
    /// <summary>The most morale or luck an event gives or takes (HoMM3's ±3).</summary>
    public const int MaxMoraleOrLuck = 3;

    private readonly Queue<Run> _queue = new();
    private Run? _current;
    private (HeroId? Attacker, HeroId? Defender, MapObjectId? Guard, PlayerId? AttackerOwner, PlayerId? DefenderOwner)? _battle;

    private MapEventState Data => state.MapEvents;

    /// <summary>Whether an event is running or waiting to run.</summary>
    public bool IsBusy => _current is not null || _queue.Count > 0;

    // Triggers ------------------------------------------------------------------------------------------------------

    /// <summary>A hero stepped onto <paramref name="cell"/>: its hero event runs, on every step until it disables or removes itself (EVT-2 b).</summary>
    internal void OnHeroEntered(Hero hero, MapPosition cell)
    {
        if (Data.Cells.TryGetValue(cell, out var name) && Data.Set.Find(name) is { Kind: MapEventKind.Hero } definition)
        {
            _queue.Enqueue(new Run(definition, hero.Owner, hero.Id, null, cell, startOfTurn: false));
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
                _queue.Enqueue(new Run(definition, player, null, null, null, startOfTurn: true));
            }
            else if (definition.Kind == MapEventKind.Town)
            {
                foreach (var town in TownsOf(definition).Where(t => t.Owner == player))
                {
                    _queue.Enqueue(new Run(definition, player, null, town.Id, null, startOfTurn: true));
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
                _queue.Enqueue(new Run(definition, null, null, town.Id, null, startOfTurn: true));
            }
        }

        Pump();
    }

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
        _queue.Enqueue(new Run(quest, hero.Owner, hero.Id, null, null, startOfTurn: false, actions));
        Pump();
    }

    /// <summary>
    /// A stage is complete: the quest object moves on, the other players' open entries for it go, and the completer's log shows the next stage
    /// (its proposal is shown at once) or the quest done; a Quest Guard whose last stage is done leaves the map.
    /// </summary>
    private void AdvanceQuest(Run run, AdvanceQuestAction advance, Hero hero, PlayerId completer)
    {
        if (state.FindObject(advance.Site) is not QuestSite site || site.Stage != advance.Stage)
        {
            return;
        }

        site.Stage++;
        events.Publish(new QuestStageCompleted(site.Id, run.Event.Name, advance.Stage, hero.Id, completer));
        foreach (var player in Data.DropOpenQuestEntries(site.Id, completer))
        {
            events.Publish(new QuestLogChanged(player));
        }

        if (site.Stage < run.Event.Stages.Count)
        {
            WriteLog(completer, new QuestLogEntry(site.Id, run.Event.Name, site.Stage, Done: false));
            run.Frames.Push(new Frame([new MessageAction(run.Event.Stages[site.Stage].Proposal)]));
            return;
        }

        WriteLog(completer, new QuestLogEntry(site.Id, run.Event.Name, advance.Stage, Done: true));
        if (site.Kind == MapObjectKind.QuestGuard)
        {
            state.RemoveObject(site);
            events.Publish(new MapObjectRemoved(site.Id));
        }
    }

    private void WriteLog(PlayerId player, QuestLogEntry entry)
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
        while (state.PendingDecision is null && state.Battle is null && !state.IsOver)
        {
            ran |= _current is not null || _queue.Count > 0;
            if (_current is null)
            {
                if (!_queue.TryDequeue(out var next))
                {
                    break;
                }

                if (!Data.IsEnabled(next.Event.Name))
                {
                    continue;
                }

                _current = next;
                events.Publish(new MapEventRan(next.Event.Name, next.Event.Kind, next.Player, next.Hero, next.Town));
            }

            var run = _current;
            switch (run.Waiting)
            {
                // The answer went away unanswered (its hero left the map): the event ends.
                case Waiting.Decision:
                    _current = null;
                    continue;

                // The battle is over: a hero still on the map won it; lost, fled or surrendered, the event stops (EVT-4 d).
                case Waiting.Battle when HeroOf(run) is null:
                    _current = null;
                    continue;
                case Waiting.Battle:
                    run.Waiting = Waiting.None;
                    break;
            }

            Continue(run);
            if (run.Waiting == Waiting.None && run.Frames.Count == 0)
            {
                _current = null;
            }
        }

        if (ran && !IsBusy && state.PendingDecision is null && state.Battle is null)
        {
            progression.OfferNext();
        }
    }

    /// <summary>Runs the actions until the event ends, waits, or something else becomes pending.</summary>
    private void Continue(Run run)
    {
        while (run.Waiting == Waiting.None && run.Frames.Count > 0 && state.PendingDecision is null && state.Battle is null && !state.IsOver)
        {
            var frame = run.Frames.Peek();
            if (frame.Next >= frame.Actions.Count)
            {
                run.Frames.Pop();
                continue;
            }

            if (!Apply(run, frame.Actions[frame.Next++]))
            {
                run.Frames.Clear();
            }
        }
    }

    /// <summary>Answers the pending message or question (<see cref="MapEventDecision"/>) and goes on.</summary>
    internal void Answer(int option)
    {
        if (state.PendingDecision is not MapEventDecision decision || _current is not { Waiting: Waiting.Decision } run)
        {
            throw new InvalidOperationException("No map event is waiting for an answer.");
        }

        if (option < 0 || option >= Math.Max(1, decision.Answers.Count))
        {
            throw new InvalidOperationException(decision.IsQuestion ? $"Answer 0 to {decision.Answers.Count - 1}." : "A message is closed with 0.");
        }

        state.PendingDecision = null;
        run.Waiting = Waiting.None;
        if (run.Answers is { } answers)
        {
            run.Frames.Push(new Frame(answers[option].Actions));
            run.Answers = null;
        }

        events.Publish(new DecisionResolved(decision));
        Pump();
    }

    /// <summary>Keeps the chosen stacks of a full army and its newcomers (<see cref="MapEventArmyDecision"/>), then goes on.</summary>
    internal void ChooseArmy(IReadOnlyList<int> keep)
    {
        if (state.PendingDecision is not MapEventArmyDecision decision || _current is not { Waiting: Waiting.Decision } run)
        {
            throw new InvalidOperationException("No map event is waiting for an army choice.");
        }

        if (keep.Count is < 1 or > Army.SlotCount || keep.Distinct().Count() != keep.Count || keep.Any(i => i < 0 || i >= decision.Stacks.Count))
        {
            throw new InvalidOperationException($"Keep one to {Army.SlotCount} different stacks of the {decision.Stacks.Count}.");
        }

        state.PendingDecision = null;
        run.Waiting = Waiting.None;
        var hero = state.GetHero(decision.Hero);
        Refill(hero.Army, keep.Select(i => decision.Stacks[i]));
        events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        events.Publish(new DecisionResolved(decision));
        Pump();
    }

    // Actions -------------------------------------------------------------------------------------------------------

    /// <summary>Applies one action; false stops the event.</summary>
    private bool Apply(Run run, EventAction action)
    {
        var hero = HeroOf(run);
        var town = run.Town is { } townId ? state.FindObject(townId) as Town : null;
        switch (action)
        {
            case StopAction:
                return false;
            case IfAction branch:
                run.Frames.Push(new Frame(Test(run, branch.Condition) ? branch.Then : branch.Else));
                break;
            case SetVariableAction set:
                SetVariable(set.Variable, MapEventEvaluator.Combine(set.Op, Data.Variable(set.Variable), Evaluate(run, set.Value)));
                break;
            case EnableEventAction enable:
                var name = enable.Event ?? run.Event.Name;
                if (Data.IsEnabled(name) != enable.Enabled)
                {
                    Data.SetEnabled(name, enable.Enabled);
                    events.Publish(new MapEventEnabledChanged(name, enable.Enabled));
                }

                break;
            case RemoveCellAction when run.Cell is { } cell && Data.RemoveCell(cell):
                events.Publish(new MapEventCellRemoved(cell));
                break;
            case MessageAction message:
                Show(run, message);
                break;
            case QuestionAction question when run.Player is { } asked && question.Answers.Count > 0:
                run.Answers = question.Answers;
                Ask(run, new MapEventDecision(hero?.Id ?? MapEventDecision.NoHero, run.Event.Name, question.Text, [], question.Answers.Select(a => a.Text).ToList()));
                break;
            case ResourceAction resource when run.Player is { } player:
                GiveResource(player, resource.Resource, Evaluate(run, resource.Amount));
                break;
            case CreaturesAction creatures when hero is not null:
                GiveCreatures(run, hero, creatures.Creature, Evaluate(run, creatures.Count));
                break;
            case CreaturesAction creatures when town is not null:
                GiveGarrison(town, creatures.Creature, Evaluate(run, creatures.Count));
                break;
            case AvailableCreaturesAction available when town is not null && TownRules.LineOf(state, town, available.Line) is { } line:
                town.SetAvailable(line.Id, Math.Max(0, town.AvailableOf(line.Id) + Evaluate(run, available.Count)));
                events.Publish(new TownCreaturesGrew(town.Id, town.Available.ToDictionary()));
                break;
            case BuildAction build when town is not null:
                Build(town, build.Building, build.Level);
                break;
            case RevealAction reveal when run.Player is { } viewer:
                events.Publish(new AreaRevealed(viewer, new MapPosition(reveal.X, reveal.Y, reveal.Level), reveal.Radius));
                break;
            case GiveHeroAction give when run.Player is { } owner:
                GiveHero(run, owner, give, hero, town);
                break;
            case AdvanceQuestAction advance when hero is not null && run.Player is { } completer:
                AdvanceQuest(run, advance, hero, completer);
                break;
            case BattleAction fight when hero is not null:
                if (hero.Army.IsEmpty || fight.Army.Count == 0)
                {
                    return false; // nobody to fight with: the event stops, the hero stays
                }

                run.Waiting = Waiting.Battle;
                battles.BeginEventBattle(hero, fight.Army);
                break;
            default:
                if (hero is not null)
                {
                    ApplyToHero(run, hero, action);
                }

                break;
        }

        return true;
    }

    /// <summary>The actions that change the hero itself; gains only for experience, stats and skills (EVT-4 b).</summary>
    private void ApplyToHero(Run run, Hero hero, EventAction action)
    {
        switch (action)
        {
            case ExperienceAction experience when Evaluate(run, experience.Amount) is > 0 and var gained:
                progression.GrantExperience(hero, gained);
                break;
            case PrimaryStatAction stat when Evaluate(run, stat.Amount) is > 0 and var raised:
                progression.RaiseStat(hero, stat.Stat, raised);
                break;
            case SkillAction skill when BuiltInSkills.Contains(skill.Skill):
                var id = BuiltInSkills.Resolve(skill.Skill);
                var level = (SkillLevel)Math.Min((int)skill.Level, (int)BuiltInSkills.Get(id).MaxLevel);
                if (hero.Skills.Level(id) < level && hero.Skills.CanRaise(id))
                {
                    hero.Skills.Set(id, level);
                    events.Publish(new MapEventSkillGained(hero.Id, id, level));
                }

                break;
            case ArtifactAction { Take: false } artifact:
                artifacts.Grant(hero, artifact.Artifact, ArtifactSource.MapEvent);
                break;
            case ArtifactAction artifact when hero.Artifacts.IsWorn(artifact.Artifact) ? hero.Artifacts.RemoveWorn(artifact.Artifact) : hero.Artifacts.RemoveFromBackpack(artifact.Artifact):
                state.RefreshArtifacts(hero);
                events.Publish(new ArtifactLost(hero.Id, artifact.Artifact));
                break;
            case WarMachineAction { Take: false } machine when hero.AddWarMachine(machine.Slot):
                events.Publish(new MapEventWarMachineGained(hero.Id, machine.Slot));
                break;
            case WarMachineAction { Take: true } machine when hero.RemoveWarMachine(machine.Slot):
                events.Publish(new WarMachineLost(hero.Id, machine.Slot));
                break;
            case SpellBookAction when !hero.HasSpellBook:
                hero.HasSpellBook = true;
                events.Publish(new MapEventSpellBookGained(hero.Id));
                break;
            case SpellAction spell when hero.HasSpellBook && state.Spells.Contains(spell.Spell) && hero.Learn(spell.Spell):
                events.Publish(new MapEventSpellLearned(hero.Id, spell.Spell));
                break;
            case ManaAction mana:
                ChangeMana(hero, Evaluate(run, mana.Amount));
                break;
            case MovementAction movement when Evaluate(run, movement.Amount) is not 0 and var points:
                var before = hero.MovementPoints;
                hero.MovementPoints = Math.Max(0, before + points);
                events.Publish(new HeroMovementGained(hero.Id, hero.MovementPoints - before, hero.MovementPoints));
                break;
            case MoraleAction morale:
                var source = $"event:{run.Event.Name}";
                if (Math.Clamp(morale.Morale, -MaxMoraleOrLuck, MaxMoraleOrLuck) is not 0 and var m)
                {
                    progression.GrantStatus(hero, new HeroStatus(source, HeroStatusKind.Morale, m, StatusExpiry.NextBattle));
                }

                if (Math.Clamp(morale.Luck, -MaxMoraleOrLuck, MaxMoraleOrLuck) is not 0 and var l)
                {
                    progression.GrantStatus(hero, new HeroStatus(source, HeroStatusKind.Luck, l, StatusExpiry.NextBattle));
                }

                break;
        }
    }

    /// <summary>
    /// A message: a person reads it with a line per reward, then gets the rewards; anyone else gets them at once. The rewards' amounts are drawn
    /// now, so the lines tell what comes.
    /// </summary>
    private void Show(Run run, MessageAction message)
    {
        var rewards = message.Rewards.Select(r => Fix(run, r)).ToList();
        run.Frames.Push(new Frame(rewards));
        if (run.Player is { } player && state.GetPlayer(player).IsHuman)
        {
            var lines = rewards.Select(Describe).OfType<string>().ToList();
            Ask(run, new MapEventDecision(HeroOf(run)?.Id ?? MapEventDecision.NoHero, run.Event.Name, message.Text, lines, []));
        }
    }

    private void Ask(Run run, PendingDecision decision)
    {
        run.Waiting = Waiting.Decision;
        state.PendingDecision = decision;
        events.Publish(new DecisionRequested(decision));
    }

    private void SetVariable(string name, int value)
    {
        Data.SetVariable(name, value);
        events.Publish(new MapVariableChanged(name, value));
    }

    private void GiveResource(PlayerId player, ResourceType resource, int amount)
    {
        if (amount > 0)
        {
            economy.Grant(player, ResourceSet.Of(resource, amount));
        }
        else if (Math.Min(-amount, state.GetPlayer(player).Resources[resource]) is > 0 and var taken)
        {
            economy.Set(player, state.GetPlayer(player).Resources - ResourceSet.Of(resource, taken));
        }
    }

    private void ChangeMana(Hero hero, int amount)
    {
        var before = hero.Mana;
        if (amount > 0 && Math.Min(2 * hero.MaxMana, before + amount) is var raised && raised > before)
        {
            hero.Mana = raised;
            events.Publish(new HeroManaRestored(hero.Id, hero.Mana, hero.MaxMana));
        }
        else if (amount < 0 && before > 0)
        {
            hero.Mana = before + amount;
            events.Publish(new HeroManaSpent(hero.Id, before - hero.Mana, hero.Mana, hero.MaxMana));
        }
    }

    /// <summary>Creatures for the hero: they join a stack or a free slot; a full army asks a person what to leave and keeps a computer's strongest.</summary>
    private void GiveCreatures(Run run, Hero hero, string creature, int count)
    {
        if (!state.Creatures.Contains(creature) || count == 0)
        {
            return;
        }

        if (count < 0)
        {
            if (Take(hero.Army, creature, -count))
            {
                events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
            }

            return;
        }

        var stack = new CreatureStack(creature, count);
        if (hero.Army.TryAdd(stack))
        {
            events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
            return;
        }

        var all = hero.Army.Stacks.Select(s => s.Stack).Append(stack).ToList();
        if (state.GetPlayer(hero.Owner).IsHuman)
        {
            Ask(run, new MapEventArmyDecision(hero.Id, all));
            return;
        }

        Refill(hero.Army, all.OrderByDescending(s => (long)s.Count * state.Creatures.Get(s.Creature).Health).Take(Army.SlotCount));
        events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
    }

    /// <summary>Creatures for a town event's garrison; those without room are lost.</summary>
    private void GiveGarrison(Town town, string creature, int count)
    {
        var changed = count switch
        {
            > 0 when state.Creatures.Contains(creature) => town.Garrison.TryAdd(new CreatureStack(creature, count)),
            < 0 => Take(town.Garrison, creature, -count),
            _ => false,
        };
        if (changed)
        {
            events.Publish(new GarrisonChanged(town.Id, town.Garrison.Slots.ToList()));
        }
    }

    /// <summary>Takes up to <paramref name="count"/> of the creature from the army, last stack first; false when it has none.</summary>
    private static bool Take(Army army, string creature, int count)
    {
        var taken = false;
        foreach (var (slot, stack) in army.Stacks.Where(s => s.Stack.Creature == creature).Reverse().ToList())
        {
            if (count <= 0)
            {
                break;
            }

            var removed = Math.Min(count, stack.Count);
            army.Set(slot, stack.Count == removed ? null : new CreatureStack(creature, stack.Count - removed));
            count -= removed;
            taken = true;
        }

        return taken;
    }

    private static void Refill(Army army, IEnumerable<CreatureStack> stacks)
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

    /// <summary>
    /// Builds a town building, or a line's dwelling, up to <paramref name="level"/> (EVT-4 a): no cost, no day's build, and every requirement
    /// built first.
    /// </summary>
    private void Build(Town town, string building, int level, int depth = 0)
    {
        if (depth > 32)
        {
            return;
        }

        var catalog = TownRules.Buildings(state, town);
        if (TownRules.LineOf(state, town, building) is { } line && line.Id == building)
        {
            var dwelling = BuiltInBuildings.Dwelling(line.Tier);
            var top = Math.Min(level, line.UpgradesTo is null ? 1 : catalog.MaxLevel(dwelling));
            for (var l = town.DwellingLevel(line.Id) + 1; l <= top; l++)
            {
                BuildRequirements(town, catalog.Get(dwelling, l), depth);
                town.SetDwelling(line.Id, l);
                if (l == 1)
                {
                    town.SetAvailable(line.Id, town.AvailableOf(line.Id) + TownRules.WeeklyGrowth(state, town, line));
                }

                events.Publish(new DwellingBuilt(town.Id, line.Id, l, town.AvailableOf(line.Id)));
            }

            return;
        }

        if (BuiltInBuildings.DwellingTier(building) is { } tier)
        {
            // A requirement on a tier ("dwelling-3"): the town's first line of that tier.
            if (TownRules.Lines(state, town).Where(l => l.Tier == tier).OrderBy(l => l.Id, StringComparer.Ordinal).FirstOrDefault() is { } first)
            {
                Build(town, first.Id, level, depth + 1);
            }

            return;
        }

        if (!catalog.Contains(building))
        {
            return;
        }

        for (var l = town.Level(building) + 1; l <= Math.Min(level, catalog.MaxLevel(building)); l++)
        {
            BuildRequirements(town, catalog.Get(building, l), depth);
            town.SetLevel(building, l);
            events.Publish(new TownBuilt(town.Id, building, l));
            if (building is BuiltInBuildings.MageGuild or BuiltInBuildings.WarLodge || BuiltInBuildings.SchoolOf(building) is not null)
            {
                Magic.MagicRules.VisitBy(state, events, town);
            }
        }
    }

    private void BuildRequirements(Town town, BuildingLevel level, int depth)
    {
        foreach (var requirement in level.Requires.Where(r => TownRules.Level(state, town, r.Building) < r.Level))
        {
            Build(town, requirement.Building, requirement.Level, depth + 1);
        }
    }

    /// <summary>A roster hero joins the player (EVT-4 c): not when it is in play or the player has as many heroes as it may.</summary>
    private void GiveHero(Run run, PlayerId owner, GiveHeroAction give, Hero? near, Town? town)
    {
        if (!state.HeroRoster.Contains(give.Hero) || state.HeroLimitReached(owner) || InPlay(give.Hero))
        {
            return;
        }

        var target = give.X is { } x && give.Y is { } y ? new MapPosition(x, y, give.Level) : near?.Position ?? town?.VisitPosition;
        if (target is not { } around || FreeCellNear(around) is not { } position)
        {
            return;
        }

        var joined = Hero.Create(state.NextUnusedHeroId(), owner, position, null, state.HeroRoster.Get(give.Hero));
        joined.Experience = Math.Max(0, give.Experience);
        state.AddHero(joined);
        progression.LevelUpAutomatically(joined);
        joined.MaxMovementPoints = MovementAllowance.Daily(joined, state.Creatures);
        joined.MovementPoints = run.StartOfTurn ? joined.MaxMovementPoints : 0; // given in the middle of a turn: it moves next turn, like a prisoner
        events.Publish(new MapEventHeroGiven(joined.Id, owner, give.Hero, joined.Level));
    }

    private bool InPlay(string definition) =>
        state.Heroes.Concat(state.GarrisonHeroes).Concat(state.RetiredHeroes).Any(h => h.Definition == definition)
        || state.ObjectsOf<Objects.Prison>().Any(p => p.Hero == definition);

    /// <summary>The place itself when a hero may stand there, else the first such neighbour.</summary>
    private MapPosition? FreeCellNear(MapPosition around) =>
        new[] { around }.Concat(DirectionExtensions.All.Select(around.Step))
            .Cast<MapPosition?>()
            .FirstOrDefault(p => MovementRules.CheckTile(state, p!.Value) == MoveResult.Moved && state.HeroAt(p.Value) is null && state.ObjectCellAt(p.Value) is null);

    // Expressions and conditions ------------------------------------------------------------------------------------

    private Hero? HeroOf(Run run) => run.Hero is { } id ? state.Heroes.FirstOrDefault(h => h.Id == id) : null;

    private int Evaluate(Run run, EventExpr expr) => MapEventEvaluator.Evaluate(state, Scope(run), expr);

    private bool Test(Run run, EventCondition condition) => MapEventEvaluator.Test(state, Scope(run), condition);

    private EventScope Scope(Run run) => new(run.Player, HeroOf(run), run.Town);

    /// <summary>A reward with its amounts drawn (a message shows them before they are given).</summary>
    private EventAction Fix(Run run, EventAction action) => action switch
    {
        ResourceAction a => a with { Amount = new NumberExpr(Evaluate(run, a.Amount)) },
        ExperienceAction a => a with { Amount = new NumberExpr(Evaluate(run, a.Amount)) },
        PrimaryStatAction a => a with { Amount = new NumberExpr(Evaluate(run, a.Amount)) },
        ManaAction a => a with { Amount = new NumberExpr(Evaluate(run, a.Amount)) },
        MovementAction a => a with { Amount = new NumberExpr(Evaluate(run, a.Amount)) },
        CreaturesAction a => a with { Count = new NumberExpr(Evaluate(run, a.Count)) },
        AvailableCreaturesAction a => a with { Count = new NumberExpr(Evaluate(run, a.Count)) },
        _ => action,
    };

    /// <summary>A message's line for a reward (amounts already drawn), or null for an action that shows nothing.</summary>
    private string? Describe(EventAction action)
    {
        static string Signed(int value) => value >= 0 ? $"+{value}" : $"{value}";
        static int Amount(EventExpr expr) => expr is NumberExpr number ? number.Value : 0;
        return action switch
        {
            ResourceAction a => $"{Signed(Amount(a.Amount))} {a.Resource}",
            ExperienceAction a => $"{Signed(Amount(a.Amount))} experience",
            PrimaryStatAction a => $"{Signed(Amount(a.Amount))} {a.Stat}",
            ManaAction a => $"{Signed(Amount(a.Amount))} mana",
            MovementAction a => $"{Signed(Amount(a.Amount))} movement",
            CreaturesAction a => $"{Signed(Amount(a.Count))} {(state.Creatures.TryGet(a.Creature, out var c) ? c.Name : a.Creature)}",
            SkillAction a => $"{(BuiltInSkills.Contains(a.Skill) ? BuiltInSkills.Get(a.Skill).Name : a.Skill)} ({a.Level})",
            ArtifactAction a => $"{(a.Take ? "−" : "+")} {(state.Artifacts.TryGet(a.Artifact, out var artifact) ? artifact.Name : a.Artifact)}",
            WarMachineAction a => $"{(a.Take ? "−" : "+")} {a.Slot}",
            SpellBookAction => "+ spell book",
            SpellAction a => $"+ {(state.Spells.TryGet(a.Spell, out var spell) ? spell.Name : a.Spell)}",
            MoraleAction a => string.Join(", ", new[] { a.Morale != 0 ? $"{Signed(a.Morale)} morale" : null, a.Luck != 0 ? $"{Signed(a.Luck)} luck" : null }.OfType<string>()),
            _ => null,
        };
    }

    // Defeat records (EVT-3 e) --------------------------------------------------------------------------------------

    public void Handle(BattleStarted @event) =>
        _battle = (@event.AttackerHero, @event.DefenderHero, @event.Guard, OwnerOf(@event.AttackerHero), OwnerOf(@event.DefenderHero));

    public void Handle(BattleEnded @event)
    {
        if (_battle is not { } battle)
        {
            return;
        }

        _battle = null;
        var (beatenHero, beatenGuard, winner) = @event.Winner == BattleSide.Attacker
            ? (battle.Defender, battle.Guard, battle.AttackerOwner)
            : (battle.Attacker, (MapObjectId?)null, battle.DefenderOwner);
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

    private PlayerId? OwnerOf(HeroId? hero) =>
        hero is { } id && state.Heroes.Concat(state.GarrisonHeroes).FirstOrDefault(h => h.Id == id) is { } found ? found.Owner : null;

    /// <summary>The last step of a quest stage's completion: the quest object moves on (not map data, so no map can hold it).</summary>
    private sealed record AdvanceQuestAction(MapObjectId Site, int Stage) : EventAction;

    private enum Waiting
    {
        None,
        Decision,
        Battle,
    }

    private sealed class Frame(IReadOnlyList<EventAction> actions)
    {
        public IReadOnlyList<EventAction> Actions { get; } = actions;

        public int Next { get; set; }
    }

    /// <summary>One event running for a player, hero, town and cell; a quest's run carries the actions of its visit instead of the event's.</summary>
    private sealed class Run(
        MapEventDefinition definition, PlayerId? player, HeroId? hero, MapObjectId? town, MapPosition? cell, bool startOfTurn, IReadOnlyList<EventAction>? actions = null)
    {
        public MapEventDefinition Event { get; } = definition;

        public PlayerId? Player { get; } = player;

        public HeroId? Hero { get; } = hero;

        public MapObjectId? Town { get; } = town;

        public MapPosition? Cell { get; } = cell;

        public bool StartOfTurn { get; } = startOfTurn;

        public Stack<Frame> Frames { get; } = new([new Frame(actions ?? definition.Actions)]);

        public Waiting Waiting { get; set; }

        public IReadOnlyList<EventAnswer>? Answers { get; set; }
    }
}
