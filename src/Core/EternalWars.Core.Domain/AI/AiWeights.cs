using System.Globalization;
using System.Reflection;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The numbers the adventure AI weighs its choices with: target values in gold, the losses and dangers it accepts, the cushions it keeps and
/// how much it searches. <see cref="Default"/> is how the AI plays; the simulator overrides single weights by name to tune them
/// (<see cref="With(string, string)"/>). Every tuning number of the adventure AI is here, none inline in its parts (ADR-232); no weight is
/// negative.
/// </summary>
public sealed record AiWeights
{
    /// <summary>The weights the AI plays with.</summary>
    public static AiWeights Default { get; } = new();

    // Search -------------------------------------------------------------------------------------------

    /// <summary>Candidate targets whose route is priced per choice, best first (unsafe fights do not count).</summary>
    public int CandidatesChecked { get; init; } = 10;

    /// <summary>
    /// Most fights one choice plays out, unsafe ones included: a bound on the work for a hero ringed by monsters too strong for it, high
    /// enough that an ordinary choice never reaches it.
    /// </summary>
    public int FightsChecked { get; init; } = 40;

    /// <summary>Frontier tiles next to unexplored ground whose route is priced when looking for where to explore.</summary>
    public int FrontierChecked { get; init; } = 8;

    /// <summary>How much a target's value falls per tile of distance when the candidates are ranked: value / (1 + penalty × distance).</summary>
    public double DistancePenalty { get; init; } = 0.08;

    /// <summary>
    /// A guarded target is a candidate only when the hero's army power is at least this % of its guard's: a loose filter on the power sum
    /// before the fight is played out.
    /// </summary>
    public int PlausiblePercent { get; init; } = 70;

    // Fights and danger --------------------------------------------------------------------------------

    /// <summary>The days of output a mine or a deposit is worth when weighed as a target.</summary>
    public int MineDays { get; init; } = 8;

    /// <summary>The share of its army power a hero accepts to lose clearing a monster stack.</summary>
    public double MonsterLoss { get; init; } = 0.4;

    /// <summary>
    /// The share of its army power a hero accepts to lose in a fight when the deciding battle is near: on an arena's preparation day (ADR-160),
    /// in a tournament's last <see cref="CarefulDays"/> days (ADR-366).
    /// </summary>
    public double ArenaLoss { get; init; } = 0.15;

    /// <summary>The last days of a tournament in which its one hero fights only for small losses (<see cref="ArenaLoss"/>, ADR-366).</summary>
    public int CarefulDays { get; init; } = 3;

    /// <summary>
    /// The last days of a tournament in which its towns spend everything on the battle's army, once no week begins before the battle (ADR-366).
    /// </summary>
    public int ArmingDays { get; init; } = 5;

    /// <summary>
    /// The share of a full day's movement per day left that a tournament's hero may end a day away from its home town, so it is back there on
    /// the last day with room for detours (ADR-366).
    /// </summary>
    public double LeashShare { get; init; } = 0.75;

    /// <summary>A place is safe from an enemy hero when the army there has at least this share of the enemy's power.</summary>
    public double SafeShare { get; init; } = 0.8;

    /// <summary>A secondary hero hands its troops to the main hero when it carries at least this much army power (or a quarter of the main army).</summary>
    public long CarryThreshold { get; init; } = 1000;

    // Target values ------------------------------------------------------------------------------------

    /// <summary>A town not of the player's team, in gold.</summary>
    public double TownValue { get; init; } = 4000;

    /// <summary>What an enemy's town is worth beyond a neutral one.</summary>
    public double EnemyTownValue { get; init; } = 2000;

    /// <summary>What each dwelling adds to a town, to take or to defend.</summary>
    public double TownDwellingValue { get; init; } = 500;

    /// <summary>Defending an own town that an enemy can reach and the garrison alone cannot hold.</summary>
    public double DefendTownValue { get; init; } = 6000;

    /// <summary>Beating an enemy hero (also one on a town's entrance), before the share of its army.</summary>
    public double EnemyHeroValue { get; init; } = 2000;

    /// <summary>The share of a beaten enemy hero's army power that counts as the target's value.</summary>
    public double HeroArmyShare { get; init; } = 0.8;

    /// <summary>The share of a beaten guard's army power (monsters, garrisons, dwelling guards, event battles) that counts as its value.</summary>
    public double GuardArmyShare { get; init; } = 0.35;

    /// <summary>What reaching unexplored ground is worth, against the targets' values (a small treasure).</summary>
    public double ExploreValue { get; init; } = 900;

    /// <summary>The share of its army power a main hero puts on exploring while it knows no enemy town: a strong army goes looking for one.</summary>
    public double HuntShare { get; init; } = 0.1;

    /// <summary>How far around a guard or garrison unexplored ground makes it worth the exploring it opens.</summary>
    public int WayRadius { get; init; } = 3;

    /// <summary>What getting a Quest Guard out of the way is worth on its own (more when unexplored ground is behind it).</summary>
    public double GuardOpened { get; init; } = 500;

    /// <summary>An artifact to pick up (also one in a Pandora's Box or given by an event), in gold, before <see cref="ArtifactScoreValue"/>.</summary>
    public double ArtifactValue { get; init; } = 800;

    /// <summary>What each point of an artifact's score for the hero adds to <see cref="ArtifactValue"/>.</summary>
    public double ArtifactScoreValue { get; init; } = 80;

    /// <summary>Each artifact a creature bank gives, in gold (beside its resources).</summary>
    public double BankArtifactValue { get; init; } = 1500;

    /// <summary>A treasure chest, in gold.</summary>
    public double ChestValue { get; init; } = 1500;

