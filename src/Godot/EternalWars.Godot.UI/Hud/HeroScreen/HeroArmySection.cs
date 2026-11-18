using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.HeroScreen;

/// <summary>The army's seven slots: click a stack, then a slot to move it ("Split" moves half); the picked slot is the screen's.</summary>
internal sealed class HeroArmySection(HeroPanel panel) : HeroSection(panel)
{
    public HBoxContainer Slots { get; } = new HBoxContainer().WithCellGap();

    protected override Node Anchor => Slots;

    public override void Show(Hero hero)
    {
        Slots.ClearChildren();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var stack = hero.Army[slot];
            var button = new Button
            {
                Text = stack is null ? "—" : $"{stack.Count}\n{State.Creatures.Get(stack.Creature).Name}",
                CustomMinimumSize = UiTheme.HeroSlotSize,
                ToggleMode = true,
                ButtonPressed = Panel.PickedSlot == slot,
            };
            var target = slot;
            button.Pressed += () => Panel.ClickSlot(hero.Id, target);
            if (stack is not null)
            {
                button.WithInfo(detailed => InfoSheets.Creature(Panel.Session.State, stack.Creature, detailed, InfoSheets.Known(stack)));
            }

            Slots.AddChild(button);
        }
    }

    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) => refresh.On<ArmyChanged>(events);
}
