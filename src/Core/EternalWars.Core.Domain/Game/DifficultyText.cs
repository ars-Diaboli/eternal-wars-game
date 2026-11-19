using System.Globalization;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Game;

/// <summary>The difficulty levels in words (ADR-225): what the status bar says over a level's button and beside its icon.</summary>
public static class DifficultyText
{
    /// <summary>The chess piece each level shows (ADR-225; icon-prompts.md sheet 29), from the humblest up.</summary>
    public static string Piece(Difficulty level) => level switch
    {
        Difficulty.Easy => "pawn",
        Difficulty.Normal => "knight",
        Difficulty.Hard => "bishop",
        Difficulty.Expert => "rook",
        _ => "queen",
    };

    /// <summary>"Hard: every player starts with 5000 gold, 15 lumber, 15 ore, 3 crystal, 3 mercury, 3 sulfur; the score counts 130 %."</summary>
    public static string Describe(Difficulty level) => string.Create(
        CultureInfo.InvariantCulture,
        $"{level}: every player starts with {Amounts(StartingResources.For(level))}; the score counts {ScoreRules.DifficultyPercent(level)} %.");

    private static string Amounts(ResourceSet resources) => string.Join(
        ", ",
        Enum.GetValues<ResourceType>().Where(r => resources[r] != 0)
            .Select(r => string.Create(CultureInfo.InvariantCulture, $"{resources[r]} {r.ToString().ToLowerInvariant()}")));
}
