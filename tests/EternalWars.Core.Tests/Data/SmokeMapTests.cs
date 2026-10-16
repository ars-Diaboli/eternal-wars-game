using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Tests.Data;

/// <summary>The maps under <c>tools/smoke/</c> load, and do what the scripted input of <c>tools/smoke-godot.ps1</c> expects.</summary>
public class SmokeMapTests
{
    [Fact]
    public void BattleMap_FirstStepEastStartsAFightTheHumanCommands()
    {
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Load(SmokeMap("battle.ewmap.json"))));
        session.Start();

        session.Step(new HeroId(1), Direction.East);

        Assert.Equal(BattlePhase.Fighting, session.State.Battle?.Phase);
        Assert.NotNull(session.Battles.Commander(BattleSide.Attacker));
    }

    private static string SmokeMap(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EternalWars.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found."), "tools", "smoke", name);
    }
}
