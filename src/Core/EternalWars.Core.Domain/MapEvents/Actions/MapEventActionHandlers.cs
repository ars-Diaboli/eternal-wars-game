namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>
/// The registry of map event actions: the one <see cref="IMapEventActionHandler"/> of each <see cref="EventAction"/> type. The service runs an action
/// and shows a message's rewards through it instead of switching on the type, so a new action is its record plus one handler class registered here
/// (a test checks every action type has exactly one).
/// </summary>
internal static class MapEventActionHandlers
{
    /// <summary>Every handler: the flow and the event's own actions, then those of the player, the town and the world, then the hero's.</summary>
    public static IReadOnlyList<IMapEventActionHandler> All { get; } =
    [
        new StopHandler(),
        new IfHandler(),
        new RepeatHandler(),
        new WhileHandler(),
        new RunEventHandler(),
        new SetVariableHandler(),
        new EnableEventHandler(),
        new RemoveCellHandler(),
        new MessageHandler(),
        new QuestionHandler(),
        new ResourceHandler(),
        new CreaturesHandler(),
        new AvailableCreaturesHandler(),
        new BuildHandler(),
        new RevealHandler(),
        new GiveHeroHandler(),
        new AdvanceQuestHandler(),
        new BattleHandler(),
        new ExperienceHandler(),
        new PrimaryStatHandler(),
        new SkillHandler(),
        new ArtifactHandler(),
        new WarMachineHandler(),
        new SpellBookHandler(),
        new SpellHandler(),
        new ManaHandler(),
        new MovementHandler(),
        new MoraleHandler(),
    ];

    private static readonly Dictionary<Type, IMapEventActionHandler> ByType = All.ToDictionary(h => h.Action);

    /// <summary>The handler of the action's type.</summary>
    public static IMapEventActionHandler For(EventAction action) =>
        ByType.TryGetValue(action.GetType(), out var handler)
            ? handler
            : throw new ArgumentOutOfRangeException(nameof(action), action.GetType().Name, "Unregistered map event action.");
}
