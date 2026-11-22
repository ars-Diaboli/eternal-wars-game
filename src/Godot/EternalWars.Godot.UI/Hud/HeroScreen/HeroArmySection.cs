using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud.HeroScreen;

/// <summary>The hero's army as an <see cref="ArmyBar"/> (ADR-294): drag to move, merge or swap (Shift: split, Ctrl: one), Alt + click to spread.</summary>
internal sealed class HeroArmySection : HeroSection
{
    public HeroArmySection(HeroPanel panel)
        : base(panel)
    {
        Bars = new ArmyBarGroup
        {
            Move = (from, to, count) => Panel.Shown is { } hero
                ? new MoveStackCommand(Panel.Session.State.CurrentPlayer.Id, hero.Id, from.Slot, hero.Id, to.Slot, count)
                : null,
            Run = panel.RunAll,
            Info = (at, detailed) => Panel.Shown?.Army[at.Slot] is { } stack
                ? InfoSheets.Creature(Panel.Session.State, stack.Creature, detailed, InfoSheets.Known(stack))
                : null,
            Spread = at => Panel.Shown is { } hero ? StackSpread.ForHero(Panel.Session.State.CurrentPlayer.Id, hero.Id, hero.Army, at.Slot) : [],
        };
        Bar = new ArmyBar(Bars, "hero");
    }

    /// <summary>The bar's group: the stack picked by a click.</summary>
    public ArmyBarGroup Bars { get; }

    public ArmyBar Bar { get; }

    protected override Node Anchor => Bar;

    public override void Show(Hero hero) => Bar.Show(State, hero.Army, hero.Name);

    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) => refresh.On<ArmyChanged>(events);
}
