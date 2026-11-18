using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Magic.Parsing;

namespace EternalWars.Core.Domain.Magic;

/// <summary>A spell's element besides its school (ADR-106): air, earth, fire or water; Magic Arrow is all four, Bind Elemental the bound kind.</summary>
public enum SpellElement
{
    Air,
    Earth,
    Fire,
    Water,
    All,
    Bound,
}

/// <summary>What a spell does in battle (rank I, ADR-099): damage, healing, or an effect on stacks for some rounds.</summary>
public enum SpellEffectKind
{
    /// <summary>Damage to enemy stacks (or to every stack of an area or of the field).</summary>
    Damage,

    /// <summary>Health back to an own stack's top creature.</summary>
    Heal,

    /// <summary>An effect on own stacks.</summary>
    Buff,

    /// <summary>An effect on enemy stacks.</summary>
    Curse,

    /// <summary>A stack of town creatures (or Conflux elementals) for the battle (ADR-106, ADR-120): it vanishes afterwards and is worth no experience.</summary>
    Summon,

    /// <summary>Health back to an own stack, its top creature first, then its dead up to its count at the start of the battle (Resurrection, ADR-123).</summary>
    Revive,

    /// <summary>Spells taken off stacks (Purge, Unravel, Dispel, ADR-123).</summary>
    Dispel,

    /// <summary>A percent of an enemy stack's creatures die outright, at least 1 (Final Reckoning, ADR-123).</summary>
    Slay,

    /// <summary>
    /// A field zone for some rounds (ADR-149): cells of the field that hurt, heal, block or darken (Consecrate, Thornwall, Bone Wall, Fire Wall,
    /// Darkness).
    /// </summary>
    Zone,

    /// <summary>An own stack is moved to a free cell the caster picks (Teleport, Shadow Step, ADR-151): aimed with two clicks, the stack and the cell.</summary>
    Move,

    /// <summary>A copy of an own stack fights for some rounds and vanishes when hit (Mirror Image, ADR-152).</summary>
    Copy,

    /// <summary>
    /// Skeletons worth a percent of the slain enemies' health join the caster's army after the battle; ranks II / III also raise destroyed stacks
    /// as skeletons during it (Raise Dead, ADR-152).
    /// </summary>
    Raise,

    /// <summary>An own stack loses a percent of its creatures and the hero gains mana for the health lost (Blood Price, ADR-152).</summary>
    Sacrifice,
}

/// <summary>How often Raise Dead (ADR-152) raises a destroyed stack during the battle: never (only after it), once, or once each round.</summary>
public enum SpellRises
{
    None,
    Once,
    EachRound,
}

/// <summary>
/// What a moved stack (ADR-151) may still do this round when it had already taken its turn: nothing more, a whole turn (<c>again</c>), or a turn
/// in which it does not move (<c>again-attack</c>: it may attack what stands next to it, shoot or defend).
/// </summary>
public enum SpellTurnAgain
{
    None,
    Act,
    Attack,
}

/// <summary>Whose spells a dispel takes off and from whom (ADR-123): <c>own</c> stacks lose the enemy's, <c>enemy</c> stacks lose their own side's, <c>any</c> stack loses every spell.</summary>
public enum DispelScope
{
    Own,
    Enemy,
    Any,
}

/// <summary>
/// What a buff or curse does besides numbers (ADR-123): <c>blind</c> (no turns until hit or the spell ends; retaliates at half), <c>confused</c>
/// (moves and attacks at random), <c>entangled</c> (cannot move), <c>no-shoot</c>, <c>no-attack</c> (cannot attack or shoot; retaliates at half),
/// <c>no-retaliation</c>, <c>fearless</c> (Terror does not take hold), <c>vigil</c> (retaliates against every attack), <c>true-shot</c> (no distance
/// penalty or cover), <c>sanctuary</c> (cannot be attacked in melee), <c>stasis</c> (no turns, cannot be attacked or harmed by spells),
/// <c>unseen</c> (cannot be attacked or aimed at by the enemy until it acts), <c>phase</c> (moves like a flyer), <c>ward-mind</c> (immune to
/// <c>mind</c> spells). ADR-150: <c>frenzied</c> (its turn attacks the nearest stack, friend or foe), <c>controlled</c> (it fights for the
/// spell's caster: Hypnotize, Puppet Master). ADR-152: <c>twice</c> (takes a second turn right after its turn, once a round: Clockwork Precision).
/// ADR-177: <c>screened</c> (an enemy shooter needs a clear line to it: no stack or obstacle in between; Sanctuary III).
/// </summary>
[Flags]
public enum SpellStatus
{
    None = 0,
    Blind = 1,
    Confused = 1 << 1,
    Entangled = 1 << 2,
    NoShoot = 1 << 3,
    NoAttack = 1 << 4,
    NoRetaliation = 1 << 5,
    Fearless = 1 << 6,
    Vigil = 1 << 7,
    TrueShot = 1 << 8,
    Sanctuary = 1 << 9,
    Stasis = 1 << 10,
    Unseen = 1 << 11,
    Phase = 1 << 12,
    WardMind = 1 << 13,
    Frenzied = 1 << 14,
    Controlled = 1 << 15,
    Twice = 1 << 16,
    Screened = 1 << 17,
}

