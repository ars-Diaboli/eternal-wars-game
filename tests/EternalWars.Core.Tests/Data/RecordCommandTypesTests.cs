using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;

namespace EternalWars.Core.Tests.Data;

/// <summary>
/// Every command has its name in records and LAN messages (<c>GameRecordSerializer.CommandTypes</c>): one left out would only fail when a game
/// that used it is saved, replayed or played over the LAN.
/// </summary>
public class RecordCommandTypesTests
{
    private static readonly List<Type> Commands =
        typeof(GameCommand).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GameCommand)) && !t.IsAbstract).ToList();

    [Fact]
    public void EveryCommand_HasARecordName()
    {
        var named = GameRecordSerializer.CommandTypes.Select(c => c.Type).ToHashSet();
        var missing = Commands.Where(t => !named.Contains(t)).Select(t => t.Name).Order();

        Assert.True(!missing.Any(), $"Add to GameRecordSerializer.CommandTypes: {string.Join(", ", missing)}");
    }

    [Fact]
    public void OnlyConcreteCommands_HaveRecordNames()
    {
        var notCommands = GameRecordSerializer.CommandTypes.Select(c => c.Type).Where(t => !Commands.Contains(t)).Select(t => t.Name);

        Assert.Empty(notCommands);
    }

    [Fact]
    public void EachCommandAndEachName_AppearsOnce()
    {
        Assert.Equal(GameRecordSerializer.CommandTypes.Count, GameRecordSerializer.CommandTypes.Select(c => c.Type).Distinct().Count());
        Assert.Equal(GameRecordSerializer.CommandTypes.Count, GameRecordSerializer.CommandTypes.Select(c => c.Name).Distinct().Count());
    }
}
