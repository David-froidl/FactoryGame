namespace Factory.Sim.Items;

/// <summary>
/// An immutable (item, count) pair. Used for machine input/output buffers and inventories.
/// Belts do <b>not</b> use this — they store bare <see cref="ItemId"/>s plus spacing.
///
/// Immutable-with-returned-copies keeps buffer mutation explicit at call sites, which
/// makes machine logic much easier to reason about than in-place mutation.
/// </summary>
public readonly struct ItemStack : IEquatable<ItemStack>
{
    public readonly ItemId Item;
    public readonly int Count;

    public ItemStack(ItemId item, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Count cannot be negative.");
        // Normalise: a zero count is always the canonical empty stack.
        if (count == 0 || !item.IsValid)
        {
            Item = ItemId.None;
            Count = 0;
            return;
        }

        Item = item;
        Count = count;
    }

    public static ItemStack Empty => default;

    public bool IsEmpty => Count == 0;

    /// <summary>True when this stack holds <paramref name="item"/>, or is empty (so anything fits).</summary>
    public bool Accepts(ItemId item) => item.IsValid && (IsEmpty || Item == item);

    /// <summary>How many more of <paramref name="item"/> fit given <paramref name="capacity"/>.</summary>
    public int RoomFor(ItemId item, int capacity)
        => Accepts(item) ? Math.Max(0, capacity - Count) : 0;

    /// <summary>
    /// Adds <paramref name="count"/> of <paramref name="item"/> without exceeding
    /// <paramref name="capacity"/>. Returns false and leaves <paramref name="result"/>
    /// as this stack when it does not fit — the caller is expected to treat that as
    /// backpressure, never as a reason to drop the item.
    /// </summary>
    public bool TryAdd(ItemId item, int count, int capacity, out ItemStack result)
    {
        if (count <= 0 || !Accepts(item) || Count + count > capacity)
        {
            result = this;
            return false;
        }

        result = new ItemStack(item, Count + count);
        return true;
    }

    /// <summary>Removes <paramref name="count"/> items, or fails if there are not that many.</summary>
    public bool TryRemove(int count, out ItemStack result)
    {
        if (count <= 0 || count > Count)
        {
            result = this;
            return false;
        }

        result = new ItemStack(Item, Count - count);
        return true;
    }

    public ItemStack WithCount(int count) => new(Item, count);

    public bool Equals(ItemStack other) => Item == other.Item && Count == other.Count;

    public override bool Equals(object? obj) => obj is ItemStack other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Item, Count);

    public override string ToString() => IsEmpty ? "<empty>" : $"{Item} x{Count}";

    public static bool operator ==(ItemStack a, ItemStack b) => a.Equals(b);

    public static bool operator !=(ItemStack a, ItemStack b) => !a.Equals(b);
}
