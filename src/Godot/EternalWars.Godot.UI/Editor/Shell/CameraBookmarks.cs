using EternalWars.Godot.Shared.Cameras;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>A stored view of the map: where the camera looked from and the level shown (ADR-260).</summary>
public sealed record CameraBookmark(EditorCameraView View, int Level);

/// <summary>
/// The editor's camera bookmarks (ADR-260): Ctrl+1 … 9 store the view in a slot, 1 … 9 return to it. They belong to the map open and go
/// with it; they are not saved.
/// </summary>
public sealed class CameraBookmarks
{
    /// <summary>The slots, 1 to 9 as the number keys.</summary>
    public const int First = 1, Last = 9;

    private readonly SortedDictionary<int, CameraBookmark> _slots = [];

    /// <summary>The slots that hold a view, in order.</summary>
    public IEnumerable<int> Stored => _slots.Keys;

    public CameraBookmark? this[int slot] => _slots.GetValueOrDefault(slot);

    public void Store(int slot, CameraBookmark bookmark)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(slot, First);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(slot, Last);
        _slots[slot] = bookmark;
    }

    public void Clear() => _slots.Clear();
}
