namespace EternalWars.Core.Domain.Game;

/// <summary>
/// When a game shown on this computer is autosaved (ADR-058, ADR-231): once it has started, and at each turn's start once the command that began
/// the turn is logged and settled, so the save replays to a whole command. The replay viewer (<c>watching</c>) and a session following a LAN
/// host never autosave: the viewer changes nothing, and the host keeps the game (ADR-193; a client saves the host's record, ADR-196). The
/// front-end tells it the game started, each <c>TurnStarted</c> and each <c>CommandExecuted</c>, and saves when it answers true.
/// </summary>
public sealed class AutosaveSchedule(GameSession session, bool watching = false)
{
    private bool _turnBegan;

    /// <summary>Whether this computer autosaves the game at all.</summary>
    public bool Saves => !watching && !session.FollowsHost;

    /// <summary>The game has just started: true when it is autosaved now (the first turn's start is this save).</summary>
    public bool Started()
    {
        _turnBegan = false;
        return Saves;
    }

    /// <summary>A turn started: the next command settled is the turn's save.</summary>
    public void TurnStarted() => _turnBegan = Saves;

    /// <summary>A command was executed: true when the game is autosaved now.</summary>
    public bool CommandExecuted()
    {
        var due = _turnBegan;
        _turnBegan = false;
        return due;
    }
}
