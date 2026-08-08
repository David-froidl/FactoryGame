using Factory.Sim.Tests.Support;

namespace Factory.Sim.Tests;

/// <summary>
/// Splitter fairness. The cursor advances only on a successful hand-off, which is what
/// makes the split both strictly even when everything is free and lossless when one
/// branch backs up.
/// </summary>
public class SplitterTests
{
    private static (BeltNetwork Network, Splitter Splitter, RecordingSink[] Outputs) Build(int outputs)
    {
        var network = new BeltNetwork();
        var splitter = network.Add(new Splitter());
        var sinks = new RecordingSink[outputs];
        for (int i = 0; i < outputs; i++)
        {
            sinks[i] = new RecordingSink($"out{i}");
            network.Connect(splitter, sinks[i]);
        }

        return (network, splitter, sinks);
    }

    /// <summary>Offers one item per tick, exactly as a saturated belt head would.</summary>
    private static void FeedAndRun(BeltNetwork network, Splitter splitter, int ticks, ItemId item)
    {
        for (int t = 0; t < ticks; t++)
        {
            splitter.TryAccept(item);
            network.Tick();
        }
    }

    [Fact]
    public void TwoFreeOutputsAlternateStrictly()
    {
        (BeltNetwork network, Splitter splitter, RecordingSink[] outputs) = Build(2);

        FeedAndRun(network, splitter, 60, TestItems.IronOre);

        Assert.Equal(outputs[0].Count, outputs[1].Count);
        Assert.True(outputs[0].Count > 0);
    }

    [Fact]
    public void ThreeFreeOutputsSplitExactlyEvenly()
    {
        (BeltNetwork network, Splitter splitter, RecordingSink[] outputs) = Build(3);

        FeedAndRun(network, splitter, 300, TestItems.IronOre);

        int total = outputs.Sum(o => o.Count);
        Assert.Equal(total / 3, outputs[0].Count);
        Assert.Equal(total / 3, outputs[1].Count);
        Assert.Equal(total / 3, outputs[2].Count);
    }

    [Fact]
    public void CursorVisitsOutputsInOrder()
    {
        (BeltNetwork network, Splitter splitter, RecordingSink[] outputs) = Build(3);

        // Tag each item so the exact interleaving is observable.
        for (int i = 0; i < 9; i++)
        {
            splitter.TryAccept(new ItemId((ushort)(100 + i)));
            network.Tick();
        }

        Assert.Equal(3, outputs[0].Count);
        Assert.Equal(3, outputs[1].Count);
        Assert.Equal(3, outputs[2].Count);

        // Item n went to output n % 3.
        for (int i = 0; i < 3; i++)
            for (int k = 0; k < 3; k++)
                Assert.Equal(new ItemId((ushort)(100 + i + k * 3)), outputs[i].Received[k]);
    }

    [Fact]
    public void BlockedOutputIsSkippedWithoutLosingThroughput()
    {
        (BeltNetwork network, Splitter splitter, RecordingSink[] outputs) = Build(3);
        outputs[1].Open = false;

        FeedAndRun(network, splitter, 300, TestItems.IronOre);

        Assert.Equal(0, outputs[1].Count);
        Assert.Equal(outputs[0].Count, outputs[2].Count);
        Assert.True(outputs[0].Count > 100, "the two open outputs must keep taking items");
    }

    [Fact]
    public void FairnessResumesAfterABlockedOutputReopens()
    {
        (BeltNetwork network, Splitter splitter, RecordingSink[] outputs) = Build(2);
        outputs[1].Open = false;

        FeedAndRun(network, splitter, 50, TestItems.IronOre);
        Assert.Equal(0, outputs[1].Count);

        outputs[1].Open = true;
        foreach (RecordingSink sink in outputs) sink.Reset();
        FeedAndRun(network, splitter, 200, TestItems.IronOre);

        Assert.Equal(outputs[0].Count, outputs[1].Count);
    }

    [Fact]
    public void SplitterHoldsAtMostOneItemAndStallsItsInput()
    {
        (BeltNetwork network, Splitter splitter, RecordingSink[] outputs) = Build(2);
        foreach (RecordingSink sink in outputs) sink.Open = false;

        int accepted = 0;
        for (int t = 0; t < 100; t++)
        {
            if (splitter.TryAccept(TestItems.IronOre)) accepted++;
            network.Tick();
        }

        Assert.Equal(1, accepted); // one item occupies the slot, everything after is refused
        Assert.Equal(0, outputs.Sum(o => o.Count));
        Assert.True(splitter.Held.IsValid);
        Assert.False(splitter.CanAccept(TestItems.IronOre));
    }

    [Fact]
    public void SplitterSplitsARealBeltEvenlyBetweenTwoBelts()
    {
        var network = new BeltNetwork();
        BeltSegment input = Scenario.Belt(3, BeltTiers.Mk3);
        var splitter = new Splitter();
        BeltSegment left = Scenario.Belt(3, BeltTiers.Mk3);
        BeltSegment right = Scenario.Belt(3, BeltTiers.Mk3);
        var leftSink = new ItemVoid();
        var rightSink = new ItemVoid();

        network.Connect(input, splitter);
        network.Connect(splitter, left);
        network.Connect(splitter, right);
        network.Connect(left, leftSink);
        network.Connect(right, rightSink);

        Scenario.Run(network, 6000, (input, TestItems.IronOre));

        // Dispatch alternates strictly, so the branches differ by at most the item in flight.
        Assert.InRange(left.TotalInserted - right.TotalInserted, 0, 1);
        Assert.True(leftSink.Consumed > 500 && rightSink.Consumed > 500);

        // Conservation: everything that left the input belt is somewhere downstream.
        Assert.Equal(input.TotalPopped,
            leftSink.Consumed + rightSink.Consumed + left.Count + right.Count
            + (splitter.Held.IsValid ? 1 : 0));
    }
}
