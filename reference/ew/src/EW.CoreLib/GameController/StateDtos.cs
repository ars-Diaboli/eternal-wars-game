using System.Text.Json.Serialization;
using EW.CoreLib.Types;

namespace EW.CoreLib;

public class GameScenarioControllerStateDto
{
    public ScenarioMetadataStateDto Metadata { get; set; } = null!;
    public required MapStateDto Map { get; set; }
    public List<GamePlayerStateDto> Players { get; set; } = [];
    public string? SelectedHeroObjectId { get; set; }
    public string HostPlayerName { get; set; } = null!;
    public string CurrentPlayerName { get; set; } = null!;
}

public class ScenarioMetadataStateDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MaxPlayers { get; set; }
}

public class MapStateDto
{
    public required SurfaceLayerStateDto SurfaceLayer { get; set; }
    public List<StaticMapObjectStateDto> StaticObjects { get; set; } = [];
    public List<RemovableMapObjectStateDto> RemovableObjects { get; set; } = [];
    public List<HeroStateDto> Heroes { get; set; } = [];
}

public class SurfaceLayerStateDto
{
    public int Height { get; set; }
    public int Width { get; set; }
    public List<TerrainSurface> Tiles { get; set; } = [];
}

public enum MapObjectType
{
    Town = 0
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TownStateDto), "town")]
public class StaticMapObjectStateDto
{
    public MapObjectType ObjectType { get; set; }
    public string ObjectId { get; set; } = string.Empty;
    public required Position Position { get; set; }
}

public enum RemovableMapObjectTypeDto
{
    ResourcePile = 0
}

public class TownStateDto : StaticMapObjectStateDto
{
    public Faction Faction { get; set; }
}

public class RemovableMapObjectStateDto
{
    public RemovableMapObjectTypeDto ObjectType { get; set; }
    public string ObjectId { get; set; } = string.Empty;
    public required Position Position { get; set; }
    public ResourceType ResourceType { get; set; }
    public int Amount { get; set; }
}

public class GamePlayerStateDto
{
    public string DisplayName { get; set; } = string.Empty;
    public PlayerControlType ControlType { get; set; }
    public PlayerColor Color { get; set; } = PlayerColor.Neutral;
    public List<ResourceStateDto> Resources { get; set; } = [];
}

public class ResourceStateDto
{
    public ResourceType ResourceType { get; set; }
    public int Amount { get; set; }
}

public class HeroStateDto
{
    public string ObjectId { get; set; } = string.Empty;
    public int OwnerPlayerIndex { get; set; }
    public Faction Faction { get; set; }
    public required Position Position { get; set; }
    public HeroDataStateDto BaseStats { get; set; } = new();
    public HeroPathStateDto Path { get; set; } = new();
}

public class HeroDataStateDto
{
    public int MaxMovementPoints { get; set; } = 2500;
}

public class HeroPathStateDto
{
    public int CurrentMovementPoints { get; set; } = 2500;
    public PathWaypoint[] MovementWaypoints { get; set; } = [];
}
