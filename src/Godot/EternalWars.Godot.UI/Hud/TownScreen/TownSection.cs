using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>A part of the town screen (<see cref="TownPanel"/>, ADR-223): rebuilt alone on its own events, and with the whole screen.</summary>
internal abstract class TownSection(ITownSectionHost panel) : ScreenSection<Town>(() => panel.Shown)
{
    protected ITownSectionHost Panel => panel;

    protected GameSession Session => panel.Session;

    protected GameState State => panel.Session.State;

    protected PlayerId Player => panel.Session.State.CurrentPlayer.Id;

    /// <summary>Runs a town command; a refusal shows in the screen's status line.</summary>
    protected void Run(GameCommand command) => panel.Run(command);

    /// <summary>
    /// The events that say an own hero came to or left a town's entrance or garrison: the visiting hero the governor, army, transformer,
    /// blacksmith and tavern rows read.
    /// </summary>
    protected static RefreshScheduler OnHeroPresence(RefreshScheduler refresh, IEventSubscriber events) =>
        refresh.On<HeroMoved>(events)
            .On<HeroHired>(events)
            .On<HeroEnteredGarrison>(events)
            .On<HeroLeftGarrison>(events);
}
