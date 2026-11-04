using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents;

/// <summary>When a map event runs (ADR-096, ADR-115, EVT-2).</summary>
public enum MapEventKind
{
    /// <summary>A hero steps onto one of the event's cells; the event acts on that hero and its owner.</summary>
    Hero,

    /// <summary>The start of every player's turn, after income, growth and the week's event; it acts on that player.</summary>
    Player,

    /// <summary>The start of the town owner's turn (neutral towns: the start of the day); it acts on the town and its owner.</summary>
    Town,

    /// <summary>
    /// A quest (EVT-5): a Seer's Hut, Quest Guard or Quest Gate names it and a visiting hero meets its <see cref="MapEventDefinition.Stages"/>,
    /// one after another; it acts on that hero and its owner. A quest has stages instead of actions.
    /// </summary>
    Quest,
}

/// <summary>
/// The map author's events (ADR-115, EVT-1): a data tree stored in the map file, no script language. <see cref="Variables"/> are map-wide whole
/// numbers with their starting values; <see cref="Cells"/> are the invisible cells that run hero events. Events run in list order.
/// </summary>
public sealed record MapEventSet
{
    public IReadOnlyList<MapEventDefinition> Events { get; init; } = [];

    public IReadOnlyDictionary<string, int> Variables { get; init; } = new Dictionary<string, int>();

    public IReadOnlyList<EventCell> Cells { get; init; } = [];

    public static MapEventSet Empty { get; } = new();

    public bool IsEmpty => Events.Count == 0 && Variables.Count == 0 && Cells.Count == 0;

    public MapEventDefinition? Find(string name) => Events.FirstOrDefault(e => e.Name == name);
}

/// <summary>
/// One map event: a unique <see cref="Name"/>, when it runs (<see cref="Kind"/>) and its <see cref="Actions"/>, run top to bottom. Who triggers it is
/// a condition (an <see cref="IfAction"/>). A town event names its <see cref="Towns"/> (town object ids; one event may serve several). An event
/// that starts disabled runs only once an action enables it.
/// </summary>
public sealed record MapEventDefinition(string Name, MapEventKind Kind, IReadOnlyList<EventAction> Actions)
{
    public bool Enabled { get; init; } = true;

    public IReadOnlyList<int> Towns { get; init; } = [];

    /// <summary>A quest's stages, done one after another (a Quest Gate's quest has one, which takes and gives nothing).</summary>
    public IReadOnlyList<QuestStage> Stages { get; init; } = [];
}

/// <summary>
/// One stage of a quest (EVT-5): what the hero must meet (<see cref="Requirement"/>; no random numbers, so routes can test it), the
/// <see cref="Proposal"/> shown on the first visit and in the quest log, the <see cref="Progress"/> shown on later visits before the hero meets it
/// (empty: the proposal again), the <see cref="Completion"/> asked once it does, and the <see cref="Actions"/> run when the hero completes it: what
/// the stage takes, then its rewards (<see cref="QuestForms"/> writes HoMM3's quests this way). A lost battle among the actions leaves the stage open.
/// </summary>
public sealed record QuestStage(EventCondition Requirement, string Proposal, IReadOnlyList<EventAction> Actions)
{
    public string Progress { get; init; } = "";

    public string Completion { get; init; } = "";
}

/// <summary>One quest a player's heroes were shown (EVT-5): the quest object, its quest, the stage and whether the player completed the quest.</summary>
public sealed record QuestLogEntry(Common.MapObjectId Site, string Quest, int Stage, bool Done);

/// <summary>A question waiting for its answer (<see cref="MapEventService.PendingQuestion"/>): the answers with their actions, and who the event is for.</summary>
public sealed record MapEventQuestion(IReadOnlyList<EventAnswer> Answers, Common.PlayerId? Player, Common.HeroId? Hero, Common.MapObjectId? Town);

/// <summary>An invisible event cell (EVT-2): a hero stepping onto (X, Y, Level) runs the hero event <see cref="Event"/>; several cells may share one event.</summary>
public sealed record EventCell(int X, int Y, int Level, string Event)
{
    public Common.MapPosition Position => new(X, Y, Level);
}

// Expressions ---------------------------------------------------------------------------------------------------

/// <summary>A whole-number expression (EVT-3).</summary>
public abstract record EventExpr;

public sealed record NumberExpr(int Value) : EventExpr;

/// <summary>A map variable's value; an unknown name is 0.</summary>
public sealed record VariableExpr(string Name) : EventExpr;

/// <summary>A random number from <see cref="Min"/> to <see cref="Max"/>, both included, drawn from the game's source (EVT-3 d).</summary>
public sealed record RandomExpr(int Min, int Max) : EventExpr;

public enum ArithmeticOp
{
    Add,
    Subtract,
    Multiply,

    /// <summary>Whole-number division towards zero; dividing by 0 gives 0.</summary>
    Divide,
}

public sealed record ArithmeticExpr(ArithmeticOp Op, EventExpr Left, EventExpr Right) : EventExpr;

