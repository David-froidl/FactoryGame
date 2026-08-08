namespace Factory.Sim.Tests.Support;

/// <summary>
/// Item ids used by the tests. Real definitions become data in Phase 1; the sim only ever
/// sees the id, so tests can invent their own without touching a registry.
/// </summary>
public static class TestItems
{
    public static readonly ItemId IronOre = new(1);
    public static readonly ItemId IronIngot = new(2);
    public static readonly ItemId CopperOre = new(3);
    public static readonly ItemId Screw = new(4);
}
