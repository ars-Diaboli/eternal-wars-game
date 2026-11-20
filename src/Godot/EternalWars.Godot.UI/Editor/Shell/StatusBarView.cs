using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// The editor's status bar (EDIT-1, ADR-250), left to right: the tile under the pointer (x, y, level, terrain, height, what stands there),
/// the editor's last message, the background work that is running, the problem counts (a click asks for the Problems panel), the save
/// state and the zoom. The app tells it each of them when it changes.
/// </summary>
public partial class StatusBarView : HBoxContainer
{
    private readonly Label _tile = new() { CustomMinimumSize = new Vector2(360, 0), TooltipText = "The tile under the pointer" };
    private readonly Label _message = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
    private readonly Label _work = new() { TooltipText = "Work running in the background" };
    private readonly Button _problems = new() { Flat = true, TooltipText = "The map's problems: click to open Problems" };
    private readonly Label _save = new() { TooltipText = "Whether the map has changes that are not saved" };
    private readonly Label _zoom = new() { CustomMinimumSize = new Vector2(56, 0), HorizontalAlignment = HorizontalAlignment.Right, TooltipText = "Zoom" };

    /// <summary>The problem counts were clicked.</summary>
    public event Action? ProblemsRequested;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        foreach (var label in new[] { _tile, _message, _work, _save, _zoom })
        {
            label.MouseFilter = MouseFilterEnum.Pass; // a label shows its tooltip only when it takes the pointer
        }

        AddChild(_tile);
        AddChild(new VSeparator());
        AddChild(_message);
        AddChild(_work);
        AddChild(new VSeparator());
        AddChild(_problems);
        AddChild(new VSeparator());
        AddChild(_save);
        AddChild(new VSeparator());
        AddChild(_zoom);
        _problems.Pressed += () => ProblemsRequested?.Invoke();
        ShowTile(null, null);
        ShowProblems(0, 0);
    }

    /// <summary>The tile under the pointer, or null when the pointer is off the map.</summary>
    public void ShowTile(GameState? scenario, MapPosition? tile)
    {
        if (scenario is null || tile is not { } at)
        {
            _tile.Text = "";
            return;
        }

        var there = scenario.HeroAt(at)?.Name ?? scenario.ObjectCellAt(at)?.Object switch
        {
            null => null,
            Town town => town.Name,
            var other => other.Definition.Name,
        };
        _tile.Text = $"x {at.X}  y {at.Y}  L{at.Level} · {scenario.Map.GetTerrain(at)}, height {scenario.Map.GroundHeightAt(at):0.0}{(there is null ? "" : $" · {there}")}";
    }

    /// <summary>The editor's last message; empty clears it.</summary>
    public void ShowMessage(string text)
    {
        _message.Text = text;
        _message.TooltipText = text;
    }

    /// <summary>What runs in the background ("Checking the map…"), or null when nothing does.</summary>
    public void ShowWork(string? work) => _work.Text = work ?? "";

    public void ShowProblems(int errors, int warnings)
    {
        _problems.Text = $"Errors: {errors}  Warnings: {warnings}";
        var colour = errors > 0 ? UiTheme.IssueError : warnings > 0 ? UiTheme.IssueWarning : UiTheme.TextQuiet;
        _problems.AddThemeColorOverride("font_color", colour);
        _problems.AddThemeColorOverride("font_hover_color", colour);
    }

    public void ShowSaveState(string state) => _save.Text = state;

    public void ShowZoom(int percent) => _zoom.Text = $"{percent} %";
}