/// <summary>Whom a spell reaches: one stack, a square area, every stack of the target side, or every stack on the field.</summary>
public enum SpellReach
{
    Stack,
    Area,
    Side,
    All,
}

/// <summary>
/// Which stacks a spell works on at all: any, the living (not undead, constructs or machines), undead, undead and demons, shooters, melee stacks
/// (no shots; ranks II / III, ADR-122), stacks on the ground (not flyers; war machines count as on the ground), war machines only (ADR-152).
/// </summary>
public enum SpellFilter
{
    None,
    Living,
    Undead,
    UndeadOrDemon,
    Shooter,
    Melee,
    Ground,
    Machine,
}

/// <summary>
/// What a buff or curse changes on a stack while it lasts (ADR-099's rank I numbers): flat Attack, Defense, speed, morale and luck; percent speed and
/// initiative; percent damage dealt in melee or at range; percent damage taken from every attack, from melee, from shots and from spells; the
/// damage roll (+1 maximum, Bless; −1 minimum, Curse). ADR-123 adds the <see cref="SpellStatus"/> flags, damage (<c>burn</c>) and healing
/// (<c>regen</c>) at the start of each round, creatures lost each round (<c>decay%</c>), immunity to every spell up to a level (<c>immune-level</c>,
/// Anti-Magic) or to the enemy's up to a level (<c>shroud</c>), melee damage given back (<c>thorns%</c>) and damage dealt healed and revived
/// (<c>lifesteal%</c>). <c>burn</c> and <c>regen</c> grow with Spell Power when cast. ADR-152: the share of the damage meant for the stack that
/// another own stack takes (<c>martyr%</c>, Martyrdom). ADR-177: the extra damage of an attack the stack makes while Unseen (<c>ambush%</c>,
/// Unseen III).
/// </summary>
public sealed record SpellModifiers(
    int Attack = 0, int Defense = 0, int Speed = 0, int SpeedPercent = 0, int InitiativePercent = 0, int MeleePercent = 0, int RangedPercent = 0,
    int DamageTakenPercent = 0, int MeleeTakenPercent = 0, int RangedTakenPercent = 0, int SpellTakenPercent = 0, int Morale = 0, int Luck = 0,
    int Roll = 0, SpellStatus Status = SpellStatus.None, int Burn = 0, int Regen = 0, int DecayPercent = 0, int ImmuneLevel = 0, int ShroudLevel = 0,
    int ThornsPercent = 0, int LifestealPercent = 0, int MartyrPercent = 0, int AmbushPercent = 0)
{
    public static SpellModifiers None { get; } = new();

    public bool IsEmpty => this == None;

    public bool Has(SpellStatus status) => (Status & status) != 0;

    public static SpellModifiers operator +(SpellModifiers a, SpellModifiers b) => new(
        a.Attack + b.Attack, a.Defense + b.Defense, a.Speed + b.Speed, a.SpeedPercent + b.SpeedPercent, a.InitiativePercent + b.InitiativePercent,
        a.MeleePercent + b.MeleePercent, a.RangedPercent + b.RangedPercent, a.DamageTakenPercent + b.DamageTakenPercent,
        a.MeleeTakenPercent + b.MeleeTakenPercent, a.RangedTakenPercent + b.RangedTakenPercent, a.SpellTakenPercent + b.SpellTakenPercent,
        a.Morale + b.Morale, a.Luck + b.Luck, a.Roll + b.Roll, a.Status | b.Status, a.Burn + b.Burn, a.Regen + b.Regen,
        a.DecayPercent + b.DecayPercent, Math.Max(a.ImmuneLevel, b.ImmuneLevel), Math.Max(a.ShroudLevel, b.ShroudLevel),
        a.ThornsPercent + b.ThornsPercent, a.LifestealPercent + b.LifestealPercent, Math.Max(a.MartyrPercent, b.MartyrPercent),
        a.AmbushPercent + b.AmbushPercent);
}

