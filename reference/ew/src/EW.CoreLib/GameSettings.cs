namespace EW.CoreLib;

public class GameSettings
{
    public static GameSettings Default { get; } = new();

    public float AiSpeed { get; set; } = 5f;
    public float PlayerSpeed { get; set; } = 5f;
}
