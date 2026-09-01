using Factory.Sim.Core;
using Factory.Sim.Items;

namespace Factory.Sim.Production;

/// <summary>
/// One typed input slot of a <see cref="Machine"/>, exposed as an <see cref="IItemSink"/>
/// so belts can connect to it exactly like they connect to another belt or a splitter
/// (<c>BeltNetwork.Connect(BeltSegment, IItemSink)</c>). Only accepts the one item type
/// its recipe slot requires — a belt carrying the wrong item simply stalls against it,
/// which is the same backpressure behaviour as every other sink in the sim.
/// </summary>
internal sealed class MachineInputPort : IItemSink
{
    private readonly Machine _machine;
    private readonly int _index;

    public MachineInputPort(Machine machine, int index)
    {
        _machine = machine;
        _index = index;
    }

    public bool CanAccept(ItemId item) => _machine.CanAcceptAt(_index, item);

    public bool TryAccept(ItemId item) => _machine.TryAcceptAt(_index, item);
}
