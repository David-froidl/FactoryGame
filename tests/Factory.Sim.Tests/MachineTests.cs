using Factory.Sim.Data;
using Factory.Sim.Production;
using Factory.Sim.Tests.Support;

namespace Factory.Sim.Tests;

public class MachineTests
{
    private static readonly ItemId Ore = new(1);
    private static readonly ItemId Ingot = new(2);
    private static readonly ItemId CopperIngot = new(3);
    private static readonly ItemId Core = new(4);

    // ---- Core production rules ----

    [Fact]
    public void MachineDoesNotProduceWithoutInput()
    {
        var machine = new Machine(ProductionScenario.SingleInputRecipe("smelt", Ore, 1, Ingot, 1, 3), inputCapacityPerSlot: 5, outputCapacity: 5);

        for (int i = 0; i < 10; i++) machine.Tick();

        Assert.Equal(MachineStatus.WaitingForInput, machine.Status);
        Assert.Equal(0, machine.OutputCount);
        Assert.Equal(0, machine.TotalCyclesCompleted);
    }

    [Fact]
    public void InputIsConsumedOnlyWhenCycleValidlyStarts()
    {
        var machine = new Machine(ProductionScenario.SingleInputRecipe("smelt", Ore, 1, Ingot, 1, 3), inputCapacityPerSlot: 5, outputCapacity: 5);
        machine.InputPort(0).TryAccept(Ore);

        // Fed, but not yet ticked: must still be sitting in the input buffer, untouched.
        Assert.Equal(1, machine.InputCount(0));

        machine.Tick();

        Assert.Equal(0, machine.InputCount(0));
        Assert.Equal(MachineStatus.Producing, machine.Status);
    }

    [Fact]
    public void RecipeConsumesInputsInExactRatio()
    {
        RecipeDefinition recipe = ProductionScenario.TwoInputRecipe("assemble", Ingot, 2, CopperIngot, 1, Core, 1, 2);
        var machine = new Machine(recipe, inputCapacityPerSlot: 5, outputCapacity: 5);

        machine.InputPort(0).TryAccept(Ingot);
        machine.InputPort(0).TryAccept(Ingot);
        machine.InputPort(1).TryAccept(CopperIngot);

        machine.Tick(); // starts the cycle: sets the timer, does not itself count down
        Assert.Equal(0, machine.InputCount(0));
        Assert.Equal(0, machine.InputCount(1));

        // Completion is exactly `duration` ticks after the start tick (2 more: 2 -> 1 -> 0).
        machine.Tick();
        machine.Tick();
        Assert.Equal(1, machine.OutputCount);
    }

    [Fact]
    public void RecipeProducesExactOutput()
    {
        RecipeDefinition recipe = ProductionScenario.SingleInputRecipe("smelt", Ore, 1, Ingot, 1, 1);
        var machine = new Machine(recipe, inputCapacityPerSlot: 5, outputCapacity: 5);

        machine.InputPort(0).TryAccept(Ore);
        machine.Tick(); // starts (duration = 1, sets the timer)
        machine.Tick(); // counts down 1 -> 0, completes

        Assert.Equal(1, machine.OutputCount);
        Assert.Equal(1, machine.TotalCyclesCompleted);
    }

    /// <summary>
    /// Completion happens exactly <c>duration</c> ticks after the tick a cycle starts
    /// (the start tick sets the timer but does not itself count down) — so a cold start
    /// needs <c>duration + 1</c> total <see cref="Machine.Tick"/> calls before output appears.
    /// </summary>
    [Fact]
    public void CycleCompletesExactlyAfterDurationTicks()
    {
        const int duration = 5;
        var machine = new Machine(ProductionScenario.SingleInputRecipe("smelt", Ore, 1, Ingot, 1, duration), inputCapacityPerSlot: 5, outputCapacity: 5);
        machine.InputPort(0).TryAccept(Ore);

        machine.Tick(); // starts the cycle on this tick
        for (int i = 0; i < duration; i++)
        {
            Assert.Equal(0, machine.OutputCount);
            machine.Tick();
        }

        Assert.Equal(1, machine.OutputCount);
    }

