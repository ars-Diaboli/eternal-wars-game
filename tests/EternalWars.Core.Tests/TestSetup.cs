using System.Runtime.CompilerServices;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Tests;

internal static class TestSetup
{
    /// <summary>Every game a test plays checks that each refused command left it as it was (<see cref="GameSession.CheckRefusals"/>).</summary>
    [ModuleInitializer]
    internal static void CheckEveryRefusal() => GameSession.CheckRefusals = true;
}
