using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;

namespace EternalWars.Core.Domain.Commands;

/// <summary>
/// A player intent: the only way front-ends, AI, tests and replays change a running game (ADR-036).
/// Commands are plain serializable data; <see cref="Game.GameSession.Execute"/> validates and applies them, and
/// every accepted command is appended to the session's command log, so seed + map + log replay the game.
/// </summary>
/// <param name="Player">Who issues the command; checked against whose turn (or battle side) it is.</param>
public abstract record GameCommand(PlayerId Player);

// Adventure map ----------------------------------------------------------------------------------------

/// <summary>One step in a direction (keyboard movement).</summary>
public sealed record StepHeroCommand(PlayerId Player, HeroId Hero, Direction Direction) : GameCommand(Player);

/// <summary>Plan a route to <paramref name="Target"/> and follow it as far as movement points allow (click-to-move).</summary>
public sealed record MoveHeroCommand(PlayerId Player, HeroId Hero, MapPosition Target) : GameCommand(Player);

public sealed record EndTurnCommand(PlayerId Player) : GameCommand(Player);

// Decisions --------------------------------------------------------------------------------------------

public sealed record ResolveTreasureChestCommand(PlayerId Player, TreasureChestReward Reward) : GameCommand(Player);

/// <summary>Answers a <c>ResourceChoiceDecision</c>: takes the option at <paramref name="Option"/> (0-based) of a warehouse (ADR-068).</summary>
public sealed record ChooseResourceRewardCommand(PlayerId Player, int Option) : GameCommand(Player);

/// <summary>Answers a <c>TrainingDecision</c>: one of its options, or null to leave without training when the site allows it (ADR-068).</summary>
public sealed record ChooseTrainingCommand(PlayerId Player, Objects.TrainingOption? Option) : GameCommand(Player);

/// <summary>Answers a <c>TownGateDecision</c>: the town to go to, or null to stay (ADR-068).</summary>
public sealed record ChooseTownGateCommand(PlayerId Player, MapObjectId? Town) : GameCommand(Player);

/// <summary>The player's hero on a hill fort's entrance upgrades the stack in <paramref name="Slot"/> (ADR-068).</summary>
public sealed record UpgradeAtHillFortCommand(PlayerId Player, MapObjectId Fort, int Slot) : GameCommand(Player);

/// <summary>The player's hero on a War Machine Factory's entrance buys the machine of <paramref name="Slot"/> (ADR-068).</summary>
public sealed record BuyWarMachineAtFactoryCommand(PlayerId Player, MapObjectId Factory, Battles.WarMachineSlot Slot) : GameCommand(Player);

/// <summary>Closes the exchange screen.</summary>
public sealed record CompleteExchangeCommand(PlayerId Player) : GameCommand(Player);

/// <summary>Answers a <c>LevelUpDecision</c>: raise one of the offered skills.</summary>
public sealed record ChooseSkillCommand(PlayerId Player, string Skill) : GameCommand(Player);

/// <summary>
/// Moves creatures between army slots of one hero, or between two heroes whose exchange is open.
/// <paramref name="Count"/> null = the whole stack (a whole stack onto a different creature swaps them).
/// </summary>
public sealed record MoveStackCommand(PlayerId Player, HeroId FromHero, int FromSlot, HeroId ToHero, int ToSlot, int? Count = null)
    : GameCommand(Player);

/// <summary>Answer to a <see cref="GuardEncounterDecision"/>: accept the join/flee offer, or decline and fight.</summary>
public sealed record RespondToGuardCommand(PlayerId Player, bool Accept) : GameCommand(Player);

// Towns (the owner's turn, ADR-046) --------------------------------------------------------------------

/// <summary>Builds the next level of a town building (<c>Towns.BuiltInBuildings</c>); one build per town per day.</summary>
public sealed record BuildCommand(PlayerId Player, MapObjectId Town, string Building) : GameCommand(Player);

/// <summary>Builds the dwelling of a creature line (its base creature id), or upgrades it once built (ADR-030).</summary>
public sealed record BuildDwellingCommand(PlayerId Player, MapObjectId Town, string Line) : GameCommand(Player);

/// <summary>
/// Buys <paramref name="Count"/> creatures from a dwelling (the base creature, or the upgraded one from an upgraded
/// dwelling) into the garrison, or into the visiting hero <paramref name="Hero"/>.
/// </summary>
public sealed record RecruitCommand(PlayerId Player, MapObjectId Town, string Creature, int Count, HeroId? Hero = null) : GameCommand(Player);

/// <summary>A hero standing on a map dwelling's entrance buys <paramref name="Count"/> of its creatures into its army (ADR-054).</summary>
public sealed record RecruitAtDwellingCommand(PlayerId Player, MapObjectId Dwelling, int Count) : GameCommand(Player);

/// <summary>Hires <paramref name="Count"/> creatures from an owned map dwelling for a town with a caravan; they travel there (ADR-054).</summary>
public sealed record OrderCaravanCommand(PlayerId Player, MapObjectId Town, MapObjectId Dwelling, int Count) : GameCommand(Player);

/// <summary>Sends troops of a town with a caravan to another own town (<paramref name="Count"/> null = the whole stack, ADR-054).</summary>
public sealed record SendTroopsCommand(PlayerId Player, MapObjectId FromTown, int Slot, MapObjectId ToTown, int? Count = null) : GameCommand(Player);

/// <summary>Upgrades a whole stack of the garrison or the visiting hero for the price difference.</summary>
public sealed record UpgradeStackCommand(PlayerId Player, MapObjectId Town, Towns.TownArmy Army, int Slot) : GameCommand(Player);

