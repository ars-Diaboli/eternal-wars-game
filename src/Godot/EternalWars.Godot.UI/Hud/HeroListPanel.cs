using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The hero list (ADR-309): one entry per hero of the listed player, a column growing with the heroes, no heading. An entry is the hero's
/// portrait and two thin upright bars, movement left (green) and mana (blue); the selected hero's entry is framed in the player's colour.
/// The name goes to the status bar on hover, and a card beside the list (<see cref="HeroHoverCard"/>) gives the numbers, the four stats and
/// the army. Clicking one selects it (the app centres the map on it).
/// </summary>
public partial class HeroListPanel : BoxContainer
{
    /// <summary>The portrait's side in an entry.</summary>
    public const int PortraitSize = 48;

    private const int BarWidth = 6;

    private readonly HeroHoverCard _card = new();
    private GameSession _session = null!;
    private Func<PlayerId> _listed = null!;
    private HeroId? _selected;
    private HeroId? _hovered;
    private bool _refreshing;

    public HeroListPanel()
    {
        Vertical = true;
        AddThemeConstantOverride("separation", UiSpacing.Cell);
        AddChild(_card); // on its own (top level): the column leaves it out
    }

    public event Action<HeroId>? HeroSelected;

    /// <summary>The hover card opens to the left of the list: the list is on the screen's right (the mirrored layout).</summary>
    public bool CardToLeft { get; set; }

    /// <summary>The card beside the list while the pointer is over an entry.</summary>
    public HeroHoverCard Card => _card;

    /// <param name="session">The game.</param>
    /// <param name="listed">Whose heroes are listed: the current player's by default; in a LAN game this screen's player's in another's turn.</param>
    public void Bind(GameSession session, Func<PlayerId>? listed = null)
    {
        _session = session;
        _listed = listed ?? (() => session.State.CurrentPlayer.Id);
        Refresh();
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroMoved>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroMovementRestored>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroMovementGained>(session.Events, _ => Refresh()); // Stables, Rally Flag
        this.SubscribeWhileInTree<HoleDug>(session.Events, _ => Refresh()); // digging takes the day's movement
        this.SubscribeWhileInTree<SphinxAnswered>(session.Events, _ => Refresh()); // a wrong answer too
        this.SubscribeWhileInTree<HeroDefeated>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArmyChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroHired>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroManaSpent>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroManaRestored>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<AdventureSpellCast>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroLeveledUp>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroFreed>(session.Events, _ => Refresh()); // a freed prisoner joins the list
        this.SubscribeWhileInTree<HeroPrimaryStatRaised>(session.Events, _ => Refresh()); // Knowledge raises the mana's maximum
        this.SubscribeWhileInTree<HeroArtifactsChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArtifactEquipped>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArtifactUnequipped>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArtifactGained>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArtifactLost>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<RelicAssembled>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<RelicTakenApart>(session.Events, _ => Refresh());
    }

    /// <summary>Highlights the hero the app selected (by click on the map or here).</summary>
    public void ShowSelected(HeroId? hero)
    {
        _selected = hero;
        Refresh();
    }

    private void Refresh()
    {
        // Taking out the hovered entry sends it mouse_exited at once: that is not the pointer leaving, so the card stays.
        _refreshing = true;
        foreach (var entry in GetChildren().OfType<Button>())
        {
            RemoveChild(entry);
            entry.QueueFree();
        }

        _refreshing = false;
        var state = _session.State;
        var heroes = state.HeroesOf(_listed()).ToList();
        Button? hoveredEntry = null;
        foreach (var hero in heroes)
        {
            var entry = Entry(hero);
            AddChild(entry);
            if (hero.Id == _hovered)
            {
                hoveredEntry = entry;
            }
        }

        if (hoveredEntry is null)
        {
            _hovered = null;
            _card.Leave();
            return;
        }

        // The card again beside the new entry, with the hero's new numbers, once the column has placed it.
        var id = _hovered!.Value;
        Callable.From(() =>
        {
            if (_hovered == id && IsInstanceValid(hoveredEntry) && hoveredEntry.IsInsideTree()
                && _session.State.Heroes.FirstOrDefault(h => h.Id == id) is { } hero)
            {
                _card.Show(HeroCard.Of(hero), hoveredEntry.GetGlobalRect(), CardToLeft);
            }
        }).CallDeferred();
    }

    /// <summary>One hero's entry: a toggle button with the portrait and the two bars, its label the hero's name for the status bar.</summary>
    private Button Entry(Hero hero)
    {
        var card = HeroCard.Of(hero);
        var colour = Palette.ForPlayer(_session.State, hero.Owner);
        var button = new Button
        {
            Text = hero.Name,
            ToggleMode = true,
            ButtonPressed = hero.Id == _selected,
            FocusMode = FocusModeEnum.None,
        }.WithIcon(HeroIcons.PortraitOrPlaceholder(card.Portrait, hero.Name, PortraitSize), PortraitSize);
        button.IconAlignment = HorizontalAlignment.Left;
        button.CustomMinimumSize = new Vector2(PortraitSize + (2 * UiIcons.IconPadding) + (2 * BarWidth) + (3 * UiSpacing.Cell), PortraitSize + (2 * UiIcons.IconPadding));
        UiFrames.Bevel(button, BevelKind.Slot).SelectedTint = colour; // the selected hero's rim and glow in the player's colour (ADR-310)

        var bars = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        bars.AddThemeConstantOverride("separation", UiSpacing.Cell);
        bars.AddChild(new GaugeBar { Colour = GaugeBar.Movement, Gauge = card.Movement, CustomMinimumSize = new Vector2(BarWidth, 0) });
        bars.AddChild(new GaugeBar { Colour = GaugeBar.Mana, Gauge = card.Mana, CustomMinimumSize = new Vector2(BarWidth, 0) });
        button.AddChild(bars);
        bars.SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
        bars.OffsetLeft = -((2 * BarWidth) + (2 * UiSpacing.Cell));
        bars.OffsetRight = -UiSpacing.Cell - 2;
        bars.OffsetTop = UiIcons.IconPadding;
        bars.OffsetBottom = -UiIcons.IconPadding;

        var id = hero.Id;
        button.Pressed += () => HeroSelected?.Invoke(id);
        button.MouseEntered += () =>
        {
            _hovered = id;
            _card.Show(HeroCard.Of(_session.State.GetHero(id)), button.GetGlobalRect(), CardToLeft); // the hero now, not as the list was built
        };
        button.MouseExited += () =>
        {
            if (_hovered == id && !_refreshing)
            {
                _hovered = null;
                _card.Leave();
            }
        };
        return button;
    }
}
