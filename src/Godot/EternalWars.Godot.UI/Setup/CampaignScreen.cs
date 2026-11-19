using EternalWars.Core.Domain.Campaigns;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>A campaign the "Campaign" screen offers (ADR-241): where it comes from (the app's name for it, kept in saves) and the campaign.</summary>
public sealed record CampaignChoice(string Source, Campaign Campaign);

/// <summary>
/// "Campaign" in the main menu (ADR-241): the campaigns on the left; the chosen one's description and maps on the right, the difficulty (kept for
/// every map) and Start. Back (or Escape, which the app maps) returns.
/// </summary>
public partial class CampaignScreen : PanelContainer
{
    private readonly VBoxContainer _list = new() { CustomMinimumSize = new Vector2(260, 0) };
    private readonly Label _name = new();
    private readonly Label _description = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(460, 0) };
    private readonly Label _maps = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly DifficultyRow _difficulty = new();
    private readonly Button _start = new Button { Text = "Start", Disabled = true }.WithIcon(UiIcon.Confirm, keepText: true);
    private readonly Button _back = new Button { Text = "Back (Esc)" }.WithIcon(UiIcon.ArrowLeft, keepText: true);
    private CampaignChoice? _chosen;

    /// <summary>Start: the chosen campaign at the difficulty pressed.</summary>
    public event Action<CampaignChoice, Difficulty>? Started;

    public event Action? BackRequested;

    public CampaignScreen()
    {
        this.UseMenuPanel();
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(rows);
        rows.AddChild(new Label { Text = "Campaign", HorizontalAlignment = HorizontalAlignment.Center });

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", UiSpacing.Gap * 2);
        rows.AddChild(columns);
        columns.AddChild(_list);
        var details = new VBoxContainer();
        details.AddThemeConstantOverride("separation", UiSpacing.Gap);
        columns.AddChild(details);
        _name.AddThemeFontSizeOverride("font_size", 24);
        details.AddChild(_name);
        details.AddChild(_description);
        details.AddChild(_maps);
        var difficulty = new HBoxContainer();
        difficulty.AddThemeConstantOverride("separation", UiSpacing.Gap);
        difficulty.AddChild(new Label { Text = "Difficulty", VerticalAlignment = VerticalAlignment.Center });
        difficulty.AddChild(_difficulty);
        details.AddChild(difficulty);

        _start.Pressed += () =>
        {
            if (_chosen is { } chosen)
            {
                Started?.Invoke(chosen, _difficulty.Selected);
            }
        };
        _back.Pressed += () => BackRequested?.Invoke();
        rows.AddChild(Screens.ButtonRow(_back, _start));
    }

    /// <summary>Lists the campaigns; the first is chosen. Without any the screen says so.</summary>
    public void Bind(IReadOnlyList<CampaignChoice> campaigns)
    {
        _list.ClearChildren();
        var group = new ButtonGroup();
        foreach (var choice in campaigns)
        {
            var button = new Button { Text = choice.Campaign.Name, ToggleMode = true, ButtonGroup = group, Alignment = HorizontalAlignment.Left };
            button.Pressed += () => Choose(choice);
            _list.AddChild(button);
        }

        if (campaigns.Count == 0)
        {
            _name.Text = "No campaigns";
            _description.Text = "Put campaign files (*.ewcampaign.json) next to their maps in the map editor's maps folder, or in the game's campaigns folder.";
            _back.CallDeferred(Control.MethodName.GrabFocus);
            return;
        }

        ((Button)_list.GetChild(0)).ButtonPressed = true;
        Choose(campaigns[0]);
        _start.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Choose(CampaignChoice choice)
    {
        _chosen = choice;
        _name.Text = choice.Campaign.Name;
        _description.Text = choice.Campaign.Description;
        _maps.Text = string.Join('\n', choice.Campaign.Maps.Select((m, i) => $"{i + 1}. {m.Title}"));
        _start.Disabled = false;
    }
}

/// <summary>
/// A campaign's story between maps (ADR-241): the map's title and its intro before it starts, or the outro once it is won, with one button to go
/// on and one back to the main menu.
/// </summary>
public partial class CampaignStoryScreen : PanelContainer
{
    public event Action? ContinueRequested;

    public event Action? MainMenuRequested;

    public CampaignStoryScreen(string title, string text, string go)
    {
        this.UseMenuPanel();
        var rows = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        rows.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(rows);
        var heading = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
        heading.AddThemeFontSizeOverride("font_size", 28);
        rows.AddChild(heading);
        rows.AddChild(new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var menu = new Button { Text = "Main menu" };
        menu.Pressed += () => MainMenuRequested?.Invoke();
        var next = new Button { Text = go }.WithIcon(UiIcon.Confirm, keepText: true);
        next.Pressed += () => ContinueRequested?.Invoke();
        rows.AddChild(Screens.ButtonRow(menu, next));
        next.CallDeferred(Control.MethodName.GrabFocus);
    }
}
