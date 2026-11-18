using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Tools.Sim.Commands;

/// <summary><c>sim selftest</c>, the CI check: the same seed gives the same game, and a recorded game replays through JSON to the same state.</summary>
internal sealed class SelfTestCommand : SimCommand
{
    public override string Name => "selftest";

    public override bool TakesWeights => true;

    public override int Run(CommandLine line, SimContext sim)
    {
        var (map, first) = DemoGame.Play(sim, days: 10, seed: 99);
        var (_, second) = DemoGame.Play(sim, days: 10, seed: 99);
        if (GameStateDigest.Compute(first.State) != GameStateDigest.Compute(second.State))
        {
            Console.Error.WriteLine("selftest: two runs with the same seed diverged");
            return 1;
        }

        var json = GameRecordSerializer.Serialize(GameReplay.Record(map, first));
        GameReplay.Replay(GameRecordSerializer.Deserialize(json), sim.Content);

        var aiGame = SimGames.HandMadeGame(sim, "towns", seed: 7);
        var (aiFirst, firstAi) = SimGames.PlayAiGame(sim, aiGame, days: 15);
        var (aiSecond, _) = SimGames.PlayAiGame(sim, SimGames.HandMadeGame(sim, "towns", seed: 7), days: 15);
        if (firstAi.Refused != 0 || GameStateDigest.Compute(aiFirst.State) != GameStateDigest.Compute(aiSecond.State))
        {
            Console.Error.WriteLine($"selftest: the AI game diverged or issued {firstAi.Refused} refused commands");
            return 1;
        }

        GameReplay.Replay(GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(aiGame.Map, aiFirst))), sim.Content);

        var army = sim.ParseArmy($"{BuiltInCreatures.Pikeman}:20,{BuiltInCreatures.Archer}:10");
        var defender = sim.ParseArmy($"{BuiltInCreatures.HellHound}:10");
        if (Describe(Fights.FightOnce(sim, army, defender, 5)) != Describe(Fights.FightOnce(sim, army, defender, 5)))
        {
            Console.Error.WriteLine("selftest: the same battle had two outcomes");
            return 1;
        }

        Console.WriteLine($"selftest ok: {first.CommandLog.Count} commands replayed, digest {GameStateDigest.Compute(first.State)}; AI game {aiFirst.CommandLog.Count} commands");
        return 0;

        static string Describe((BattleEnded Ended, int Rounds) result) =>
            $"{result.Ended.Winner} {result.Rounds} {string.Join(",", result.Ended.Casualties)}";
    }
}
