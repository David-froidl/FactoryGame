using Factory.Sim.Tests.Support;

namespace Factory.Sim.Tests;

public class ItemStackTests
{
    [Fact]
    public void DefaultStackIsEmpty()
    {
        ItemStack stack = ItemStack.Empty;
        Assert.True(stack.IsEmpty);
        Assert.Equal(0, stack.Count);
        Assert.False(stack.Item.IsValid);
    }

    [Fact]
    public void ZeroCountNormalisesToEmpty()
    {
        var stack = new ItemStack(TestItems.IronOre, 0);
        Assert.True(stack.IsEmpty);
        Assert.Equal(ItemStack.Empty, stack);
    }

    [Fact]
    public void AddRespectsCapacityAndRefusesRatherThanTruncating()
    {
        var stack = new ItemStack(TestItems.IronOre, 8);

        Assert.True(stack.TryAdd(TestItems.IronOre, 2, capacity: 10, out ItemStack filled));
        Assert.Equal(10, filled.Count);

        Assert.False(filled.TryAdd(TestItems.IronOre, 1, capacity: 10, out ItemStack unchanged));
        Assert.Equal(filled, unchanged);
    }

    [Fact]
    public void StackHoldsOneItemTypeAtATime()
    {
        var stack = new ItemStack(TestItems.IronOre, 1);
        Assert.False(stack.Accepts(TestItems.CopperOre));
        Assert.False(stack.TryAdd(TestItems.CopperOre, 1, 10, out _));
        Assert.Equal(0, stack.RoomFor(TestItems.CopperOre, 10));
    }

    [Fact]
    public void EmptyStackAcceptsAnything()
    {
        ItemStack stack = ItemStack.Empty;
        Assert.True(stack.Accepts(TestItems.CopperOre));
        Assert.Equal(10, stack.RoomFor(TestItems.CopperOre, 10));
        Assert.True(stack.TryAdd(TestItems.CopperOre, 3, 10, out ItemStack result));
        Assert.Equal(new ItemStack(TestItems.CopperOre, 3), result);
    }

    [Fact]
    public void RemoveFailsWhenThereIsNotEnough()
    {
        var stack = new ItemStack(TestItems.Screw, 2);
        Assert.False(stack.TryRemove(3, out ItemStack unchanged));
        Assert.Equal(stack, unchanged);

        Assert.True(stack.TryRemove(2, out ItemStack emptied));
        Assert.True(emptied.IsEmpty);
    }

    [Fact]
    public void NegativeCountIsRejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ItemStack(TestItems.IronOre, -1));

    [Fact]
    public void ItemIdNoneIsTheDefaultAndIsInvalid()
    {
        Assert.False(ItemId.None.IsValid);
        Assert.Equal(default, ItemId.None);
        Assert.True(TestItems.IronOre.IsValid);
        Assert.NotEqual(TestItems.IronOre, TestItems.CopperOre);
    }
}
