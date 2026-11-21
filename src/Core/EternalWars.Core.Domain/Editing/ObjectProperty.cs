using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// What one field shows over the objects being edited together (EDIT-1, ADR-255): the value they all have, or that they differ
/// (the inspector then shows "—").
/// </summary>
public readonly record struct SharedValue<T>(bool Differs, T Value)
{
    /// <summary>The value every one of <paramref name="values"/> has, or <see cref="Differs"/> (no values: differs).</summary>
    public static SharedValue<T> Of(IEnumerable<T> values, IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        comparer ??= EqualityComparer<T>.Default;
        using var each = values.GetEnumerator();
        if (!each.MoveNext())
        {
            return new SharedValue<T>(true, default!);
        }

        var first = each.Current;
        while (each.MoveNext())
        {
            if (!comparer.Equals(first, each.Current))
            {
                return new SharedValue<T>(true, first);
            }
        }

        return new SharedValue<T>(false, first);
    }
}

/// <summary>
/// One field of one kind of object (EDIT-1, ADR-255): how it is read from an object and written to it through the session's editors.
/// The write takes the object as it is, so a field that an editor sets together with others (a monster's creature, count and
/// disposition) keeps each object's own other values. <see cref="Apply"/> writes one value to several objects as one undo step.
/// </summary>
public sealed class ObjectProperty<TObject, TValue>(
    string name,
    Func<TObject, TValue> read,
    Func<MapEditorSession, TObject, TValue, EditResult> write,
    IEqualityComparer<TValue>? comparer = null)
    where TObject : MapObject
{
    /// <summary>The field's name, also the undo step's ("Set disposition of 4 objects").</summary>
    public string Name { get; } = name;

    public TValue Read(TObject mapObject) => read(mapObject);

    /// <summary>The value all of <paramref name="objects"/> have, or that they differ.</summary>
    public SharedValue<TValue> Shared(IEnumerable<TObject> objects) => SharedValue<TValue>.Of(objects.Select(read), comparer);

    /// <summary>
    /// Writes <paramref name="value"/> to every one of <paramref name="objects"/> as one undo step (<see cref="MapEditorSession.EditAll"/>):
    /// all of them or, when one is refused, none.
    /// </summary>
    public EditResult Apply(MapEditorSession editor, IReadOnlyCollection<TObject> objects, TValue value)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(objects);
        return editor.EditAll(
            objects.Count == 1 ? $"Set {Name}" : $"Set {Name} of {objects.Count} objects",
            [.. objects.Select(o => o.Id)],
            o => o is TObject typed ? write(editor, typed, value) : EditResult.InvalidTarget);
    }
}
