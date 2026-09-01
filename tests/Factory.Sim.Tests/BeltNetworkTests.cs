using Factory.Sim.Production;
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

    // ---- RegisterFeed: for source types (e.g. Machine) that manage Output directly ----

    [Fact]
    public void RegisterFeedOrdersASourceBeforeItsSinkDownstreamFirst()
    {
        var network = new BeltNetwork();
        var machine = new Machine(ProductionScenario.ZeroInputRecipe("extract", new ItemId(1), 1, 1), inputCapacityPerSlot: 5, outputCapacity: 5);
        BeltSegment belt = Scenario.Belt(1, BeltTiers.Mk3);

        machine.Output = belt;
        network.RegisterFeed(machine, belt);
        network.Connect(belt, new ItemVoid());

        IReadOnlyList<ISimNode> order = network.TickOrder;
        int beltIndex = -1, machineIndex = -1;
        for (int i = 0; i < order.Count; i++)
        {
            if (ReferenceEquals(order[i], belt)) beltIndex = i;
            if (ReferenceEquals(order[i], machine)) machineIndex = i;
        }

        Assert.True(beltIndex >= 0 && machineIndex >= 0, "both nodes must be in the tick order");
        Assert.True(beltIndex < machineIndex, "the belt (downstream) must tick before the machine (upstream) that feeds it");
    }

    [Fact]
    public void RegisterFeedActuallyMovesItemsEndToEnd()
    {
        var network = new BeltNetwork();
        var recipe = ProductionScenario.ZeroInputRecipe("extract", new ItemId(1), 1, 1);
        var machine = new Machine(recipe, inputCapacityPerSlot: 5, outputCapacity: 5);
        BeltSegment belt = Scenario.Belt(1, BeltTiers.Mk3);
        var sink = new ItemVoid();

        machine.Output = belt;
        network.RegisterFeed(machine, belt);
        network.Connect(belt, sink);

        for (int t = 0; t < 200; t++) network.Tick();

        Assert.True(sink.Consumed > 0, "an item produced by the machine must actually arrive at the belt's sink");
    }

    [Fact]
    public void RegisterFeedRejectsNullArguments()
    {
        var network = new BeltNetwork();
        BeltSegment belt = Scenario.Belt(1, BeltTiers.Mk3);
        var machine = new Machine(ProductionScenario.ZeroInputRecipe("extract", new ItemId(1), 1, 1), inputCapacityPerSlot: 5, outputCapacity: 5);

        Assert.Throws<ArgumentNullException>(() => network.RegisterFeed(null!, belt));
        Assert.Throws<ArgumentNullException>(() => network.RegisterFeed(machine, null!));
    }
}
