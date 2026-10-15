using EW.Scripts.DataTypes.Enums;

namespace EW.Scripts.DataTypes;

public interface IFactionOwned
{
    Faction Faction { get; set; }
}
