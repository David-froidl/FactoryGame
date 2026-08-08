namespace Factory.Sim.Items;

/// <summary>
/// A runtime handle for an item type. Deliberately a 16-bit value: belts store one of
/// these per item, so shrinking it directly shrinks the hot array the tick loop streams.
///
/// This is *not* the item definition. Names, icons, stack sizes and meshes live in data
/// (Godot Resources / JSON) and are resolved through a registry at load time; the sim
/// only ever sees the id.
/// </summary>
public readonly struct ItemId : IEquatable<ItemId>, IComparable<ItemId>
{
    /// <summary>Raw value. 0 is reserved for <see cref="None"/>.</summary>
    public readonly ushort Value;

    public ItemId(ushort value) => Value = value;

    /// <summary>The absence of an item. Equivalent to <c>default</c>.</summary>
    public static ItemId None => default;

    /// <summary>False only for <see cref="None"/>.</summary>
    public bool IsValid => Value != 0;

    public bool Equals(ItemId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is ItemId other && Equals(other);

    public override int GetHashCode() => Value;

    public int CompareTo(ItemId other) => Value.CompareTo(other.Value);

    public override string ToString() => IsValid ? $"Item#{Value}" : "Item#none";

    public static bool operator ==(ItemId a, ItemId b) => a.Value == b.Value;

    public static bool operator !=(ItemId a, ItemId b) => a.Value != b.Value;

    public static explicit operator ushort(ItemId id) => id.Value;

    public static explicit operator ItemId(ushort value) => new(value);
}
