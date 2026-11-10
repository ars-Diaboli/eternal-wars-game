using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The session's command handlers and the marker interfaces its authorization goes by.</summary>
public class CommandHandlerTests
{
    private static readonly List<Type> Commands =
        typeof(GameCommand).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GameCommand)) && !t.IsAbstract).ToList();

    /// <summary>Commands with a <c>HeroId Hero</c> whose service checks the hero itself (with its own refusals), so they are no <see cref="IHeroCommand"/>.</summary>
    private static readonly Type[] HeroCheckedByTheirService =
    [
        typeof(EquipArtifactCommand), typeof(UnequipArtifactCommand), typeof(AssembleRelicCommand), typeof(TakeRelicApartCommand),
        typeof(AppointGovernorCommand),
    ];

    [Fact]
    public void EveryCommand_HasAHandler()
    {
        var session = new GameSession(Create("A..."));

        Assert.Equal(Commands.Select(t => t.Name).Order(), session.HandledCommandTypes.Select(t => t.Name).Order());
    }

    [Fact]
    public void ASecondHandlerForACommand_Throws()
    {
        var handlers = new CommandHandlers();
        handlers.Add<EndTurnCommand>(_ => { });

        Assert.Throws<InvalidOperationException>(() => handlers.Add<EndTurnCommand>(_ => { }));
    }

    [Fact]
    public void TheBattleCommands_AreTheCommandsOfABattle()
    {
        var battle = Commands.Where(t => typeof(IBattleCommand).IsAssignableFrom(t)).Select(t => t.Name).Order().ToList();

        Assert.Equal(Commands.Where(t => t.Name.Contains("Battle", StringComparison.Ordinal)).Select(t => t.Name).Order(), battle);
        Assert.Equal(16, battle.Count);
    }

    [Fact]
    public void TheHeroCommands_AreTheHeroesActionsOnTheMap()
    {
        var hero = Commands.Where(t => typeof(IHeroCommand).IsAssignableFrom(t)).ToList();

        Assert.Equal(
            new[] { nameof(CastAdventureSpellCommand), nameof(DigCommand), nameof(MoveHeroCommand), nameof(MoveStackCommand), nameof(StepHeroCommand) },
            hero.Select(t => t.Name).Order());
        Assert.DoesNotContain(hero, t => typeof(IBattleCommand).IsAssignableFrom(t));

        // Every other command naming its hero "Hero" leaves the check to its service.
        var named = Commands.Where(t => t.GetProperty("Hero")?.PropertyType == typeof(HeroId)).Except(hero);
        Assert.Equal(HeroCheckedByTheirService.Select(t => t.Name).Order(), named.Select(t => t.Name).Order());
    }

    [Fact]
    public void AHeroCommand_IsRefusedForAnUnknownOrForeignHero()
    {
        var session = Start(Create("A..B"));
        var unknown = new HeroId(99);

        Assert.Equal($"Unknown hero {unknown}.", session.Execute(new StepHeroCommand(Blue, unknown, Direction.East)).Error);
        Assert.Equal($"{B} does not belong to {Blue}.", session.Execute(new DigCommand(Blue, B)).Error);
        Assert.Equal($"{B} does not belong to {Blue}.", session.Execute(new MoveStackCommand(Blue, B, 0, A, 1)).Error);
        Assert.Equal($"Unknown hero {unknown}.", session.Execute(new MoveStackCommand(Blue, A, 0, unknown, 1)).Error);
    }
}
