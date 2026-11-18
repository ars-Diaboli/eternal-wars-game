using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>One row of a ranking table as the screens show it (ADR-101); <see cref="Highlight"/> marks the rows this game added.</summary>
public sealed record RankingRowView(
    int Rank, string Player, string Faction, string Map, string Difficulty, int Days, int Score, string Title, string Date, bool Highlight);

/// <summary>A ranking table to show: its name and its rows, best first.</summary>
public sealed record RankingView(string Table, IReadOnlyList<RankingRowView> Rows);

/// <summary>A ranking table (ADR-101): its name, the column headings and the rows, highlighted rows in gold. Used by the victory screen and the leaderboard.</summary>
public partial class RankingTableView : VBoxContainer
{
    private static readonly string[] Headings = ["#", "Player", "Faction", "Map", "Difficulty", "Days", "Score", "Title", "Date"];

    private readonly Label _name = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly GridContainer _grid = new() { Columns = Headings.Length };
    private readonly Label _empty = new() { Text = "No games in this table yet.", HorizontalAlignment = HorizontalAlignment.Center };

    public RankingTableView()
    {
        AddChild(_name);
        _grid.AddThemeConstantOverride("h_separation", UiSpacing.Section);
        AddChild(_grid);
        AddChild(_empty);
        Display(null);
    }

    /// <summary>Shows a table; null hides it.</summary>
    public void Display(RankingView? view)
    {
        _grid.ClearChildren();

        Visible = view is not null;
        if (view is null)
        {
            return;
        }

        _name.Text = view.Table;
        _empty.Visible = view.Rows.Count == 0;
        _grid.Visible = view.Rows.Count > 0;
        foreach (var heading in Headings)
        {
            _grid.AddChild(new Label { Text = heading });
        }

        foreach (var row in view.Rows)
        {
            var cells = new[] { row.Rank.ToString(), row.Player, row.Faction, row.Map, row.Difficulty, row.Days.ToString(), row.Score.ToString(), row.Title, row.Date };
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                Color? color = row.Highlight ? new Color(1f, 0.85f, 0.3f) : null;
                if (Headings[i] == "Faction" && Heraldry.Crest(cell) is not null)
                {
                    _grid.AddChild(Heraldry.Titled(cell, cell, color: color));
                    continue;
                }

                var label = new Label { Text = cell };
                if (color is { } font)
                {
                    label.AddThemeColorOverride("font_color", font);
                }

                _grid.AddChild(label);
            }
        }
    }
}
