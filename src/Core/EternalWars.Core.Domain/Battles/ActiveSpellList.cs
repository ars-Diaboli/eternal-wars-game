using System.Collections.ObjectModel;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// A stack's buffs and curses (<see cref="BattleUnit.Spells"/>) with their sum (<see cref="Modifiers"/>) kept until the list changes: the sum is read
/// under every damage roll and AI estimate, so it is not folded again each time.
/// </summary>
internal sealed class ActiveSpellList : Collection<ActiveSpell>
{
    private SpellModifiers? _modifiers;

    /// <summary>What the spells change, all added up.</summary>
    public SpellModifiers Modifiers => _modifiers ??= Count == 0 ? SpellModifiers.None : this.Aggregate(SpellModifiers.None, (total, s) => total + s.Modifiers);

    public void AddRange(IEnumerable<ActiveSpell> spells)
    {
        foreach (var spell in spells.ToList())
        {
            Add(spell);
        }
    }

    public int RemoveAll(Func<ActiveSpell, bool> match)
    {
        var removed = 0;
        for (var i = Count - 1; i >= 0; i--)
        {
            if (match(this[i]))
            {
                RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    protected override void InsertItem(int index, ActiveSpell item)
    {
        base.InsertItem(index, item);
        _modifiers = null;
    }

    protected override void SetItem(int index, ActiveSpell item)
    {
        base.SetItem(index, item);
        _modifiers = null;
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        _modifiers = null;
    }

    protected override void ClearItems()
    {
        base.ClearItems();
        _modifiers = null;
    }
}
