using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests;

/// <summary>Opens editor sessions on the scenarios tests start from.</summary>
internal static class TestEditor
{
    /// <summary>An editor on a blank grass scenario.</summary>
    public static MapEditorSession Blank(int width = 12, int height = 10, int levels = 1) => new(BlankScenario.Create(width, height, levels));

    /// <summary>An editor on a <see cref="TestWorld"/> ASCII world.</summary>
    public static MapEditorSession World(params string[] rows) => new(TestWorld.Create(rows));
}
