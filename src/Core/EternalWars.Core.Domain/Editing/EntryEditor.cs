using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>What <see cref="EntryEditor.Place"/> put on the map: an object, or a hero.</summary>
public readonly record struct PlacedEntry(MapObjectId? Object, HeroId? Hero);

/// <summary>
/// Places what the object browser offers (EDIT-1, ADR-253), with the clicked tile as the top-left one of its footprint: an object as it
/// is, a monster or a dwelling of the entry's creature, a pickup of the entry's artifact, or a hero. One undo step each, under the
/// rules of the editors it goes through.
/// </summary>
public sealed class EntryEditor
{
    private readonly EditorContext _context;
    private readonly ObjectEditor _objects;
    private readonly ObjectContentsEditor _contents;
    private readonly MonsterEditor _monsters;
    private readonly HeroEditor _heroes;

    internal EntryEditor(EditorContext context, ObjectEditor objects, ObjectContentsEditor contents, MonsterEditor monsters, HeroEditor heroes) =>
        (_context, _objects, _contents, _monsters, _heroes) = (context, objects, contents, monsters, heroes);

    private Game.GameState Scenario => _context.Scenario;

    /// <summary>
    /// Whether <paramref name="entry"/> fits with its top-left tile on <paramref name="tile"/>, and the cells it would take, each with
    /// whether it fits there: what a ghost shows before the click. <paramref name="owner"/> is the player of a hero (null: the first).
    /// The result is the one <see cref="Place"/> would give. When it is refused for a reason no single cell shows (the map's limit of
    /// such objects, a hero too many, an entry the map does not know), no cell fits.
    /// </summary>
    public MoveCheck Check(BrowserEntry entry, MapPosition tile, PlayerId? owner = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Definition is not { } definition)
        {
            var check = entry.Placement == BrowserPlacement.Hero ? _heroes.CanPlace(owner ?? Scenario.Players[0].Id, tile) : EditResult.InvalidTarget;
            return new MoveCheck(check, [new MoveCell(tile, check == EditResult.Ok)]);
        }

        var cells = new List<MoveCell>();
        foreach (var (position, cell) in MapObject.Create(default, definition, tile).OccupiedCells())
        {
            cells.Add(new MoveCell(
                position,
                Scenario.Map.Contains(position) && Scenario.ObjectCellAt(position) is null && Scenario.HeroAt(position) is null
                && (cell != FootprintCell.Visit || Scenario.Terrains.IsPassable(Scenario.Map.GetTerrain(position)))));
        }

        var result = Known(entry);
        result = result != EditResult.Ok ? result : _objects.CanPlace(definition, tile) is var fits and not EditResult.Ok ? fits : _objects.CheckLimit(definition);
        return new MoveCheck(result, result != EditResult.Ok && cells.All(c => c.Fits) ? [.. cells.Select(c => c with { Fits = false })] : cells);
    }

    /// <summary>Whether the map knows what <paramref name="entry"/> names: its definition, and the creature or artifact it is made ready with.</summary>
    private EditResult Known(BrowserEntry entry) => entry switch
    {
        { Placement: BrowserPlacement.Hero, Definition: null } => EditResult.Ok,
        { Definition: null } or { Placement: BrowserPlacement.Hero } => EditResult.InvalidTarget,
        { Definition: { } definition } when !_context.Catalog.TryGet(definition.Id, out _) => EditResult.InvalidTarget,
        { Placement: BrowserPlacement.Object } => EditResult.Ok,
        { Placement: BrowserPlacement.Monster, Creature: { } creature } => Scenario.Creatures.Contains(creature) ? EditResult.Ok : EditResult.UnknownCreature,
        { Placement: BrowserPlacement.Dwelling, Creature: { } creature } =>
            !Scenario.Creatures.Contains(creature) ? EditResult.UnknownCreature
            : Scenario.Creatures.IsUpgrade(creature) ? EditResult.UpgradedCreature : EditResult.Ok,
        { Placement: BrowserPlacement.Artifact, Artifact: { } artifact } => Scenario.Artifacts.Contains(artifact) ? EditResult.Ok : EditResult.InvalidValue,
        _ => EditResult.InvalidTarget,
    };

    /// <summary>
    /// Places <paramref name="entry"/> with its top-left tile on <paramref name="tile"/>, as one undo step. A hero is
    /// <paramref name="owner"/>'s (null: the first player's) and is named by its number.
    /// </summary>
    public EditResult Place(BrowserEntry entry, MapPosition tile, out PlacedEntry placed, PlayerId? owner = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        placed = default;
        var id = default(MapObjectId);
        var result = Known(entry);
        if (result != EditResult.Ok)
        {
            return result;
        }

        switch (entry.Placement)
        {
            case BrowserPlacement.Hero:
                result = _heroes.Place(owner ?? Scenario.Players[0].Id, tile, $"Hero {Scenario.NextHeroId().Value}", out var hero);
                placed = result == EditResult.Ok ? new PlacedEntry(null, hero) : default;
                return result;
            case BrowserPlacement.Monster when entry.Creature is { } creature:
                result = _monsters.Place(tile, creature, count: null, GuardDisposition.Aggressive, neverFlees: false, out id);
                break;
            case BrowserPlacement.Dwelling when entry is { Creature: { } sold, Definition: { } dwelling }:
                result = Made(entry, dwelling, tile, out id, made => _objects.SetDwelling(made, sold, owner: null, available: null));
                break;
            case BrowserPlacement.Artifact when entry is { Artifact: { } artifact, Definition: { } pickup }:
                result = Made(entry, pickup, tile, out id, made => _contents.SetArtifactPickup(made, artifact));
                break;
            case BrowserPlacement.Object when entry.Definition is { } definition:
                result = _objects.Place(definition.Id, tile, out id);
                break;
            default:
                return EditResult.InvalidTarget;
        }

        placed = result == EditResult.Ok ? new PlacedEntry(id, null) : default;
        return result;
    }

    /// <summary>An object placed and then set up by <paramref name="setUp"/>, as one step named after the entry.</summary>
    private EditResult Made(BrowserEntry entry, MapObjectDefinition definition, MapPosition tile, out MapObjectId id, Func<MapObjectId, EditResult> setUp)
    {
        var (result, made) = (EditResult.Ok, default(MapObjectId));
        _context.AsOneStep($"Place {entry.Name}", () =>
        {
            result = _objects.Place(definition.Id, tile, out made);
            if (result == EditResult.Ok)
            {
                setUp(made); // checked before the object was placed
            }
        });
        id = made;
        return result;
    }
}