/// <summary>
/// Moves creatures within or between the garrison and the visiting hero, with the rules of <see cref="MoveStackCommand"/>
/// (<paramref name="Count"/> null = the whole stack; the hero keeps one stack).
/// </summary>
public sealed record MoveTownStackCommand(PlayerId Player, MapObjectId Town, Towns.TownArmy From, int FromSlot, Towns.TownArmy To, int ToSlot, int? Count = null)
    : GameCommand(Player);

// Battle -----------------------------------------------------------------------------------------------

/// <summary>The town's visiting hero buys a war machine at its blacksmith (ADR-056).</summary>
public sealed record BuyWarMachineCommand(PlayerId Player, MapObjectId Town, Battles.WarMachineSlot Slot) : GameCommand(Player);

// Artifacts (the owner's turn; no movement points; ADR-061) -------------------------------------------

/// <summary>Puts an artifact from the hero's backpack on; one worn in that slot goes to the backpack when none is free.</summary>
public sealed record EquipArtifactCommand(PlayerId Player, HeroId Hero, string Artifact) : GameCommand(Player);

/// <summary>Takes a worn artifact off into the backpack (which must have room).</summary>
public sealed record UnequipArtifactCommand(PlayerId Player, HeroId Hero, string Artifact) : GameCommand(Player);

/// <summary>Gives an artifact (from the backpack first, else off the body) to another own hero while their exchange is open.</summary>
public sealed record GiveArtifactCommand(PlayerId Player, HeroId FromHero, string Artifact, HeroId ToHero) : GameCommand(Player);

/// <summary>Hires a hero the town's tavern offers (roster id) for the owner; it appears on the town's entrance (ADR-060).</summary>
public sealed record HireHeroCommand(PlayerId Player, MapObjectId Town, string Hero) : GameCommand(Player);

/// <summary>Appoints an own hero with Stewardship governor of an own town (ADR-063); it stops governing any other town.</summary>
public sealed record AppointGovernorCommand(PlayerId Player, MapObjectId Town, HeroId Hero) : GameCommand(Player);

/// <summary>Dismisses the governor of an own town.</summary>
public sealed record DismissGovernorCommand(PlayerId Player, MapObjectId Town) : GameCommand(Player);

/// <summary>
/// Trades <paramref name="Amount"/> units of a resource against gold at a marketplace (ADR-067), in the owner's turn, with no cap and no
/// hero needed. Without <paramref name="MapMarketplace"/> the player's marketplace towns set the rate; with it one of the player's
/// heroes must stand on that map marketplace, which trades at a fixed rate.
/// </summary>
public sealed record TradeResourceCommand(
    PlayerId Player, Economy.ResourceType Resource, Economy.TradeDirection Direction, int Amount, MapObjectId? MapMarketplace = null)
    : GameCommand(Player);

public sealed record ChooseBattleWidthCommand(PlayerId Player, int Width) : GameCommand(Player);

/// <summary>Deployment step (ADR-072): put the own stack <paramref name="Unit"/> at <paramref name="To"/> within the deployment zone.</summary>
public sealed record BattleDeployCommand(PlayerId Player, int Unit, CellPosition To) : GameCommand(Player);

/// <summary>Ends the deployment step ("Start battle").</summary>
public sealed record BattleStartCommand(PlayerId Player) : GameCommand(Player);

public sealed record BattleMoveCommand(PlayerId Player, CellPosition To) : GameCommand(Player);

/// <summary>Melee attack; <paramref name="From"/> is where to stand (null = closest placement next to the target).</summary>
public sealed record BattleAttackCommand(PlayerId Player, int Target, CellPosition? From = null) : GameCommand(Player);

public sealed record BattleShootCommand(PlayerId Player, int Target) : GameCommand(Player);

/// <summary>The player's hero uses a warcry (Stronghold, ADR-067): no mana, and the active unit keeps its action.</summary>
public sealed record BattleWarcryCommand(PlayerId Player, Battles.Warcry Warcry) : GameCommand(Player);

public sealed record BattleWaitCommand(PlayerId Player) : GameCommand(Player);

public sealed record BattleDefendCommand(PlayerId Player) : GameCommand(Player);

/// <summary>The player's hero flees: its army is lost and it leaves the map (ADR-038).</summary>
public sealed record BattleRetreatCommand(PlayerId Player) : GameCommand(Player);

/// <summary>Quick combat: the AI fights the rest of the battle for both sides.</summary>
public sealed record AutoResolveBattleCommand(PlayerId Player) : GameCommand(Player);

/// <summary>Outcome of <see cref="Game.GameSession.Execute"/>.</summary>
/// <param name="IsAccepted">False when the command was refused; then nothing changed and it was not logged.</param>
/// <param name="Error">Why it was refused.</param>
/// <param name="Outcome">Command-specific result: <see cref="Heroes.MoveResult"/> for steps, <see cref="Heroes.MovementOutcome"/> for moves.</param>
public sealed record CommandResult(bool IsAccepted, string? Error = null, object? Outcome = null)
{
    public static CommandResult Accepted(object? outcome = null) => new(true, null, outcome);

    public static CommandResult Rejected(string error) => new(false, error);

    /// <summary>Throws <see cref="InvalidOperationException"/> for a refused command; returns the result otherwise.</summary>
    public CommandResult ThrowIfRejected() => IsAccepted ? this : throw new InvalidOperationException(Error);
}