/// <summary>What a <see cref="QuantityExpr"/> reads. Hero quantities are 0 without a hero; <see cref="Creatures"/> counts a town event's garrison.</summary>
public enum Quantity
{
    /// <summary>The player's amount of the resource named by the id (<c>Gold</c>, <c>Crystal</c>, …).</summary>
    Resource,

    /// <summary>The creatures with the id in the hero's army (a town event: the garrison).</summary>
    Creatures,

    HeroLevel,
    Experience,
    Attack,
    Defense,
    SpellPower,
    Knowledge,
    Mana,
    MovementPoints,

    /// <summary>The hero's level of the skill with the id: 0 none … 4 Master.</summary>
    SkillLevel,

    /// <summary>How many of the artifact with the id the hero wears or carries.</summary>
    Artifacts,

    /// <summary>The day of the game, from 1.</summary>
    Day,

    /// <summary>1 (Monday) … 7.</summary>
    DayOfWeek,

    /// <summary>The week of the month, 1 … 4.</summary>
    Week,

    /// <summary>The month, from 1.</summary>
    Month,

    /// <summary>The game's complexity (HotA's difficulty, EVT-3 a): 0 Beginner … 4 Expert; 2 when the game has none.</summary>
    Difficulty,

    /// <summary>The player's heroes on the map.</summary>
    Heroes,

    /// <summary>The player's towns.</summary>
    Towns,
}

public sealed record QuantityExpr(Quantity Quantity, string? Id = null) : EventExpr;

// Conditions ----------------------------------------------------------------------------------------------------

/// <summary>A condition (EVT-3). Without the player or hero a condition needs, it is false.</summary>
public abstract record EventCondition;

public sealed record AllCondition(IReadOnlyList<EventCondition> Conditions) : EventCondition;

public sealed record AnyCondition(IReadOnlyList<EventCondition> Conditions) : EventCondition;

public sealed record NotCondition(EventCondition Condition) : EventCondition;

public enum CompareOp
{
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
}

public sealed record CompareCondition(EventExpr Left, CompareOp Op, EventExpr Right) : EventCondition;

/// <summary>The hero is the roster hero <see cref="Hero"/>.</summary>
public sealed record HeroIsCondition(string Hero) : EventCondition;

/// <summary>The hero's class, and its town when <see cref="Town"/> is set (HoMM3's hero classes).</summary>
public sealed record HeroClassCondition(HeroClass Class, string? Town = null) : EventCondition;

/// <summary>The player is slot <see cref="Player"/> (0-based).</summary>
public sealed record PlayerIsCondition(int Player) : EventCondition;

/// <summary>The player is a person (HoMM3's "computer may trigger" is its negation).</summary>
public sealed record PlayerIsHumanCondition : EventCondition;

/// <summary>The player's starting faction (HotA's starting town) is <see cref="Town"/>.</summary>
public sealed record FactionCondition(string Town) : EventCondition;

/// <summary>The player is on team <see cref="Team"/>.</summary>
public sealed record TeamCondition(int Team) : EventCondition;

/// <summary>The player is an ally of slot <see cref="Player"/> (another player on its team).</summary>
public sealed record AllyCondition(int Player) : EventCondition;

/// <summary>The town (object id) belongs to slot <see cref="Owner"/>, or is neutral when it is null.</summary>
public sealed record TownOwnerCondition(int Town, int? Owner) : EventCondition;

/// <summary>The map monster (object id) was beaten in battle by slot <see cref="Player"/>, or by the event's player when it is null.</summary>
public sealed record MonsterDefeatedCondition(int Monster, int? Player = null) : EventCondition;

/// <summary>The map hero (hero id) was beaten in battle by slot <see cref="Player"/>, or by the event's player when it is null.</summary>
public sealed record HeroDefeatedCondition(int Hero, int? Player = null) : EventCondition;

// Actions -------------------------------------------------------------------------------------------------------

/// <summary>
/// One step of an event (EVT-4). Gains and losses as HotA: experience, primary and secondary skills only rise; resources, creatures, artifacts, war
/// machines, mana and movement may also be taken. An action that needs a hero, a town or a player the event does not have is skipped.
/// </summary>
public abstract record EventAction;

public sealed record IfAction(EventCondition Condition, IReadOnlyList<EventAction> Then) : EventAction
{
    public IReadOnlyList<EventAction> Else { get; init; } = [];
}

/// <summary>Ends the event it is in here (a run event returns to the event that ran it).</summary>
public sealed record StopAction : EventAction;

/// <summary>
/// Runs <see cref="Actions"/> <see cref="Times"/> times (EVT-1, step 3): the count is drawn once, when the loop starts; 0 or less runs nothing, and
/// more than <see cref="MapEventService.MaxLoopRounds"/> runs that many.
/// </summary>
public sealed record RepeatAction(EventExpr Times, IReadOnlyList<EventAction> Actions) : EventAction;

/// <summary>
/// Runs <see cref="Actions"/> again and again while <see cref="Condition"/> holds, tested before every round (EVT-1, step 3), at most
/// <see cref="MapEventService.MaxLoopRounds"/> rounds.
/// </summary>
public sealed record WhileAction(EventCondition Condition, IReadOnlyList<EventAction> Actions) : EventAction;

