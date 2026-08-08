using Factory.Sim.Tests.Support;

namespace Factory.Sim.Tests;

/// <summary>
/// Throughput at maximum density. These are exact-equality assertions on purpose: belt
/// rates are the core arithmetic of a factory game, and a belt that quietly delivers 96%
/// of its rating would poison every ratio the player computes.
/// </summary>
public class ThroughputTests
{
    private const int OneMinuteInTicks = 60 * SimConstants.TicksPerSecond;

    [Theory]
    [InlineData(BeltTiers.Mk1)]
    [InlineData(BeltTiers.Mk2)]
    [InlineData(BeltTiers.Mk3)]
    [InlineData(BeltTiers.Mk4)]
    [InlineData(BeltTiers.Mk5)]
    [InlineData(BeltTiers.Mk6)]
    public void SaturatedBeltDeliversExactlyItsRatedItemsPerMinute(int itemsPerMinute)
    {
        BeltSegment belt = Scenario.Belt(tiles: 5, itemsPerMinute);
        var sink = new ItemVoid();
        var network = new BeltNetwork();
        network.Connect(belt, sink);

        // Warm up past the fill time (5 tiles at the slowest tier is 400 ticks).
        Scenario.Run(network, 1000, (belt, TestItems.IronOre));

        long before = sink.Consumed;
        Scenario.Run(network, OneMinuteInTicks, (belt, TestItems.IronOre));

        Assert.Equal(itemsPerMinute, sink.Consumed - before);
    }

    [Fact]
    public void ChainOfSegmentsKeepsFullThroughput()
    {
        // Splitting one long belt into segments must not cost throughput, only latency.
        var network = new BeltNetwork();
        BeltSegment a = Scenario.Belt(3, BeltTiers.Mk3);
        BeltSegment b = Scenario.Belt(3, BeltTiers.Mk3);
        BeltSegment c = Scenario.Belt(3, BeltTiers.Mk3);
        var sink = new ItemVoid();

        network.Connect(a, b);
        network.Connect(b, c);
        network.Connect(c, sink);

        Scenario.Run(network, 2000, (a, TestItems.IronOre));

        long before = sink.Consumed;
        Scenario.Run(network, OneMinuteInTicks, (a, TestItems.IronOre));

        Assert.Equal(BeltTiers.Mk3, sink.Consumed - before);
    }

    [Fact]
    public void SaturatedBeltPacksToFullCapacityWhenTheOutputStops()
    {
        BeltSegment belt = Scenario.Belt(6, BeltTiers.Mk3);
        var sink = new ItemVoid { Open = false };
        var network = new BeltNetwork();
        network.Connect(belt, sink);

        Scenario.Run(network, 5000, (belt, TestItems.IronOre));

        Assert.Equal(belt.Capacity, belt.Count);
        Assert.Equal(0, sink.Consumed);

        // Max density means every gap is exactly ItemSpacing and the head is on the end.
        Assert.Equal(0, belt.GapAt(0));
        for (int i = 1; i < belt.Count; i++) Assert.Equal(belt.ItemSpacing, belt.GapAt(i));
    }

    [Fact]
    public void SlowBeltFeedingFastBeltIsLimitedByTheSlowOne()
    {
        var network = new BeltNetwork();
        BeltSegment slow = Scenario.Belt(3, BeltTiers.Mk1);
        BeltSegment fast = Scenario.Belt(3, BeltTiers.Mk6);
        var sink = new ItemVoid();

        network.Connect(slow, fast);
        network.Connect(fast, sink);

        Scenario.Run(network, 3000, (slow, TestItems.IronOre));

        long before = sink.Consumed;
        Scenario.Run(network, OneMinuteInTicks, (slow, TestItems.IronOre));

        Assert.Equal(BeltTiers.Mk1, sink.Consumed - before);
    }

    [Fact]
    public void FastBeltFeedingSlowBeltIsLimitedByTheSlowOneAndBacksUp()
    {
        var network = new BeltNetwork();
        BeltSegment fast = Scenario.Belt(3, BeltTiers.Mk6);
        BeltSegment slow = Scenario.Belt(3, BeltTiers.Mk1);
        var sink = new ItemVoid();

        network.Connect(fast, slow);
        network.Connect(slow, sink);

        Scenario.Run(network, 3000, (fast, TestItems.IronOre));

        long before = sink.Consumed;
        Scenario.Run(network, OneMinuteInTicks, (fast, TestItems.IronOre));

        Assert.Equal(BeltTiers.Mk1, sink.Consumed - before);
        // The fast belt is the one that has to absorb the mismatch. It momentarily drops
        // one below capacity on the tick after it hands an item to the slow belt.
        Assert.InRange(fast.Count, fast.Capacity - 1, fast.Capacity);
    }
}
