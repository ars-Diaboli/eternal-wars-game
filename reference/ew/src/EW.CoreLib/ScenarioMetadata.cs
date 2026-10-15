using EW.CoreLib.Interfaces;

namespace EW.CoreLib;

public class ScenarioMetadata : IPersist<ScenarioMetadataStateDto> {
    public string DisplayName { get; set; } = "The Gathering Storm";
    public string Description { get; set; } = "Defeat the Necromancer in 30 days.";
    public int MaxPlayers { get; set; } = 4;

    public ScenarioMetadataStateDto ToState() => new()
    {
        DisplayName = DisplayName,
        Description = Description,
        MaxPlayers = MaxPlayers
    };

    public void FromState(ScenarioMetadataStateDto dto)
    {
        DisplayName = dto.DisplayName;
        Description = dto.Description;
        MaxPlayers = dto.MaxPlayers;
    }
}
