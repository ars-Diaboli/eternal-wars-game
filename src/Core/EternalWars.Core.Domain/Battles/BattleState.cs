using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;

namespace EternalWars.Core.Domain.Battles;

/// <summary>A creature stack on the battlefield. Only the top creature carries damage (<see cref="TopHealth"/>).</summary>
public sealed class BattleUnit
{
    internal BattleUnit(int id, BattleSide side, CreatureDefinition creature, int count, int sourceSlot, CellPosition position, WarMachineKind? machine = null)
    {
        Machine = machine;
        Id = id;
        Side = side;
        Creature = creature;
        Count = count;
        InitialCount = count;
        SourceSlot = sourceSlot;
        Position = position;
        (Width, Height) = machine is { } kind ? WarMachines.Footprint(kind) : (creature.Size.Cells(), creature.Size.Cells());
        TopHealth = creature.Health;
        ShotsLeft = creature.Shots;
        CastsLeft = new Dictionary<string, int>(creature.SpellCasts, StringComparer.Ordinal);
    }

    public int Id { get; }

    /// <summary>The war machine this unit is (ADR-056); null for creature stacks.</summary>
    public WarMachineKind? Machine { get; }

    public bool IsMachine => Machine is not null;

    public BattleSide Side { get; }

    public CreatureDefinition Creature { get; }

    /// <summary>Army slot the stack came from; -1 for a split neutral guard.</summary>
    public int SourceSlot { get; }

    public int InitialCount { get; }

    public int Count { get; internal set; }

    public int TopHealth { get; internal set; }

    /// <summary>Top-left cell of the footprint.</summary>
    public CellPosition Position { get; internal set; }

    /// <summary>Side of a creature's square footprint (2 / 3 / 4 cells); a machine's footprint is <see cref="Width"/> × <see cref="Height"/>.</summary>
    public int Size => Creature.Size.Cells();

    /// <summary>Footprint width in cells: the creature's size, 1 for war machines (ADR-073).</summary>
    public int Width { get; }

    /// <summary>Footprint height in cells: the creature's size, 3 or 2 for war machines (ADR-073).</summary>
    public int Height { get; }

    public CellRect Rect => RectAt(Position);

    /// <summary>The footprint if the unit stood at <paramref name="topLeft"/>.</summary>
    public CellRect RectAt(CellPosition topLeft) => new(topLeft.X, topLeft.Y, Width, Height);

    public int ShotsLeft { get; internal set; }

    /// <summary>Spell id → casts left this battle (Caster capability; used once magic exists).</summary>
    public IReadOnlyDictionary<string, int> CastsLeft { get; }

    public bool IsAlive => Count > 0;

    public int TotalHealth => Count == 0 ? 0 : ((Count - 1) * Creature.Health) + TopHealth;

    /// <summary>Has already retaliated this round.</summary>
    public bool HasRetaliated { get; internal set; }

    /// <summary>Defending until its next turn (+20 % Defense).</summary>
    public bool IsDefending { get; internal set; }

    /// <summary>Times it was hit this round (hit-history hook for abilities).</summary>
    public int HitsThisRound { get; internal set; }

    /// <summary>What this round's morale roll did (ADR-053); an extra action turns into None once taken.</summary>
    public MoraleEffect RoundMorale { get; internal set; }

    /// <summary>Attack gained from kills with Blood Rage, for the rest of the battle (ADR-067).</summary>
    public int RageAttack { get; internal set; }

    /// <summary>Terror (ADR-067): the unit's next action is lost.</summary>
    public bool LosesNextAction { get; internal set; }

    public override string ToString() => $"#{Id} {Count}× {Creature.Name} ({Side}) at {Position}";
}

/// <summary>
/// A battle in progress or just finished (the current one is <c>GameState.Battle</c>). Its random generator
/// is forked from the game's, so a battle replays exactly from the game seed plus the command log.
/// </summary>
public sealed class BattleState
{
    public const int Height = 24;
    public const int DefaultWidth = 30;
    public const int SiegeWidth = 36;

    /// <summary>Every whole this many cells between the footprints adds 1/8 to a shot's ranged penalty (ADR-070, CMB-6).</summary>
    public const int DistancePenaltyCells = 10;

    /// <summary>A ranged penalty (distance and cover, less Marksmanship) above this many eighths makes a shot impossible (ADR-035, ADR-070).</summary>
    public const int MaxRangedPenaltyEighths = 7;

    private readonly List<BattleUnit> _units = [];
    private readonly List<BattleObstacle> _obstacles = [];
    private readonly List<int> _queue = [];
    private readonly List<int> _waiting = [];

    internal BattleState(
        BattleParticipant attacker,
        BattleParticipant defender,
        Map.TerrainType terrain,
        MapPosition location,
        bool isAmbush,
        GameRandom random,
        IReadOnlyCollection<string>? nativeTowns = null)
    {
        Attacker = attacker;
        Defender = defender;
        Terrain = terrain;
        Location = location;
        IsAmbush = isAmbush;
        Random = random;
        NativeTowns = nativeTowns ?? [];
    }

