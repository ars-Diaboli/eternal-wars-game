using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// The editor's file dialog (EDIT-1, ADR-250), for the map's files and the template's: Godot's <see cref="FileDialog"/>, the system's own
/// where there is one and <c>native</c> asks for it; a save dialog asks itself before a file is overwritten.
/// </summary>
public static class EditorFileDialog
{
    /// <summary>
    /// Opens a dialog under <paramref name="parent"/> in <paramref name="folder"/> (made when missing, else <paramref name="fallback"/>);
    /// <paramref name="filter"/> is Godot's ("*.ext;Kind"), <paramref name="file"/> the name offered. <paramref name="closed"/> runs when it
    /// closes either way (the dialog is freed), then <paramref name="picked"/> gets the chosen path.
    /// </summary>
    public static FileDialog Open(
        Node parent, string title, bool save, string filter, string? file, string folder, string fallback, bool native, Action<string> picked, Action closed)
    {
        ArgumentNullException.ThrowIfNull(parent);
        Directory.CreateDirectory(fallback);
        var dialog = new FileDialog
        {
            Title = title,
            FileMode = save ? FileDialog.FileModeEnum.SaveFile : FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = native,
            Filters = [filter],
            CurrentDir = Directory.Exists(folder) ? folder : fallback,
        };
        if (file is not null)
        {
            dialog.CurrentFile = file;
        }

        parent.AddChild(dialog);
        dialog.FileSelected += path =>
        {
            dialog.QueueFree();
            closed();
            picked(path);
        };
        dialog.Canceled += () =>
        {
            dialog.QueueFree();
            closed();
        };
        dialog.PopupCentered(new Vector2I(900, 600));
        return dialog;
    }

    /// <summary>A file name made from <paramref name="name"/>: the characters a file name cannot hold become '_' ("untitled" when nothing is left).</summary>
    public static string SafeName(string name)
    {
        var safe = string.Concat((name ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        return safe.Length == 0 ? "untitled" : safe;
    }
}
