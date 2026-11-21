namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// The map's texts (EDIT-11, ADR-258): <see cref="List"/> reads them all (<see cref="MapTexts"/>), <see cref="Set"/> changes one by its id with the
/// edit of the panel it belongs to, so the editor's rules for it hold and the change is one undo step.
/// </summary>
public sealed class MapTextEditor
{
    private readonly MapEditorSession _session;

    internal MapTextEditor(MapEditorSession session) => _session = session;

    public IReadOnlyList<MapText> List() => MapTexts.List(_session.Scenario);

    /// <summary>
    /// Sets the text with <paramref name="id"/>: <see cref="EditResult.InvalidTarget"/> when the map has no such text (any more) or, with
    /// <paramref name="expected"/>, when the text there is not that one (an id is a place, which another edit may give another text); the edit's
    /// own refusal when the text does not fit; a text that must not be blank (the map's and players' names, a hero's name, an event's message,
    /// question, answer or dialogue line, a quest stage's proposal) is refused blank. A text the map would keep as it is (also once trimmed, where
    /// its panel trims) records nothing.
    /// </summary>
    public EditResult Set(string id, string text, string? expected = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (List().FirstOrDefault(t => t.Id == id) is not { } found || (expected is not null && expected != found.Text))
        {
            return EditResult.InvalidTarget;
        }

        return found.Kept(text) == found.Text ? EditResult.Ok : found.Setter(_session, text);
    }
}