/// <summary>
/// What a field zone does (ADR-149), its amounts at Spell Power 0: damage to a stack whose walk takes it into the zone (a flyer or teleporter only
/// when it lands there; <c>enter</c>), to a stack in it as a round starts (<c>start</c>), to a stack that moves while standing in it
/// (<c>leave</c>), to a stack next to it as a round starts (<c>beside</c>); health back to the top creature of each own stack in it as a round starts
/// (<c>heal</c>); walkers cannot step into it (<c>blocks</c>; flyers and teleporters pass over), own undead excepted (<c>own-undead</c>); shots whose
/// line crosses it are impossible (<c>blocks-shots</c>); shots into or out of it deal half (<c>dims-shots</c>).
/// </summary>
public sealed record ZoneEffect(
    int Enter = 0, int Start = 0, int Leave = 0, int Beside = 0, int Heal = 0, bool BlocksMovement = false, bool BlocksShots = false,
    bool OwnUndeadPass = false, bool DimsShots = false);

/// <summary>
/// A spell's battle effect at rank I, read from the <c>battle</c> column of <c>spells.csv</c>: <c>damage 35 area 3</c>, <c>heal 40</c>,
/// <c>buff attack 3 rounds 3</c>, <c>curse morale -2 rounds battle</c>, <c>damage 50 enemies undead-demon</c>, <c>damage 90 speed -2 rounds 2</c>
/// (damage that also leaves an effect). Amounts and rounds are base numbers at Spell Power 0 (ADR-072). <c>Rounds</c> is null for the rest of
/// the battle (not lengthened by Spell Power).
/// <c>walls 2</c> also damages that many standing wall sections by 1 in a siege (Earthquake, ADR-120). <c>summon sylvan.pegasus 150</c> calls
/// creatures worth that much health (several ids split by <c>|</c>: the first kind not yet summoned this battle), <c>rooted</c> ones never move.
/// Ranks II / III (ADR-122) add <c>targets N</c> (a stack spell also reaches the N − 1 stacks nearest the aimed one that it works on, on the same
/// side), <c>spare-own</c> (an area or field spell leaves the caster's own stacks alone) and the filters <c>melee</c> and <c>ground</c>.
/// ADR-123: <c>rounds 0</c> lasts the rest of this round; <c>revive 150 [lasting] [fallen]</c> (lasting: the revived stay after the battle; fallen:
/// also a stack destroyed in this battle), <c>dispel own|enemy|any</c>, <c>slay 10</c> (percent of the creatures); for damage <c>splash 50</c> (the
/// stacks next to a target take that percent), <c>chain N</c> (jumps to the N stacks nearest, each at half the one before), <c>pierce</c> (creature and
/// hero spell resistance do not count), <c>stun% N</c> (the chance a stack hit loses its next action), <c>execute N</c> (a stack below N % of its
/// starting health is destroyed), <c>debt% N</c> (plus that percent of the damage the stack has dealt in the battle), <c>ring</c> (an area without its
/// centre cell) and <c>column N</c> (an area N columns wide, the field's height).
/// ADR-149: <c>zone area N|line N [wide W] [enter N] [start N] [leave N] [beside N] [heal N] [blocks] [blocks-shots] [own-undead] [dims-shots]
/// [defense N] [filter] [spare-own] rounds N</c> (a line runs down the field, centred on the aimed cell; <see cref="ZoneEffect"/>; the filter and
/// <c>spare-own</c> choose whom its damage hits, <c>defense</c> is for own stacks in it).
/// ADR-150: a buff or curse may last <c>turns N</c> instead of rounds (the stack's next N turns, played or passed); <c>health N</c> (with
/// <c>controlled</c>) is the most total health a stack may have to be taken.
/// ADR-151: <c>move N|field [shadow] [again|again-attack]</c> moves an own stack to a free cell within N cells (or anywhere), with <c>shadow</c> only
/// where it is not next to an enemy stack; for damage aimed at a stack, <c>drain% N</c> heals an own stack the caster picks by that percent of the
/// damage dealt, <c>drain-revive</c> also reviving its dead.
/// ADR-152: <c>copy rounds N [survives N]</c> (a copy of an own stack), <c>raise N [once|each-round]</c> (no aim: skeletons worth N % of the slain
/// enemies' health after the battle, and destroyed stacks raised during it), <c>sacrifice N per M</c> (an own stack loses N % of its creatures, the
/// hero gains 1 mana per M health lost); for buffs <c>martyr% N</c> (another own stack, the second aim, takes that share of the damage meant for the
/// stack) with <c>martyr-defense N</c> for the martyr, the status <c>twice</c> and the filter <c>machine</c>; for <c>controlled</c> with
/// <c>health N</c>, <c>split</c> (a bigger stack is split, not refused).
/// ADR-177: for healing <c>cure</c> (the enemy's spells come off the stack) and <c>revive% N</c> (health beyond the top creature's wounds revives its
/// dead, for good, up to N % of those it lost); for a dispel <c>heal N</c> (each stack it reaches also regains N), <c>sap-power N</c> (the enemy hero
/// loses N Spell Power for the battle) and <c>refund% N</c> (that percent of the mana back when only the enemy's spells came off); for a curse
/// <c>hit N</c> (damage as it takes hold); for damage <c>rubble</c> (the free cells of a square area become rubble), <c>spare-own-demons</c> and
/// <c>own% N</c> (own stacks take that percent); for a summon <c>stays</c> (its creatures join the army after a won battle) and <c>kinds N</c> (N of
/// its kinds at once, each worth the amount).
/// </summary>
public sealed record SpellEffect(
    SpellEffectKind Kind, int Amount, SpellReach Reach, int AreaSize, SpellFilter Filter, SpellModifiers Modifiers, int? Rounds, int Walls = 0,
    IReadOnlyList<string>? Creatures = null, bool Rooted = false, int Targets = 1, bool SpareOwn = false)
{
    /// <summary>Percent of a damage spell's damage the stacks next to each target take (Smite II).</summary>
    public int SplashPercent { get; init; }

    /// <summary>How many times the damage jumps on to the nearest stack not yet hit, at half the damage before (Chain Lightning).</summary>
    public int Chain { get; init; }

    /// <summary>Creature spell resistance and the enemy hero's Spell resistance do not count (Smite III).</summary>
    public bool Pierce { get; init; }

    /// <summary>The chance in percent that a stack the spell hits loses its next action (Holy Word III, Frost Bolt III).</summary>
    public int StunPercent { get; init; }

    /// <summary>A stack below this percent of its starting health is destroyed instead of taking the damage (Harvest).</summary>
    public int ExecutePercent { get; init; }

    /// <summary>The stack also takes this percent of the damage it has dealt in the battle (Call the Debt).</summary>
    public int DebtPercent { get; init; }

    /// <summary>An area without its centre cell: a stack on the centre is not hit (Frost Ring).</summary>
    public bool Ring { get; init; }

    /// <summary>The area is <see cref="SpellEffect.AreaSize"/> whole columns of the field (Hellfire).</summary>
    public bool Column { get; init; }

    /// <summary>A revive's creatures stay after the battle (else they are lost again when it ends).</summary>
    public bool Lasting { get; init; }

    /// <summary>A revive may bring back an own stack destroyed in this battle.</summary>
    public bool Fallen { get; init; }

    /// <summary>Whose spells a dispel takes off.</summary>
    public DispelScope Dispels { get; init; }

    /// <summary>
    /// The area's height in cells when it is not a square (a zone's <c>line N [wide W]</c>, ADR-149: <see cref="AreaSize"/> is then its width);
    /// 0 for a square.
    /// </summary>
    public int AreaHeight { get; init; }

    /// <summary>What a field zone does (ADR-149); null for every other kind.</summary>
    public ZoneEffect? Zone { get; init; }

    /// <summary>A buff or curse that lasts the stack's next this many turns instead of rounds (ADR-150; Frenzy, Hypnotize); null for rounds.</summary>
    public int? Turns { get; init; }

    /// <summary>The most total health a stack may have for a <c>controlled</c> spell to take it (Hypnotize, ADR-150), at Spell Power 0; 0 for none.</summary>
    public int HealthLimit { get; init; }

    /// <summary>How far a <see cref="SpellEffectKind.Move"/> takes the stack, in cells (straight line, like a flyer's reach); 0 for anywhere on the field.</summary>
    public int MoveRange { get; init; }

    /// <summary>A move only to a cell in shadow: the stack may not land next to an enemy stack (Shadow Step, ADR-151).</summary>
    public bool Shadow { get; init; }

    /// <summary>What a moved stack that had already taken its turn this round may still do (ADR-151).</summary>
    public SpellTurnAgain Again { get; init; }

    /// <summary>Percent of the damage a damage spell deals that heals an own stack the caster picks (Drain Life, ADR-151); 0 for none.</summary>
    public int DrainPercent { get; init; }

    /// <summary>The drained health also revives the picked stack's dead, for good (Drain Life III).</summary>
    public bool DrainRevives { get; init; }

    /// <summary>Hits a copy takes before it vanishes (Mirror Image, ADR-152): 1, or 2 when it survives one hit.</summary>
    public int CopyHits { get; init; } = 1;

    /// <summary>Defense the martyr gets while it guards the stack (Martyrdom III, ADR-152).</summary>
    public int MartyrDefense { get; init; }

    /// <summary>
    /// A <c>controlled</c> spell's stack above the health limit is split (Puppet Master III, ADR-152): the creatures within the limit are taken, the
    /// rest stays on its side.
    /// </summary>
    public bool Split { get; init; }

    /// <summary>How often Raise Dead raises a destroyed stack during the battle (ADR-152).</summary>
    public SpellRises Rises { get; init; }

    /// <summary>A sacrifice gives the hero 1 mana for this much health lost (Blood Price, ADR-152).</summary>
    public int HealthPerMana { get; init; }

    /// <summary>Healing also takes the enemy's spells off the stack (Heal II, ADR-177).</summary>
    public bool Cures { get; init; }

    /// <summary>
    /// Healing beyond the top creature's wounds revives the stack's dead, for good, up to this percent of the creatures it lost (Heal III, ADR-177);
    /// 0 for none.
    /// </summary>
    public int RevivePercent { get; init; }

    /// <summary>The free cells of the area become rubble, a small obstacle, once the damage is dealt (Meteor Shower III, ADR-177).</summary>
    public bool Rubble { get; init; }

    /// <summary>A field or area spell leaves the caster's own demons alone (Ashfall III, ADR-177).</summary>
    public bool SparesOwnDemons { get; init; }

    /// <summary>The percent of the damage the caster's own stacks take (Hellfire III, ADR-177); 100 for all of it.</summary>
    public int OwnPercent { get; init; } = 100;

    /// <summary>The summoned creatures join the hero's army after a won battle, as far as its slots allow (Summon Demons III, ADR-177).</summary>
    public bool Stays { get; init; }

    /// <summary>How many of the summon's kinds one cast calls, each worth the amount (Bind Elemental III, ADR-177).</summary>
    public int Kinds { get; init; } = 1;

    /// <summary>Spell Power the enemy hero loses for the rest of the battle, once a cast (Unravel III, ADR-177).</summary>
    public int SapPower { get; init; }

    /// <summary>Percent of the hero's mana cost given back when every spell a dispel took off was the enemy's (Dispel III, ADR-177).</summary>
    public int RefundPercent { get; init; }

    /// <summary>
    /// Whether the spell is aimed with a second click after its stack: the cell a move goes to, the own stack a drain heals (ADR-151), the own stack
    /// that takes the blows (Martyrdom, ADR-152).
    /// </summary>
    public bool TwoClicks => Kind == SpellEffectKind.Move || DrainPercent > 0 || Modifiers.MartyrPercent > 0;

    /// <summary>The second aim is an own stack (a drain's drinker, a martyr), not a cell.</summary>
    public bool SecondIsStack => DrainPercent > 0 || Modifiers.MartyrPercent > 0;

    /// <summary>
    /// Damage, curses, kills, the enemy's dispel and field zones work on the enemy (damage to the whole field or an area hits own stacks too; a zone
    /// may also hurt own stacks); healing, revives, buffs and the own dispel on own stacks; <see cref="EitherSide"/> spells on any stack. Creature
    /// spell resistance and the army's immunities count against hostile spells.
    /// </summary>
    public bool IsHostile => Kind is SpellEffectKind.Damage or SpellEffectKind.Curse or SpellEffectKind.Slay or SpellEffectKind.Zone
                             || (Kind == SpellEffectKind.Dispel && Dispels == DispelScope.Enemy);

    /// <summary>A spell aimed at a stack of either side (Dispel).</summary>
    public bool EitherSide => Kind == SpellEffectKind.Dispel && Dispels == DispelScope.Any;

    /// <summary>The creatures a summon calls, by id (empty for other spells).</summary>
    public IReadOnlyList<string> Summons => Creatures ?? [];

    /// <summary>
    /// Reads a <c>battle</c> column: the kind's word picks its head (<see cref="SpellEffectHeads"/>), each further word its parser
    /// (<see cref="SpellEffectWords"/>), and the reader checks the words go together.
    /// </summary>
    public static SpellEffect Parse(string text)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 || !Enum.TryParse<SpellEffectKind>(tokens[0], ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            throw new FormatException($"'{text}' does not start with damage, heal, buff, curse, summon, revive, dispel, slay, zone, move, copy, raise or sacrifice");
        }

        var reader = new SpellEffectReader(text, tokens, kind);
        if (SpellEffectHeads.For(kind).Read(reader) is { } whole)
        {
            return whole;
        }

        while (reader.More)
        {
            SpellEffectWords.Read(reader, reader.Next());
        }

        return reader.Build();
    }
}