    /// <summary>The share of their value that creatures in a Pandora's Box count for, where they fit in the hero's army (ADR-100).</summary>
    public double BoxCreatureShare { get; init; } = 0.85;

    /// <summary>
    /// The troops waiting in an own town (its garrison and the creatures the gold buys there) call a hero home when they reach this % of its
    /// army power (ADR-114).
    /// </summary>
    public int TroopsTripPercent { get; init; } = 60;

    /// <summary>The share of the troops waiting in a town that the trip is worth, at most the hero's army power (and at least <see cref="TroopsTripFloor"/>).</summary>
    public double TroopsTripShare { get; init; } = 0.3;

    /// <summary>The army power a hero's own is taken to be at least, for <see cref="TroopsTripShare"/>: a hero without an army still fetches troops.</summary>
    public long TroopsTripFloor { get; init; } = 1000;

    /// <summary>A skill level bought at a school, in gold (beside a level-up's worth).</summary>
    public double LessonValue { get; init; } = 1200;

    /// <summary>An obelisk the player has not visited yet, in gold (a step toward the Grail, ADR-140).</summary>
    public double ObeliskValue { get; init; } = 1000;

    /// <summary>Digging up the Grail, and bringing it into a town without a Grail building, in gold (a building worth +5000 gold a day).</summary>
    public double GrailValue { get; init; } = 12000;

    // Spending -----------------------------------------------------------------------------------------

    /// <summary>Gold kept after buying a war machine or a spell book: creatures and buildings come first.</summary>
    public int WarMachineCushion { get; init; } = 1500;

    /// <summary>Gold kept after paying gold to train (a Gazebo, a Tree of Knowledge, a training site's gold price).</summary>
    public int TrainingCushion { get; init; } = 3000;

    /// <summary>Spending at a map object (a school, a Cartographer) keeps this part of the gold back: 3 keeps a third (at least 1).</summary>
    public int KeptGoldPart { get; init; } = 3;

    /// <summary>A Cartographer's map is bought when the gold is at least this many times its price.</summary>
    public int CartographerGoldTimes { get; init; } = 3;

    /// <summary>A building is saved for when its gold comes within this many days of income (else its resources may be bought).</summary>
    public int BuildGoldDays { get; init; } = 3;

    /// <summary>A building is saved for when each resource other than gold comes within this many days of income.</summary>
    public int BuildResourceDays { get; init; } = 7;

    /// <summary>The most heroes a player hires: one more than its towns, up to this.</summary>
    public int MostHeroes { get; init; } = 4;

    /// <summary>The most of the gold (on hand plus <see cref="SavingDays"/> days of income) a marketplace trade may take (ADR-189).</summary>
    public int TradeTreasuryPercent { get; init; } = 50;

    /// <summary>A missing resource that this many days of income bring is waited for, not bought.</summary>
    public int TradeWaitDays { get; init; } = 2;

    /// <summary>Days of gold income a building may be saved for when buying its resources at the marketplace.</summary>
    public int SavingDays { get; init; } = 3;

    /// <summary>Lumber and ore the AI keeps when it sells for gold: the next buildings want them.</summary>
    public int KeepBasic { get; init; } = 20;

    /// <summary>Crystal, sulfur and mercury the AI keeps when it sells for gold.</summary>
    public int KeepMagic { get; init; } = 10;

    // Adventure spells (ADR-184) -----------------------------------------------------------------------

    /// <summary>Movement a Dimension Door must save, beyond the 500 the cast costs, to be worth it.</summary>
    public int DoorSaving { get; init; } = 300;

    /// <summary>How often a turn the AI weighs a Dimension Door for one hero (each weighing prices up to 72 landings).</summary>
    public int DoorChecksPerHero { get; init; } = 3;

    /// <summary>View Terrain is cast when at least this share of the ground in its radius is unexplored.</summary>
    public double ViewTerrainShare { get; init; } = 0.3;

    /// <summary>Pathfinder's Call or Ashwalk must save this much movement today beyond what the cast costs.</summary>
    public int NativeWalkSaving { get; init; } = 100;

    /// <summary>The names of the weights, as <see cref="With(string, string)"/> takes them.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. Weights().Select(p => p.Name)];

    /// <summary>
    /// A copy with the named weight set to <paramref name="value"/> (invariant culture). Throws <see cref="FormatException"/> for an unknown
    /// name (case-sensitive), a value that is not a number of the weight's type, or a negative one (ADR-232).
    /// </summary>
    public AiWeights With(string name, string value)
    {
        var property = Weights().FirstOrDefault(p => p.Name == name)
            ?? throw new FormatException($"'{name}' is not an AI weight ({string.Join(", ", Names)}).");
        object parsed = property.PropertyType switch
        {
            var t when t == typeof(int) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) && i >= 0 => (object)i,
            var t when t == typeof(long) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) && l >= 0 => (object)l,
            var t when t == typeof(double) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) && d >= 0 => (object)d,
            _ => throw new FormatException($"'{value}' is not a value for the AI weight {name} (a {property.PropertyType.Name} of 0 or more)."),
        };
        var copy = this with { };
        property.SetValue(copy, parsed);
        return copy;
    }

    /// <summary>A copy with one weight set from <c>Name=value</c> (<see cref="With(string, string)"/>).</summary>
    public AiWeights With(string assignment) =>
        assignment.Split('=', 2) is [var name, var value]
            ? With(name.Trim(), value.Trim())
            : throw new FormatException($"'{assignment}' is not Name=value.");

    private static IEnumerable<PropertyInfo> Weights() =>
        typeof(AiWeights).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite).OrderBy(p => p.MetadataToken);
}
