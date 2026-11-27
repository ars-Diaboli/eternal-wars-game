using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Tools.Sim;

/// <summary>
/// The fights of a `sim game` run per player and kind (neutral monsters, garrisons, towns, heroes), won and lost; and the champions of an arena's
/// or tournament's deciding battle as they came to it (ADR-160, ADR-366).
/// </summary>
internal sealed class FightLog
{
    private readonly Dictionary<(PlayerId Player, string Kind), (int Won, int Lost)> _counts = [];
    private readonly List<Champion> _champions = [];
    private (PlayerId Attacker, PlayerId? Defender, string Kind)? _current;

    /// <summary>A champion of the deciding battle when it was chosen: its hero's level and army value (Σ count × creature value).</summary>
    public sealed record Champion(PlayerId Player, string Hero, int Level, long Army);

    /// <summary>The deciding battle's champions, attacker first; empty when none was fought.</summary>
    public IReadOnlyList<Champion> Champions => _champions;

    public void Attach(GameSession session)
    {
        session.Events.Subscribe<ChampionsChosen>(e =>
        {
            foreach (var id in new[] { e.AttackerHero, e.DefenderHero })
            {
                var hero = session.State.GetHero(id);
                _champions.Add(new Champion(hero.Owner, hero.Name, hero.Level, DecidingBattles.ArmyValue(session.State, hero)));
            }
        });
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
