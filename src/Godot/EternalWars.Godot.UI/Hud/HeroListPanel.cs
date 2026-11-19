using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Heroes of the player whose turn it is, with movement points left; clicking one selects it (the action buttons' hero screen and spell book
/// then open its, ADR-210). A column, or a row of narrower entries when <see cref="Horizontal"/> (the "Layout" tab, ADR-156).
/// </summary>
public partial class HeroListPanel : BoxContainer
{
    private GameSession _session = null!;
    private Func<PlayerId> _listed = null!;
    private HeroId? _selected;

    public HeroListPanel() => Vertical = true;

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

    public event Action<HeroId>? HeroSelected;

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
    }

    /// <summary>Highlights the hero the app selected (by click on the map or here).</summary>
    public void ShowSelected(HeroId? hero)
    {
        _selected = hero;
        Refresh();
    }

    private void Refresh()
    {
        this.ClearChildren();

        var player = _session.State.GetPlayer(_listed());
        AddChild(new Label { Text = $"{player.Name}'s heroes" });
        foreach (var hero in _session.State.HeroesOf(player.Id))
        {
            var stacks = string.Join(", ", hero.Army.Stacks.Select(s => $"{s.Stack.Count} {s.Stack.Creature.Split('.')[^1]}"));
            var button = new Button
            {
                Text = $"{hero.Name}  {hero.MovementPoints}/{hero.MaxMovementPoints} MP",
                TooltipText = stacks,
                ToggleMode = true,
                ButtonPressed = hero.Id == _selected,
                Alignment = HorizontalAlignment.Left,
                ClipText = true,
                CustomMinimumSize = new Vector2(Horizontal ? 180 : 270, 0),
                SizeFlagsHorizontal = Horizontal ? SizeFlags.Fill : SizeFlags.ExpandFill,
            };
            button.AddThemeColorOverride("font_color", Palette.ForPlayer(_session.State, hero.Owner).Lightened(0.4f));
            var id = hero.Id;
            button.Pressed += () => HeroSelected?.Invoke(id);
            AddChild(button);
        }
    }
}