/// <summary>
/// A spell (ADR-047, ADR-099): its school (the town whose school it is), level 1–4, base mana, element, immunity tags, whether it is cast on the
/// adventure map, and its rank I battle effect where it has been built (null: not castable in battle yet). <paramref name="Rank2"/> and
/// <paramref name="Rank3"/> are the whole battle effect at ranks II and III (ADR-122); null means the rank below's (its change is not built yet).
/// </summary>
public sealed record SpellDefinition(
    string Id, string Name, string School, int Level, int Mana, SpellElement Element, IReadOnlyList<string> Tags, bool IsAdventure, SpellEffect? Effect,
    string Notes = "", SpellEffect? Rank2 = null, SpellEffect? Rank3 = null)
{
    public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.Ordinal);

    /// <summary>
    /// The elements the spell counts as (ADR-106, ADR-123): its own; all four for Magic Arrow; for Bind Elemental the kind it binds (the element
    /// in the creature's id, <c>conflux.fire-elemental</c>), none while that is not known.
    /// </summary>
    public IReadOnlyList<SpellElement> ElementsFor(string? creature = null) => Element switch
    {
        SpellElement.All => [SpellElement.Air, SpellElement.Earth, SpellElement.Fire, SpellElement.Water],
        SpellElement.Bound => creature is null ? [] : [.. new[] { SpellElement.Air, SpellElement.Earth, SpellElement.Fire, SpellElement.Water }
            .Where(e => creature.Split('.')[^1].StartsWith(e.ToString().ToLowerInvariant() + "-", StringComparison.Ordinal))],
        var element => [element],
    };

    /// <summary>The battle effect at a rank (1–3): the rank's own, else that of the nearest rank below with one.</summary>
    public SpellEffect? EffectAt(int rank) => rank switch
    {
        >= 3 => Rank3 ?? Rank2 ?? Effect,
        2 => Rank2 ?? Effect,
        _ => Effect,
    };
}

