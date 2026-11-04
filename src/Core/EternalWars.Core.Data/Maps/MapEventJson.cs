using System.Text.Json.Serialization.Metadata;
using EternalWars.Core.Domain.MapEvents;

namespace EternalWars.Core.Data.Maps;

/// <summary>
/// The JSON shape of the map events (map format v31, ADR-115): the domain's event tree is written as it is, each node with a <c>type</c> name, so
/// the tree needs no parallel document types. Added to every serializer that writes maps (maps, saves, replays).
/// </summary>
internal static class MapEventJson
{
    private static readonly IReadOnlyDictionary<Type, (Type Type, string Name)[]> Hierarchies = new Dictionary<Type, (Type, string)[]>
    {
        [typeof(EventExpr)] =
        [
            (typeof(NumberExpr), "number"), (typeof(VariableExpr), "variable"), (typeof(RandomExpr), "random"),
            (typeof(ArithmeticExpr), "arithmetic"), (typeof(QuantityExpr), "quantity"),
        ],
        [typeof(EventCondition)] =
        [
            (typeof(AllCondition), "all"), (typeof(AnyCondition), "any"), (typeof(NotCondition), "not"), (typeof(CompareCondition), "compare"),
            (typeof(HeroIsCondition), "heroIs"), (typeof(HeroClassCondition), "heroClass"), (typeof(PlayerIsCondition), "playerIs"),
            (typeof(PlayerIsHumanCondition), "playerIsHuman"), (typeof(FactionCondition), "faction"), (typeof(TeamCondition), "team"),
            (typeof(AllyCondition), "ally"), (typeof(TownOwnerCondition), "townOwner"), (typeof(MonsterDefeatedCondition), "monsterDefeated"),
            (typeof(HeroDefeatedCondition), "heroDefeated"),
        ],
        [typeof(EventAction)] =
        [
            (typeof(IfAction), "if"), (typeof(StopAction), "stop"), (typeof(SetVariableAction), "setVariable"), (typeof(EnableEventAction), "enable"),
            (typeof(RemoveCellAction), "removeCell"), (typeof(MessageAction), "message"), (typeof(QuestionAction), "question"),
            (typeof(ResourceAction), "resource"), (typeof(ExperienceAction), "experience"), (typeof(PrimaryStatAction), "stat"),
            (typeof(SkillAction), "skill"), (typeof(ArtifactAction), "artifact"), (typeof(WarMachineAction), "warMachine"),
            (typeof(SpellBookAction), "spellBook"), (typeof(SpellAction), "spell"), (typeof(ManaAction), "mana"), (typeof(MovementAction), "movement"),
            (typeof(MoraleAction), "morale"), (typeof(CreaturesAction), "creatures"), (typeof(AvailableCreaturesAction), "available"),
            (typeof(BuildAction), "build"), (typeof(BattleAction), "battle"), (typeof(RevealAction), "reveal"), (typeof(GiveHeroAction), "giveHero"),
            (typeof(RepeatAction), "repeat"), (typeof(WhileAction), "while"), (typeof(RunEventAction), "runEvent"),
        ],
    };

    /// <summary>A <see cref="DefaultJsonTypeInfoResolver"/> modifier: the three node hierarchies, discriminated by <c>type</c>.</summary>
    public static void AddTypes(JsonTypeInfo info)
    {
        if (!Hierarchies.TryGetValue(info.Type, out var derived))
        {
            return;
        }

        info.PolymorphismOptions = new JsonPolymorphismOptions { TypeDiscriminatorPropertyName = "type" };
        foreach (var (type, name) in derived)
        {
            info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type, name));
        }
    }
}
