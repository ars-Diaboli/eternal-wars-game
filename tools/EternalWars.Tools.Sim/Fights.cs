using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Events;

namespace EternalWars.Tools.Sim;

/// <summary>Single battles between two computer heroes, for the battle statistics and balance commands.</summary>
internal static class Fights
{
    /// <summary>One battle of copies of the two armies on a tiny map with <paramref name="seed"/>; the battle AI levels default to the latest.</summary>
    public static (BattleEnded Ended, int Rounds) FightOnce(SimContext sim, Army attacker, Army defender, ulong seed, (BattleAiLevel, BattleAiLevel)? levels = null)
    {
        var map = new AdventureMap(3, 1);
        var players = new[] { new Player(new PlayerId(0), "Attacker", isHuman: false), new Player(new PlayerId(1), "Defender", isHuman: false) };
        var heroes = new[]
        {
            new Hero(new HeroId(1), "Attacker", players[0].Id, new MapPosition(0, 0), Copy(attacker)),
            new Hero(new HeroId(2), "Defender", players[1].Id, new MapPosition(1, 0), Copy(defender)),
        };
        var session = new GameSession(new GameState(map, players, heroes, options: new GameOptions(Seed: seed), content: sim.Content));
        BattleEnded? ended = null;
        var rounds = 0;
        session.Events.Subscribe<BattleEnded>(e => ended = e);
        session.Events.Subscribe<BattleRoundStarted>(e => rounds = e.Round);
        session.Battles.AiLevels = levels ?? (BattleAiLevel.Latest, BattleAiLevel.Latest);
        session.Start();
        session.Execute(new StepHeroCommand(players[0].Id, heroes[0].Id, Direction.East)).ThrowIfRejected();
        return (ended ?? throw new InvalidOperationException("The battle did not finish."), rounds);

        static Army Copy(Army army) => new(army.Slots);
    }
}
