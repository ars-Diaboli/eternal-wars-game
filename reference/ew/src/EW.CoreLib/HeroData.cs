using EW.CoreLib.Interfaces;

namespace EW.CoreLib.Types;

public class HeroData : IPersist<HeroDataStateDto>
{
    public int MaxMovementPoints { get; set; } = 2500;

    public HeroDataStateDto ToState() => new()
    {
        MaxMovementPoints = MaxMovementPoints
    };

    public void FromState(HeroDataStateDto dto)
    {
       MaxMovementPoints = dto.MaxMovementPoints;
    }
}
