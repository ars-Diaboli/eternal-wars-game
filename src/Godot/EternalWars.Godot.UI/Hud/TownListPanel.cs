using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>Towns of the player whose turn it is, next to the hero list; clicking one centres the map on it and opens its town screen.</summary>
public partial class TownListPanel : VBoxContainer
{
    private GameSession _session = null!;

    public event Action<MapObjectId>? TownSelected;

    public void Bind(GameSession session)
    {
        _session = session;
        Refresh();
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TownOwnerChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<GovernorChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TownBuilt>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DwellingBuilt>(session.Events, _ => Refresh());
    }

    /// <summary>The current player's towns in map order, for the town hotkey.</summary>
    public static IReadOnlyList<Town> TownsOf(GameState state) =>
        state.ObjectsOf<Town>().Where(t => t.Owner == state.CurrentPlayer.Id).OrderBy(t => t.Id.Value).ToList();

    private void Refresh()
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var state = _session.State;
        AddChild(new Label { Text = $"{state.CurrentPlayer.Name}'s towns (T)" });
        foreach (var town in TownsOf(state))
        {
            var governor = StewardshipRules.GovernorOf(state, town);
            var button = new Button
            {
                Text = $"{town.Name} ({town.TownType}){(governor is null ? "" : $"  governor {governor.Name}")}",
                TooltipText = town.LastBuilt == state.Date ? "Built something today" : "Can build today",
                Alignment = HorizontalAlignment.Left,
                ClipText = true,
                CustomMinimumSize = new Vector2(320, 0),
            };
            button.AddThemeColorOverride("font_color", Palette.ForPlayer(town.Owner!.Value).Lightened(0.4f));
            var id = town.Id;
            button.Pressed += () => TownSelected?.Invoke(id);
            AddChild(button);
        }
    }
}
