using System.Globalization;
using System.Reflection;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The numbers the adventure AI weighs its choices with: target values in gold, the losses and dangers it accepts, the cushions it keeps and
/// how much it searches. <see cref="Default"/> is how the AI plays; the simulator overrides single weights by name to tune them
/// (<see cref="With(string, string)"/>).
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

    // Fights and danger --------------------------------------------------------------------------------

    /// <summary>The share of its army power a hero accepts to lose clearing a monster stack.</summary>
    public double MonsterLoss { get; init; } = 0.4;

    /// <summary>The share of its army power a hero accepts to lose in a fight on an arena's preparation day (ADR-160).</summary>
    public double ArenaLoss { get; init; } = 0.15;

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

    /// <summary>A skill level bought at a school, in gold (beside a level-up's worth).</summary>
    public double LessonValue { get; init; } = 1200;

    /// <summary>An obelisk the player has not visited yet, in gold (a step toward the Grail, ADR-140).</summary>
    public double ObeliskValue { get; init; } = 1000;

    /// <summary>Digging up the Grail, and bringing it into a town without a Grail building, in gold (a building worth +5000 gold a day).</summary>
    public double GrailValue { get; init; } = 12000;

    // Spending -----------------------------------------------------------------------------------------

    /// <summary>Gold kept after buying a war machine or a spell book: creatures and buildings come first.</summary>
    public int WarMachineCushion { get; init; } = 1500;

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
    /// name (case-sensitive) or a value that is not a number of the weight's type.
    /// </summary>
    public AiWeights With(string name, string value)
    {
        var property = Weights().FirstOrDefault(p => p.Name == name)
            ?? throw new FormatException($"'{name}' is not an AI weight ({string.Join(", ", Names)}).");
        object parsed = property.PropertyType switch
        {
            var t when t == typeof(int) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => (object)i,
            var t when t == typeof(long) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) => (object)l,
            var t when t == typeof(double) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) => (object)d,
            _ => throw new FormatException($"'{value}' is not a value for the AI weight {name} ({property.PropertyType.Name})."),
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
