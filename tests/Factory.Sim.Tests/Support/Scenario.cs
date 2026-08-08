namespace Factory.Sim.Tests.Support;

/// <summary>Small builders so the tests read as scenarios rather than wiring.</summary>
public static class Scenario
{
    public const int Tile = SimConstants.UnitsPerTile;

    public static BeltSegment Belt(int tiles, int itemsPerMinute)
        => new(tiles * Tile, SimConstants.ItemsPerMinuteToSpeed(itemsPerMinute));

    /// <summary>
    /// Pushes as many items onto the belt's tail as physically fit right now. A saturated
    /// upstream machine behaves exactly like this: it offers, and accepts a refusal.
    /// Returns how many were accepted this call (0 or 1 in practice).
    /// </summary>
    public static int FeedSaturated(BeltSegment belt, ItemId item)
    {
        int accepted = 0;
        while (belt.TryAccept(item)) accepted++;
        return accepted;
    }

    /// <summary>Ticks the network, feeding each listed belt beforehand.</summary>
    public static void Run(BeltNetwork network, int ticks, params (BeltSegment Belt, ItemId Item)[] feeds)
    {
        for (int t = 0; t < ticks; t++)
        {
            foreach ((BeltSegment belt, ItemId item) in feeds) FeedSaturated(belt, item);
            network.Tick();
        }
    }
}
