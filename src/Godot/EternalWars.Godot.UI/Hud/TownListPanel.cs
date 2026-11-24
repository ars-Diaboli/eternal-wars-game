using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Towns of the player whose turn it is, next to the hero list; clicking one centres the map on it and opens its town screen. A column, or a
/// row of narrower entries when <see cref="Horizontal"/> (the "Layout" tab, ADR-156).
/// </summary>
public partial class TownListPanel : BoxContainer
{
    /// <summary>The size of the building mark after a town's name.</summary>
    private const int BuildMarkSize = 24;

    private GameSession _session = null!;
    private Func<PlayerId> _listed = null!;

    public TownListPanel() => Vertical = true;

    /// <summary>A row of entries instead of a column.</summary>
    public bool Horizontal
    {
        get => !Vertical;
        set
        {
            Vertical = !value;
            if (_session is not null)
            {
                Refresh();
            }
        }
    }

    public event Action<MapObjectId>? TownSelected;

    /// <param name="session">The game.</param>
    /// <param name="listed">Whose towns are listed: the current player's by default; in a LAN game this screen's player's in another's turn.</param>
    public void Bind(GameSession session, Func<PlayerId>? listed = null)
    {
        _session = session;
        _listed = listed ?? (() => session.State.CurrentPlayer.Id);
        Refresh();
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TownOwnerChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<GovernorChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TownBuilt>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TownBuildingDowngraded>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DwellingBuilt>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TownRenamed>(session.Events, _ => Refresh());
    }

    /// <summary>A player's towns in map order, for the list and the town hotkey.</summary>
    public static IReadOnlyList<Town> TownsOf(GameState state, PlayerId player) =>
        state.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value).ToList();

    private void Refresh()
    {
        this.ClearChildren();

        var state = _session.State;
        var player = state.GetPlayer(_listed());
        AddChild(new Label { Text = $"{player.Name}'s towns (T)" });
        foreach (var town in TownsOf(state, player.Id))
        {
            var governor = StewardshipRules.GovernorOf(state, town);
            var fullyBuilt = BuildingTree.IsFullyBuilt(state, town);
            var builtToday = TownRules.HasBuiltToday(state, town);
            var buildState = fullyBuilt ? "Nothing left to build" : builtToday ? "Built something today" : "Can build today";
            var button = new Button
            {
                Text = $"{town.Name} ({town.TownType}){(governor is null ? "" : $"  governor {governor.Name}")}",
                TooltipText = buildState,
                Alignment = HorizontalAlignment.Left,
                ClipText = true,
                CustomMinimumSize = new Vector2(Horizontal ? 200 : 320, 0),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            }.WithCrest(town.TownType);
            button.AddThemeColorOverride("font_color", Palette.ForPlayer(state, town.Owner!.Value).Lightened(0.4f));
            var id = town.Id;
            button.Pressed += () => TownSelected?.Invoke(id);

            // The building mark after the name (ADR-309): the hammer lit while the town can build today and dimmed once it has, the keep with a laurel wreath when
            // nothing is left to build.
            var row = new HBoxContainer();
            row.AddChild(button);
            if (UiIcons.Get(fullyBuilt ? UiIcon.TownFullyBuilt : UiIcon.TownCanBuild, BuildMarkSize) is { } mark)
            {
                row.AddChild(new TextureRect
                {
                    Texture = mark,
                    StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                    TooltipText = buildState,
                    Modulate = !fullyBuilt && builtToday ? new Color(0.4f, 0.4f, 0.4f) : Colors.White,
                });
            }

            AddChild(row);
        }
    }
}
