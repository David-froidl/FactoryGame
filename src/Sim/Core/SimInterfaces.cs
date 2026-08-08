using Factory.Sim.Items;

namespace Factory.Sim.Core;

/// <summary>Anything the <c>BeltNetwork</c> steps once per simulation tick.</summary>
public interface ISimNode
{
    /// <summary>
    /// Advance this node by exactly one tick. Called at most once per network tick, in
    /// downstream-first order so a jam clears along a whole chain within a single tick.
    /// </summary>
    void Tick();
}

/// <summary>
/// Something that can receive an item: a belt's tail, a splitter, a machine input buffer.
///
/// <see cref="TryAccept"/> returning false is the one and only backpressure mechanism in
/// the sim. Callers must keep hold of the item and retry — never drop it.
/// </summary>
public interface IItemSink
{
    /// <summary>Non-mutating test. True if a matching <see cref="TryAccept"/> would succeed now.</summary>
    bool CanAccept(ItemId item);

    /// <summary>Takes ownership of <paramref name="item"/>, or returns false and takes nothing.</summary>
    bool TryAccept(ItemId item);
}

/// <summary>
/// Something an item can be pulled out of. Used by pull-style nodes (a merger picking
/// between inputs by priority); push-style links go through <see cref="IItemSink"/> instead.
/// </summary>
public interface IItemSource
{
    /// <summary>Non-mutating look at the next item that would be handed over.</summary>
    bool TryPeek(out ItemId item);

    /// <summary>Removes and returns the next item, or returns false and removes nothing.</summary>
    bool TryTake(out ItemId item);
}
