using System.Collections.Generic;
using Godot;

namespace EW.Scripts;

public static class SaveGameStore
{
    public const string SaveDirectoryPath = "user://saves";
    public const string SaveFileExtension = ".tscn";

    private const string SaveFilePrefix = "save";

    public static string CreateSavePath()
    {
        return $"{SaveDirectoryPath}/{SaveFilePrefix}_{System.DateTime.Now:yyyyMMdd_HHmmss}{SaveFileExtension}";
    }

    public static Error EnsureSaveDirectoryExists()
    {
        return DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(SaveDirectoryPath));
    }

    public static List<string> GetSavedGamePaths()
    {
        List<string> savePaths = new();
        DirAccess saveDirectory = DirAccess.Open(SaveDirectoryPath);
        if (saveDirectory == null)
        {
            return savePaths;
        }

        saveDirectory.ListDirBegin();
        while (true)
        {
            string fileName = saveDirectory.GetNext();
            if (string.IsNullOrEmpty(fileName))
            {
                break;
            }

            if (saveDirectory.CurrentIsDir() || fileName.StartsWith(".") || !fileName.EndsWith(SaveFileExtension))
            {
                continue;
            }

            savePaths.Add($"{SaveDirectoryPath}/{fileName}");
        }

        savePaths.Sort((left, right) => string.CompareOrdinal(right, left));
        return savePaths;
    }
}
