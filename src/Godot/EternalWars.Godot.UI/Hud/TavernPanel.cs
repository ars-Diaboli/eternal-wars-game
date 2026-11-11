using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The tavern screen (ADR-060, ADR-081, ADR-114): one card per hero the town's tavern offers this week, with class and faction, the specialty and
/// what it does, starting skills, primary stats, preferred creature and the troops it brings (a returning hero: its level and army), and a hire
/// button with the reason it is refused. A right click on a card opens the hero's information window, on its preferred creature or its troops
/// the creature's (ADR-154). The Thieves' Guild opens from here. Opened over the town screen; Escape or "Close" shut it.
/// </summary>
public partial class TavernPanel : PanelContainer
{
    private readonly Label _title = new();
    private readonly HBoxContainer _balance = new();
    private readonly HBoxContainer _cards = new();
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(500, 0) };
    private GameSession _session = null!;
    private MapObjectId? _town;

    /// <summary>The player opened the Thieves' Guild from the tavern (ADR-079).</summary>
    public event Action? ThievesGuildRequested;

    public void Bind(GameSession session)
    {
        _session = session;
        this.CenterAsScreen();
        var box = new VBoxContainer();
        AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        header.AddChild(_title);
        header.AddChild(_balance);
        var guild = new Button { Text = "Thieves' guild", TooltipText = "How you rank against the other players; more taverns show more" };
        guild.Pressed += () => ThievesGuildRequested?.Invoke();
        header.AddChild(guild);
        var close = new Button { Text = "Close" }.WithIcon(UiIcon.Close);
        close.Pressed += Close;
        header.AddChild(close);
        box.AddChild(_cards);
        box.AddChild(_status);
        Visible = false;

        this.SubscribeWhileInTree<HeroHired>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ResourcesChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TownBuilt>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroMoved>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Close());
    }

    /// <summary>Shows the tavern of a town of the player whose turn it is.</summary>
    public void Open(MapObjectId town)
    {
        _town = town;
        _status.Text = "";
        Refresh();
    }

    public void Close()
    {
        _town = null;
        Visible = false;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        var state = _session.State;
        if (_town is not { } id || state.FindObject(id) is not Town town || town.Owner != state.CurrentPlayer.Id || town.Level(BuiltInBuildings.Tavern) < 1)
        {
            Visible = false;
            return;
        }

        Visible = true;
        var level = town.Level(BuiltInBuildings.Tavern);
        _title.Text = $"{TownRules.Buildings(state, town).Get(BuiltInBuildings.Tavern, level).Name} of {town.Name}: heroes for hire this week";
        _balance.ClearChildren();

        _balance.AddChild(ResourceIcons.Balance(state.CurrentPlayer.Resources.Gold));
        _cards.ClearChildren(detach: true);

        var offers = TavernRules.Offers(state, state.CurrentPlayer.Id, town);
        if (offers.Count == 0)
        {
            _cards.AddChild(new Label { Text = "Nobody is looking for work this week." });
        }

        foreach (var offer in offers)
        {
            _cards.AddChild(Card(state, town, offer));
        }
    }

    private Control Card(GameState state, Town town, HeroDefinition offer)
    {
        var player = state.CurrentPlayer.Id;
        var card = new PanelContainer { CustomMinimumSize = new Vector2(250, 0) }
            .WithInfo(detailed => InfoSheets.TavernHero(_session.State, player, town, offer, detailed));
        var box = new VBoxContainer();
        card.AddChild(box);
        Label Line(string text, Color? color = null)
        {
            var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(240, 0), Modulate = color ?? Colors.White };
            box.AddChild(label);
            return label;
        }

        var returning = TavernRules.Returning(state, state.CurrentPlayer.Id, offer.Id);
        Line(offer.Name, new Color(1f, 0.9f, 0.6f));
        Line($"{offer.Class}, {offer.Town}{(offer.Town == town.TownType ? " (native)" : "")}");
        Line($"Specialty: {offer.Specialty.Describe(state.Spells)}");
        var skills = returning?.Skills.ToString() ?? string.Join(", ", offer.StartingSkills.Select(s => $"{s.Value} {(BuiltInSkills.Contains(s.Key) ? BuiltInSkills.Get(s.Key).Name : s.Key)}"));
        Line($"Skills: {skills}");
        Line(returning is { } back
            ? $"Attack {back.Attack}, Defense {back.Defense}, Power {back.SpellPower}, Knowledge {back.Knowledge}"
            : $"Attack {offer.Attack}, Defense {offer.Defense}, Power {offer.SpellPower}, Knowledge {offer.Knowledge}");
        var preferred = state.Creatures.TryGet(offer.PreferredCreature, out var creature) ? creature.Name : offer.PreferredCreature;
        var prefers = Line($"Prefers {preferred} (+{SkillEffects.PreferredAttack} Attack, +{SkillEffects.PreferredDefense} Defense, +{SkillEffects.PreferredSpeed} speed)");
        if (creature is not null)
        {
            prefers.WithInfo(detailed => InfoSheets.Creature(_session.State, creature.Id, detailed));
        }
        if (returning is { } hero)
        {
            Line($"Returns at level {hero.Level}{(hero.Army.IsEmpty ? ", without an army" : $" with {string.Join(", ", hero.Army.Stacks.Select(s => $"{s.Stack.Count} {state.Creatures.Get(s.Stack.Creature).Name}"))}")}", new Color(0.7f, 0.9f, 1f));
        }
        else if (TavernRules.StarterStack(state, offer) is { } stack)
        {
            Line($"Brings {stack.Count} {state.Creatures.Get(stack.Creature).Name}")
                .WithInfo(detailed => InfoSheets.Creature(_session.State, stack.Creature, detailed, InfoSheets.Known(stack)));
        }

        var refusal = TavernRules.CheckHire(state, town, offer.Id);
        var hire = new Button { Text = "Hire", Disabled = refusal is not null, TooltipText = refusal ?? $"{offer.Name} appears at the town's entrance." }
            .WithCost(TavernRules.HeroCost)
            .WithInfo(detailed => InfoSheets.TavernHero(_session.State, player, town, offer, detailed));
        hire.Pressed += () =>
        {
            var result = _session.Execute(new HireHeroCommand(state.CurrentPlayer.Id, town.Id, offer.Id));
            _status.Text = result.IsAccepted ? $"{offer.Name} joins you." : $"Refused: {result.Error}";
        };
        box.AddChild(hire);
        if (refusal is not null)
        {
            Line(refusal, new Color(1f, 0.6f, 0.5f));
        }

        return card;
    }
}
