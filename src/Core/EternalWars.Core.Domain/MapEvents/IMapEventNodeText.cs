namespace EternalWars.Core.Domain.MapEvents;

/// <summary>
/// One node of an event tree as editable text and back (EVT-6): the editor's "Events" tab edits every action, condition and expression this way
/// instead of with a form of its own. The map format implements it (Core.Data), so the panel does not depend on the file format.
/// </summary>
public interface IMapEventNodeText
{
    string Write(EventAction action);

    /// <summary>A quest stage without its actions (they are edited in the tree).</summary>
    string Write(QuestStage stage);

    string Write(EventCondition condition);

    /// <exception cref="FormatException">The text is not a complete action.</exception>
    EventAction ReadAction(string text);

    /// <exception cref="FormatException">The text is not a complete stage.</exception>
    QuestStage ReadStage(string text, IReadOnlyList<EventAction> actions);
}
