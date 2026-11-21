namespace EternalWars.Core.Generation;

/// <summary>
/// The template editor's mirror tools (ADR-272), engine-free: the zones of a mirror cycle (<see cref="TemplateZone.Mirror"/>, ADR-117), a zone's
/// image a number of symmetry steps on, where an image stands in the graph, and how an edit of one zone or connection carries to its images, so
/// a mirrored template stays alike while it is edited.
/// </summary>
public static class TemplateMirrors
{
    /// <summary>
    /// The closed cycle of mirrors through <paramref name="id"/>, starting with it (<c>start-1, start-2</c>); null when the template has no
    /// symmetry, the zone names no mirror, or following the mirrors does not come back to it.
    /// </summary>
    public static IReadOnlyList<string>? Cycle(MapTemplate template, string id)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.Symmetry == TemplateSymmetry.None || template.Zone(id) is null)
        {
            return null;
        }

        var cycle = new List<string> { id };
        for (var next = template.Zone(id)!.Mirror; next != id; next = template.Zone(next)!.Mirror)
        {
            if (next is null || template.Zone(next) is null || cycle.Contains(next))
            {
                return null;
            }

            cycle.Add(next);
        }

        return cycle.Count >= 2 ? cycle : null;
    }

    /// <summary>Whether the zone names no mirror but itself (the validator reads both as "its own image").</summary>
    public static bool IsOwnImage(TemplateZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return zone.Mirror is null || zone.Mirror == zone.Id;
    }

    /// <summary>Whether any other zone names <paramref name="id"/> as its mirror.</summary>
    public static bool IsNamed(MapTemplate template, string id)
    {
        ArgumentNullException.ThrowIfNull(template);
        return template.Zones.Any(z => z.Mirror == id && z.Id != id);
    }

    /// <summary>
    /// The zone <paramref name="id"/> turns into after <paramref name="steps"/> steps of the symmetry: the zone that many places on in its cycle,
    /// the zone itself when it is its own image (no mirror, none naming it); null when its mirrors are broken (an open chain).
    /// </summary>
    public static string? Image(MapTemplate template, string id, int steps)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.Zone(id) is not { } zone)
        {
            return null;
        }

        if (Cycle(template, id) is { } cycle)
        {
            return cycle[steps % cycle.Count];
        }

        return IsOwnImage(zone) && !IsNamed(template, id) ? id : null;
    }

    /// <summary>
    /// The graph place of a point's image after <paramref name="steps"/> steps of <paramref name="symmetry"/> around <paramref name="centre"/>:
    /// across the axis (a random axis draws as the vertical one), a half turn, or quarter turns clockwise; rounded to two decimals.
    /// </summary>
    public static (double X, double Y) Place((double X, double Y) point, TemplateSymmetry symmetry, int steps, (double X, double Y) centre)
    {
        var (dx, dy) = (point.X - centre.X, point.Y - centre.Y);
        for (var i = 0; i < steps; i++)
        {
            (dx, dy) = symmetry switch
            {
                TemplateSymmetry.Vertical or TemplateSymmetry.RandomAxis => (-dx, dy),
                TemplateSymmetry.Horizontal => (dx, -dy),
                TemplateSymmetry.Diagonal => (dy, dx),
                TemplateSymmetry.HalfTurn => (-dx, -dy),
                TemplateSymmetry.QuarterTurn => (-dy, dx),
                _ => (dx, dy),
            };
        }

        return (Math.Round(centre.X + dx, 2), Math.Round(centre.Y + dy, 2));
    }

    /// <summary>
    /// <paramref name="image"/> with the fields that differ between <paramref name="before"/> and <paramref name="after"/> (an edit of a zone
    /// of its cycle) taken from <paramref name="after"/>: everything the zones of a cycle share (type, size, level, towns, mines, treasure,
    /// monsters, terrains, …), never the id, the player slot, the team, the mirror or the graph place. A start zone made of another type takes
    /// <paramref name="slot"/>; a start zone made of something else drops its slot and team. Without a change of type, slot and team stay.
    /// </summary>
    public static TemplateZone Carry(TemplateZone before, TemplateZone after, TemplateZone image, Func<int> slot)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(slot);
        T Pick<T>(T was, T now, T own, Func<T, T, bool>? same = null) => (same ?? EqualityComparer<T>.Default.Equals)(was, now) ? own : now;
        var carried = image with
        {
            Type = Pick(before.Type, after.Type, image.Type),
            Size = Pick(before.Size, after.Size, image.Size),
            Level = Pick(before.Level, after.Level, image.Level),
            PlayerTowns = Pick(before.PlayerTowns, after.PlayerTowns, image.PlayerTowns),
            NeutralTowns = Pick(before.NeutralTowns, after.NeutralTowns, image.NeutralTowns),
            TownTypes = Pick(before.TownTypes, after.TownTypes, image.TownTypes, SameList),
            StartBuildings = Pick(before.StartBuildings, after.StartBuildings, image.StartBuildings, SameMap),
            StartDwellings = Pick(before.StartDwellings, after.StartDwellings, image.StartDwellings, SameList),
            Mines = Pick(before.Mines, after.Mines, image.Mines, SameMap),
            ExtraPiles = Pick(before.ExtraPiles, after.ExtraPiles, image.ExtraPiles),
            Treasure = Pick(before.Treasure, after.Treasure, image.Treasure, SameList),
            Obelisks = Pick(before.Obelisks, after.Obelisks, image.Obelisks),
            Monsters = Pick(before.Monsters, after.Monsters, image.Monsters),
            MineGuards = Pick(before.MineGuards, after.MineGuards, image.MineGuards),
            Obstacles = Pick(before.Obstacles, after.Obstacles, image.Obstacles),
            Terrains = Pick(before.Terrains, after.Terrains, image.Terrains, SameList),
        };
        if (before.Type == after.Type)
        {
            return carried;
        }

        return carried.Type is ZoneType.PlayerStart or ZoneType.AiStart
            ? carried with { Owner = carried.Owner ?? slot() }
            : carried with { Owner = null, Team = null };
    }

    /// <summary>
    /// <paramref name="image"/> (a connection's image) with the fields that differ between <paramref name="before"/> and <paramref name="after"/>
    /// taken from <paramref name="after"/>: kind, guard value, passages, guard, Keymaster colour, chance; never the zones it joins.
    /// </summary>
    public static TemplateConnection Carry(TemplateConnection before, TemplateConnection after, TemplateConnection image)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(image);
        T Pick<T>(T was, T now, T own) => EqualityComparer<T>.Default.Equals(was, now) ? own : now;
        return image with
        {
            Kind = Pick(before.Kind, after.Kind, image.Kind),
            GuardValue = Pick(before.GuardValue, after.GuardValue, image.GuardValue),
            Passages = Pick(before.Passages, after.Passages, image.Passages),
            Guard = Pick(before.Guard, after.Guard, image.Guard),
            Colour = Pick(before.Colour, after.Colour, image.Colour),
            Chance = Pick(before.Chance, after.Chance, image.Chance),
        };
    }

    /// <summary>The index of the connection joining <paramref name="a"/> and <paramref name="b"/> (either way); -1 when none does.</summary>
    public static int Find(MapTemplate template, string a, string b)
    {
        ArgumentNullException.ThrowIfNull(template);
        return template.Connections.ToList().FindIndex(c => (c.From == a && c.To == b) || (c.From == b && c.To == a));
    }

    private static bool SameList<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b) => (a ?? []).SequenceEqual(b ?? []);

    private static bool SameMap<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>? a, IReadOnlyDictionary<TKey, TValue>? b)
        where TKey : notnull =>
        (a?.Count ?? 0) == (b?.Count ?? 0)
        && (a ?? new Dictionary<TKey, TValue>()).All(p => b is not null && b.TryGetValue(p.Key, out var v) && EqualityComparer<TValue>.Default.Equals(v, p.Value));
}
