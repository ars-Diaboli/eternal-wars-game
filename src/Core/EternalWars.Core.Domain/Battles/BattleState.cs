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
        Owner = side;
        Creature = creature;
        Count = count;
        InitialCount = count;
        SourceSlot = sourceSlot;
        Position = position;
        (Width, Height) = machine is { } kind ? WarMachines.Footprint(kind) : (creature.Size.Cells(), creature.Size.Cells());
        TopHealth = creature.Health;
        ShotsLeft = creature.Shots;
        MutableCastsLeft = new Dictionary<string, int>(creature.SpellCasts, StringComparer.Ordinal);
    }

    public int Id { get; }

    /// <summary>The war machine this unit is (ADR-056); null for creature stacks.</summary>
    public WarMachineKind? Machine { get; }

    public bool IsMachine => Machine is not null;

    /// <summary>The side the stack came with; its creatures go back to it when the battle ends (ADR-150).</summary>
    public BattleSide Owner { get; }

    /// <summary>
    /// The side the stack fights for now: its <see cref="Owner"/>'s, or while a <c>controlled</c> spell is on it (Hypnotize, Puppet Master, ADR-150)
    /// that spell's caster's, whose hero then commands it and gives it its skills.
    /// </summary>
    public BattleSide Side => MutableSpells.Count > 0 && MutableSpells.LastOrDefault(s => s.Modifiers.Has(Magic.SpellStatus.Controlled)) is { } control
        ? control.Caster
        : Owner;

    public CreatureDefinition Creature { get; }

    /// <summary>Army slot the stack came from; -1 for a split neutral guard.</summary>
    public int SourceSlot { get; }

    /// <summary>The count it started the battle with; a stack Puppet Master III split (ADR-152) gives the split part's creatures to that part.</summary>
    public int InitialCount { get; internal set; }

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

    /// <summary>The creature table's caster spell id → casts left this battle (Caster capability, ADR-120).</summary>
    public IReadOnlyDictionary<string, int> CastsLeft => MutableCastsLeft;

    internal Dictionary<string, int> MutableCastsLeft { get; init; }

    /// <summary>Called by a spell (ADR-106, ADR-120): it vanishes when the battle ends, is nobody's loss and is worth no experience.</summary>
    public bool IsSummoned { get; internal init; }

    /// <summary>A copy of another stack (Mirror Image, ADR-152): summoned, it vanishes when hit (<see cref="CopyHits"/>) or when its rounds end.</summary>
    public bool IsCopy { get; internal init; }

    /// <summary>Hits a copy can still take; it vanishes when they reach 0.</summary>
    public int CopyHits { get; internal set; }

    /// <summary>The last round a copy fights in, and the spell that made it.</summary>
    public int CopyUntilRound { get; internal init; }

    public string? CopySpell { get; internal init; }

    /// <summary>Skeletons Raise Dead raised from a destroyed stack (ADR-152): they join the caster's army when its hero wins.</summary>
    public bool IsRaised { get; internal init; }

    /// <summary>It took this round's second turn of a <c>twice</c> spell (Clockwork Precision, ADR-152).</summary>
    public bool TookSecondTurn { get; internal set; }

    /// <summary>Rooted where it was called (Awaken the Grove's treants): it never moves, but fights what stands next to it.</summary>
    public bool IsRooted { get; internal init; }

    /// <summary>
    /// Whether it stays where it stands now: rooted, the keep's stack while the keep stands, held by Entangle (ADR-123), or on the turn Shadow Step III
    /// gave it back (ADR-151).
    /// </summary>
    public bool IsFixed => IsRooted || InKeep || StaysPut || Has(Magic.SpellStatus.Entangled);

    /// <summary>Its next turn this round, given back by a move spell (Shadow Step III, ADR-151), is one in which it does not move.</summary>
    public bool StaysPut { get; internal set; }

    /// <summary>Moves in a straight line over everything: a flyer or teleporter, or a stack under Fleetfoot (ADR-123).</summary>
    public bool MovesFreely => Creature.MovesFreely || Has(Magic.SpellStatus.Phase);

    /// <summary>Whether one of the stack's spells gives it the status (ADR-123).</summary>
    public bool Has(Magic.SpellStatus status) => MutableSpells.Count > 0 && SpellModifiers.Has(status);

    /// <summary>
    /// Creatures a revive brought back only for this battle (Resurrection rank I, ADR-123): they are lost again when it ends, and die first when the
    /// stack loses creatures.
    /// </summary>
    public int Revived { get; internal set; }

    /// <summary>What the stack keeps after the battle: its count less the creatures revived only for the battle.</summary>
    public int KeptCount => Math.Max(0, Count - Revived);

    /// <summary>The damage the stack has dealt this battle (Call the Debt, ADR-123).</summary>
    public long DamageDealt { get; internal set; }

    public bool IsAlive => Count > 0;

    public int TotalHealth => Count == 0 ? 0 : ((Count - 1) * Creature.Health) + TopHealth;

    /// <summary>The health of <paramref name="creatures"/> of the stack's creatures, the wounded top creature first (what killing them takes).</summary>
    internal int HealthOf(int creatures) => TotalHealth - ((Count - creatures) * Creature.Health);

    /// <summary>The stack as views first hear of it.</summary>
    internal Events.BattleUnitInfo Info() => new(Id, Side, Creature.Id, Count, Position, Width, Height, Machine);

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

    /// <summary>
    /// The defenders' shooting stack in the keep (ADR-106 g, ADR-120): while the keep stands it does not move, cannot be attacked in melee and shots
    /// at it take the keep's 1/2 cover.
    /// </summary>
    public bool InKeep { get; internal set; }

    /// <summary>Buffs and curses on the stack (ADR-110), oldest first; a spell cast again replaces its earlier effect.</summary>
    public IReadOnlyList<ActiveSpell> Spells => MutableSpells;

    internal ActiveSpellList MutableSpells { get; private init; } = [];

    /// <summary>What the stack's spells change, all added up.</summary>
    public Magic.SpellModifiers SpellModifiers => MutableSpells.Modifiers;

    public override string ToString() => $"#{Id} {Count}× {Creature.Name} ({Side}) at {Position}";

    /// <summary>The unit as it is now, for a battle copy (<see cref="BattleState.Copy"/>).</summary>
    internal BattleUnit Copy() => new(Id, Owner, Creature, InitialCount, SourceSlot, Position, Machine)
    {
        Count = Count,
        TopHealth = TopHealth,
        ShotsLeft = ShotsLeft,
        MutableCastsLeft = new Dictionary<string, int>(CastsLeft, StringComparer.Ordinal),
        IsSummoned = IsSummoned,
        IsRooted = IsRooted,
        IsCopy = IsCopy,
        CopyHits = CopyHits,
        CopyUntilRound = CopyUntilRound,
        CopySpell = CopySpell,
        IsRaised = IsRaised,
        TookSecondTurn = TookSecondTurn,
        InitialCount = InitialCount,
        HasRetaliated = HasRetaliated,
        IsDefending = IsDefending,
        HitsThisRound = HitsThisRound,
        RoundMorale = RoundMorale,
        RageAttack = RageAttack,
        LosesNextAction = LosesNextAction,
        InKeep = InKeep,
        StaysPut = StaysPut,
        MutableSpells = [.. MutableSpells],
        Revived = Revived,
        DamageDealt = DamageDealt,
    };
}

