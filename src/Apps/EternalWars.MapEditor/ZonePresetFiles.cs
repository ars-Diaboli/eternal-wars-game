using EternalWars.Core.Data.Templates;
using EternalWars.Core.Generation;
using EternalWars.Godot.UI.Editor;

namespace EternalWars.MapEditor;

/// <summary>The template editor's zone presets of the user (ADR-273): <c>*.ewzone.json</c> files in a folder (<see cref="ZonePresetFolder"/>).</summary>
internal sealed class ZonePresetFiles(string folder) : IZonePresetFiles
{
    public IReadOnlyList<ZonePreset> Load() => ZonePresetFolder.All(folder);

    public bool Exists(string name) => ZonePresetFolder.Find(folder, name) is not null;

    public void Save(ZonePreset preset) => ZonePresetFolder.Save(folder, preset);

    public bool Delete(string name) => ZonePresetFolder.Delete(folder, name);
}
