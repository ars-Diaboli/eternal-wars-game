using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// What the per-area editors of a <see cref="MapEditorSession"/> share: the scenario, the catalog, the event bus and the undo history with its
/// open stroke. Every edit runs through <see cref="Run(IEditorCommand)"/>, which executes it and records it (or adds it to the stroke).
/// </summary>
internal sealed class EditorContext(GameState scenario, MapObjectCatalog catalog, IEventBus bus)
{
    private List<IEditorCommand>? _stroke;
    private string _strokeDescription = "";

    public GameState Scenario { get; } = scenario;

    public MapObjectCatalog Catalog { get; } = catalog;

    public IEventBus Bus { get; } = bus;

    public UndoHistory History { get; } = new();

    public void BeginStroke(string description)
    {
        if (_stroke is not null)
        {
            throw new InvalidOperationException("A stroke is already in progress.");
        }

        _stroke = [];
        _strokeDescription = description;
    }

    public void EndStroke()
    {
        var stroke = _stroke ?? throw new InvalidOperationException("No stroke in progress.");
        _stroke = null;
        if (stroke.Count > 0)
        {
            History.Record(stroke.Count == 1 ? stroke[0] : new CompositeCommand(_strokeDescription, stroke));
            PublishHistory();
        }
    }

    public bool ChangeHistory(Func<bool> change)
    {
        if (_stroke is not null)
        {
            throw new InvalidOperationException("Finish the current stroke before undo/redo.");
        }

        var changed = change();
        if (changed)
        {
            PublishHistory();
        }

        return changed;
    }

    /// <summary>Executes the command and records it as one undo step (or as part of the open stroke).</summary>
    public void Run(IEditorCommand command)
    {
        command.Execute();
        Done(command);
    }

    public void Run(string description, Action execute, Action undo) => Run(new DelegateCommand(description, execute, undo));

    /// <summary>Records a command that has already run (one that had to run to learn whether it changes anything).</summary>
    public void Done(IEditorCommand command)
    {
        if (_stroke is not null)
        {
            _stroke.Add(command);
            return;
        }

        History.Record(command);
        PublishHistory();
    }

    /// <summary>
    /// Sets one value through <paramref name="set"/> as an undoable <see cref="PropertyEdit{T}"/>. Nothing happens (no undo step, no event) when
    /// <paramref name="next"/> equals the current value (<paramref name="equals"/>, else the default equality).
    /// </summary>
    public void Edit<T>(string description, Func<T> get, Action<T> set, T next, Func<T, T, bool>? equals = null)
    {
        if (PropertyEdit<T>.Create(description, get, set, next, equals) is { } edit)
        {
            Run(edit);
        }
    }

    public void Publish<TEvent>(TEvent @event)
        where TEvent : IEvent => Bus.Publish(@event);

    /// <summary>The scenario's object with the id when it is a <typeparamref name="TObject"/>, else null.</summary>
    public TObject? Object<TObject>(MapObjectId id)
        where TObject : MapObject => Scenario.Objects.FirstOrDefault(o => o.Id == id) as TObject;

    public Hero? Hero(HeroId id) => Scenario.Heroes.FirstOrDefault(h => h.Id == id);

    /// <summary>Adds the object at the end of the scenario's objects, or back at <paramref name="index"/> when undoing its removal.</summary>
    public void AddObject(MapObject mapObject, int? index = null)
    {
        Scenario.AddObject(mapObject, index);
        Bus.Publish(new MapObjectPlaced(mapObject.Id, mapObject.Definition.Id, mapObject.Position));
    }

    /// <summary>Removes the object; returns where it was, for an undo to put it back there (the order is the order rules visit objects in).</summary>
    public int RemoveObject(MapObject mapObject)
    {
        var index = Scenario.RemoveObject(mapObject);
        Bus.Publish(new MapObjectRemoved(mapObject.Id));
        return index;
    }

    /// <summary>Adds the hero at the end of the scenario's heroes, or back at <paramref name="index"/> when undoing its removal.</summary>
    public void AddHero(Hero hero, int? index = null)
    {
        Scenario.AddHero(hero, index);
        Bus.Publish(new HeroPlaced(hero.Id, hero.Owner, hero.Position));
    }

    /// <summary>Removes the hero; returns where it was, for an undo to put it back there.</summary>
    public int RemoveHero(Hero hero)
    {
        var index = Scenario.RemoveHero(hero);
        Bus.Publish(new HeroRemoved(hero.Id));
        return index;
    }

    /// <summary>Whether two lists hold the same items in the same order (no-op check for list-valued edits).</summary>
    public static bool SameItems<T>(IReadOnlyList<T> a, IReadOnlyList<T> b) => a.SequenceEqual(b);

    private void PublishHistory() => Bus.Publish(new UndoHistoryChanged(History.UndoDescription, History.RedoDescription));
}
