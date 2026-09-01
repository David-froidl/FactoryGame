using Factory.Sim.Core;
using Factory.Sim.Items;

namespace Factory.Sim.Production;

/// <summary>
/// One generic production building, driven entirely by its <see cref="RecipeDefinition"/>.
/// This is the single class behind the extractor, smelter and assembler alike (CLAUDE.md
/// rule #4: "nothing about a specific machine type should require a new C# class") — an
/// extractor is simply a <see cref="Machine"/> whose recipe has zero inputs.
///
/// Cycle algorithm, run once per <see cref="Tick"/>:
/// <list type="number">
/// <item>If mid-cycle, count down; on reaching zero, move the recipe's output into the
/// output buffer (<see cref="OutputCount"/>) and immediately try to start the next cycle
/// in the same tick, so back-to-back cycles complete exactly <see cref="RecipeDefinition.DurationTicks"/>
/// ticks apart with no lost tick.</item>
/// <item>Otherwise, start a new cycle only if every input slot already holds enough items
/// <b>and</b> the output buffer would have room for the result. Starting a cycle is the
/// only place inputs are consumed — never before a cycle validly starts, and the output
/// room check happens before consuming, so a completed cycle can never have nowhere to
/// put its output.</item>
/// <item>Always try to push one item from the output buffer to <see cref="Output"/>, same
/// as a <c>BeltSegment</c> hands off its head item — at most one item leaves per tick.</item>
/// </list>
///
/// Every input port only accepts the one item type its recipe slot names; <see cref="Status"/>
/// is a live, derived read of the buffers, never a stored value that could drift out of sync.
/// </summary>
public sealed class Machine : ISimNode
{
    private readonly int[] _inputCounts;
    private readonly IItemSink[] _inputPorts;
    private int _outputCount;
    private int _progressTicks;

    public Machine(RecipeDefinition recipe, int inputCapacityPerSlot, int outputCapacity)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (recipe.Outputs.Count != 1)
            throw new ArgumentException(
                $"Machine only supports single-output recipes; '{recipe.Id}' has {recipe.Outputs.Count} outputs.",
                nameof(recipe));
        if (inputCapacityPerSlot <= 0)
            throw new ArgumentOutOfRangeException(nameof(inputCapacityPerSlot), inputCapacityPerSlot, "Must be positive.");
        if (outputCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputCapacity), outputCapacity, "Must be positive.");

        for (int i = 0; i < recipe.Inputs.Count; i++)
        {
            if (recipe.Inputs[i].Count > inputCapacityPerSlot)
                throw new ArgumentException(
                    $"Recipe '{recipe.Id}' input slot {i} needs {recipe.Inputs[i].Count} per cycle, " +
                    $"which exceeds inputCapacityPerSlot ({inputCapacityPerSlot}).",
                    nameof(inputCapacityPerSlot));
        }

        if (recipe.Outputs[0].Count > outputCapacity)
            throw new ArgumentException(
                $"Recipe '{recipe.Id}' produces {recipe.Outputs[0].Count} per cycle, " +
                $"which exceeds outputCapacity ({outputCapacity}).",
                nameof(outputCapacity));

        Recipe = recipe;
        InputCapacityPerSlot = inputCapacityPerSlot;
        OutputCapacity = outputCapacity;

        _inputCounts = new int[recipe.Inputs.Count];
        _inputPorts = new IItemSink[recipe.Inputs.Count];
        for (int i = 0; i < recipe.Inputs.Count; i++)
            _inputPorts[i] = new MachineInputPort(this, i);
    }

    public RecipeDefinition Recipe { get; }

    /// <summary>Shared capacity of every input slot, in items.</summary>
    public int InputCapacityPerSlot { get; }

    /// <summary>Capacity of the single output buffer, in items.</summary>
    public int OutputCapacity { get; }

    /// <summary>Where finished output is pushed. Null means nothing is connected yet.</summary>
    public IItemSink? Output { get; set; }

    public bool IsOutputConnected => Output is not null;

    public int InputPortCount => _inputPorts.Length;

    /// <summary>Items currently sitting in the output buffer, waiting to be pushed.</summary>
    public int OutputCount => _outputCount;

    /// <summary>Ticks remaining in the current cycle, or 0 when not producing.</summary>
    public int ProgressTicks => _progressTicks;

    /// <summary>Lifetime count of completed production cycles. Debug/UI data, and a test hook.</summary>
    public long TotalCyclesCompleted { get; private set; }

    public MachineStatus Status
    {
        get
        {
            if (_progressTicks > 0) return MachineStatus.Producing;
            if (!HasRoomForCycleOutput()) return MachineStatus.OutputBlocked;
            if (!HasSufficientInput()) return MachineStatus.WaitingForInput;
            return MachineStatus.Idle;
        }
    }

    /// <summary>How many items are currently buffered in input slot <paramref name="index"/>.</summary>
    public int InputCount(int index)
    {
        if ((uint)index >= (uint)_inputCounts.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return _inputCounts[index];
    }

    /// <summary>
    /// The sink for input slot <paramref name="index"/> — connect a belt to this exactly
    /// like connecting it to another belt or a splitter.
    /// </summary>
    public IItemSink InputPort(int index)
    {
        if ((uint)index >= (uint)_inputPorts.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return _inputPorts[index];
    }

    public void Tick()
    {
        if (_progressTicks > 0)
        {
            _progressTicks--;
            if (_progressTicks == 0) CompleteCycle();
        }

        if (_progressTicks == 0) TryStartCycle();

        TryPushOutput();
    }

    internal bool CanAcceptAt(int index, ItemId item)
        => item.IsValid && item == Recipe.Inputs[index].Item && _inputCounts[index] < InputCapacityPerSlot;

    internal bool TryAcceptAt(int index, ItemId item)
    {
        if (!CanAcceptAt(index, item)) return false;
        _inputCounts[index]++;
        return true;
    }

    private bool HasSufficientInput()
    {
        for (int i = 0; i < _inputCounts.Length; i++)
            if (_inputCounts[i] < Recipe.Inputs[i].Count) return false;
        return true;
    }

    private bool HasRoomForCycleOutput() => _outputCount + Recipe.Outputs[0].Count <= OutputCapacity;

    private void TryStartCycle()
    {
        if (!HasSufficientInput() || !HasRoomForCycleOutput()) return;

        for (int i = 0; i < _inputCounts.Length; i++)
            _inputCounts[i] -= Recipe.Inputs[i].Count;

        _progressTicks = Recipe.DurationTicks;
    }

    private void CompleteCycle()
    {
        _outputCount += Recipe.Outputs[0].Count;
        TotalCyclesCompleted++;
    }

    private void TryPushOutput()
    {
        if (_outputCount <= 0 || Output is null) return;
        if (Output.TryAccept(Recipe.Outputs[0].Item)) _outputCount--;
    }

    public override string ToString()
        => $"Machine({Recipe.Id}, {Status}, progress={_progressTicks}/{Recipe.DurationTicks}, out={_outputCount}/{OutputCapacity})";
}
