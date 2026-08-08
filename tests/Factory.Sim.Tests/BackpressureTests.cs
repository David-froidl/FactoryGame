using Factory.Sim.Tests.Support;

namespace Factory.Sim.Tests;

/// <summary>
/// Backpressure is the one thing a factory sim must never get wrong: a full belt has to
/// stall whatever is upstream, and items must be conserved exactly. Every test here
/// checks conservation as well as the stall, because a silent item drop looks like
/// correct behaviour from the outside until the player's ratios stop adding up.
/// </summary>
public class BackpressureTests
{
    [Fact]
    public void BlockedOutputStallsTheUpstreamFeederWithoutLosingItems()
    {
        BeltSegment belt = Scenario.Belt(4, BeltTiers.Mk3);
        var sink = new ItemVoid { Open = false };
        var network = new BeltNetwork();
        network.Connect(belt, sink);

        long offered = 0;
        long accepted = 0;
        for (int t = 0; t < 3000; t++)
        {
            offered++;
            if (belt.TryAccept(TestItems.IronOre)) accepted++;
            network.Tick();
        }

        Assert.Equal(0, sink.Consumed);
        Assert.Equal(belt.Capacity, belt.Count);
        Assert.Equal(accepted, belt.Count + belt.TotalPopped); // every accepted item still exists
        Assert.True(offered > accepted, "the feeder must have been refused once the belt filled");
    }

    [Fact]
    public void ReopeningTheOutputDrainsEverythingThatWasHeld()
    {
        BeltSegment belt = Scenario.Belt(4, BeltTiers.Mk3);
        var sink = new ItemVoid { Open = false };
        var network = new BeltNetwork();
        network.Connect(belt, sink);

        Scenario.Run(network, 3000, (belt, TestItems.IronOre));
        int held = belt.Count;
        Assert.Equal(belt.Capacity, held);

        sink.Open = true;
        for (int t = 0; t < 3000; t++) network.Tick(); // no more feeding

        Assert.True(belt.IsEmpty);
        Assert.Equal(held, sink.Consumed);
    }

    [Fact]
    public void JamPropagatesBackwardsThroughAChainAndItemsAreConserved()
    {
        var network = new BeltNetwork();
        BeltSegment a = Scenario.Belt(2, BeltTiers.Mk3);
        BeltSegment b = Scenario.Belt(2, BeltTiers.Mk3);
        BeltSegment c = Scenario.Belt(2, BeltTiers.Mk3);
        var sink = new ItemVoid { Open = false };

        network.Connect(a, b);
        network.Connect(b, c);
        network.Connect(c, sink);

        long accepted = 0;
        for (int t = 0; t < 5000; t++)
        {
            if (a.TryAccept(TestItems.IronOre)) accepted++;
            network.Tick();
        }

        Assert.Equal(a.Capacity, a.Count);
        Assert.Equal(b.Capacity, b.Count);
        Assert.Equal(c.Capacity, c.Count);
        Assert.Equal(0, sink.Consumed);
        Assert.Equal(accepted, a.Count + b.Count + c.Count + sink.Consumed);
    }

    [Fact]
    public void UnjammingDeliversAtFullRateFromTheVeryFirstTick()
    {
        // Downstream-first tick order means freed space is consumed on the same tick it
        // appears. If the order were upstream-first the chain would stutter while the
        // "there is room now" signal walked backwards one segment per tick.
        var network = new BeltNetwork();
        BeltSegment a = Scenario.Belt(2, BeltTiers.Mk6); // Mk6 == one item per tick
        BeltSegment b = Scenario.Belt(2, BeltTiers.Mk6);
        BeltSegment c = Scenario.Belt(2, BeltTiers.Mk6);
        var sink = new ItemVoid { Open = false };

        network.Connect(a, b);
        network.Connect(b, c);
        network.Connect(c, sink);

        Scenario.Run(network, 3000, (a, TestItems.IronOre));
        int held = a.Count + b.Count + c.Count;
        Assert.Equal(a.Capacity + b.Capacity + c.Capacity, held);

        sink.Open = true;
        for (int t = 1; t <= held; t++)
        {
            network.Tick(); // deliberately no further feeding
            Assert.Equal(t, sink.Consumed);
        }

        Assert.True(a.IsEmpty && b.IsEmpty && c.IsEmpty);
        Assert.Equal(held, sink.Consumed);
    }

    [Fact]
    public void PartiallyBlockedSinkThrottlesTheBeltToTheSinkRate()
    {
        BeltSegment belt = Scenario.Belt(4, BeltTiers.Mk6);
        var sink = new RecordingSink { RemainingCapacity = 10 };
        var network = new BeltNetwork();
        network.Connect(belt, sink);

        long accepted = 0;
        for (int t = 0; t < 4000; t++)
        {
            if (belt.TryAccept(TestItems.IronOre)) accepted++;
            network.Tick();
        }

        Assert.Equal(10, sink.Count);
        Assert.Equal(belt.Capacity, belt.Count);
        Assert.Equal(accepted, belt.Count + belt.TotalPopped);
    }

    [Fact]
    public void ADeadEndBeltNeverExceedsItsCapacity()
    {
        BeltSegment belt = Scenario.Belt(8, BeltTiers.Mk5);

        for (int t = 0; t < 10_000; t++)
        {
            Scenario.FeedSaturated(belt, TestItems.IronOre);
            belt.Tick();
            Assert.True(belt.Count <= belt.Capacity);
        }

        Assert.Equal(belt.Capacity, belt.Count);
    }
}
