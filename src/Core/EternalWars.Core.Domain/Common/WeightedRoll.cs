namespace EternalWars.Core.Domain.Common;

/// <summary>Picks from a table of weighted outcomes with the game RNG, e.g. "30 % … 30 % … 25 % … 10 % … 5 %" (ADR-068).</summary>
public static class WeightedRoll
{
    /// <summary>
    /// One outcome, each as likely as its weight against the sum of all weights (percentages need not add up to 100). Draws one
    /// number from <paramref name="random"/>; zero weights never win.
    /// </summary>
    public static T Pick<T>(IReadOnlyList<(int Weight, T Value)> table, GameRandom random)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(random);
        if (table.Any(e => e.Weight < 0))
        {
            throw new ArgumentException("Weights cannot be negative.", nameof(table));
        }

        var total = table.Sum(e => e.Weight);
        if (total <= 0)
        {
            throw new ArgumentException("The table needs a positive weight.", nameof(table));
        }

        var roll = random.Next(total);
        foreach (var (weight, value) in table)
        {
            if (roll < weight)
            {
                return value;
            }

            roll -= weight;
        }

        throw new InvalidOperationException("Unreachable: the roll is below the total.");
    }
}