/// <summary>
/// A buff or curse on a stack (ADR-110): what it changes, until the end of round <c>UntilRound</c>; a spell that lasts turns (ADR-150) has
/// <c>TurnsLeft</c> of the stack's turns to go as well. Martyrdom (ADR-152) names the stack that takes the blows (<c>Martyr</c>); the martyr's own
/// share of it (its Defense, rank III) is marked <c>IsMartyr</c>.
/// </summary>
public sealed record ActiveSpell(string Spell, BattleSide Caster, Magic.SpellModifiers Modifiers, int UntilRound, int? TurnsLeft = null)
{
    public int? Martyr { get; init; }

    public bool IsMartyr { get; init; }
}

/// <summary>Raise Dead on a side (ADR-152): the spell, the percent of the slain health raised after the battle, and its raises during it.</summary>
internal sealed record RaiseDead(string Spell, int Percent, Magic.SpellRises Rises);

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
    private readonly List<BattleZone> _zones = [];
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
        foreach (var participant in new[] { attacker, defender })
        {
            if (participant.Caster is { } caster)
            {
                ManaLeft[participant.Side] = caster.Mana;
            }
        }
    }

    /// <summary>The mana a side's hero has left (ADR-110); written back to the hero when the battle ends.</summary>
    internal Dictionary<BattleSide, int> ManaLeft { get; } = [];

    /// <summary>Raise Dead cast by a side's hero (ADR-152): its spell, percent and how often it still raises a destroyed stack.</summary>
    internal Dictionary<BattleSide, RaiseDead> Raising { get; } = [];

    /// <summary>Destroyed stacks Raise Dead has raised already (they rise once, and count no more after the battle).</summary>
    internal HashSet<int> Risen { get; } = [];

    /// <summary>The round a side's hero last cast in; it casts at most once a round (ADR-071, MAG-4).</summary>
    internal Dictionary<BattleSide, int> CastRounds { get; } = [];

    public int ManaOf(BattleSide side) => ManaLeft.GetValueOrDefault(side);

    /// <summary>Whether the side's hero already cast this round.</summary>
    public bool HasCastThisRound(BattleSide side) => CastRounds.TryGetValue(side, out var round) && round == Round;

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

    /// <summary>The town's fortifications when the battle is a siege (ADR-099, ADR-106); null for an ordinary battle.</summary>
    public SiegeState? Siege { get; internal set; }

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

    /// <summary>Whether the field may get a dividing cliff (ADR-146): not in a creature bank's battle (and never in a siege).</summary>
    internal bool CliffsAllowed { get; set; } = true;

    public int Round { get; internal set; }

    public IReadOnlyList<BattleUnit> Units => _units;

    public IReadOnlyList<BattleObstacle> Obstacles => _obstacles;

    /// <summary>Field zones the spells left (ADR-149), oldest first.</summary>
    public IReadOnlyList<BattleZone> Zones => _zones;

    internal List<BattleZone> MutableZones => _zones;

    /// <summary>Zones made this battle, for the next zone's id.</summary>
    internal int ZonesMade { get; set; }

    /// <summary>The unit whose turn it is.</summary>
    public BattleUnit? ActiveUnit { get; internal set; }

    /// <summary>Remaining units this round, in acting order (excluding the active unit).</summary>
    public IReadOnlyList<int> Queue => _queue;

    /// <summary>Units that waited, acting after the queue in reverse initiative order.</summary>
    public IReadOnlyList<int> Waiting => _waiting;

    /// <summary>Quick combat: the AI commands both sides until the end.</summary>
    public bool AutoResolve { get; internal set; }

    /// <summary>The game's creatures, for what spells summon (ADR-120).</summary>
    internal CreatureCatalog Creatures { get; init; } = CreatureCatalog.Default;

    /// <summary>The game's spells, for what creature casters cast (ADR-120).</summary>
    internal Magic.SpellCatalog Spells { get; init; } = Magic.SpellCatalog.Default;

    public BattleSide? Winner { get; internal set; }

    public BattleEndReason? EndReason { get; internal set; }

    internal GameRandom Random { get; }

    /// <summary>Per-round tie-break rolls (unit id → roll).</summary>
    internal Dictionary<int, int> TieRolls { get; } = [];

    internal HashSet<int> WaitedThisRound { get; } = [];

    /// <summary>Stacks waiting to be deployed while the width is being chosen.</summary>
    internal (IReadOnlyList<BattleStackSpec> Attackers, IReadOnlyList<BattleStackSpec> Defenders) PendingStacks { get; set; } = ([], []);

    internal List<BattleUnit> MutableUnits => _units;

    /// <summary>The id for a stack that joins the battle (a summon, a split, a copy, a raised stack): one above the highest so far.</summary>
    internal int NextUnitId() => _units.Max(u => u.Id) + 1;

    internal List<BattleObstacle> MutableObstacles => _obstacles;

    internal List<int> MutableQueue => _queue;

    internal List<int> MutableWaiting => _waiting;

    public BattleParticipant Participant(BattleSide side) => side == BattleSide.Attacker ? Attacker : Defender;

    /// <summary>
    /// A copy that plays on by itself (the auto combat preview, ADR-090): its own units, queues and random generator, all as they are now,
    /// so it draws the same numbers as this battle would.
    /// </summary>
    internal BattleState Copy()
    {
        var copy = new BattleState(Attacker, Defender, Terrain, Location, IsAmbush, new GameRandom(Random.State), NativeTowns)
        {
            Phase = Phase,
            Width = Width,
            WidthOptions = WidthOptions,
            WidthChooser = WidthChooser,
            Deployer = Deployer,
            DeploymentColumns = DeploymentColumns,
            DeploymentAllowed = DeploymentAllowed,
            CliffsAllowed = CliffsAllowed,
            Round = Round,
            AutoResolve = AutoResolve,
            Winner = Winner,
            EndReason = EndReason,
            PendingStacks = PendingStacks,
            Siege = Siege?.Copy(),
            Creatures = Creatures,
            Spells = Spells,
            ZonesMade = ZonesMade,
        };
        copy._units.AddRange(_units.Select(u => u.Copy()));
        copy._obstacles.AddRange(_obstacles);
        copy._zones.AddRange(_zones);
        copy._queue.AddRange(_queue);
        copy._waiting.AddRange(_waiting);
        copy.ActiveUnit = ActiveUnit is { } active ? copy.GetUnit(active.Id) : null;
        foreach (var (unit, roll) in TieRolls)
        {
            copy.TieRolls[unit] = roll;
        }

        copy.WaitedThisRound.UnionWith(WaitedThisRound);
        copy.SideEffects.AddRange(SideEffects);
        copy.UsedWarcries.UnionWith(UsedWarcries);
        foreach (var (side, round) in WarcryRounds)
        {
            copy.WarcryRounds[side] = round;
        }

        foreach (var (side, mana) in ManaLeft)
        {
            copy.ManaLeft[side] = mana;
        }

        foreach (var (side, round) in CastRounds)
        {
            copy.CastRounds[side] = round;
        }

        foreach (var (side, raise) in Raising)
        {
            copy.Raising[side] = raise;
        }

        copy.Risen.UnionWith(Risen);

        return copy;
    }

    /// <summary>Battlefield speed: the creature's, plus the hero's Drill, the preferred-creature bonus (ADR-048) and native terrain (ADR-068).</summary>
    public int SpeedOf(BattleUnit unit)
    {
        if (unit.IsMachine)
        {
            return 0; // war machines never move
        }

        var bonus = Participant(unit.Side).Bonus;
        var speed = unit.Creature.Speed + bonus.SpeedBonus + (bonus.Prefers(unit.Creature) ? Heroes.SkillEffects.PreferredSpeed : 0)
                    + (IsNative(unit) ? NativeSpeed : 0);
        if (unit.Spells.Count == 0)
        {
            return speed;
        }

        // Spells (ADR-110): flat first, then percent, at least 1.
        var spells = unit.SpellModifiers;
        return Math.Max(1, (speed + spells.Speed) * (100 + spells.SpeedPercent) / 100);
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
    /// Creature morale plus the hero's, the army's (ADR-075), warcries' and spells', within ±5 (ADR-053, ADR-067); war machines, undead,
    /// constructs and elementals have none (ADR-071).
    /// </summary>
    public int MoraleOf(BattleUnit unit) => unit.IsMachine || !MoraleAndLuck.HasMorale(unit.Creature)
        ? 0
        : MoraleAndLuck.Clamp(unit.Creature.Morale + Participant(unit.Side).Bonus.Morale + Participant(unit.Side).ArmyMorale
                              + ActiveEffects(unit.Side).Sum(e => e.Morale) + unit.SpellModifiers.Morale);

    /// <summary>Living creature stacks of a side; war machines do not keep a side in the battle (ADR-056).</summary>
    public IEnumerable<BattleUnit> LivingArmyOf(BattleSide side) => LivingUnitsOf(side).Where(u => !u.IsMachine);

    /// <summary>
    /// Living creature stacks the side came with, whoever they fight for now (ADR-150): they keep the side in the battle and go back to it when
    /// the battle ends.
    /// </summary>
    public IEnumerable<BattleUnit> OwnedArmyOf(BattleSide side) => _units.Where(u => u.IsAlive && u.Owner == side && !u.IsMachine && !u.IsCopy);

    /// <summary>The side's living war machine of that kind, if any.</summary>
    public BattleUnit? LivingMachine(BattleSide side, WarMachineKind kind) => LivingUnitsOf(side).FirstOrDefault(u => u.Machine == kind);

    /// <summary>A unit's shots do not run out: an offensive machine, or a shooter whose side's cart gives unlimited shots.</summary>
    public bool HasUnlimitedShots(BattleUnit unit) =>
        unit.IsMachine ||
        (WarMachines.CartShotBonusPercent(Participant(unit.Side).Bonus.AmmoSupply) is null && LivingMachine(unit.Side, WarMachineKind.AmmoCart) is not null);

    /// <summary>Creature luck plus the hero's and spells', within ±5 (ADR-053, ADR-110).</summary>
    public int LuckOf(BattleUnit unit) => MoraleAndLuck.Clamp(unit.Creature.Luck + Participant(unit.Side).Bonus.Luck + unit.SpellModifiers.Luck);

    /// <summary>
    /// Initiative for this round's order: the creature's plus the hero's Alertness (flat, ADR-069), changed by warcries and spells (percent), then by
    /// morale's Haste or Slow (ADR-053, ADR-067).
    /// </summary>
    public int InitiativeOf(BattleUnit unit)
    {
        var flat = unit.Creature.Initiative + (unit.IsMachine ? 0 : Participant(unit.Side).Bonus.Initiative);
        var percent = unit.IsMachine ? 0 : ActiveEffects(unit.Side).Sum(e => e.InitiativePercent) + unit.SpellModifiers.InitiativePercent;
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
