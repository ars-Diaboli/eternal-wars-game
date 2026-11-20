namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// What the editor told its user, a line a message with the time it was written: the Output panel shows it. The app keeps one log while maps
/// are opened and closed, so a message about a map that was just loaded outlives the panels that are rebuilt for it.
/// </summary>
public sealed class EditorLog(int capacity = 500)
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines => _lines;

    /// <summary>A line was added; the flag says that old lines were dropped to make room, so a view rewrites itself.</summary>
    public event Action<string, bool>? Written;

    public void Write(string text)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {text}";
        _lines.Add(line);
        var dropped = _lines.Count > capacity;
        if (dropped)
        {
            _lines.RemoveRange(0, _lines.Count - (capacity / 2)); // half at once, so a view is not rewritten for every line
        }

        Written?.Invoke(line, dropped);
    }
}
