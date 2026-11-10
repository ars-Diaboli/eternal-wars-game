namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// An undoable change of one value: <see cref="Execute"/> sets the new value, <see cref="Undo"/> the one it had before. The setter also publishes
/// whatever the change means for views, so redo and undo publish the same events as the edit.
/// </summary>
internal sealed class PropertyEdit<T> : IEditorCommand
{
    private readonly T _previous;
    private readonly T _next;
    private readonly Action<T> _set;

    private PropertyEdit(string description, T previous, T next, Action<T> set)
    {
        Description = description;
        (_previous, _next, _set) = (previous, next, set);
    }

    public string Description { get; }

    /// <summary>
    /// An edit from the current value (<paramref name="get"/>) to <paramref name="next"/>, or null when they are equal (<paramref name="equals"/>,
    /// else the default equality): a no-op edit is not one.
    /// </summary>
    public static PropertyEdit<T>? Create(string description, Func<T> get, Action<T> set, T next, Func<T, T, bool>? equals = null)
    {
        var current = get();
        var same = equals ?? EqualityComparer<T>.Default.Equals;
        return same(current, next) ? null : new PropertyEdit<T>(description, current, next, set);
    }

    public void Execute() => _set(_next);

    public void Undo() => _set(_previous);
}