/// <summary>
/// The spell table as CSV (<c>data/spells.csv</c>): <c>id, name, school, level, mana, element, tags, adventure, battle, battle2, battle3, notes</c>;
/// <c>battle2</c> and <c>battle3</c> hold the whole effect at ranks II and III (ADR-122), empty where the rank changes nothing built yet.
/// </summary>
public static class SpellTable
{
    public const string Id = "id";
    public const string Name = "name";
    public const string School = "school";
    public const string Level = "level";
    public const string Mana = "mana";
    public const string Element = "element";
    public const string Tags = "tags";
    public const string Adventure = "adventure";
    public const string Battle = "battle";
    public const string Battle2 = "battle2";
    public const string Battle3 = "battle3";
    public const string Notes = "notes";

    /// <summary>The columns of the shipped spell table, in order.</summary>
    public static IReadOnlyList<string> Columns { get; } = [Id, Name, School, Level, Mana, Element, Tags, Adventure, Battle, Battle2, Battle3, Notes];

    public static IReadOnlyList<SpellDefinition> Parse(string text) =>
        CsvTable.Parse(text, "Spell", [Id, Name, School, Level, Mana, Element])
            .Read(row => row.Text(Id) is { Length: > 0 } id ? Read(row, id) : null)
            .OfType<SpellDefinition>()
            .ToList();

