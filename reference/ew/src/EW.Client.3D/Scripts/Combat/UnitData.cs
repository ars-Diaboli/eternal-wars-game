namespace EW.Engine;

using EW.Scripts.DataTypes.Enums;
using Godot;

[GlobalClass] // Allows it to show up in the "Create Resource" menu
public partial class UnitData : Resource
{
    [Export] public string UnitName { get; set; }
    [Export] public Faction Faction { get; set; } = Faction.Neutral;
    [Export] public UnitTier Tier { get; set; } = UnitTier.Tier1;
    [Export] public int MaxHealth { get; set; } = 25;
    [Export] public int MinDamage { get; set; } = 5;
    [Export] public int MaxDamage { get; set; } = 10;
    [Export] public int Attack { get; set; } = 10;
    [Export] public int Defense { get; set; } = 10;
    [Export] public int Speed { get; set; } = 15;
    [Export] public int Initiative { get; set; } = 10;
    [Export] public bool IsFlying { get; set; } = false;
    [Export] public bool IsRanged { get; set; } = false;
    [Export] public int Shots { get; set; } = 0;
    [Export] public int Experience { get; set; } = 25;
    [Export] public float Growth { get; set; }
    [Export] public PackedScene ModelScene { get; set; } // The 3D model
}
