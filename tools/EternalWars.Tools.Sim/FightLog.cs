using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Tools.Sim;

/// <summary>The fights of a `sim game` run per player and kind (neutral monsters, garrisons, towns, heroes), won and lost.</summary>
internal sealed class FightLog
{
    private readonly Dictionary<(PlayerId Player, string Kind), (int Won, int Lost)> _counts = [];
    private (PlayerId Attacker, PlayerId? Defender, string Kind)? _current;

    public void Attach(GameSession session)
    {
        session.Events.Subscribe<BattleStarted>(e =>
        {
            var state = session.State;
            var attacker = e.AttackerHero is { } a ? state.GetHero(a).Owner : (PlayerId?)null;
            var defender = e.DefenderHero is { } d ? state.GetHero(d).Owner : (PlayerId?)null;
            var guard = e.Guard is { } g ? state.FindObject(g) : null;
            var kind = e.DefenderHero is not null ? "hero" : guard switch { Town => "town", MapGarrison => "garrison", CreatureBank => "bank", _ => "neutral" };
            _current = attacker is { } owner ? (owner, defender, kind) : null;
        });
        session.Events.Subscribe<BattleEnded>(e =>
        {
            if (_current is not { } fight)
            {
                return;
            }

            Count(fight.Attacker, fight.Kind, e.Winner == BattleSide.Attacker);
            if (fight.Defender is { } defender)
            {
                Count(defender, "defending", e.Winner == BattleSide.Defender);
            }

            _current = null;
        });
    }

    public string Describe(PlayerId player)
    {
        var rows = _counts.Where(c => c.Key.Player == player).OrderBy(c => c.Key.Kind).Select(c => $"{c.Key.Kind} {c.Value.Won}/{c.Value.Lost}").ToList();
        return rows.Count == 0 ? "none" : string.Join(", ", rows);
    }

    private void Count(PlayerId player, string kind, bool won)
    {
        var (w, l) = _counts.GetValueOrDefault((player, kind));
        _counts[(player, kind)] = won ? (w + 1, l) : (w, l + 1);
    }
}