    private static SpellDefinition Read(CsvTable.Row row, string id)
    {
        var level = row.Int(Level);
        if (level is < 1 or > Creatures.SpellLevels.Max)
        {
            throw row.Error(Level, $"{level} is not a spell level from 1 to {Creatures.SpellLevels.Max}");
        }

        var effect = Effect(row, Battle);
        var rank2 = Effect(row, Battle2);
        var rank3 = Effect(row, Battle3);
        foreach (var (rank, column) in new[] { (rank2, Battle2), (rank3, Battle3) })
        {
            if (rank is not null && effect is null)
            {
                throw row.Error(column, "a rank II or III effect needs the rank I effect");
            }

            if (rank is not null && rank.Kind != effect!.Kind)
            {
                throw row.Error(column, $"a rank changes the effect, not its kind ({effect.Kind})");
            }
        }

        return new SpellDefinition(
            id, row.Text(Name), row.Text(School), level, row.Int(Mana), row.Enum<SpellElement>(Element), row.List(Tags),
            row.Optional(Adventure) is "yes", effect, row.Optional(Notes) ?? "", rank2, rank3);
    }

    private static SpellEffect? Effect(CsvTable.Row row, string column)
    {
        try
        {
            return row.Optional(column) is { } battle ? SpellEffect.Parse(battle) : null;
        }
        catch (FormatException e)
        {
            throw row.Error(column, e.Message);
        }
    }
}

