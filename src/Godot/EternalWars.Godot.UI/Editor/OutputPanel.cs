using EternalWars.Godot.UI.Editor.Shell;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>The editor's messages (<see cref="EditorLog"/>) as a text that can be selected and copied, the newest at the bottom.</summary>
public partial class OutputPanel : RichTextLabel
{
    private EditorLog _log = null!;

    public void Bind(EditorLog log)
    {
        _log = log;
        BbcodeEnabled = false;
        SelectionEnabled = true;
        ScrollFollowing = true;
        FocusMode = FocusModeEnum.Click; // for Ctrl+C on a selection
        Rewrite();
        log.Written += OnWritten;
        TreeExiting += () => log.Written -= OnWritten;
    }

    private void OnWritten(string line, bool droppedOld)
    {
        if (droppedOld)
        {
            Rewrite();
        }
        else
        {
            AddText(line + "\n");
        }
    }

    private void Rewrite()
    {
        Clear();
        foreach (var line in _log.Lines)
        {
            AddText(line + "\n");
        }
    }
}
