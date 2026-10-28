using System.Globalization;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The kingdom overview (ADR-077, ADR-084): every town, mine, flagged provider and hero that brings the player resources, with what each pays a day
/// and the totals, from <see cref="KingdomOverviews.For"/>; providers show what a week brings and what waits there. Opened from the HUD (or K),
/// closed with Escape or "Close".
/// </summary>
public partial class KingdomOverviewPanel : PanelContainer
{
    private readonly Label _title = new();
    private readonly GridContainer _table = new() { Columns = 2 + 6 };
    private GameSession _session = null!;

    public void Bind(GameSession session)
    {
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        var box = new VBoxContainer();
        AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        header.AddChild(_title);
        var close = new Button { Text = "Close" };
        close.Pressed += () => Visible = false;
        header.AddChild(close);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(760, 420) };
        box.AddChild(scroll);
        scroll.AddChild(_table);
        Visible = false;

        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Visible = false);
    }

    public void Open(PlayerId player)
    {
        var overview = KingdomOverviews.For(_session.State, player);
        _title.Text = $"Kingdom of {_session.State.GetPlayer(player).Name}: {_session.State.Date}";
        foreach (var child in _table.GetChildren())
        {
            _table.RemoveChild(child);
            child.QueueFree();
        }

        Row("Source", "", Enum.GetValues<ResourceType>().Select(r => r.ToString()));
        foreach (var row in overview.Rows)
        {
            var amounts = row.Kind == IncomeSourceKind.Provider ? row.Stored : row.Daily;
            Row(row.Name, row.Kind == IncomeSourceKind.Provider ? $"{row.Detail}; waiting:" : row.Detail, Amounts(amounts));
        }

        Row("Every day", "", Amounts(overview.DailyTotal));
        if (overview.StoredTotal != ResourceSet.Empty)
        {
            Row("Waiting at providers", "fetch with a hero or a caravan", Amounts(overview.StoredTotal));
        }

        Visible = true;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    private static IEnumerable<string> Amounts(ResourceSet amounts) =>
        Enum.GetValues<ResourceType>().Select(r => amounts[r] == 0 ? "" : amounts[r].ToString(CultureInfo.InvariantCulture));

    private void Row(string name, string detail, IEnumerable<string> cells)
    {
        _table.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(150, 0) });
        _table.AddChild(new Label { Text = detail, CustomMinimumSize = new Vector2(220, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart });
        foreach (var cell in cells)
        {
            _table.AddChild(new Label { Text = cell, CustomMinimumSize = new Vector2(56, 0), HorizontalAlignment = HorizontalAlignment.Right });
        }
    }
}
