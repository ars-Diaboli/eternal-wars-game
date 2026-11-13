using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Editing;

/// <summary>Places and removes map objects, links gates and flags owned objects (mines, providers, garrisons, dwellings).</summary>
public sealed class ObjectEditor
{
    private readonly EditorContext _context;
    private readonly HeightEditor _heights;

    internal ObjectEditor(EditorContext context, HeightEditor heights)
    {
        _context = context;
        _heights = heights;
    }

    private Game.GameState Scenario => _context.Scenario;

    public EditResult CanPlace(MapObjectDefinition definition, MapPosition topLeft)
    {
        var probe = MapObject.Create(default, definition, topLeft);
        foreach (var (position, cell) in probe.OccupiedCells())
        {
            if (!Scenario.Map.Contains(position))
            {
                return EditResult.OutOfBounds;
            }

            if (Scenario.ObjectCellAt(position) is not null || Scenario.HeroAt(position) is not null)
            {
                return EditResult.Overlaps;
            }

            if (cell == FootprintCell.Visit && !Scenario.Terrains.IsPassable(Scenario.Map.GetTerrain(position)))
            {
                return EditResult.ImpassableTerrain;
            }
        }

        return EditResult.Ok;
    }

    /// <summary>Places an object; a multi-tile one with an entrance (a town, a mine…) gets its ground levelled in the same command (ADR-182 f).</summary>
    public EditResult Place(string definitionId, MapPosition topLeft, out MapObjectId id)
    {
        id = default;
        if (!_context.Catalog.TryGet(definitionId, out var definition))
        {
            return EditResult.InvalidTarget;
        }

        var check = CanPlace(definition, topLeft);
        if (check != EditResult.Ok)
        {
            return check;
        }

        if (definition.Kind == MapObjectKind.MagiEye && Scenario.Objects.OfType<MagiEye>().Count(e => e.Colour == definition.Colour) >= MagiWindow.MaxEyes)
        {
            return EditResult.TooManyEyes;
        }

        if (definition.Kind == MapObjectKind.Obelisk && Scenario.ObjectsOf<Obelisk>().Count() >= GrailRules.MaxObelisks)
        {
            return EditResult.TooManyObelisks;
        }

        var mapObject = MapObject.Create(Scenario.NextObjectId(), definition, topLeft);
        id = mapObject.Id;
        var ground = _heights.Under(mapObject); // levelled first, so the object's view is built on its ground
        _context.Run(
            $"Place {definition.Name}",
            () =>
            {
                ground?.Apply();
                _context.AddObject(mapObject);
            },
            () =>
            {
                _context.RemoveObject(mapObject);
                ground?.Revert();
            });
        return EditResult.Ok;
    }

    public EditResult Remove(MapObjectId id)
    {
        if (_context.Object<MapObject>(id) is not { } mapObject)
        {
            return EditResult.InvalidTarget;
        }

        // Whatever points at the removed object loses its link: a two-way partner, or one-way entrances.
        var dependents = mapObject switch
        {
            TwoWayGate { LinkedGate: { } partner } => new List<MapObjectId> { partner },
            OneWayGateExit => Scenario.ObjectsOf<OneWayGateEntrance>().Where(e => e.Exit == id).Select(e => e.Id).ToList(),
            _ => new List<MapObjectId>(),
        };
        var links = SnapshotLinks([id, .. dependents]);
        var index = -1;
        _context.Run(
            $"Remove {mapObject.Definition.Name}",
            () =>
            {
                dependents.ForEach(d => SetLink(d, null));
                index = _context.RemoveObject(mapObject);
            },
            () =>
            {
                _context.AddObject(mapObject, index);
                RestoreLinks(links);
            });
        return EditResult.Ok;
    }

    /// <summary>Points a one-way entrance at an exit (or clears it with null).</summary>
    public EditResult SetGateExit(MapObjectId entranceId, MapObjectId? exitId)
    {
        if (_context.Object<OneWayGateEntrance>(entranceId) is not { } entrance ||
            (exitId is { } e && _context.Object<OneWayGateExit>(e) is null))
        {
            return EditResult.InvalidTarget;
        }

        _context.Edit("Set gate exit", () => entrance.Exit, value => SetLink(entrance.Id, value), exitId);
        return EditResult.Ok;
    }

    /// <summary>Links two two-way gates both ways, unlinking whatever they were linked to before.</summary>
    public EditResult LinkGates(MapObjectId first, MapObjectId second)
    {
        if (first == second || _context.Object<TwoWayGate>(first) is not { } a || _context.Object<TwoWayGate>(second) is not { } b)
        {
            return EditResult.InvalidTarget;
        }

        var links = SnapshotLinks(a.Id, b.Id, a.LinkedGate, b.LinkedGate);
        _context.Run(
            "Link gates",
            () =>
            {
                foreach (var old in new[] { a.LinkedGate, b.LinkedGate })
                {
                    if (old is { } partner && partner != a.Id && partner != b.Id)
                    {
                        SetLink(partner, null);
                    }
                }

                SetLink(a.Id, b.Id);
                SetLink(b.Id, a.Id);
            },
            () => RestoreLinks(links));
        return EditResult.Ok;
    }

