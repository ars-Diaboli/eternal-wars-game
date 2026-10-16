using EternalWars.Core.Domain.Game;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The end of the game (ADR-057): victory, defeat or a draw, why, and who won. Shown over everything by the app once
/// the game is over (and a battle screen, if one is open, has been closed). Commands are refused from then on, so
/// the only thing left to do is to close the panel and look at the map.
/// </summary>
public partial class GameResultPanel : CenterContainer
{
    private readonly Label _title = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Label _text = new() { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) };

    public GameResultPanel()
    {
        AnchorRight = 1;
        AnchorBottom = 1;
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;

        var panel = new PanelContainer();
        AddChild(panel);
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 12);
        panel.AddChild(rows);
        _title.AddThemeFontSizeOverride("font_size", 32);
        rows.AddChild(_title);
        rows.AddChild(_text);
        var close = new Button { Text = "Look at the map" };
        close.Pressed += () => Visible = false;
        rows.AddChild(close);
    }

    /// <summary>The headline: Victory when a human player won, Defeat when only computers did, Draw when nobody won.</summary>
    public static string Headline(GameState state, GameOutcome outcome) =>
        !outcome.HasWinner ? "Draw"
        : state.Players.Any(p => p.IsHuman && outcome.Won(p.Id)) ? "Victory"
        : state.Players.Any(p => p.IsHuman) ? "Defeat"
        : "Game over";

    public void ShowOutcome(GameState state, GameOutcome outcome)
    {
        var lines = new List<string> { outcome.Reason };
        if (outcome.HasWinner)
        {
            lines.Add($"Winners: {string.Join(", ", outcome.Winners.Select(w => state.GetPlayer(w).Name))}");
        }

        lines.Add($"Day {outcome.Date.AbsoluteDay} ({outcome.Date})");
        _title.Text = Headline(state, outcome);
        _text.Text = string.Join('\n', lines);
        Visible = true;
    }
}
