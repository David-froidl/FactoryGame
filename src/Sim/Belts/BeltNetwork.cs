using Factory.Sim.Core;
using Factory.Sim.Items;

namespace Factory.Sim.Belts;

/// <summary>
/// Owns a set of belt nodes and the order they tick in.
///
/// Tick order is <b>downstream-first</b> (reverse topological). That single decision is
/// what makes a jam clear along an entire chain within one tick instead of crawling
/// backwards one segment per tick: by the time an upstream belt tries to hand its head
/// item over, the belt in front has already advanced and freed the space.
///
/// Belt loops are legal and expected, so a cycle is not an error: nodes left over after
/// the topological sort are appended in insertion order. Inside a loop the tick order is
/// arbitrary but stable, which costs at most one tick of latency somewhere in the ring.
/// </summary>
public sealed class BeltNetwork
{
    private readonly List<ISimNode> _nodes = new();
    private readonly Dictionary<ISimNode, int> _index = new(ReferenceComparer.Instance);
    private readonly List<List<int>> _successors = new();

    private ISimNode[] _tickOrder = Array.Empty<ISimNode>();
    private bool _orderDirty = true;

    /// <summary>Number of ticks simulated since construction. Part of the save state.</summary>
    public long TickCount { get; private set; }

    public IReadOnlyList<ISimNode> Nodes => _nodes;

    /// <summary>Registers a node, or returns it unchanged if already registered.</summary>
    public T Add<T>(T node) where T : ISimNode
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_index.ContainsKey(node)) return node;

        _index[node] = _nodes.Count;
        _nodes.Add(node);
        _successors.Add(new List<int>(2));
        _orderDirty = true;
        return node;
    }

    /// <summary>Belt pushes its head item into <paramref name="to"/> (another belt, a splitter, a machine).</summary>
    public void Connect(BeltSegment from, IItemSink to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (ReferenceEquals(from, to)) throw new ArgumentException("A belt cannot feed itself.", nameof(to));
        if (from.Output is not null && !ReferenceEquals(from.Output, to))
            throw new InvalidOperationException("Belt already has an output. Insert a splitter to fan out.");

        from.Output = to;
        LinkSink(from, to);
    }

    /// <summary>Adds a round-robin output to a splitter.</summary>
    public void Connect(Splitter from, IItemSink to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        from.AddOutput(to);
        LinkSink(from, to);
    }

    /// <summary>Sets the merger's single output.</summary>
    public void Connect(Merger from, IItemSink to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        from.Output = to;
        LinkSink(from, to);
    }

    /// <summary>
    /// Adds a pull-side input to a merger, at the lowest priority so far. Belts wired this
    /// way must not also push, or the item would be handed over twice.
    /// </summary>
    public void Connect(IItemSource from, Merger to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (from is BeltSegment { Output: not null })
            throw new InvalidOperationException(
                "A belt feeding a merger must not have an Output: the merger pulls from it.");

        to.AddInput(from);
        if (from is ISimNode node) Link(node, to);
    }

    /// <summary>Steps every node exactly once, downstream-first.</summary>
    public void Tick()
    {
        if (_orderDirty) RebuildTickOrder();

        ISimNode[] order = _tickOrder;
        for (int i = 0; i < order.Length; i++) order[i].Tick();

        TickCount++;
    }

    /// <summary>The resolved downstream-first order. Exposed for tests and debugging.</summary>
    public IReadOnlyList<ISimNode> TickOrder
    {
        get
        {
            if (_orderDirty) RebuildTickOrder();
            return _tickOrder;
        }
    }

    /// <summary>A sink that is not an <see cref="ISimNode"/> never ticks, so it needs no ordering.</summary>
    private void LinkSink(ISimNode from, IItemSink to)
    {
        Add(from);
        if (to is ISimNode toNode) Link(from, toNode);
    }

    private void Link(ISimNode from, ISimNode to)
    {
        Add(from);
        Add(to);
        List<int> successors = _successors[_index[from]];
        int toIndex = _index[to];
        if (!successors.Contains(toIndex)) successors.Add(toIndex);
        _orderDirty = true;
    }

    /// <summary>Kahn's algorithm, then reversed. Cyclic remainder keeps insertion order.</summary>
    private void RebuildTickOrder()
    {
        int n = _nodes.Count;
        var inDegree = new int[n];
        foreach (List<int> successors in _successors)
            foreach (int s in successors)
                inDegree[s]++;

        var upstreamFirst = new List<ISimNode>(n);
        var visited = new bool[n];
        var ready = new Queue<int>();
        for (int i = 0; i < n; i++)
            if (inDegree[i] == 0) ready.Enqueue(i);

        while (ready.Count > 0)
        {
            int i = ready.Dequeue();
            visited[i] = true;
            upstreamFirst.Add(_nodes[i]);
            foreach (int s in _successors[i])
                if (--inDegree[s] == 0) ready.Enqueue(s);
        }

        // Anything still unvisited is inside a cycle (a belt loop). Append deterministically.
        for (int i = 0; i < n; i++)
            if (!visited[i]) upstreamFirst.Add(_nodes[i]);

        upstreamFirst.Reverse();
        _tickOrder = upstreamFirst.ToArray();
        _orderDirty = false;
    }

    /// <summary>Nodes are identified by reference; none of them override Equals.</summary>
    private sealed class ReferenceComparer : IEqualityComparer<ISimNode>
    {
        public static readonly ReferenceComparer Instance = new();

        public bool Equals(ISimNode? x, ISimNode? y) => ReferenceEquals(x, y);

        public int GetHashCode(ISimNode obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
