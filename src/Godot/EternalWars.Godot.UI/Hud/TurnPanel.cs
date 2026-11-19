using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Game;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The End turn button: in a row at the top of the map; with the side panel on (<see cref="InPanel"/>) the button stands with the panel's other
/// actions (ADR-204). The calendar is on the status bar (<see cref="CalendarLabel"/>, ADR-225).
/// </summary>
public partial class TurnPanel : BoxContainer
{
    private Button? _endTurn;

    /// <summary>The End turn button, which the HUD moves into the side panel's actions (<see cref="InPanel"/>); null before <see cref="Bind"/>.</summary>
    public Button? EndTurn => _endTurn;

    /// <summary>Whether the side panel is on: the End turn button is then the HUD's to place (<see cref="EndTurn"/>).</summary>
    public bool InPanel
    {
        get => Vertical;
        set
        {
            Vertical = value;
            AddThemeConstantOverride("separation", value ? 0 : UiSpacing.Section);
        }
    }

    /// <summary><paramref name="endTurn"/> false leaves out the End turn button (the replay viewer, ADR-164).</summary>
    public void Bind(GameSession session, bool endTurn = true)
    {
        InPanel = false;
        _endTurn = new Button { Text = "End turn", Visible = endTurn }.WithIcon(UiIcon.EndTurn, UiIcons.BarSize);
        _endTurn.Pressed += () => session.Execute(new EndTurnCommand(session.State.CurrentPlayer.Id));
        AddChild(_endTurn);
    }
}