/// <summary>
/// Runs another event's actions here, for the same player, hero, town and cell, then goes on (EVT-1, step 3): a shared event written once and run
/// from many. A disabled event is skipped; a stop in it ends only it; runs nested deeper than <see cref="MapEventService.MaxRunDepth"/> are skipped.
/// </summary>
public sealed record RunEventAction(string Event) : EventAction;

public enum VariableOp
{
    Set,
    Add,
    Subtract,
    Multiply,
    Divide,
}

public sealed record SetVariableAction(string Variable, VariableOp Op, EventExpr Value) : EventAction;

/// <summary>Enables or disables an event; <see cref="Event"/> null is this event ("once" is a disable at the end, EVT-2 b).</summary>
public sealed record EnableEventAction(string? Event, bool Enabled) : EventAction;

/// <summary>Removes the cell that ran this hero event (the others of the event stay).</summary>
public sealed record RemoveCellAction : EventAction;

/// <summary>
/// A message to the player (EVT-4 f), with a line for each of <see cref="Rewards"/>, which are given once it is read. A computer player is not
/// shown it and gets the rewards at once.
/// </summary>
public sealed record MessageAction(string Text) : EventAction
{
    public IReadOnlyList<EventAction> Rewards { get; init; } = [];
}

/// <summary>A question with two or three answers; the chosen answer's actions run next. A computer player answers by itself.</summary>
public sealed record QuestionAction(string Text, IReadOnlyList<EventAnswer> Answers) : EventAction;

public sealed record EventAnswer(string Text, IReadOnlyList<EventAction> Actions);

/// <summary>Gives (or, negative, takes, down to 0) a resource to the player.</summary>
public sealed record ResourceAction(ResourceType Resource, EventExpr Amount) : EventAction;

/// <summary>Experience for the hero (gains only).</summary>
public sealed record ExperienceAction(EventExpr Amount) : EventAction;

/// <summary>A primary stat of the hero (gains only).</summary>
public sealed record PrimaryStatAction(PrimaryStat Stat, EventExpr Amount) : EventAction;

/// <summary>Raises a secondary skill of the hero to at least <see cref="Level"/>, up to Master, while its group has a free slot (EVT-4 a).</summary>
public sealed record SkillAction(string Skill, SkillLevel Level) : EventAction;

/// <summary>Gives an artifact to the hero's backpack, or takes one it wears or carries.</summary>
public sealed record ArtifactAction(string Artifact, bool Take = false) : EventAction;

/// <summary>Gives or takes one of our war machines (ADR-056).</summary>
public sealed record WarMachineAction(WarMachineSlot Slot, bool Take = false) : EventAction;

/// <summary>Gives the hero a spell book.</summary>
public sealed record SpellBookAction : EventAction;

/// <summary>Teaches the hero a spell (it needs a spell book).</summary>
public sealed record SpellAction(string Spell) : EventAction;

/// <summary>Changes the hero's mana: up to twice its maximum, down to 0.</summary>
public sealed record ManaAction(EventExpr Amount) : EventAction;

/// <summary>Changes the hero's movement points for today, down to 0.</summary>
public sealed record MovementAction(EventExpr Amount) : EventAction;

/// <summary>Morale or luck for the hero until its next battle (−3 … +3).</summary>
public sealed record MoraleAction(int Morale, int Luck) : EventAction;

/// <summary>
/// Gives creatures to the hero (a town event: to the garrison), or takes them when negative. A person's hero without room chooses what to leave
/// (EVT-4 e); a computer player's keeps its strongest stacks.
/// </summary>
public sealed record CreaturesAction(string Creature, EventExpr Count) : EventAction;

/// <summary>Adds creatures to hire in the town, to the dwelling of <see cref="Line"/> (its base creature); negative takes them away.</summary>
public sealed record AvailableCreaturesAction(string Line, EventExpr Count) : EventAction;

/// <summary>
/// Builds a town building, or a creature line's dwelling (a base creature id), up to <see cref="Level"/>: it ignores bans and the day's build and
/// adds what it needs first (EVT-4 a).
/// </summary>
public sealed record BuildAction(string Building, int Level = 1) : EventAction;

/// <summary>The hero fights the author's army on its cell (EVT-4 d): won, the event goes on; lost, fled or surrendered, it stops.</summary>
public sealed record BattleAction(IReadOnlyList<Armies.CreatureStack> Army) : EventAction;

/// <summary>Reveals a circle of the map to the player (EVT-4 c).</summary>
public sealed record RevealAction(int X, int Y, int Level, int Radius) : EventAction;

/// <summary>
/// The roster hero <see cref="Hero"/> joins the player like a freed prisoner (EVT-4 c), at <see cref="Experience"/>, at (X, Y, Level) or, when that
/// is left out, next to the event's hero or at the town's entrance. Nothing happens when the hero is in play or the player is at its hero limit.
/// </summary>
public sealed record GiveHeroAction(string Hero) : EventAction
{
    public int Experience { get; init; }

    public int? X { get; init; }

    public int? Y { get; init; }

    public int Level { get; init; }
}
