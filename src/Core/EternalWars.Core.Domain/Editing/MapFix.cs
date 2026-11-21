using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// A fix for a <see cref="MapIssue"/> (ADR-265): an edit whose answer is clear, named by <see cref="Text"/> (the Problems panel's Fix button).
/// A fix is data (the ids of what it changes and what it found there), as the validator may run on a copy of the map: the session applies it
/// to the live map (<see cref="MapEditorSession.Fix"/>) as one undo step, and refuses it unless the live map still gives the same fix.
/// </summary>
public abstract record MapFix(string Text)
{
    /// <summary>Makes the edit through the session's editors; <see cref="EditResult.InvalidTarget"/> when the map has changed since the check.</summary>
    internal abstract EditResult Apply(MapEditorSession session);
}

/// <summary>
/// Links two two-way gates of one kind to each other (the fix of an unlinked gate whose partner is the only other unlinked gate of its kind,
/// or the one gate that points at it): each must still be unlinked or linked to the other, and not both already.
/// </summary>
public sealed record LinkGatesFix(string Text, MapObjectId First, MapObjectId Second) : MapFix(Text)
{
    internal override EditResult Apply(MapEditorSession session)
    {
        if (session.Scenario.FindObject(First) is not TwoWayGate a || session.Scenario.FindObject(Second) is not TwoWayGate b
            || a.Definition.Id != b.Definition.Id || (a.LinkedGate == b.Id && b.LinkedGate == a.Id))
        {
            return EditResult.InvalidTarget;
        }

        var free = a.LinkedGate is null || a.LinkedGate == b.Id;
        return free && (b.LinkedGate is null || b.LinkedGate == a.Id) ? session.Objects.LinkGates(First, Second) : EditResult.InvalidTarget;
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
/// Gives a town, mine or garrison the owner <see cref="Owner"/> (null: neutral), when it still has the owner <see cref="Was"/> the check saw.
/// </summary>
public sealed record OwnerFix(string Text, MapObjectId Object, PlayerId? Was, PlayerId? Owner) : MapFix(Text)
{
    internal override EditResult Apply(MapEditorSession session) => session.Scenario.FindObject(Object) switch
    {
        IFlaggable flagged when flagged.Owner != Was => EditResult.InvalidTarget,
        Town town => session.Towns.SetOwner(town.Id, Owner),
        Mine mine => session.Objects.SetMineOwner(mine.Id, Owner),
        MapGarrison garrison => session.Objects.SetGarrisonOwner(garrison.Id, Owner),
        _ => EditResult.InvalidTarget,
    };
}