    [Fact]
    public void MultiInputRecipeWaitsForEveryInputSlot()
    {
        RecipeDefinition recipe = ProductionScenario.TwoInputRecipe("assemble", Ingot, 2, CopperIngot, 1, Core, 1, 2);
        var machine = new Machine(recipe, inputCapacityPerSlot: 5, outputCapacity: 5);

        machine.InputPort(0).TryAccept(Ingot);
        machine.InputPort(0).TryAccept(Ingot);
        // Copper ingot slot still empty.
        machine.Tick();

        Assert.Equal(MachineStatus.WaitingForInput, machine.Status);
        Assert.Equal(0, machine.ProgressTicks);

        machine.InputPort(1).TryAccept(CopperIngot);
        machine.Tick();

        Assert.Equal(MachineStatus.Producing, machine.Status);
    }

    // ---- Output backpressure ----

    [Fact]
    public void MachinePausesWhenOutputBufferIsFull()
    {
        var machine = new Machine(ProductionScenario.SingleInputRecipe("smelt", Ore, 1, Ingot, 1, 1), inputCapacityPerSlot: 5, outputCapacity: 1);
        // No Output sink attached: the buffer can never drain.
        ProductionScenario.FeedSaturated(machine.InputPort(0), Ore);

        machine.Tick(); // starts the first cycle (duration = 1)
        machine.Tick(); // completes it, fills the 1-slot output buffer
        Assert.Equal(1, machine.OutputCount);

        int before = machine.InputCount(0);
        machine.Tick();
        machine.Tick();

        Assert.Equal(MachineStatus.OutputBlocked, machine.Status);
        Assert.Equal(1, machine.OutputCount); // never exceeds capacity
        Assert.Equal(1, machine.TotalCyclesCompleted); // no further cycle started
        Assert.Equal(before, machine.InputCount(0)); // held input was never consumed for a cycle that couldn't complete
    }

    [Fact]
    public void BlockedOutputResumesOnceDownstreamAcceptsAgain()
    {
        var machine = new Machine(ProductionScenario.SingleInputRecipe("smelt", Ore, 1, Ingot, 1, 1), inputCapacityPerSlot: 5, outputCapacity: 1);
        var sink = new ItemVoid { Open = false };
        machine.Output = sink;
        ProductionScenario.FeedSaturated(machine.InputPort(0), Ore);

        machine.Tick();
        machine.Tick();
        Assert.Equal(MachineStatus.OutputBlocked, machine.Status);
        Assert.Equal(0, sink.Consumed);

        sink.Open = true;
        machine.Tick();

        Assert.Equal(1, sink.Consumed);
        Assert.Equal(0, machine.OutputCount);
    }

    [Fact]
    public void IsOutputConnectedReflectsWhetherASinkIsAttached()
    {
        var machine = new Machine(ProductionScenario.SingleInputRecipe("smelt", Ore, 1, Ingot, 1, 1), inputCapacityPerSlot: 5, outputCapacity: 5);
        Assert.False(machine.IsOutputConnected);

        machine.Output = new ItemVoid();
        Assert.True(machine.IsOutputConnected);
    }

    // ---- Conservation: no duplication, no loss ----

    [Fact]
    public void NoItemsAreDuplicatedOrLostOverManyTicks()
    {
        RecipeDefinition recipe = ProductionScenario.SingleInputRecipe("smelt", Ore, 1, Ingot, 1, 3);
        var machine = new Machine(recipe, inputCapacityPerSlot: 100, outputCapacity: 100);
        var sink = new ItemVoid();
        machine.Output = sink;

        int fed = 0;
        for (int t = 0; t < 200; t++)
        {
            fed += ProductionScenario.FeedSaturated(machine.InputPort(0), Ore);
            machine.Tick();
        }

        // A cycle's input is consumed at start, before it shows up as completed output — so
        // one cycle can be "in flight" (already consumed, not yet produced) when the loop
        // stops. Account for it, or the tally looks like a lost item when none was lost.
        int workInProgress = machine.ProgressTicks > 0 ? recipe.Inputs[0].Count : 0;
        Assert.Equal(fed, machine.TotalCyclesCompleted * recipe.Inputs[0].Count + machine.InputCount(0) + workInProgress);
        Assert.Equal(machine.TotalCyclesCompleted * recipe.Outputs[0].Count, sink.Consumed + machine.OutputCount);
    }

    // ---- Zero-input (extractor-shaped) recipes ----

    [Fact]
    public void ZeroInputRecipeProducesContinuouslyWithoutAnyInput()
    {
        var machine = new Machine(ProductionScenario.ZeroInputRecipe("extract", Ore, 1, 2), inputCapacityPerSlot: 5, outputCapacity: 100);
        var sink = new ItemVoid();
        machine.Output = sink;

        for (int i = 0; i < 20; i++) machine.Tick();

        Assert.True(machine.TotalCyclesCompleted > 0);
        Assert.Equal(machine.TotalCyclesCompleted, sink.Consumed);
    }

