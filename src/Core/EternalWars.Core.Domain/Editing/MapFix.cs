using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// A fix for a <see cref="MapIssue"/> (ADR-265): an edit whose answer is clear, named by <see cref="Text"/> (the Problems panel's Fix button).
/// A fix is data (the ids of what it changes and what it found there), as the validator may run on a copy of the map: the session applies it
/// to the live map (<see cref="MapEditorSession.Fix"/>) as one undo step, and refuses it when the map no longer is as the check saw it.
/// </summary>
public abstract record MapFix(string Text)
{
    /// <summary>Makes the edit through the session's editors; <see cref="EditResult.InvalidTarget"/> when the map has changed since the check.</summary>
    internal abstract EditResult Apply(MapEditorSession session);
}

/// <summary>
/// Links two two-way gates to each other: each must still be unlinked or already linked to the other (the fix of an unlinked gate whose
/// partner is the only other unlinked gate, or that another gate already points at).
/// </summary>
public sealed record LinkGatesFix(string Text, MapObjectId First, MapObjectId Second) : MapFix(Text)
{
    internal override EditResult Apply(MapEditorSession session)
    {
        bool Free(MapObjectId id, MapObjectId other) => session.Scenario.FindObject(id) is TwoWayGate { LinkedGate: var link } && (link is null || link == other);
        return Free(First, Second) && Free(Second, First) ? session.Objects.LinkGates(First, Second) : EditResult.InvalidTarget;
    }
}

/// <summary>Points a one-way entrance without an exit at the one exit no entrance uses.</summary>
public sealed record GateExitFix(string Text, MapObjectId Entrance, MapObjectId Exit) : MapFix(Text)
{
    internal override EditResult Apply(MapEditorSession session) =>
        session.Scenario.FindObject(Entrance) is OneWayGateEntrance { Exit: null } && session.Scenario.FindObject(Exit) is OneWayGateExit
        && !session.Scenario.ObjectsOf<OneWayGateEntrance>().Any(e => e.Exit == Exit)
            ? session.Objects.SetGateExit(Entrance, Exit)
            : EditResult.InvalidTarget;
}

/// <summary>
/// Gives an ownable thing (a town, mine, garrison, resource provider or dwelling) the owner <see cref="Owner"/> (null: neutral), when it still
/// has the owner <see cref="Was"/> the check saw.
/// </summary>
public sealed record OwnerFix(string Text, MapObjectId Object, PlayerId? Was, PlayerId? Owner) : MapFix(Text)
{
    internal override EditResult Apply(MapEditorSession session) => session.Scenario.FindObject(Object) switch
    {
        IFlaggable flagged when flagged.Owner != Was => EditResult.InvalidTarget,
        Town town => session.Towns.SetOwner(town.Id, Owner),
        Mine mine => session.Objects.SetMineOwner(mine.Id, Owner),
        MapGarrison garrison => session.Objects.SetGarrisonOwner(garrison.Id, Owner),
        ResourceProvider provider => session.Objects.SetProviderOwner(provider.Id, Owner),
        CreatureDwelling dwelling => session.Objects.SetDwelling(dwelling.Id, dwelling.Creature, Owner, dwelling.Available),
        _ => EditResult.InvalidTarget,
    };
}
