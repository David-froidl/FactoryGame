using Factory.Sim.Tests.Support;

namespace Factory.Sim.Tests;

/// <summary>
/// Merger priority. Input 0 wins whenever it has an item ready; lower-priority inputs get
/// the leftover tick capacity. That is deliberate, and it does mean a saturated high
/// priority input starves the rest — which the first test pins down explicitly.
/// </summary>
public class MergerTests
{
    [Fact]
    public void HighestPriorityInputWinsUnderContention()
    {
        var network = new BeltNetwork();
        // Mk6 == one item per tick, which is also the merger's ceiling, so input 0 alone
        // can saturate it and inputs 1 and 2 must get nothing.
        BeltSegment first = Scenario.Belt(3, BeltTiers.Mk6);
        BeltSegment second = Scenario.Belt(3, BeltTiers.Mk6);
        BeltSegment third = Scenario.Belt(3, BeltTiers.Mk6);
        var merger = new Merger();
        var sink = new RecordingSink();

        network.Connect(first, merger);
        network.Connect(second, merger);
        network.Connect(third, merger);
        network.Connect(merger, sink);

        Scenario.Run(network, 2000,
            (first, TestItems.IronOre), (second, TestItems.CopperOre), (third, TestItems.Screw));

        Assert.True(sink.Count > 1000);
        Assert.Equal(sink.Count, sink.CountOf(TestItems.IronOre));
        Assert.Equal(0, sink.CountOf(TestItems.CopperOre));
        Assert.Equal(0, sink.CountOf(TestItems.Screw));

        // The starved inputs must be backed up, not silently drained.
        Assert.Equal(second.Capacity, second.Count);
        Assert.Equal(third.Capacity, third.Count);
    }

    [Fact]
    public void LowerPriorityInputsUseTheSpareCapacity()
    {
        var network = new BeltNetwork();
        // Mk3 is 240/min == one item every five ticks, so four ticks in five are spare.
        BeltSegment first = Scenario.Belt(3, BeltTiers.Mk3);
        BeltSegment second = Scenario.Belt(3, BeltTiers.Mk3);
        var merger = new Merger();
        var sink = new RecordingSink();

        network.Connect(first, merger);
        network.Connect(second, merger);
        network.Connect(merger, sink);

        Scenario.Run(network, 3000, (first, TestItems.IronOre), (second, TestItems.CopperOre));

        int ore = sink.CountOf(TestItems.IronOre);
        int copper = sink.CountOf(TestItems.CopperOre);

        Assert.True(ore > 500, $"priority input should run at full rate, got {ore}");
        Assert.True(copper > 500, $"spare capacity should carry the second input, got {copper}");
        Assert.Equal(sink.Count, ore + copper);
    }

    [Fact]
    public void PriorityInputIsNeverDelayedByALowerPriorityOne()
    {
        var network = new BeltNetwork();
        BeltSegment first = Scenario.Belt(2, BeltTiers.Mk5);  // 600/min, one item every 2 ticks
        BeltSegment second = Scenario.Belt(2, BeltTiers.Mk6); // saturated low-priority input
        var merger = new Merger();
        var sink = new ItemVoid();

        network.Connect(first, merger);
        network.Connect(second, merger);
        network.Connect(merger, sink);

        Scenario.Run(network, 2000, (first, TestItems.IronOre), (second, TestItems.CopperOre));

        long before = first.TotalPopped;
        Scenario.Run(network, 1200, (first, TestItems.IronOre), (second, TestItems.CopperOre));

        // The high-priority belt still runs at its full rated throughput.
        Assert.Equal(BeltTiers.Mk5, first.TotalPopped - before);
    }

    [Fact]
    public void BlockedOutputStallsEveryInputWithoutLosingItems()
    {
        var network = new BeltNetwork();
        BeltSegment first = Scenario.Belt(2, BeltTiers.Mk3);
        BeltSegment second = Scenario.Belt(2, BeltTiers.Mk3);
        var merger = new Merger();
        var sink = new RecordingSink { Open = false };

        network.Connect(first, merger);
        network.Connect(second, merger);
        network.Connect(merger, sink);

        Scenario.Run(network, 3000, (first, TestItems.IronOre), (second, TestItems.CopperOre));

        Assert.Equal(0, sink.Count);
        Assert.Equal(first.Capacity, first.Count);
        Assert.Equal(second.Capacity, second.Count);
        // Exactly one item is in the merger's slot; nothing vanished in between.
        Assert.True(merger.Held.IsValid);
        Assert.Equal(1, first.TotalPopped + second.TotalPopped);
    }

    [Fact]
    public void MergerRoundTripsEverythingItTakes()
    {
        var network = new BeltNetwork();
        BeltSegment first = Scenario.Belt(2, BeltTiers.Mk2);
        BeltSegment second = Scenario.Belt(2, BeltTiers.Mk2);
        var merger = new Merger();
        var sink = new RecordingSink();

        network.Connect(first, merger);
        network.Connect(second, merger);
        network.Connect(merger, sink);

        Scenario.Run(network, 4000, (first, TestItems.IronOre), (second, TestItems.CopperOre));

        long taken = first.TotalPopped + second.TotalPopped;
        Assert.Equal(taken, sink.Count + (merger.Held.IsValid ? 1 : 0));
    }

    [Fact]
    public void PriorityFollowsInputOrderNotTickOrder()
    {
        // Same graph, inputs registered in the opposite order: the winner must flip.
        var network = new BeltNetwork();
        BeltSegment first = Scenario.Belt(3, BeltTiers.Mk6);
        BeltSegment second = Scenario.Belt(3, BeltTiers.Mk6);
        var merger = new Merger();
        var sink = new RecordingSink();

        network.Connect(second, merger); // registered first, so it now has priority
        network.Connect(first, merger);
        network.Connect(merger, sink);

        Scenario.Run(network, 2000, (first, TestItems.IronOre), (second, TestItems.CopperOre));

        Assert.Equal(sink.Count, sink.CountOf(TestItems.CopperOre));
        Assert.Equal(0, sink.CountOf(TestItems.IronOre));
    }
}
