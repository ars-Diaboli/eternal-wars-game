using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Hud.HeroScreen;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Hero screen for a hero of the player whose turn it is: stats (with what artifacts add), mana, experience, specialty,
/// skills with their descriptions, the army (click a stack, then a slot to move it; "Split" moves half), the paper doll
/// and the backpack (click a worn artifact to take it off, a carried one to put it on, or drag it there: ADR-162), and war machines. Every change is
/// a command; refusals show below. A right click on a skill, a spell, a stack or an artifact opens its information window (ADR-154). Opened with <see cref="Open"/>; Previous / Next go through the player's heroes. With
/// <see cref="FullScreen"/> it fills its parent and scrolls; Escape or "Close" shut it.
/// The screen is made of <see cref="HeroSection"/>s (ADR-223), each rebuilt alone on its own events; the screen rebuilds them all when it opens
/// and when the turn passes or a hero is defeated (the hero may no longer be the player's).
/// </summary>
public partial class HeroPanel : PanelContainer
{
    private readonly HeroHeaderSection _header;
    private readonly HeroStatsSection _stats;
    private readonly HeroSkillsSection _skills;
    private readonly HeroArmySection _army;
    private readonly WarMachinesSection _machines;
    private readonly SpellBookSection _spells;
    private readonly AdventureSpellsSection _adventure;
    private readonly HeroArtifactsSection _artifacts;
    private readonly HeroSection[] _sections;
    private readonly CheckBox _split = new() { Text = "Split (move half)" };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(UiTheme.ScreenTextWidth, 0) };
    private GameSession _session = null!;
    private HeroId? _hero;

    public HeroPanel()
    {
        _header = new HeroHeaderSection(this);
        _stats = new HeroStatsSection(this);
        _skills = new HeroSkillsSection(this);
        _army = new HeroArmySection(this);
        _artifacts = new HeroArtifactsSection(this);
        _machines = new WarMachinesSection(this);
        _adventure = new AdventureSpellsSection(this);
        _spells = new SpellBookSection(this);
        _sections = [_header, _stats, _skills, _army, _artifacts, _machines, _adventure, _spells];
    }

    /// <summary>Fill the parent and scroll (the game app's hero screen) instead of shrinking to the content. Set before <see cref="Bind"/>.</summary>
    public bool FullScreen { get; set; }

    public event Action? Closed;

    /// <summary>
    /// An adventure spell that is aimed at a cell was picked for the hero (Dimension Door, Distant Vision, Eavesdrop, Town Portal from rank II):
    /// the map's next click is its cell. The screen closes.
    /// </summary>
    public event Action<HeroId, string>? AdventureSpellArmed;

    /// <summary>Ledger of Ages III is open on the hero for the week: read it again (no cost).</summary>
    public event Action<HeroId>? LedgerReopened;

    /// <summary>Eavesdrop III listens to a town for the week: show it again (no cost).</summary>
    public event Action<HeroId, MapObjectId>? EavesdropReopened;

    /// <summary>The hero the screen shows, if it is open.</summary>
    public HeroId? CurrentHero => Visible ? _hero : null;

    internal GameSession Session => _session;

    /// <summary>The hero the sections show: the open hero while the screen is in sight and the hero is the current player's.</summary>
    internal Hero? Shown => Visible && _hero is { } id ? Of(id) : null;

    /// <summary>The army slot picked to move.</summary>
    internal int? PickedSlot { get; private set; }

    public void Bind(GameSession session)
    {
        _session = session;
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (FullScreen)
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            OffsetBottom = -StatusBar.Height; // the status bar stays in sight below (ADR-204)
            this.UseOpaqueBackground();
            var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            AddChild(scroll);
            scroll.AddChild(box);
        }
        else
        {
            AddChild(box);
        }

        var header = new HBoxContainer();
        box.AddChild(header);
        var previous = new Button { Text = "Previous hero" }.WithIcon(UiIcon.ArrowLeft);
        previous.Pressed += () => Step(-1);
        header.AddChild(previous);
        header.AddChild(_header.Crest);
        header.AddChild(_header.Title);
        var next = new Button { Text = "Next hero" }.WithIcon(UiIcon.ArrowRight);
        next.Pressed += () => Step(1);
        header.AddChild(next);
        var close = new Button { Text = "Close" }.WithIcon(UiIcon.Close);
        close.Pressed += Close;
        header.AddChild(close);

        _stats.Badges.AddThemeConstantOverride("h_separation", UiSpacing.Section);
        box.AddChild(_stats.Badges);
        box.AddChild(_stats.Stats);
        box.AddChild(_stats.Specialty);
        box.AddHeading("Skills");
        box.AddChild(_skills.Rows);
        box.AddHeading("Army");
        box.AddChild(_army.Slots);
        box.AddChild(_split);
        box.AddChild(_machines.Row);
        box.AddChild(_spells.Row);
        box.AddHeading("Adventure spells (mana and 500 movement points)");
        box.AddChild(_adventure.Row);
        box.AddHeading("Worn artifacts (click or drag into the backpack to take off)");
        box.AddChild(_artifacts.Doll);
        box.AddHeading("Backpack (click or drag onto the doll to put on)");
        box.AddChild(_artifacts.Backpack);
        box.AddChild(_status);
        Visible = false;

        // Whether the hero is still the current player's.
        new RefreshScheduler(this, Refresh)
            .On<TurnStarted>(session.Events)
            .On<HeroDefeated>(session.Events);
        this.SubscribeWhileInTree<RelicReady>(session.Events, e => _status.Text = $"All pieces of {_session.State.Artifacts.Get(e.Relic).Name} are worn: press Assemble to join them.");
        foreach (var section in _sections)
        {
            section.Watch(session.Events);
        }
    }

    /// <summary>Shows a hero of the player whose turn it is.</summary>
    public void Open(HeroId hero)
    {
        _hero = hero;
        PickedSlot = null;
        _status.Text = "";
        Refresh();
    }

    public void Close()
    {
        var wasOpen = _hero is not null;
        _hero = null;
        Visible = false;
        if (wasOpen)
        {
            Closed?.Invoke();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    internal void ArmAdventureSpell(HeroId hero, string spell)
    {
        AdventureSpellArmed?.Invoke(hero, spell);
        Close();
    }

    internal void ReopenLedger(HeroId hero) => LedgerReopened?.Invoke(hero);

    internal void ReopenEavesdrop(HeroId hero, MapObjectId town) => EavesdropReopened?.Invoke(hero, town);

    /// <summary>An army slot clicked: picks its stack, or moves the picked stack there (half with "Split").</summary>
    internal void ClickSlot(HeroId id, int slot)
    {
        var hero = _session.State.GetHero(id);
        if (PickedSlot is not { } from)
        {
            if (hero.Army[slot] is not null)
            {
                PickedSlot = slot;
            }

            _army.Refresh();
            return;
        }

        PickedSlot = null;
        if (from != slot)
        {
            var stack = hero.Army[from]!;
            int? count = _split.ButtonPressed && stack.Count > 1 ? stack.Count / 2 : null;
            Run(new MoveStackCommand(_session.State.CurrentPlayer.Id, id, from, id, slot, count));
        }

        _army.Refresh();
    }

    internal void Run(GameCommand command)
    {
        var result = _session.Execute(command);
        _status.Text = result.IsAccepted ? "" : $"Refused: {result.Error}";
    }

    private Hero? Of(HeroId id)
    {
        var state = _session.State;
        return state.HeroesOf(state.CurrentPlayer.Id).FirstOrDefault(h => h.Id == id);
    }

    private void Step(int direction)
    {
        var state = _session.State;
        var heroes = state.HeroesOf(state.CurrentPlayer.Id).Select(h => h.Id).ToList();
        if (_hero is { } current && heroes.IndexOf(current) is var index and >= 0 && heroes.Count > 1)
        {
            Open(heroes[(index + direction + heroes.Count) % heroes.Count]);
        }
    }

    private void Refresh()
    {
        if (_hero is not { } id)
        {
            return;
        }

        if (Of(id) is not { } hero)
        {
            Close();
            return;
        }

        Visible = true;
        foreach (var section in _sections)
        {
            section.Show(hero);
        }
    }
}
