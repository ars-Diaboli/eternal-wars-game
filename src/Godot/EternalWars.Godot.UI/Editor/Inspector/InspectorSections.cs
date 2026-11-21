using EternalWars.Core.Domain.Objects;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>
/// The inspector's fields for one map object type (ADR-223); the registry is <see cref="InspectorSections"/>. It puts its rows into
/// the form's parts (<see cref="InspectorPart"/>, ADR-254): Identity unless it names another with <see cref="InspectorForm.In"/>.
/// It shows one object or several of its type at once (multi-edit, ADR-255): a field bound to an object property sets every one;
/// a field that only makes sense for one object says so (<see cref="InspectorForm.Single"/>).
/// </summary>
internal interface IInspectorSection
{
    /// <summary>The object type whose fields this section shows.</summary>
    Type ObjectType { get; }

    /// <summary>Adds the rows of <paramref name="objects"/> (one or more, all of <see cref="ObjectType"/>) to the form.</summary>
    void Show(InspectorForm form, IReadOnlyList<MapObject> objects);
}

/// <summary>An <see cref="IInspectorSection"/> for <typeparamref name="T"/>.</summary>
internal abstract class InspectorSection<T> : IInspectorSection
    where T : MapObject
{
    public Type ObjectType => typeof(T);

    public void Show(InspectorForm form, IReadOnlyList<MapObject> objects) => Show(form, [.. objects.Cast<T>()]);

    protected abstract void Show(InspectorForm form, IReadOnlyList<T> objects);
}

/// <summary>
/// The registry of the inspector's sections: at most one per map object type (a second throws when the registry is built), looked up by the
/// object's own type. Fields are optional for a kind (architecture.md, map objects): an object without a section shows only its caption.
/// </summary>
internal static class InspectorSections
{
    /// <summary>Every section, in no particular order.</summary>
    public static IReadOnlyList<IInspectorSection> All { get; } =
    [
        new DwellingInspector(),
        new ProviderInspector(),
        new MineInspector(),
        new MapGarrisonInspector(),
        new SignPostInspector(),
        new PrisonInspector(),
        new ArtifactPickupInspector(),
        new SphinxInspector(),
        new WitchHutInspector(),
        new ShrineInspector(),
        new TreasureChestInspector(),
        new MonsterInspector(),
        new TwoWayGateInspector(),
        new OneWayGateInspector(),
        new ResourcePileInspector(),
        new TownInspector(),
        new PandorasBoxInspector(),
        new CombatPlaceInspector(),
        new QuestSiteInspector(),
    ];

    private static readonly Dictionary<Type, IInspectorSection> ByType = Index(All);

    /// <summary>The section of the object's type, or null when the type has no fields to edit.</summary>
    public static IInspectorSection? For(MapObject mapObject) => ByType.GetValueOrDefault(mapObject.GetType());

    /// <summary>Whether objects of <paramref name="type"/> have a section.</summary>
    public static bool Has(Type type) => ByType.ContainsKey(type);

    private static Dictionary<Type, IInspectorSection> Index(IEnumerable<IInspectorSection> sections)
    {
        var byType = new Dictionary<Type, IInspectorSection>();
        foreach (var section in sections)
        {
            if (!byType.TryAdd(section.ObjectType, section))
            {
                throw new InvalidOperationException($"Two inspector sections for {section.ObjectType.Name}: {byType[section.ObjectType].GetType().Name} and {section.GetType().Name}.");
            }
        }

        return byType;
    }
}