    [Fact]
    public void ZeroInputRecipeBlocksWhenOutputIsFull()
    {
        var machine = new Machine(ProductionScenario.ZeroInputRecipe("extract", Ore, 1, 1), inputCapacityPerSlot: 5, outputCapacity: 2);

        for (int i = 0; i < 10; i++) machine.Tick();

        Assert.Equal(MachineStatus.OutputBlocked, machine.Status);
        Assert.Equal(2, machine.OutputCount);
    }

    // ---- Constructor validation ----

    [Fact]
    public void ConstructorRejectsMultiOutputRecipes()
    {
        var recipe = new RecipeDefinition("bad",
            Array.Empty<ItemStack>(),
            new[] { new ItemStack(Ore, 1), new ItemStack(Ingot, 1) },
            10);

        Assert.Throws<ArgumentException>(() => new Machine(recipe, 5, 5));
    }

    [Fact]
    public void ConstructorRejectsInputCapacitySmallerThanPerCycleRequirement()
    {
        RecipeDefinition recipe = ProductionScenario.SingleInputRecipe("smelt", Ore, 3, Ingot, 1, 10);
        Assert.Throws<ArgumentException>(() => new Machine(recipe, inputCapacityPerSlot: 2, outputCapacity: 5));
    }

    [Fact]
    public void ConstructorRejectsOutputCapacitySmallerThanPerCycleYield()
    {
        RecipeDefinition recipe = ProductionScenario.SingleInputRecipe("smelt", Ore, 1, Ingot, 3, 10);
        Assert.Throws<ArgumentException>(() => new Machine(recipe, inputCapacityPerSlot: 5, outputCapacity: 2));
    }

    [Fact]
    public void InputPortThrowsForOutOfRangeIndex()
    {
        var machine = new Machine(ProductionScenario.SingleInputRecipe("smelt", Ore, 1, Ingot, 1, 3), inputCapacityPerSlot: 5, outputCapacity: 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => machine.InputPort(1));
    }

    [Fact]
    public void InputPortRejectsWrongItemType()
    {
        var machine = new Machine(ProductionScenario.SingleInputRecipe("smelt", Ore, 1, Ingot, 1, 3), inputCapacityPerSlot: 5, outputCapacity: 5);
        Assert.False(machine.InputPort(0).TryAccept(CopperIngot));
        Assert.Equal(0, machine.InputCount(0));
    }

    // ---- Integration: a Machine built from the real, checked-in Phase 1 data ----

    [Fact]
    public void FerriteSmeltingRecipeFromProductionDataConvertsOreToIngot()
    {
        GameData data = GameDataLoader.LoadGameData(
            RepoPaths.DataFile("items", "items.json"),
            RepoPaths.DataFile("recipes", "recipes.json"));
        RecipeDefinition recipe = data.Recipes.Get("ferrite_smelting");
        ItemId ore = data.Items.Get("ferrite_ore").Id;
        ItemId ingot = data.Items.Get("ferrite_ingot").Id;

        var machine = new Machine(recipe, inputCapacityPerSlot: 5, outputCapacity: 5);
        var sink = new ItemVoid();
        machine.Output = sink;

        Assert.True(machine.InputPort(0).TryAccept(ore));
        for (int i = 0; i <= recipe.DurationTicks; i++) machine.Tick(); // start tick + full duration

        Assert.Equal(1, machine.TotalCyclesCompleted);
        Assert.Equal(1, sink.Consumed);
        Assert.Equal(ingot, recipe.Outputs[0].Item);
    }

    [Fact]
    public void FerriteExtractionRecipeFromProductionDataNeedsNoInput()
    {
        GameData data = GameDataLoader.LoadGameData(
            RepoPaths.DataFile("items", "items.json"),
            RepoPaths.DataFile("recipes", "recipes.json"));
        RecipeDefinition recipe = data.Recipes.Get("ferrite_extraction");

        var machine = new Machine(recipe, inputCapacityPerSlot: 5, outputCapacity: 5);
        var sink = new ItemVoid();
        machine.Output = sink;

        Assert.Empty(recipe.Inputs);
        for (int i = 0; i <= recipe.DurationTicks; i++) machine.Tick();

        Assert.Equal(1, machine.TotalCyclesCompleted);
        Assert.Equal(1, sink.Consumed);
    }
}
