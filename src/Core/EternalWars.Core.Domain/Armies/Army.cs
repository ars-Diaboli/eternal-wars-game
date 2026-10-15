namespace EternalWars.Core.Domain.Armies;

/// <summary>A number of creatures of one type occupying one army slot.</summary>
public sealed record CreatureStack
{
    public CreatureStack(string creature, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creature);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        Creature = creature;
        Count = count;
    }

    /// <summary>Creature definition id.</summary>
    public string Creature { get; }

    public int Count { get; }

    public void Deconstruct(out string creature, out int count) => (creature, count) = (Creature, Count);

    public override string ToString() => $"{Count}× {Creature}";
}

/// <summary>A hero's army: <see cref="SlotCount"/> slots, each empty or holding one <see cref="CreatureStack"/>.</summary>
public sealed class Army
{
    public const int SlotCount = 7;

    private readonly CreatureStack?[] _slots = new CreatureStack?[SlotCount];

    public Army(IEnumerable<CreatureStack?>? slots = null)
    {
        var index = 0;
        foreach (var stack in slots ?? [])
        {
            if (index >= SlotCount)
            {
                throw new ArgumentException($"An army has at most {SlotCount} slots.", nameof(slots));
            }

            _slots[index++] = stack;
        }
    }

    /// <summary>Builds an army with the given stacks in the first slots.</summary>
    public static Army Of(params CreatureStack[] stacks) => new(stacks);

    public IReadOnlyList<CreatureStack?> Slots => _slots;

    public CreatureStack? this[int slot] => _slots[slot];

    /// <summary>Occupied slots in slot order.</summary>
    public IEnumerable<(int Slot, CreatureStack Stack)> Stacks =>
        _slots.Select((stack, slot) => (slot, stack)).Where(s => s.stack is not null).Select(s => (s.slot, s.stack!));

    public int StackCount => _slots.Count(s => s is not null);

    public bool IsEmpty => StackCount == 0;

    /// <summary>Whether a stack of <paramref name="creature"/> could be added (an equal stack or a free slot).</summary>
    public bool CanAccept(string creature) => _slots.Any(s => s is null || s.Creature == creature);

    public static bool IsValidSlot(int slot) => slot is >= 0 and < SlotCount;

    internal void Set(int slot, CreatureStack? stack) => _slots[slot] = stack;

    /// <summary>Merges into a stack of the same creature, else uses the first free slot.</summary>
    internal bool TryAdd(CreatureStack stack)
    {
        var same = Array.FindIndex(_slots, s => s?.Creature == stack.Creature);
        var slot = same >= 0 ? same : Array.IndexOf(_slots, null);
        if (slot < 0)
        {
            return false;
        }

        _slots[slot] = new CreatureStack(stack.Creature, (_slots[slot]?.Count ?? 0) + stack.Count);
        return true;
    }
}
