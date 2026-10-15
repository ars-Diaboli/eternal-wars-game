namespace EW.CoreLib.Messaging;

public static class Hub
{
    public static EventHub Instance { get; } = new();
}