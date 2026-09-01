using Factory.Sim.Core;
using Factory.Sim.Items;
using Factory.Sim.Production;

namespace Factory.Sim.Tests.Support;

/// <summary>Small builders so machine tests read as scenarios rather than wiring.</summary>
public static class ProductionScenario
{
    /// <summary>A single-input, single-output recipe (smelter-shaped).</summary>
    public static RecipeDefinition SingleInputRecipe(
        string id, ItemId inputItem, int inputCount, ItemId outputItem, int outputCount, int durationTicks)
        => new(id,
            new[] { new ItemStack(inputItem, inputCount) },
            new[] { new ItemStack(outputItem, outputCount) },
            durationTicks);

    /// <summary>A two-input, single-output recipe (assembler-shaped).</summary>
    public static RecipeDefinition TwoInputRecipe(
        string id,
        ItemId inputA, int countA,
        ItemId inputB, int countB,
        ItemId outputItem, int outputCount, int durationTicks)
        => new(id,
            new[] { new ItemStack(inputA, countA), new ItemStack(inputB, countB) },
            new[] { new ItemStack(outputItem, outputCount) },
            durationTicks);

    /// <summary>A zero-input recipe (extractor-shaped): produces on its own, given room.</summary>
    public static RecipeDefinition ZeroInputRecipe(string id, ItemId outputItem, int outputCount, int durationTicks)
        => new(id, Array.Empty<ItemStack>(), new[] { new ItemStack(outputItem, outputCount) }, durationTicks);

    /// <summary>Feeds one item into an input port as many times as it will currently accept.</summary>
    public static int FeedSaturated(IItemSink port, ItemId item)
    {
        int accepted = 0;
        while (port.TryAccept(item)) accepted++;
        return accepted;
    }
}
