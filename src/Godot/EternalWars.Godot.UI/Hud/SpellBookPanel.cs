using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Magic;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The selected hero's spell book on the adventure map (ADR-210), opened from the action buttons or with C: its mana, the adventure spells
/// to cast (as on the hero screen; an aimed one closes the book and waits for its cell), and the battle spells by school, each with its cost.
/// A right click on a spell opens its information window. Closes with Escape or "Close", and when the turn passes.
/// </summary>
public partial class SpellBookPanel : PanelContainer
{
    private readonly Label _title = new();
    private readonly VBoxContainer _body = new();
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520, 0) };
    private GameSession _session = null!;
    private HeroId? _hero;

    /// <summary>An adventure spell aimed at a cell was picked: the map's next click is its cell. The book closes.</summary>
    public event Action<HeroId, string>? AdventureSpellArmed;

    /// <summary>Ledger of Ages III is open on the hero for the week: read it again (no cost).</summary>
    public event Action<HeroId>? LedgerReopened;

    /// <summary>Eavesdrop III listens to a town for the week: show it again (no cost).</summary>
    public event Action<HeroId, MapObjectId>? EavesdropReopened;

    public void Bind(GameSession session)
    {
        _session = session;
        this.CenterAsScreen();
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_title);
        var close = new Button { Text = "Close" }.WithIcon(UiIcon.Close);
        close.Pressed += Close;
        header.AddChild(close);
        box.AddChild(_body);
        box.AddChild(_status);
        Visible = false;

        var refresh = new RefreshScheduler(this, Refresh);
        refresh.On<HeroManaSpent>(session.Events);
        refresh.On<HeroManaRestored>(session.Events);
        refresh.On<SpellsLearned>(session.Events);
        refresh.On<SpellsRaised>(session.Events);
        refresh.On<AdventureSpellCast>(session.Events);
        refresh.On<HeroMoved>(session.Events);
        refresh.On<ArtifactEquipped>(session.Events);
        refresh.On<ArtifactUnequipped>(session.Events);
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Close());
        this.SubscribeWhileInTree<HeroDefeated>(session.Events, e =>
        {
            if (e.Hero == _hero)
            {
                Close();
            }
        });
    }

    /// <summary>Opens the book of <paramref name="hero"/>, a hero of the player whose turn it is that has one.</summary>
    public void Open(HeroId hero)
    {
        _hero = hero;
        _status.Text = "";
        Visible = true;
        Refresh();
    }

    public void Close()
    {
        _hero = null;
        Visible = false;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape or Key.C })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        if (!Visible || _hero is not { } id)
        {
            return;
        }

        var state = _session.State;
        if (state.HeroesOf(state.CurrentPlayer.Id).FirstOrDefault(h => h.Id == id) is not { HasSpellBook: true } hero)
        {
            Close();
            return;
        }

        _title.Text = $"{hero.Name}'s spell book: {hero.Mana}/{hero.MaxMana} mana";
        _body.ClearChildren();

        _body.AddHeading("Adventure spells (mana and 500 movement points)");
        var adventure = new HFlowContainer();
        _body.AddChild(adventure);
        var buttons = AdventureSpellButtons.For(
            state,
            hero,
            cast: spell => Run(new CastAdventureSpellCommand(_session.State.CurrentPlayer.Id, id, spell)),
            arm: spell =>
            {
                Close();
                AdventureSpellArmed?.Invoke(id, spell);
            },
            ledger: () => LedgerReopened?.Invoke(id),
            eavesdrop: town => EavesdropReopened?.Invoke(id, town));
        foreach (var button in buttons)
        {
            adventure.AddChild(button);
        }

        if (buttons.Count == 0)
        {
            adventure.AddChild(new Label { Text = "none" });
        }

        _body.AddHeading("Battle spells");
        var battle = MagicRules.Known(hero, state.Spells).Where(k => !k.Spell.IsAdventure).ToList();
        if (battle.Count == 0)
        {
            _body.AddChild(new Label { Text = "none" });
        }

        foreach (var school in battle.GroupBy(k => k.Spell.School).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var row = new HFlowContainer();
            row.AddChild(new Label { Text = $"{Capitalized(school.Key)}:", CustomMinimumSize = new Vector2(110, 0) });
            foreach (var (spell, rank) in school.OrderBy(k => k.Spell.Level).ThenBy(k => k.Spell.Name, StringComparer.Ordinal))
            {
                var spellId = spell.Id;
                var known = rank;
                row.AddChild(new Button
                    {
                        Text = $"{MagicRules.NameAtRank(spell, rank)} ({ManaRules.SpellCost(hero, spell.Mana)})",
                        Flat = true,
                        TooltipText = $"Level {spell.Level}, {ManaRules.SpellCost(hero, spell.Mana)} mana. {spell.Notes}",
                    }
                    .WithInfo(detailed => InfoSheets.Spell(_session.State, spellId, known, detailed, hero)));
            }

            _body.AddChild(row);
        }
    }

    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private void Run(GameCommand command)
    {
        var result = _session.Execute(command);
        _status.Text = result.IsAccepted ? "" : $"Refused: {result.Error}";
    }
}
