using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// A map dwelling (ADR-054) visited by an own hero: the creatures waiting and what they cost this hero (its Stewardship counts),
/// with buttons to recruit one, a week's worth, or all it can pay into the hero's army. Closes with Escape or "Close", and when
/// the hero leaves.
/// </summary>
public partial class DwellingPanel : PanelContainer
{
    private readonly Label _title = new();
    private readonly Label _info = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(360, 0) };
    private readonly HBoxContainer _buttons = new();
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(360, 0) };
    private GameSession _session = null!;
    private MapObjectId? _dwelling;

    public event Action? Closed;

    public MapObjectId? CurrentDwelling => Visible ? _dwelling : null;

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
        close.Pressed += Close;
        header.AddChild(close);
        box.AddChild(_info);
        box.AddChild(_buttons);
        box.AddChild(_status);
        Visible = false;

        this.SubscribeWhileInTree<DwellingRecruited>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DwellingCreaturesGrew>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DwellingOwnerChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ResourcesChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroMoved>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArmyChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh());
    }

    /// <summary>Shows a dwelling an own hero of the player whose turn it is stands at.</summary>
    public void Open(MapObjectId dwelling)
    {
        _dwelling = dwelling;
        _status.Text = "";
        Refresh();
    }

    public void Close()
    {
        var wasOpen = _dwelling is not null;
        _dwelling = null;
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

    private void Refresh()
    {
        var state = _session.State;
        if (_dwelling is not { } id || state.FindObject(id) is not CreatureDwelling dwelling
            || state.HeroAt(dwelling.VisitPosition) is not { } hero || hero.Owner != state.CurrentPlayer.Id || state.PendingDecision is not null)
        {
            if (_dwelling is not null)
            {
                Close();
            }

            return;
        }

        Visible = true;
        var creature = state.Creatures.Get(dwelling.Creature);
        var available = dwelling.Available ?? 0;
        var each = StewardshipRules.DiscountCreatures(creature.Cost, hero.SkillLevelOf(BuiltInSkills.Stewardship));
        _title.Text = $"{creature.Name} dwelling";
        _info.Text = $"{available} waiting, {creature.WeeklyGrowth} more every week. {each.Gold} gold each for {hero.Name}.\n"
                     + (dwelling.Owner == hero.Owner ? "Yours: towns with a caravan can hire from it." : "Not yours yet.");

        foreach (var child in _buttons.GetChildren())
        {
            child.QueueFree();
        }

        var affordable = TownRules.MaxAffordable(state.CurrentPlayer.Resources, each, available);
        foreach (var (label, count) in new[] { ("1", 1), ($"week ({creature.WeeklyGrowth})", creature.WeeklyGrowth), ($"max ({affordable})", affordable) }
                     .Select(o => (o.Item1, Math.Min(o.Item2, available))).DistinctBy(o => o.Item2))
        {
            var button = new Button { Text = $"Recruit {label}", Disabled = count < 1 || count > affordable || !hero.Army.CanAccept(creature.Id) };
            var number = count;
            button.Pressed += () =>
            {
                var result = _session.Execute(new RecruitAtDwellingCommand(state.CurrentPlayer.Id, id, number));
                _status.Text = result.IsAccepted ? "" : $"Refused: {result.Error}";
            };
            _buttons.AddChild(button);
        }
    }
}