    /// <summary>Native terrain (ADR-068): a creature of a town native to the battlefield's terrain gets +1 Attack, Defense and Speed.</summary>
    public const int NativeAttack = 1;

    public const int NativeDefense = 1;

    public const int NativeSpeed = 1;

    /// <summary>The towns whose creatures are at home on <see cref="Terrain"/> (the terrain's native towns).</summary>
    public IReadOnlyCollection<string> NativeTowns { get; }

    /// <summary>Whether the unit fights on its native terrain; neutral creatures and war machines never do.</summary>
    public bool IsNative(BattleUnit unit) => !unit.IsMachine && unit.Creature.Town is { } town && NativeTowns.Contains(town);

    public BattleParticipant Attacker { get; }

    public BattleParticipant Defender { get; }

    public Map.TerrainType Terrain { get; }

    /// <summary>Adventure-map tile the battle is fought for.</summary>
    public MapPosition Location { get; }

    public bool IsAmbush { get; }

    public BattlePhase Phase { get; internal set; }

    public int Width { get; internal set; } = DefaultWidth;

    /// <summary>Widths the <see cref="WidthChooser"/> may pick while <see cref="BattlePhase.ChoosingWidth"/>.</summary>
    public IReadOnlyList<int> WidthOptions { get; internal set; } = [DefaultWidth];

    public BattleSide? WidthChooser { get; internal set; }

    /// <summary>The side placing its stacks while <see cref="BattlePhase.Deploying"/> (ADR-072); null when nobody deploys.</summary>
    public BattleSide? Deployer { get; internal set; }

    /// <summary>How many columns from its own edge the <see cref="Deployer"/> may use (4 / 5 / 6, the machine column included).</summary>
    public int DeploymentColumns { get; internal set; }

    /// <summary>Whether the cell rectangle lies in the deployer's zone.</summary>
    public bool IsInDeploymentZone(CellRect rect) => Deployer switch
    {
        BattleSide.Attacker => rect.X >= 0 && rect.Right <= DeploymentColumns,
        BattleSide.Defender => rect.X >= Width - DeploymentColumns && rect.Right <= Width,
        _ => false,
    };

    /// <summary>Whether this battle allows a deployment step at all (heroes and neutral monsters, not creature banks).</summary>
    internal bool DeploymentAllowed { get; set; }

    public int Round { get; internal set; }

    public IReadOnlyList<BattleUnit> Units => _units;

    public IReadOnlyList<BattleObstacle> Obstacles => _obstacles;

    /// <summary>The unit whose turn it is.</summary>
    public BattleUnit? ActiveUnit { get; internal set; }

    /// <summary>Remaining units this round, in acting order (excluding the active unit).</summary>
    public IReadOnlyList<int> Queue => _queue;

    /// <summary>Units that waited, acting after the queue in reverse initiative order.</summary>
    public IReadOnlyList<int> Waiting => _waiting;

    /// <summary>Quick combat: the AI commands both sides until the end.</summary>
    public bool AutoResolve { get; internal set; }

    public BattleSide? Winner { get; internal set; }

    public BattleEndReason? EndReason { get; internal set; }

    internal GameRandom Random { get; }

    /// <summary>Per-round tie-break rolls (unit id → roll).</summary>
    internal Dictionary<int, int> TieRolls { get; } = [];

    internal HashSet<int> WaitedThisRound { get; } = [];

    /// <summary>Stacks waiting to be deployed while the width is being chosen.</summary>
    internal (IReadOnlyList<BattleStackSpec> Attackers, IReadOnlyList<BattleStackSpec> Defenders) PendingStacks { get; set; } = ([], []);

    internal List<BattleUnit> MutableUnits => _units;

    internal List<BattleObstacle> MutableObstacles => _obstacles;

    internal List<int> MutableQueue => _queue;

    internal List<int> MutableWaiting => _waiting;

    public BattleParticipant Participant(BattleSide side) => side == BattleSide.Attacker ? Attacker : Defender;

    /// <summary>Battlefield speed: the creature's, plus the hero's Drill, the preferred-creature bonus (ADR-048) and native terrain (ADR-068).</summary>
    public int SpeedOf(BattleUnit unit)
    {
        if (unit.IsMachine)
        {
            return 0; // war machines never move
        }

        var bonus = Participant(unit.Side).Bonus;
        return unit.Creature.Speed + bonus.SpeedBonus + (bonus.Prefers(unit.Creature) ? Heroes.SkillEffects.PreferredSpeed : 0)
               + (IsNative(unit) ? NativeSpeed : 0);
    }

