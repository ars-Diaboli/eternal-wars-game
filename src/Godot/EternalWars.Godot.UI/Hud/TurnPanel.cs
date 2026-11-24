using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Game;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The End turn button (ADR-204); the adventure HUD puts it at the bottom of the side panel. The calendar is on the status bar
/// (<see cref="CalendarLabel"/>, ADR-225).
/// </summary>
public partial class TurnPanel : BoxContainer
{
    private Button? _endTurn;

    /// <summary>The End turn button; null before <see cref="Bind"/>.</summary>
    public Button? EndTurn => _endTurn;

    public TurnPanel()
    {
        Vertical = true;
        AddThemeConstantOverride("separation", 0);
    }

    /// <summary><paramref name="endTurn"/> false leaves out the End turn button (the replay viewer, ADR-164).</summary>
    public void Bind(GameSession session, bool endTurn = true)
    {
        _endTurn = new Button { Text = "End turn", Visible = endTurn }.WithIcon(UiIcon.EndTurn, UiIcons.BarSize);
        _endTurn.Pressed += () => session.Execute(new EndTurnCommand(session.State.CurrentPlayer.Id));
        AddChild(_endTurn);
    }
}
