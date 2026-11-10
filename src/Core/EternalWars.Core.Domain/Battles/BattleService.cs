using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// Battles on the adventure map: starts them from encounters (<see cref="BattleStarter"/>), lets the players (or the AI) command their
/// sides (<see cref="BattleCommandHandler"/>, <see cref="BattleFlow"/>), and applies the result to the map (<see cref="BattleAftermath"/>:
/// survivors, experience, defeated heroes and guards). While a battle runs, a <see cref="Decisions.BattleDecision"/> is pending, so the adventure
/// map is frozen. This is the session's single entry point to them.
/// </summary>
public sealed class BattleService
{
    /// <summary>Experience for defeating (not merely routing) an enemy hero.</summary>
    public const int HeroDefeatExperience = BattleAftermath.HeroDefeatExperience;

    private readonly BattleFlow _flow;
    private readonly BattleStarter _starter;
    private readonly BattleCommandHandler _commands;

    public BattleService(
        GameState state, IEventPublisher events, ArmyService armies, ProgressionService progression, ArtifactService artifacts, Economy.EconomyService economy)
    {
        var aftermath = new BattleAftermath(state, events, armies, progression, artifacts, economy);
        _flow = new BattleFlow(state, events, aftermath);
        _starter = new BattleStarter(state, events, artifacts, _flow, aftermath);
        _commands = new BattleCommandHandler(state, events, economy, _flow, aftermath);
    }

    /// <summary>Who may command <paramref name="side"/> of the current battle right now (null: the AI does).</summary>
    public PlayerId? Commander(BattleSide side) => _flow.Commander(side);

    /// <inheritdoc cref="BattleCommandHandler.AutoCombatPlayer"/>
    public PlayerId? AutoCombatPlayer() => _commands.AutoCombatPlayer();

    /// <inheritdoc cref="BattleCommandHandler.Preview"/>
    public AutoCombatPreview Preview() => _commands.Preview();

    /// <summary>The AI's throwaway estimate fights play a hero battle as a siege of these fortifications (<see cref="AI.BattleEstimator"/>).</summary>
    internal SiegeSpec? EstimateSiege
    {
        get => _starter.EstimateSiege;
        set => _starter.EstimateSiege = value;
    }

    /// <summary>Whether computer players' heroes retreat from lost battles (off in the AI's throwaway estimate fights).</summary>
    internal bool AiMayRetreat
    {
        get => _flow.AiMayRetreat;
        set => _flow.AiMayRetreat = value;
    }

    internal void BeginHeroBattle(Hero attacker, Hero defender, bool isAmbush) => _starter.BeginHeroBattle(attacker, defender, isAmbush);

    /// <inheritdoc cref="BattleStarter.BeginArenaBattle"/>
    internal void BeginArenaBattle(Hero attacker, Hero defender, MapPosition location) => _starter.BeginArenaBattle(attacker, defender, location);

    internal void BeginGuardBattle(Hero attacker, MapObject guard, string creature, int count) => _starter.BeginGuardBattle(attacker, guard, creature, count);

    /// <inheritdoc cref="BattleStarter.BeginBankBattle"/>
    internal void BeginBankBattle(Hero attacker, CreatureBank bank) => _starter.BeginBankBattle(attacker, bank);

    /// <inheritdoc cref="BattleStarter.BeginBoxBattle"/>
    internal void BeginBoxBattle(Hero attacker, PandorasBox box) => _starter.BeginBoxBattle(attacker, box);

    /// <inheritdoc cref="BattleStarter.BeginEventBattle"/>
    internal void BeginEventBattle(Hero attacker, IReadOnlyList<CreatureStack> army) => _starter.BeginEventBattle(attacker, army);

    /// <inheritdoc cref="BattleStarter.BeginTownBattle"/>
    internal void BeginTownBattle(Hero attacker, Town town) => _starter.BeginTownBattle(attacker, town);

    /// <inheritdoc cref="BattleStarter.BeginGarrisonBattle"/>
    internal void BeginGarrisonBattle(Hero attacker, MapGarrison garrison) => _starter.BeginGarrisonBattle(attacker, garrison);

    internal void ChooseWidth(PlayerId player, int width) => _commands.ChooseWidth(player, width);

    /// <inheritdoc cref="BattleCommandHandler.Redeploy"/>
    internal void Redeploy(PlayerId player, int unitId, CellPosition to) => _commands.Redeploy(player, unitId, to);

    /// <inheritdoc cref="BattleCommandHandler.EndDeployment"/>
    internal void EndDeployment(PlayerId player) => _commands.EndDeployment(player);

    internal void Act(PlayerId player, BattleAction action) => _commands.Act(player, action);

    /// <inheritdoc cref="BattleCommandHandler.UseWarcry"/>
    internal void UseWarcry(PlayerId player, Warcry warcry) => _commands.UseWarcry(player, warcry);

    /// <inheritdoc cref="BattleCommandHandler.Cast"/>
    internal void Cast(PlayerId player, string spell, int? target, CellPosition? cell, int? second = null) => _commands.Cast(player, spell, target, cell, second);

    /// <inheritdoc cref="BattleCommandHandler.Retreat"/>
    internal void Retreat(PlayerId player) => _commands.Retreat(player);

    /// <inheritdoc cref="BattleCommandHandler.Surrender"/>
    internal void Surrender(PlayerId player) => _commands.Surrender(player);

    /// <inheritdoc cref="BattleCommandHandler.AutoResolve"/>
    internal void AutoResolve(PlayerId player) => _commands.AutoResolve(player);

    /// <inheritdoc cref="BattleCommandHandler.SetAutoCombat"/>
    internal void SetAutoCombat(PlayerId player, AutoCombatMode mode) => _commands.SetAutoCombat(player, mode);
}