/// <summary>The spells of a game by id. Start from <see cref="Default"/> (the embedded table).</summary>
public sealed class SpellCatalog
{
    private readonly Dictionary<string, SpellDefinition> _spells = new(StringComparer.Ordinal);

    public SpellCatalog(IEnumerable<SpellDefinition> spells)
    {
        foreach (var spell in spells)
        {
            if (!_spells.TryAdd(spell.Id, spell))
            {
                throw new FormatException($"Duplicate spell '{spell.Id}'.");
            }

            if (spell.IsAdventure && (spell.Effect ?? spell.Rank2 ?? spell.Rank3) is not null)
            {
                throw new FormatException($"Adventure spell '{spell.Id}' has a battle effect.");
            }

            // The tags follow the effect (ADR-099, ADR-123): a summon is tagged `summon` and only a summon is.
            if (spell.Effect is { } effect && (effect.Kind == SpellEffectKind.Summon) != spell.HasTag(SummonTag))
            {
                throw new FormatException($"Spell '{spell.Id}': only a summon is tagged '{SummonTag}', and every summon is.");
            }

            if ((spell.Element == SpellElement.Bound) != (spell.Effect?.Kind == SpellEffectKind.Summon && spell.Effect.Summons.Count > 1))
            {
                throw new FormatException($"Spell '{spell.Id}': the element 'bound' is for a summon of several kinds (Bind Elemental) only.");
            }
        }
    }

    /// <summary>The tag of summons (ADR-099): their creatures vanish after the battle and give no experience.</summary>
    public const string SummonTag = "summon";

    public static SpellCatalog Default { get; } = new(SpellTable.Parse(BuiltInSpells.EmbeddedTable()));

    public IEnumerable<SpellDefinition> All => _spells.Values;

    public bool Contains(string id) => _spells.ContainsKey(id);

    public bool TryGet(string id, out SpellDefinition spell) => _spells.TryGetValue(id, out spell!);

    public SpellDefinition Get(string id) =>
        _spells.TryGetValue(id, out var spell) ? spell : throw new KeyNotFoundException($"Unknown spell '{id}'.");

    /// <summary>The spells of a school (town id) at one level, by id.</summary>
    public IReadOnlyList<SpellDefinition> Of(string school, int level) =>
        _spells.Values.Where(s => s.School == school && s.Level == level).OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
}

/// <summary>The shipped spell table, embedded at build time (<c>data/spells.csv</c>).</summary>
public static class BuiltInSpells
{
    public const string Resource = "EternalWars.Spells.csv";

    public static string EmbeddedTable()
    {
        using var stream = typeof(BuiltInSpells).Assembly.GetManifestResourceStream(Resource)
                           ?? throw new InvalidOperationException($"The spell table '{Resource}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