    public EditResult SetMineOwner(MapObjectId id, PlayerId? owner)
    {
        if (_context.Object<Mine>(id) is not { } mine)
        {
            return EditResult.InvalidTarget;
        }

        if (owner is { } player && !Scenario.HasPlayer(player))
        {
            return EditResult.UnknownPlayer;
        }

        _context.Edit("Set mine owner", () => mine.Owner, value =>
        {
            var before = mine.Owner;
            mine.Owner = value;
            _context.Publish(new MineOwnerChanged(mine.Id, before, value));
        }, owner);
        return EditResult.Ok;
    }

    /// <summary>Gives a mine a guard that must be beaten before the first flagging (null removes it).</summary>
    public EditResult SetMineGuard(MapObjectId id, GuardStack? guard)
    {
        if (_context.Object<Mine>(id) is not { } mine)
        {
            return EditResult.InvalidTarget;
        }

        if (guard is not null && !Scenario.Creatures.Contains(guard.Creature))
        {
            return EditResult.UnknownCreature;
        }

        if (guard?.Count < 1)
        {
            return EditResult.InvalidValue;
        }

        _context.Edit("Set mine guard", () => mine.Guard, value =>
        {
            mine.Guard = value;
            _context.Publish(new MapObjectChanged(mine.Id));
        }, guard);
        return EditResult.Ok;
    }

    /// <summary>Flags a resource provider for a player from the start (null = neutral; ADR-079).</summary>
    public EditResult SetProviderOwner(MapObjectId id, PlayerId? owner)
    {
        if (_context.Object<ResourceProvider>(id) is not { } provider)
        {
            return EditResult.InvalidTarget;
        }

        if (owner is { } player && !Scenario.HasPlayer(player))
        {
            return EditResult.UnknownPlayer;
        }

        _context.Edit("Set provider owner", () => provider.Owner, value =>
        {
            var before = provider.Owner;
            provider.Owner = value;
            provider.WasClaimed = value is not null;
            _context.Publish(new ProviderOwnerChanged(provider.Id, before, value));
        }, owner);
        return EditResult.Ok;
    }

    /// <summary>Flags a map garrison for a player from the start (null = neutral; ADR-070).</summary>
    public EditResult SetGarrisonOwner(MapObjectId id, PlayerId? owner)
    {
        if (_context.Object<MapGarrison>(id) is not { } garrison)
        {
            return EditResult.InvalidTarget;
        }

        if (owner is { } player && !Scenario.HasPlayer(player))
        {
            return EditResult.UnknownPlayer;
        }

        _context.Edit("Set garrison owner", () => garrison.Owner, value =>
        {
            var before = garrison.Owner;
            garrison.Owner = value;
            _context.Publish(new GarrisonOwnerChanged(garrison.Id, before, value));
        }, owner);
        return EditResult.Ok;
    }

    /// <summary>
    /// Edits a map dwelling (ADR-054): its creature, its owner and the creatures waiting in it (null = one week's growth when
    /// the game starts). Map dwellings sell base creatures only (ADR-073).
    /// </summary>
    public EditResult SetDwelling(MapObjectId id, string creature, PlayerId? owner, int? available)
    {
        if (_context.Object<CreatureDwelling>(id) is not { } dwelling)
        {
            return EditResult.InvalidTarget;
        }

        if (!Scenario.Creatures.Contains(creature))
        {
            return EditResult.UnknownCreature;
        }

        if (Scenario.Creatures.IsUpgrade(creature))
        {
            return EditResult.UpgradedCreature;
        }

        if (owner is { } player && !Scenario.HasPlayer(player))
        {
            return EditResult.UnknownPlayer;
        }

        if (available < 0)
        {
            return EditResult.InvalidValue;
        }

        _context.Edit<(string Creature, PlayerId? Owner, int? Available)>("Edit dwelling", () => (dwelling.Creature, dwelling.Owner, dwelling.Available), value =>
        {
            var before = dwelling.Owner;
            (dwelling.Creature, dwelling.Owner, dwelling.Available) = value;
            _context.Publish(new MapObjectChanged(dwelling.Id));
            if (before != value.Owner)
            {
                _context.Publish(new DwellingOwnerChanged(dwelling.Id, before, value.Owner));
            }
        }, (creature, owner, available));
        return EditResult.Ok;
    }

    /// <summary>Sets a two-way gate's partner or a one-way entrance's exit.</summary>
    private void SetLink(MapObjectId gateId, MapObjectId? linked)
    {
        switch (Scenario.GetObject(gateId))
        {
            case TwoWayGate gate:
                gate.LinkedGate = linked;
                break;
            case OneWayGateEntrance entrance:
                entrance.Exit = linked;
                break;
            default:
                throw new InvalidOperationException($"{gateId} is not a linkable gate.");
        }

        _context.Publish(new GateLinkChanged(gateId, linked));
    }

    private Dictionary<MapObjectId, MapObjectId?> SnapshotLinks(params MapObjectId?[] gateIds) =>
        gateIds.OfType<MapObjectId>()
            .Distinct()
            .Select(id => Scenario.Objects.FirstOrDefault(o => o.Id == id))
            .Where(o => o is TwoWayGate or OneWayGateEntrance)
            .ToDictionary(o => o!.Id, o => o!.TeleportTarget);

    private void RestoreLinks(Dictionary<MapObjectId, MapObjectId?> links)
    {
        foreach (var (gate, linked) in links)
        {
            SetLink(gate, linked);
        }
    }
}
