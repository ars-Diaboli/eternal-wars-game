using EternalWars.Core.Domain.Settings;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// The combat settings (ADR-314), a window over the battle opened from the action grid: the battle's own view settings, kept on this computer in
/// the settings file and changing no rule: the battle's animation speed, the grid over the field, the active stack's reach highlighted. A
/// change applies at once (<see cref="Changed"/>; the app keeps it). Escape or the close button close it.
/// </summary>
public partial class CombatSettingsWindow : PanelContainer
{
    private readonly OptionButton _speed = new() { CustomMinimumSize = new Vector2(160, 0) };
    private readonly CheckBox _grid = new() { Text = "Show the grid" };
    private readonly CheckBox _reach = new() { Text = "Light the cells the active stack can reach" };
    private (AnimationSpeed Speed, bool Grid, bool Reach) _shown = (AnimationSpeed.Normal, true, true);

    /// <summary>A setting changed: the animation speed, the grid, the reach.</summary>
    public event Action<AnimationSpeed, bool, bool>? Changed;

    public void Bind()
    {
        Name = "CombatSettings";
        this.CenterAsScreen();
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(box);
        var header = new HBoxContainer();
        var title = new Label { Text = "Combat settings (kept on this computer)" };
        title.AddThemeFontSizeOverride("font_size", 20);
        header.AddChild(title);
        header.AddCloseButton(Close);
        box.AddChild(header);

        foreach (var label in new[] { "Slow", "Normal", "Fast", "Instant" })
        {
            _speed.AddItem(label);
        }

        box.AddRow("Battle animations", _speed, 180);
        box.AddChild(_grid);
        box.AddChild(_reach);
        _speed.ItemSelected += index => Change(_shown with { Speed = (AnimationSpeed)index });
        _grid.Toggled += on => Change(_shown with { Grid = on });
        _reach.Toggled += on => Change(_shown with { Reach = on });
        Visible = false;
    }

    /// <summary>The window is open.</summary>
    public bool IsOpen => Visible;

    /// <summary>Shows the settings as they are now, without raising <see cref="Changed"/>.</summary>
    public void ShowSettings(AnimationSpeed speed, bool grid, bool reach)
    {
        _shown = (speed, grid, reach);
        _speed.Selected = (int)speed;
        _grid.SetPressedNoSignal(grid);
        _reach.SetPressedNoSignal(reach);
    }

    public void Open() => Visible = true;

    public void Close() => Visible = false;

    private void Change((AnimationSpeed Speed, bool Grid, bool Reach) settings)
    {
        _shown = settings;
        Changed?.Invoke(settings.Speed, settings.Grid, settings.Reach);
    }
}
