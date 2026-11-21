using System.Collections.Generic;
using EternalWars.Core.Domain.Battles;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The creatures' painted portraits in <c>res://shared/textures/ui/creatures/</c>, named by the creature id
/// (<c>icon_creature_academy.gremlin.png</c>; creature-icon-prompts.md), facing right: in the battle's initiative bar. A war machine shows its
/// machine icon (<see cref="HeroIcons.Machine(WarMachineKind, int)"/>). Scaled once per size and cached, like <see cref="UiIcons"/>; a creature
/// without a portrait yet gives null, and callers show its name.
/// </summary>
public static class CreatureIcons
{
    /// <summary>The portrait size in the battle's initiative bar.</summary>
    public const int TimelineSize = 56;

    private const string Folder = "res://shared/textures/ui/creatures/";
    private static readonly Dictionary<(string, int), Texture2D?> Cache = [];

    /// <summary>The portrait of the creature <paramref name="creatureId"/> at <paramref name="size"/> pixels, or null if it has none yet.</summary>
    public static Texture2D? Get(string creatureId, int size = TimelineSize)
    {
        if (!Cache.TryGetValue((creatureId, size), out var texture))
        {
            texture = UiIcons.LoadScaled($"{Folder}icon_creature_{creatureId}.png", size);
            Cache[(creatureId, size)] = texture;
        }

        return texture;
    }

    /// <summary>The picture of a battle stack: its machine's icon for a war machine, else the creature's portrait; null when it has none.</summary>
    public static Texture2D? Of(BattleUnit unit, int size = TimelineSize) =>
        unit.Machine is { } machine ? HeroIcons.Machine(machine, size) : Get(unit.Creature.Id.Value, size);
}
