using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>
/// The text chat of a LAN game (ADR-194), in the lobby and over the map: the lines so far (the newest at the bottom; the game's own lines, who
/// joined or left, in grey) and a line to write in. Enter sends the line (<see cref="Said"/>); Escape leaves the line without sending, so the
/// keys go back to the screen. The app adds every line, the player's own included, as the host passes it on (<see cref="Add"/>).
/// </summary>
public partial class ChatPanel : VBoxContainer
{
    /// <summary>The most lines kept.</summary>
    public const int MaxLines = 100;

    private static readonly Color GameLine = new(0.65f, 0.65f, 0.65f);

    private readonly RichTextLabel _log = new()
    {
        ScrollFollowing = true, BbcodeEnabled = false, SizeFlagsVertical = SizeFlags.ExpandFill, SelectionEnabled = true,
        FocusMode = FocusModeEnum.None,
    };

    private readonly LineEdit _line = new() { PlaceholderText = "Write to the other players (Enter sends)", MaxLength = 200 };
    private int _lines;

    public ChatPanel()
    {
        AddChild(_log);
        AddChild(_line);
        _line.TextSubmitted += Send;
        _line.GuiInput += e =>
        {
            if (e is InputEventKey { Pressed: true, Keycode: Key.Escape })
            {
                _line.Text = "";
                _line.ReleaseFocus();
                _line.AcceptEvent();
            }
        };
    }

    /// <summary>The player sent a line.</summary>
    public event Action<string>? Said;

    /// <summary>Whether the line is let go once sent, so the keys go back to the map (over the map; in the lobby it keeps the focus).</summary>
    private bool _releaseAfterSending;

    /// <summary>Whether the player is writing.</summary>
    public bool IsWriting => _line.HasFocus();

    /// <summary>Adds a line from <paramref name="from"/>; an empty name is the game's own line.</summary>
    public void Add(string from, string text)
    {
        if (_lines >= MaxLines)
        {
            _log.RemoveParagraph(0);
            _lines--;
        }

        if (_lines > 0)
        {
            _log.Newline();
        }

        if (from.Length == 0)
        {
            _log.PushColor(GameLine);
            _log.AddText(text);
            _log.Pop();
        }
        else
        {
            _log.PushColor(UiTheme.BorderBright);
            _log.AddText(from + ": ");
            _log.Pop();
            _log.AddText(text);
        }

        _lines++;
    }

    /// <summary>
    /// The chat over the map: the lines on a see-through ground that lets the map's clicks through, and the line let go once sent, so the keys
    /// go back to the map. The adventure HUD's layout gives its place (<see cref="PlaceAt"/>), a battle another (<see cref="PlaceOverBattle"/>).
    /// </summary>
    public void OverTheMap()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _releaseAfterSending = true;
        _log.MouseFilter = MouseFilterEnum.Ignore;
        _log.SelectionEnabled = false;
        _log.AddThemeStyleboxOverride("normal", UiTheme.Box(UiTheme.MapChatBackground, Colors.Transparent, 4, borderWidth: 0));
    }

    /// <summary>Places the chat in <paramref name="rect"/> of its layer (the map's place beside the town list, ADR-309).</summary>
    public void PlaceAt(Rect2 rect)
    {
        (AnchorLeft, AnchorRight, AnchorTop, AnchorBottom) = (0, 0, 0, 0);
        (OffsetLeft, OffsetTop, OffsetRight, OffsetBottom) = (rect.Position.X, rect.Position.Y, rect.End.X, rect.End.Y);
    }

    /// <summary>Over a battle: at the screen's bottom left, above the battle's log.</summary>
    public void PlaceOverBattle()
    {
        (AnchorLeft, AnchorRight, AnchorTop, AnchorBottom) = (0, 0, 1, 1);
        (OffsetLeft, OffsetRight) = (UiTheme.MapChatLeft, UiTheme.MapChatLeft + UiTheme.MapChatSize.X);
        (OffsetTop, OffsetBottom) = (-430, -250);
    }

    /// <summary>Puts the keys in the line, to write.</summary>
    public void StartWriting() => _line.CallDeferred(Control.MethodName.GrabFocus);

    private void Send(string text)
    {
        _line.Text = "";
        if (text.Trim().Length > 0)
        {
            Said?.Invoke(text);
        }

        if (_releaseAfterSending)
        {
            _line.ReleaseFocus();
        }
    }
}
