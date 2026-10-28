using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Generation;

/// <summary>
/// A start slot of a template as the game's new-game screen offers it (ADR-086): the player's name (by colour, <see cref="EditorMapGenerator.PlayerNames"/>),
/// whether a human may take it (a player start; an AI-only start is for computer players), whether a human takes it by default (the first slot a
/// human may take; the others are computer players, as in HoMM3) and its team when nobody changes it.
/// </summary>
public sealed record StartSlot(int Slot, string Name, bool HumanAllowed, bool HumanByDefault, int Team);

/// <summary>What the new-game screen needs from a template before it generates: its start slots and the heroes a player of a town can pick.</summary>
public static class StartSlots
{
    /// <summary>Every start slot of the template, in slot order. Teams are the start zones' (a zone without one: a team of its own, as <see cref="ScenarioAssembler"/> gives).</summary>
    public static IReadOnlyList<StartSlot> Of(MapTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var slots = Enumerable.Range(0, template.MaxPlayers).ToList();
        var teams = ScenarioAssembler.Teams(template, slots.Select(s => new PlayerSetup(s, "", false, PlayerSetup.RandomTown)).ToList());
        var firstHuman = slots.Cast<int?>().FirstOrDefault(s => HumanAllowed(template, s!.Value));
        return slots.Select(s => new StartSlot(
            s,
            s < EditorMapGenerator.PlayerNames.Count ? EditorMapGenerator.PlayerNames[s] : $"Player {s + 1}",
            HumanAllowed(template, s),
            s == firstHuman,
            teams[s])).ToList();
    }

    /// <summary>The heroes a player of <paramref name="town"/> can start with, by name.</summary>
    public static IReadOnlyList<HeroDefinition> Heroes(HeroCatalog roster, string town)
    {
        ArgumentNullException.ThrowIfNull(roster);
        return roster.All.Where(h => h.Town == town).OrderBy(h => h.Name, StringComparer.Ordinal).ToList();
    }

    private static bool HumanAllowed(MapTemplate template, int slot) =>
        template.Zones.Any(z => z.Owner == slot && z.Type == ZoneType.PlayerStart);
}
