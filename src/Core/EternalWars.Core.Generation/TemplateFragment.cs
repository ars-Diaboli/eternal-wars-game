namespace EternalWars.Core.Generation;

/// <summary>
/// Zones copied out of a template with the connections between them (ADR-271): what the template editor's clipboard holds. The zones keep
/// their ids and graph places; <see cref="TemplateEditorSession.Paste"/> gives them new ids, free player slots and a new place.
/// </summary>
public sealed record TemplateFragment(IReadOnlyList<TemplateZone> Zones, IReadOnlyList<TemplateConnection> Connections)
{
    /// <summary>The zones of <paramref name="ids"/> found in <paramref name="template"/> (in the template's order) and the connections with both ends among them; null when none is found.</summary>
    public static TemplateFragment? Of(MapTemplate template, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(ids);
        var wanted = ids.ToHashSet();
        var zones = template.Zones.Where(z => wanted.Contains(z.Id)).ToList();
        if (zones.Count == 0)
        {
            return null;
        }

        var inside = zones.Select(z => z.Id).ToHashSet();
        return new TemplateFragment(zones, [.. template.Connections.Where(c => inside.Contains(c.From) && inside.Contains(c.To))]);
    }

    /// <summary>How many of the zones are start zones (each takes a free player slot when pasted).</summary>
    public int StartZones => Zones.Count(z => z.Type is ZoneType.PlayerStart or ZoneType.AiStart);

    /// <summary>The middle of the zones' graph places (the box around them; a zone without a place counts as 0, 0).</summary>
    public (double X, double Y) Centre => TemplateGraph.Centre(Zones);
}
