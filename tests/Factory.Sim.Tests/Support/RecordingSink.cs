namespace Factory.Sim.Tests.Support;

/// <summary>
/// A sink that remembers everything it received, in order, and can be closed to simulate
/// a blocked downstream. Used to assert splitter fairness and merger priority precisely
/// rather than just counting totals.
/// </summary>
public sealed class RecordingSink : ISimNode, IItemSink
{
    private readonly List<ItemId> _received = new();

    public RecordingSink(string name = "sink") => Name = name;

    public string Name { get; }

    /// <summary>When false, every <see cref="TryAccept"/> fails — the whole chain must stall.</summary>
    public bool Open { get; set; } = true;

    /// <summary>Accept at most this many more items, then block. -1 means unlimited.</summary>
    public int RemainingCapacity { get; set; } = -1;

    public IReadOnlyList<ItemId> Received => _received;

    public int Count => _received.Count;

    public bool CanAccept(ItemId item) => Open && item.IsValid && RemainingCapacity != 0;

    public bool TryAccept(ItemId item)
    {
        if (!CanAccept(item)) return false;
        _received.Add(item);
        if (RemainingCapacity > 0) RemainingCapacity--;
        return true;
    }

    public void Tick() { }

    public int CountOf(ItemId item)
    {
        int n = 0;
        foreach (ItemId received in _received)
            if (received == item) n++;
        return n;
    }

    public void Reset() => _received.Clear();

    public override string ToString() => $"{Name}(received={_received.Count}, open={Open})";
}
