using Factory.Sim.Items;

namespace Factory.Sim.Production;

/// <summary>
/// Static definition of one recipe: what it consumes, what it produces, and how long a
/// cycle takes in sim ticks. Always produced by <see cref="RecipeRegistry"/> from loaded
/// data (see <c>Factory.Sim.Data.GameDataLoader</c>) — never hand-written per machine.
///
/// Duration is ticks, not seconds: the sim never compares floats. Data may author a
/// duration in seconds for readability; conversion to ticks happens once, centrally, at
/// load time (see <c>GameDataLoader.SecondsToTicks</c>), so nothing downstream ever sees
/// a fractional tick count.
/// </summary>
public sealed class RecipeDefinition
{
    public RecipeDefinition(string id, IReadOnlyList<ItemStack> inputs, IReadOnlyList<ItemStack> outputs, int durationTicks)
    {
        Id = id;
        Inputs = inputs;
        Outputs = outputs;
        DurationTicks = durationTicks;
    }

    public string Id { get; }

    /// <summary>Consumed at cycle start. May be empty (e.g. a future extractor with no input).</summary>
    public IReadOnlyList<ItemStack> Inputs { get; }

    /// <summary>Produced at cycle completion. Never empty — a recipe must make something.</summary>
    public IReadOnlyList<ItemStack> Outputs { get; }

    public int DurationTicks { get; }

    public override string ToString() => $"{Id} ({Inputs.Count} in -> {Outputs.Count} out, {DurationTicks} ticks)";
}
