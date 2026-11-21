using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// An editor whose every change is an undo step: the map's (<see cref="MapEditorSession"/>) and the template's (EDIT-2, ADR-250). It publishes
/// <see cref="Domain.Events.UndoHistoryChanged"/> whenever its <see cref="History"/> moves, so one History panel shows either.
/// </summary>
public interface IUndoSession
{
    UndoHistory History { get; }

    IEventSubscriber Events { get; }

    bool Undo();

    bool Redo();

    /// <summary>Undoes or redoes until <paramref name="step"/> steps are applied; false when it is there already.</summary>
    bool UndoTo(int step);
}
