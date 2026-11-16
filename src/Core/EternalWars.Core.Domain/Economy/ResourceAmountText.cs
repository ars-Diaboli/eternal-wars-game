using System.Globalization;

namespace EternalWars.Core.Domain.Economy;

/// <summary>
/// Resource amounts as the labels show them (ADR-184): gold in full up to 6 digits, the other resources up to 4; larger amounts with one
/// decimal and <c>k</c>, <c>m</c> or <c>b</c>, the smallest that keeps the number under 1000 (<c>31.7k</c>, <c>321.7m</c>). Rounded
/// towards zero, so a label never shows more than there is. The information windows show <see cref="Full"/>.
/// </summary>
public static class ResourceAmountText
{
    /// <summary>The most digits a label shows in full: 6 for gold, 4 for the others.</summary>
    public static int MaxDigits(ResourceType type) => type == ResourceType.Gold ? 6 : 4;

    /// <summary>The amount for a label: in full while it fits <see cref="MaxDigits"/>, otherwise shortened.</summary>
    public static string Short(ResourceType type, int amount)
    {
        long magnitude = Math.Abs((long)amount);
        var limit = type == ResourceType.Gold ? 999_999 : 9_999;
        if (magnitude <= limit)
        {
            return Full(amount);
        }

        var (unit, suffix) = magnitude switch
        {
            < 1_000_000 => (1_000L, "k"),
            < 1_000_000_000 => (1_000_000L, "m"),
            _ => (1_000_000_000L, "b"),
        };
        var tenths = magnitude / (unit / 10);
        var sign = amount < 0 ? "-" : "";
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{tenths / 10}.{tenths % 10}{suffix}");
    }

    /// <summary>Whether <see cref="Short"/> shortens the amount (a label then names the full amount in its tooltip).</summary>
    public static bool IsShortened(ResourceType type, int amount) => Short(type, amount) != Full(amount);

    /// <summary>The amount in full, as the information windows show it.</summary>
    public static string Full(int amount) => amount.ToString(CultureInfo.InvariantCulture);
}
