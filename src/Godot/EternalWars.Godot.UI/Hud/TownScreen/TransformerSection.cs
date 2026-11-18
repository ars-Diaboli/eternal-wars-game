using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// The Undead Transformer (ADR-137): a toggle per stack it can take (two, one per line, for a creature without a class), then "Transform" turns
/// the chosen ones at once.
/// </summary>
internal sealed class TransformerSection(TownPanel panel) : TownSection(panel)
{
    private readonly Dictionary<(TownArmy Army, int Slot), CreatureCategory> _chosen = [];

    public HFlowContainer Row { get; } = new();

    protected override Node Anchor => Row;

    /// <summary>Forgets the chosen stacks (another town, or the screen opened again).</summary>
    public void Reset() => _chosen.Clear();

    public override void Show(Town town)
    {
        var state = State;
        Row.ClearChildren();
        if (UndeadTransformer.CheckTown(town) is not null)
        {
            return;
        }

        Row.AddChild(new Label { Text = "Undead Transformer (free):" });
        var armies = new List<(TownArmy Army, Army Slots)> { (TownArmy.Garrison, town.Garrison) };
        if (TownRules.VisitingHero(state, town) is { } hero)
        {
            armies.Add((TownArmy.VisitingHero, hero.Army));
        }

        foreach (var key in _chosen.Keys.Where(k => !armies.Any(a => a.Army == k.Army && a.Slots[k.Slot] is not null)).ToList())
        {
            _chosen.Remove(key);
        }

        foreach (var (army, slots) in armies)
        {
            foreach (var (slot, stack) in slots.Stacks)
            {
                var lines = UndeadTransformer.NeedsLine(state, stack.Creature) ? new[] { CreatureCategory.A, CreatureCategory.B } : [CreatureCategory.None];
                foreach (var line in lines)
                {
                    if (UndeadTransformer.Of(state, town, stack.Creature, stack.Count, line).Transform is not { } transform)
                    {
                        continue;
                    }

                    var key = (army, slot);
                    var toggle = new Button
                    {
                        Text = $"{(army == TownArmy.Garrison ? "Garrison" : "Hero")}: {stack.Count} {state.Creatures.Get(stack.Creature).Name}"
                               + $"\n→ {transform.Result} {state.Creatures.Get(transform.To).Name}",
                        ToggleMode = true,
                        ButtonPressed = _chosen.TryGetValue(key, out var chosen) && chosen == line,
                    };
                    toggle.Toggled += on =>
                    {
                        if (on)
                        {
                            _chosen[key] = line;
                        }
                        else if (_chosen.TryGetValue(key, out var was) && was == line)
                        {
                            _chosen.Remove(key);
                        }

                        Refresh();
                    };
                    Row.AddChild(toggle);
                }
            }
        }

        var run = new Button { Text = "Transform", Disabled = _chosen.Count == 0 };
        run.Pressed += () =>
        {
            var orders = _chosen.Select(o => new TransformOrder(o.Key.Army, o.Key.Slot, o.Value)).ToList();
            _chosen.Clear();
            Run(new TransformCreaturesCommand(state.CurrentPlayer.Id, town.Id, orders));
        };
        Row.AddChild(run);
    }

    /// <summary>The stacks of the garrison and the visiting hero.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) =>
        OnHeroPresence(refresh, events)
            .On<GarrisonChanged>(events)
            .On<ArmyChanged>(events)
            .On<CreaturesRecruited>(events)
            .On<StackUpgraded>(events)
            .On<CreaturesTransformed>(events);
}
