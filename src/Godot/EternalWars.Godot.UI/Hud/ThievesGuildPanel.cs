using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The Thieves' Guild (ADR-079, TOWN-11): every player's place in each row. From a tavern the rows grow with the player's taverns; a hero's
/// visit to a Den of Thieves opens the whole table. Closes with Escape or "Close".
/// </summary>
public partial class ThievesGuildPanel : PanelContainer
{
    private readonly Label _title = new();
    private readonly GridContainer _table = new();
    private GameSession _session = null!;

    public void Bind(GameSession session)
    {
        _session = session;
        this.CenterAsScreen();
        var box = new VBoxContainer();
        AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        header.AddChild(_title);
        var close = new Button { Text = "Close" };
        close.Pressed += () => Visible = false;
        header.AddChild(close);
        box.AddChild(_table);
        Visible = false;

        // Only the human whose hero walked in sees the den's table.
        this.SubscribeWhileInTree<ThievesDenVisited>(session.Events, e =>
        {
            var state = _session.State;
            if (state.Heroes.FirstOrDefault(h => h.Id == e.Hero) is { } hero && hero.Owner == state.CurrentPlayer.Id && state.CurrentPlayer.IsHuman)
            {
                Show(hero.Owner, null);
            }
        });
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Visible = false);
    }

    /// <summary>Opens the table the player's taverns show.</summary>
    public void Open(PlayerId player) => Show(player, ThievesGuild.Taverns(_session.State, player));

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    private void Show(PlayerId viewer, int? taverns)
    {
        var state = _session.State;
        _title.Text = taverns is { } count ? $"Thieves' guild ({count} tavern(s))" : "Den of thieves: the whole picture";
        foreach (var child in _table.GetChildren())
        {
            _table.RemoveChild(child);
            child.QueueFree();
        }

        var players = state.Players.Where(p => !p.IsDefeated).OrderBy(p => p.Id.Value).ToList();
        _table.Columns = players.Count + 1;
        _table.AddChild(new Label { Text = "" });
        foreach (var player in players)
        {
            _table.AddChild(new Label { Text = player.Id == viewer ? $"{player.Name} (you)" : player.Name });
        }

        foreach (var (row, entries) in ThievesGuild.Table(state, taverns))
        {
            _table.AddChild(new Label { Text = Title(row), CustomMinimumSize = new Vector2(140, 0) });
            foreach (var player in players)
            {
                var entry = entries.First(e => e.Player == player.Id);
                _table.AddChild(new Label { Text = entry.Detail is { } detail ? $"{Ordinal(entry.Place)}: {detail}" : Ordinal(entry.Place) });
            }
        }

        if (taverns is 0)
        {
            _table.Columns = 1;
            _table.AddChild(new Label { Text = "Build a tavern to hear what the thieves know." });
        }

        Visible = true;
    }

    private static string Title(ThievesGuildRow row) => row switch
    {
        ThievesGuildRow.LumberAndOre => "Lumber and ore",
        ThievesGuildRow.RareResources => "Rare resources",
        ThievesGuildRow.ArmyStrength => "Army strength",
        ThievesGuildRow.BestHero => "Best hero",
        ThievesGuildRow.BestCreature => "Best creature",
        _ => row.ToString(),
    };

    private static string Ordinal(int place) => place switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{place}th",
    };
}
