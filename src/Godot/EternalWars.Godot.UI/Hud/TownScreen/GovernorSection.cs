using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>The governor and what Stewardship does here (ADR-063); appoint the visiting hero, or dismiss the governor.</summary>
internal sealed class GovernorSection(TownPanel panel) : TownSection(panel)
{
    public HBoxContainer Row { get; } = new();

    protected override Node Anchor => Row;

    public override void Show(Town town)
    {
        var state = State;
        Row.ClearChildren();
        var player = Player;
        var governor = StewardshipRules.GovernorOf(state, town);
        var level = StewardshipRules.LevelIn(state, town);
        Row.AddChild(new Label
        {
            Text = governor is null
                ? "No governor: Stewardship does not apply."
                : $"{governor.Name}: buildings -{StewardshipRules.BuildingDiscountPercent(level)} %, creatures -{StewardshipRules.CreatureDiscountPercent(level)} %",
            CustomMinimumSize = new Vector2(UiTheme.TownRowWidth, 0),
        });
        if (governor is not null)
        {
            var dismiss = new Button { Text = "Dismiss governor" };
            dismiss.Pressed += () => Run(new DismissGovernorCommand(player, town.Id));
            Row.AddChild(dismiss);
        }

        if (TownRules.VisitingHero(state, town) is { } visitor && visitor != governor && StewardshipRules.CheckAppoint(state, town, visitor) is null)
        {
            var appoint = new Button { Text = $"Appoint {visitor.Name}" };
            appoint.Pressed += () => Run(new AppointGovernorCommand(player, town.Id, visitor.Id));
            Row.AddChild(appoint);
        }
    }

    /// <summary>The visiting hero who could be appointed; the governor itself changes with the whole screen.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) => OnHeroPresence(refresh, events);
}
