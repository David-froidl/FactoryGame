using Factory.Sim.Tests.Support;

namespace Factory.Sim.Tests;

/// <summary>Wiring rules and the downstream-first tick order the whole sim depends on.</summary>
public class BeltNetworkTests
{
    [Fact]
    public void TickOrderIsDownstreamFirst()
    {
        var network = new BeltNetwork();
        BeltSegment a = Scenario.Belt(1, BeltTiers.Mk3);
        BeltSegment b = Scenario.Belt(1, BeltTiers.Mk3);
        BeltSegment c = Scenario.Belt(1, BeltTiers.Mk3);
        var sink = new ItemVoid();

        network.Connect(a, b);
        network.Connect(b, c);
        network.Connect(c, sink);

        IReadOnlyList<ISimNode> order = network.TickOrder;
        Assert.Equal(new ISimNode[] { sink, c, b, a }, order);
    }

    [Fact]
    public void ConnectingABeltTwiceIsRejected()
    {
        var network = new BeltNetwork();
        BeltSegment a = Scenario.Belt(1, BeltTiers.Mk3);
        BeltSegment b = Scenario.Belt(1, BeltTiers.Mk3);
        BeltSegment c = Scenario.Belt(1, BeltTiers.Mk3);

        network.Connect(a, b);
        Assert.Throws<InvalidOperationException>(() => network.Connect(a, c));
    }

    [Fact]
    public void ABeltFeedingAMergerMustNotAlsoPush()
    {
        var network = new BeltNetwork();
        BeltSegment a = Scenario.Belt(1, BeltTiers.Mk3);
        BeltSegment b = Scenario.Belt(1, BeltTiers.Mk3);
        var merger = new Merger();

        network.Connect(a, b);
        Assert.Throws<InvalidOperationException>(() => network.Connect(a, merger));
    }

    [Fact]
    public void ABeltCannotFeedItself()
    {
        var network = new BeltNetwork();
        BeltSegment a = Scenario.Belt(1, BeltTiers.Mk3);
        Assert.Throws<ArgumentException>(() => network.Connect(a, a));
    }

    [Fact]
    public void BeltLoopsAreLegalAndConserveItems()
    {
        // A closed ring: no source, no sink. Items must circulate forever, never duplicate,
        // never vanish. Cycles make a topological sort impossible, so this also proves the
        // cycle fallback in the tick-order builder is safe.
        var network = new BeltNetwork();
        BeltSegment a = Scenario.Belt(2, BeltTiers.Mk3);
        BeltSegment b = Scenario.Belt(2, BeltTiers.Mk3);
        BeltSegment c = Scenario.Belt(2, BeltTiers.Mk3);

        network.Connect(a, b);
        network.Connect(b, c);
        network.Connect(c, a);

        // Items enter one at a time: a belt only has room at its tail once the previous
        // item has travelled a full ItemSpacing.
        int loaded = 0;
        for (int t = 0; t < 500 && loaded < 5; t++)
        {
            if (a.TryAccept(new ItemId((ushort)(10 + loaded)))) loaded++;
            network.Tick();
        }

        Assert.Equal(5, loaded);
        int total = a.Count + b.Count + c.Count;

        for (int t = 0; t < 5000; t++)
        {
            network.Tick();
            Assert.Equal(total, a.Count + b.Count + c.Count);
        }

        Assert.True(a.TotalPopped > 0 && b.TotalPopped > 0 && c.TotalPopped > 0,
            "items must actually go round the loop");
    }

    [Fact]
    public void TickCountAdvances()
    {
        var network = new BeltNetwork();
        network.Add(new ItemVoid());
        for (int i = 0; i < 7; i++) network.Tick();
        Assert.Equal(7, network.TickCount);
    }

    [Fact]
    public void AddIsIdempotent()
    {
        var network = new BeltNetwork();
        BeltSegment a = Scenario.Belt(1, BeltTiers.Mk3);
        network.Add(a);
        network.Add(a);
        Assert.Single(network.Nodes);
    }
}