    /// <summary>Warcry effects still running, from <see cref="Round"/> on (ADR-067).</summary>
    internal List<BattleSideEffect> SideEffects { get; } = [];

    /// <summary>Warcries a side has used; each works once per battle.</summary>
    internal HashSet<(BattleSide Side, Warcry Warcry)> UsedWarcries { get; } = [];

    /// <summary>The round a side last used a warcry in; a side uses at most one a round.</summary>
    internal Dictionary<BattleSide, int> WarcryRounds { get; } = [];

    /// <summary>Why the side cannot use the warcry now (not known, used before, or one already this round), or null (ADR-067).</summary>
    public string? WarcryRefusal(BattleSide side, Warcry warcry)
    {
        if (!PrimalFury.Knows(Participant(side).Bonus.Warcry, warcry))
        {
            return $"The hero does not know {warcry}.";
        }

        if (UsedWarcries.Contains((side, warcry)))
        {
            return $"{warcry} was already used in this battle.";
        }

        return WarcryRounds.TryGetValue(side, out var round) && round == Round ? "Only one warcry a round." : null;
    }

    private IEnumerable<BattleSideEffect> ActiveEffects(BattleSide side) => SideEffects.Where(e => e.Side == side && e.UntilRound >= Round);

    /// <summary>
    /// Creature morale plus the hero's, the army's (ADR-075) and warcries', within ±5 (ADR-053, ADR-067); war machines, undead,
    /// constructs and elementals have none (ADR-071).
    /// </summary>
    public int MoraleOf(BattleUnit unit) => unit.IsMachine || !MoraleAndLuck.HasMorale(unit.Creature)
        ? 0
        : MoraleAndLuck.Clamp(unit.Creature.Morale + Participant(unit.Side).Bonus.Morale + Participant(unit.Side).ArmyMorale
                              + ActiveEffects(unit.Side).Sum(e => e.Morale));

    /// <summary>Living creature stacks of a side; war machines do not keep a side in the battle (ADR-056).</summary>
    public IEnumerable<BattleUnit> LivingArmyOf(BattleSide side) => LivingUnitsOf(side).Where(u => !u.IsMachine);

    /// <summary>The side's living war machine of that kind, if any.</summary>
    public BattleUnit? LivingMachine(BattleSide side, WarMachineKind kind) => LivingUnitsOf(side).FirstOrDefault(u => u.Machine == kind);

    /// <summary>A unit's shots do not run out: an offensive machine, or a shooter whose side's cart gives unlimited shots.</summary>
    public bool HasUnlimitedShots(BattleUnit unit) =>
        unit.IsMachine ||
        (WarMachines.CartShotBonusPercent(Participant(unit.Side).Bonus.AmmoSupply) is null && LivingMachine(unit.Side, WarMachineKind.AmmoCart) is not null);

    /// <summary>Creature luck plus the hero's, within ±5 (ADR-053).</summary>
    public int LuckOf(BattleUnit unit) => MoraleAndLuck.Clamp(unit.Creature.Luck + Participant(unit.Side).Bonus.Luck);

    /// <summary>
    /// Initiative for this round's order: the creature's plus the hero's Alertness (flat, ADR-069), changed by warcries (percent), then by
    /// morale's Haste or Slow (ADR-053, ADR-067).
    /// </summary>
    public int InitiativeOf(BattleUnit unit)
    {
        var flat = unit.Creature.Initiative + (unit.IsMachine ? 0 : Participant(unit.Side).Bonus.Initiative);
        var percent = unit.IsMachine ? 0 : ActiveEffects(unit.Side).Sum(e => e.InitiativePercent);
        // Rounded to the nearest whole point (halves up), so a +5 % change counts from an initiative of 10.
        var initiative = percent == 0 ? flat : Math.Max(1, ((flat * (100 + percent)) + 50) / 100);
        return MoraleAndLuck.Initiative(initiative, unit.RoundMorale);
    }

    public BattleUnit GetUnit(int id) => _units.First(u => u.Id == id);

    public BattleUnit? FindUnit(int id) => _units.FirstOrDefault(u => u.Id == id);

    public IEnumerable<BattleUnit> LivingUnits => _units.Where(u => u.IsAlive);

    public IEnumerable<BattleUnit> LivingUnitsOf(BattleSide side) => _units.Where(u => u.IsAlive && u.Side == side);

    /// <summary>Living unit whose footprint covers <paramref name="cell"/>.</summary>
    public BattleUnit? UnitAt(CellPosition cell) => _units.FirstOrDefault(u => u.IsAlive && u.Rect.Contains(cell));

    public bool IsInside(CellRect rect) => rect.X >= 0 && rect.Y >= 0 && rect.Right <= Width && rect.Bottom <= Height;

    /// <summary>Whether the side is commanded by the battle AI right now.</summary>
    public bool IsComputerControlled(BattleSide side) => AutoResolve || Participant(side).IsComputer;
}
