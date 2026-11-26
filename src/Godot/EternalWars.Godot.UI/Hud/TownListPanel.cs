using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The town list (ADR-309, ADR-313): one entry per town of the listed player, a column growing with the towns in its own panel like the hero
/// list's, no heading and no text (more towns than the room holds scroll with the wheel). An entry is the town's picture filling the slot (<see cref="TownPictures"/>: a village's without a fort; the faction's crest while
/// it has none), the governor's portrait in the top
/// corner (none without one) and the build mark in the bottom one: the hammer lit while the town can build today and dimmed once it has, the
/// keep with a laurel wreath when it is fully built. The name goes to the status bar on hover, the build state after it; no hover card.
/// Clicking one centres the map on it and opens its town screen.
/// </summary>
public partial class TownListPanel : BoxContainer
{
    /// <summary>An entry's size.</summary>
    public static readonly Vector2 EntrySize = new(130, 64);

    private const int PictureSize = 52;
    private const int CornerSize = 24;
    private const int Inset = 3;

    /// <summary>The town picture's margin inside the slot's rim.</summary>
    private const int PictureInset = 2;

    private GameSession _session = null!;
    private Func<PlayerId> _listed = null!;

    public TownListPanel()
    {
        Vertical = true;
        AddThemeConstantOverride("separation", UiSpacing.Cell);
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
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var state = _session.State;
        foreach (var town in TownsOf(state, _listed()))
        {
            AddChild(Entry(state, town));
        }
    }

    /// <summary>One town's entry: a button with the picture, its label the name for the status bar, its tooltip the build state.</summary>
    private Button Entry(GameState state, Town town)
    {
        var fullyBuilt = BuildingTree.IsFullyBuilt(state, town);
        var builtToday = TownRules.HasBuiltToday(state, town);
        var governor = StewardshipRules.GovernorOf(state, town);
        var buildState = fullyBuilt ? "Fully built" : builtToday ? "Built something today" : "Can build today";
        var button = new Button
        {
            Text = town.Name,
            TooltipText = governor is null ? buildState : $"{buildState} · governor {governor.Name}",
            FocusMode = FocusModeEnum.None,
        };
        if (TownPictures.Of(town.TownType, village: town.Level(BuiltInBuildings.Fort) == 0) is { } view)
        {
            button.WithoutText();
            button.AddChild(new TextureRect
            {
                Texture = view,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, // filling the slot, as the army strip's creatures
                MouseFilter = MouseFilterEnum.Ignore,
                AnchorRight = 1,
                AnchorBottom = 1,
                OffsetLeft = PictureInset,
                OffsetTop = PictureInset,
                OffsetRight = -PictureInset,
                OffsetBottom = -PictureInset,
            });
        }
        else if (Heraldry.Crest(town.TownType, PictureSize) is { } crest)
        {
            button.WithIcon(crest, PictureSize);
        }

        button.CustomMinimumSize = EntrySize;
        button.SizeFlagsHorizontal = SizeFlags.Fill;
        UiFrames.Bevel(button, BevelKind.Slot).Tint(Palette.ForPlayer(state, town.Owner!.Value), 0.6f); // the owner's colour on the rim (ADR-310)

        if (governor is not null)
        {
            button.AddChild(Corner(HeroIcons.PortraitOrPlaceholder(governor.PortraitHero, governor.Name, CornerSize), LayoutPreset.TopLeft, Colors.White));
        }

        if (UiIcons.Get(fullyBuilt ? UiIcon.TownFullyBuilt : UiIcon.TownCanBuild, CornerSize) is { } mark)
        {
            button.AddChild(Corner(mark, LayoutPreset.BottomRight, !fullyBuilt && builtToday ? new Color(0.4f, 0.4f, 0.4f) : Colors.White));
        }

        var id = town.Id;
        button.Pressed += () => TownSelected?.Invoke(id);
        return button;
    }

    private static TextureRect Corner(Texture2D texture, LayoutPreset corner, Color tint)
    {
        var picture = new TextureRect
        {
            Texture = texture,
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = tint,
            CustomMinimumSize = new Vector2(CornerSize, CornerSize),
        };
        // Anchored to the corner, the picture's size laid out by hand: a preset would take its size before the picture has one.
        var right = corner == LayoutPreset.BottomRight;
        picture.AnchorLeft = picture.AnchorRight = right ? 1 : 0;
        picture.AnchorTop = picture.AnchorBottom = right ? 1 : 0;
        picture.OffsetLeft = right ? -CornerSize - Inset : Inset;
        picture.OffsetRight = right ? -Inset : CornerSize + Inset;
        picture.OffsetTop = right ? -CornerSize - Inset : Inset;
        picture.OffsetBottom = right ? -Inset : CornerSize + Inset;
        return picture;
    }
}
