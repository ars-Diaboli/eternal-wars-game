using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;

namespace EternalWars.Godot.UI.Hud.HeroScreen;

/// <summary>A part of the hero screen (<see cref="HeroPanel"/>, ADR-223): rebuilt alone on its own events, and with the whole screen.</summary>
internal abstract class HeroSection(HeroPanel panel) : ScreenSection<Hero>(() => panel.Shown)
{
    protected HeroPanel Panel => panel;

    protected GameState State => panel.Session.State;

    /// <summary>Runs a hero command; a refusal shows in the screen's status line.</summary>
    protected void Run(GameCommand command) => panel.Run(command);

    /// <summary>The events that change what the hero wears and carries.</summary>
    protected static RefreshScheduler OnArtifacts(RefreshScheduler refresh, IEventSubscriber events) =>
        refresh.On<ArtifactEquipped>(events)
            .On<ArtifactUnequipped>(events)
            .On<ArtifactGiven>(events)
            .On<ArtifactGained>(events)
            .On<ArtifactLost>(events)
            .On<RelicAssembled>(events)
            .On<RelicTakenApart>(events);

    /// <summary>The events that change the hero's level, experience, mana, movement and statuses.</summary>
    protected static RefreshScheduler OnHeroState(RefreshScheduler refresh, IEventSubscriber events) =>
        refresh.On<HeroLeveledUp>(events)
            .On<HeroExperienceGained>(events)
            .On<HeroManaRestored>(events)
            .On<HeroManaSpent>(events)
            .On<AdventureSpellCast>(events)
            .On<HeroStatusGained>(events)
            .On<HeroStatusesExpired>(events)
            .On<PandorasBoxOpened>(events) // stats and skills
            .On<HeroMovementRestored>(events)
            .On<HeroMoved>(events);

    /// <summary>The events that change the spells the hero knows.</summary>
    protected static RefreshScheduler OnSpells(RefreshScheduler refresh, IEventSubscriber events) =>
        refresh.On<SpellsLearned>(events)
            .On<SpellsRaised>(events)
            .On<SpellBookBought>(events);
}
