using Factory.Sim.Tests.Support;

namespace Factory.Sim.Tests;

/// <summary>Mechanics of a single segment: entry, travel, exit, and the relative-gap invariants.</summary>
public class BeltSegmentTests
{
    [Fact]
    public void NewBeltIsEmptyAndSizedFromLength()
    {
        BeltSegment belt = Scenario.Belt(tiles: 4, itemsPerMinute: BeltTiers.Mk3);

        Assert.True(belt.IsEmpty);
        Assert.Equal(0, belt.Count);
        // 4 tiles * 4 items per tile, plus the item sitting exactly on the output end.
        Assert.Equal(4 * SimConstants.ItemsPerTile + 1, belt.Capacity);
        Assert.Equal(belt.Length, belt.TailSpace);
    }

    [Fact]
    public void InsertedItemEntersAtTheRearEnd()
    {
        BeltSegment belt = Scenario.Belt(2, BeltTiers.Mk3);

        Assert.True(belt.TryAccept(TestItems.IronOre));

        Assert.Equal(1, belt.Count);
        Assert.Equal(belt.Length, belt.GapAt(0)); // distance from the output end
        Assert.Equal(0, belt.TailSpace);
        Assert.Equal(TestItems.IronOre, belt.ItemAt(0));
    }

    [Fact]
    public void ItemTravelsAtBeltSpeedAndStopsAtTheOutputEnd()
    {
        BeltSegment belt = Scenario.Belt(1, BeltTiers.Mk3); // 4800 units at 240 units/tick
        belt.Insert(TestItems.IronOre);

        for (int t = 0; t < 20; t++) belt.Tick();
        Assert.Equal(belt.Length - 20 * belt.Speed, belt.GapAt(0));

        // 4800 / 240 = 20 ticks to arrive; then it waits, because there is no Output.
        for (int t = 0; t < 50; t++) belt.Tick();
        Assert.Equal(0, belt.GapAt(0));
        Assert.Equal(1, belt.Count);
    }

    [Fact]
    public void PopOnlySucceedsOnceTheItemHasArrived()
    {
        BeltSegment belt = Scenario.Belt(1, BeltTiers.Mk3);
        belt.Insert(TestItems.IronOre);

        Assert.False(belt.TryPeek(out _));
        Assert.False(belt.TryPop(out _));

        for (int t = 0; t < 20; t++) belt.Tick();

        Assert.True(belt.TryPeek(out ItemId peeked));
        Assert.Equal(TestItems.IronOre, peeked);
        Assert.True(belt.TryPop(out ItemId popped));
        Assert.Equal(TestItems.IronOre, popped);
        Assert.True(belt.IsEmpty);
    }

    [Fact]
    public void ItemsNeverGetCloserThanItemSpacing()
    {
        BeltSegment belt = Scenario.Belt(3, BeltTiers.Mk5);

        for (int t = 0; t < 400; t++)
        {
            Scenario.FeedSaturated(belt, TestItems.IronOre);
            belt.Tick();

            for (int i = 1; i < belt.Count; i++)
                Assert.True(belt.GapAt(i) >= belt.ItemSpacing,
                    $"tick {t}: item {i} is {belt.GapAt(i)} from the item ahead, minimum is {belt.ItemSpacing}");

            if (belt.Count > 0) Assert.True(belt.GapAt(0) >= 0);
        }
    }

    [Fact]
    public void PositionsAreStrictlyAscendingAndWithinTheBelt()
    {
        BeltSegment belt = Scenario.Belt(3, BeltTiers.Mk2);
        Span<int> positions = stackalloc int[belt.Capacity];
        Span<ItemId> items = stackalloc ItemId[belt.Capacity];

        for (int t = 0; t < 500; t++)
        {
            Scenario.FeedSaturated(belt, TestItems.IronOre);
            belt.Tick();

            int n = belt.CopyTo(positions, items);
            Assert.Equal(belt.Count, n);

            int previous = -1;
            for (int i = 0; i < n; i++)
            {
                Assert.True(positions[i] > previous, $"tick {t}: positions must ascend");
                Assert.InRange(positions[i], 0, belt.Length);
                previous = positions[i];
            }
        }
    }

    [Fact]
    public void FullBeltRefusesInsteadOfDroppingItems()
    {
        BeltSegment belt = Scenario.Belt(2, BeltTiers.Mk4);

        long offered = 0;
        long accepted = 0;
        for (int t = 0; t < 1000; t++)
        {
            offered++;
            if (belt.TryAccept(TestItems.IronOre)) accepted++;
            belt.Tick(); // no Output: the belt is a dead end and must fill up
        }

        Assert.Equal(belt.Capacity, belt.Count);
        Assert.Equal(accepted, belt.TotalInserted);
        Assert.Equal(belt.Count, (int)belt.TotalInserted); // nothing popped, nothing lost
        Assert.True(offered > accepted, "a dead-end belt must eventually refuse");
    }

    [Fact]
    public void RejectsInvalidConfiguration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BeltSegment(0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BeltSegment(4800, 0));
        // At most one item may leave a belt per tick, so speed cannot outrun the spacing.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BeltSegment(4800, SimConstants.ItemSpacing + 1));
    }

    [Fact]
    public void InsertThrowsWhenItCannotFit()
    {
        BeltSegment belt = Scenario.Belt(1, BeltTiers.Mk3);
        belt.Insert(TestItems.IronOre);
        Assert.Throws<InvalidOperationException>(() => belt.Insert(TestItems.IronOre));
    }
}
