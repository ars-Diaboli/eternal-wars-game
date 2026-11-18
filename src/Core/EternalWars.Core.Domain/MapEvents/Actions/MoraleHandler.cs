using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Morale or luck for the hero until its next battle, each at most <see cref="MapEventService.MaxMoraleOrLuck"/> either way.</summary>
internal sealed class MoraleHandler : HeroActionHandler<MoraleAction>
{
    protected override void ApplyTo(MapEventActionContext context, Hero hero, MoraleAction action)
    {
        var progression = context.Service.Progression;
        var source = $"event:{MapEventService.EventOf(context.Run).Name}";
        if (Math.Clamp(action.Morale, -MapEventService.MaxMoraleOrLuck, MapEventService.MaxMoraleOrLuck) is not 0 and var m)
        {
            progression.GrantStatus(hero, new HeroStatus(source, HeroStatusKind.Morale, m, StatusExpiry.NextBattle));
        }

        if (Math.Clamp(action.Luck, -MapEventService.MaxMoraleOrLuck, MapEventService.MaxMoraleOrLuck) is not 0 and var l)
        {
            progression.GrantStatus(hero, new HeroStatus(source, HeroStatusKind.Luck, l, StatusExpiry.NextBattle));
        }
    }

    protected override string Describe(GameState state, MoraleAction action) =>
        string.Join(", ", new[] { action.Morale != 0 ? $"{Signed(action.Morale)} morale" : null, action.Luck != 0 ? $"{Signed(action.Luck)} luck" : null }.OfType<string>());
}
