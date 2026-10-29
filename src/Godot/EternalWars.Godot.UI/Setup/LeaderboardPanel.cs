using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>"Leaderboard" in the main menu (ADR-101): the ranking tables, one tab each, best first. Back (or Escape, which the app maps) returns.</summary>
public partial class LeaderboardPanel : PanelContainer
{
    private readonly TabContainer _tabs = new() { CustomMinimumSize = new Vector2(760, 420) };
    private readonly Button _back = new() { Text = "Back (Esc)" };

    public event Action? BackRequested;

    public LeaderboardPanel()
    {
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 8);
        AddChild(rows);
        rows.AddChild(new Label { Text = "Leaderboard", HorizontalAlignment = HorizontalAlignment.Center });
        rows.AddChild(_tabs);
        _back.Pressed += () => BackRequested?.Invoke();
        rows.AddChild(_back);
        _back.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>Shows the tables; the tab is named after the table.</summary>
    public void Bind(IReadOnlyList<RankingView> tables)
    {
        foreach (var table in tables)
        {
            var scroll = new ScrollContainer();
            var view = new RankingTableView { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            scroll.AddChild(view);
            view.Display(table);
            _tabs.AddChild(scroll);
            _tabs.SetTabTitle(_tabs.GetTabCount() - 1, table.Table);
        }
    }
}
