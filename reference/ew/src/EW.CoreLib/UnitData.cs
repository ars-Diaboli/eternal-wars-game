namespace EW.CoreLib.Types;

public class UnitData
{
    public string UnitName { get; set; }
    public Faction Faction { get; set; } = Faction.Neutral;
    public UnitTier Tier { get; set; } = UnitTier.Tier1;
    public int MaxHealth { get; set; } = 25;
    public int MinDamage { get; set; } = 5;
    public int MaxDamage { get; set; } = 10;
    public int Attack { get; set; } = 10;
    public int Defense { get; set; } = 10;
    public int Speed { get; set; } = 15;
    public int Initiative { get; set; } = 10;
    public bool IsFlying { get; set; } = false;
    public bool IsRanged { get; set; } = false;
    public int Shots { get; set; } = 0;
    public int Experience { get; set; } = 25;
    public float Growth { get; set; }
}