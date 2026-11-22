using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// The tavern's offers (ADR-060): who can be hired for gold at the entrance, by the tavern's level, and "Visit the tavern…" when the screen has
/// a tavern screen to open (<see cref="TownPanel.TavernRequested"/>).
/// </summary>
internal sealed class TavernSection(ITownSectionHost panel) : TownSection(panel)
{
    public HBoxContainer Row { get; } = new();

    protected override Node Anchor => Row;

    public override void Show(Town town)
    {
        var state = State;
        Row.ClearChildren();
        var level = town.Level(BuiltInBuildings.Tavern);
        if (level < 1)
        {
            return;
        }

        Row.AddChild(new Label { Text = $"Tavern {level}", CustomMinimumSize = new Vector2(UiTheme.ArmyNameWidth, 0) });
        if (Panel.HasTavernScreen)
        {
            var open = new Button { Text = "Visit the tavern…", TooltipText = "The heroes for hire with their skills, specialty and troops" };
            open.Pressed += () => Panel.OpenTavern(town.Id);
            Row.AddChild(open);
        }

        foreach (var offer in TavernRules.Offers(state, state.CurrentPlayer.Id, town))
        {
            var refusal = TavernRules.CheckHire(state, town, offer.Id);
            var returning = TavernRules.Returning(state, state.CurrentPlayer.Id, offer.Id);
            var details = returning is null
                ? $"{offer.Class}, {offer.Town}"
                : $"returns: level {returning.Level}{(returning.Army.IsEmpty ? "" : ", with its army")}"; // ADR-081
            var hire = new Button
            {
                Text = $"Hire {offer.Name}\n{details}",
                Disabled = refusal is not null,
                TooltipText = refusal ?? "",
            }.WithCost(TavernRules.HeroCost, treasury: State.CurrentPlayer.Resources);
            var player = state.CurrentPlayer.Id;
            hire.WithInfo(detailed => InfoSheets.TavernHero(Session.State, player, town, offer, detailed));
            hire.Pressed += () => Run(new HireHeroCommand(state.CurrentPlayer.Id, town.Id, offer.Id));
            Row.AddChild(hire);
        }
    }

    /// <summary>Who stands at the entrance (a hire needs it free), who was hired, and what the player can pay for.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) =>
        OnHeroPresence(refresh, events)
            .On<ResourcesChanged>(events);
}
