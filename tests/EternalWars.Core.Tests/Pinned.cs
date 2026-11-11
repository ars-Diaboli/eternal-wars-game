using System.Runtime.CompilerServices;

namespace EternalWars.Core.Tests;

/// <summary>
/// Text pinned in a file checked in next to the tests: a change shows as a failing test with the first differing line. Running the tests with
/// <c>EW_UPDATE_PINNED=1</c> rewrites the files instead (then review and commit them).
/// </summary>
internal static class Pinned
{
    public static bool Updating => Environment.GetEnvironmentVariable("EW_UPDATE_PINNED") == "1";

    /// <summary>The folder of the calling test's source file (the pinned files live in the source tree, not the build output).</summary>
    public static string Folder([CallerFilePath] string caller = "") => Path.GetDirectoryName(caller)!;

    /// <summary>Asserts <paramref name="actual"/> is the text pinned in <paramref name="path"/>; <paramref name="advice"/> says what a difference means.</summary>
    public static void Check(string path, string actual, string advice)
    {
        actual = actual.ReplaceLineEndings("\n");
        if (Updating)
        {
            File.WriteAllText(path, actual);
            return;
        }

        Assert.True(File.Exists(path), $"{path} is missing. Run the tests with EW_UPDATE_PINNED=1 to write it.");
        var expected = File.ReadAllText(path).ReplaceLineEndings("\n");
        if (expected == actual)
        {
            return;
        }

        var pinnedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var line = 0;
        while (line < pinnedLines.Length && line < actualLines.Length && pinnedLines[line] == actualLines[line])
        {
            line++;
        }

        Assert.Fail(
            $"{Path.GetFileName(path)} differs at line {line + 1}:\n  pinned: {pinnedLines.ElementAtOrDefault(line)}\n  now:    {actualLines.ElementAtOrDefault(line)}\n" +
            $"{advice}\nIf the change is meant, run the tests with EW_UPDATE_PINNED=1 and commit the rewritten file.");
    }
}
