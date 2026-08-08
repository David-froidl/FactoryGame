namespace Factory.Sim.Tests;

/// <summary>
/// Guards the tick-rate quantisation rule. A saturated belt emits one item every
/// <c>ItemSpacing / speed</c> ticks; if that is not a whole number the belt silently
/// under-delivers. Equivalently, a rate is exact only if it divides 1200 items/min at
/// 20 Hz. Any new belt tier must satisfy this or the throughput tests will drift.
/// </summary>
public class BeltSpeedTests
{
    [Fact]
    public void EveryShippedBeltTierIsExactAtTheCurrentTickRate()
    {
        foreach (int tier in BeltTiers.All)
            Assert.True(SimConstants.IsExactRate(tier),
                $"{tier} items/min does not divide {60 * SimConstants.TicksPerSecond}; " +
                "it would under-deliver at max density. Pick a divisor or raise TicksPerSecond.");
    }

    [Fact]
    public void EveryShippedBeltTierHasAWholeNumberOfTicksPerItem()
    {
        foreach (int tier in BeltTiers.All)
        {
            int speed = SimConstants.ItemsPerMinuteToSpeed(tier);
            Assert.True(speed > 0);
            Assert.Equal(0, SimConstants.ItemSpacing % speed);
            Assert.True(speed <= SimConstants.ItemSpacing,
                "a belt may hand over at most one item per tick");
        }
    }

    [Theory]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(240)]
    [InlineData(400)]
    [InlineData(600)]
    [InlineData(1200)]
    public void SpeedConversionRoundTrips(int itemsPerMinute)
    {
        int speed = SimConstants.ItemsPerMinuteToSpeed(itemsPerMinute);
        Assert.Equal(itemsPerMinute, SimConstants.SpeedToItemsPerMinute(speed));
    }

    [Theory]
    [InlineData(270, false)] // Satisfactory's Mk3 rate: not representable exactly at 20 Hz
    [InlineData(480, false)]
    [InlineData(780, false)]
    [InlineData(300, true)]
    [InlineData(1200, true)]
    public void InexactRatesAreFlagged(int itemsPerMinute, bool expected)
        => Assert.Equal(expected, SimConstants.IsExactRate(itemsPerMinute));

    [Fact]
    public void ItemSpacingDividesATileEvenly()
        => Assert.Equal(0, SimConstants.UnitsPerTile % SimConstants.ItemSpacing);
}
