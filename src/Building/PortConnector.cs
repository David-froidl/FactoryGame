using Factory.Sim.Core;
using Factory.Sim.Production;
using FactoryGame.Game;
using FactoryGame.Render;
using FactoryGame.World;
using Godot;

namespace FactoryGame.Building;

/// <summary>
/// Wires a newly placed belt to whatever is near its two ends: another belt, a machine's
/// input port, a terminal, or a machine's output (if that machine isn't feeding anything
/// yet). Proximity-based, not real typed <see cref="PortDefinition"/> matching — none of
/// the shipped buildable data populates ports yet (<c>BuildableResource.Ports</c> is always
/// empty for now). Good enough for the vertical slice's golden path; a real port-based
/// version, positioned per <see cref="PortDefinition.LocalOffset"/>, is the natural next
/// step once port data exists — see the class-level note on <c>BuildableResource</c>.
///
/// A machine with more than one input port (the assembler) can't be disambiguated by
/// position alone without real port positions, so this just fills input ports in order —
/// tracked per machine in <see cref="_usedInputPorts"/> — as belts connect to it. Getting
/// the right ingredient onto the right physical belt is on the player; this doesn't check
/// item types match what a recipe slot expects.
/// </summary>
public partial class PortConnector : Node
{
    [Export] public float SnapDistance = 1.5f;

    private readonly Dictionary<Machine, HashSet<int>> _usedInputPorts = new();

    /// <summary>Call once, right after a belt has been added to the tree at its final transform.</summary>
    public void ConnectBeltEnds(BeltVisual3D belt, GameRoot gameRoot)
    {
        float lengthMetres = belt.Belt.Length / (float)Factory.Sim.SimConstants.UnitsPerTile;
        Vector3 outputPoint = belt.ToGlobal(new Vector3(0, 0, lengthMetres));
        Vector3 inputPoint = belt.GlobalPosition;

        ConnectOutputEnd(belt, outputPoint, gameRoot);
        ConnectInputEnd(belt, inputPoint, gameRoot);
    }

    private void ConnectOutputEnd(BeltVisual3D belt, Vector3 point, GameRoot gameRoot)
    {
        foreach (Node node in gameRoot.GetTree().GetNodesInGroup(BuildingGroups.PlacedBuildings))
        {
            if (node is not Node3D placed || placed.GlobalPosition.DistanceTo(point) > SnapDistance) continue;

            if (node is MachineVisual3D { Sim: { } machine } && NextFreeInputPort(machine) is int portIndex)
            {
                IItemSink target = machine.InputPort(portIndex);
                belt.Belt.Output = target;
                gameRoot.Network.RegisterFeed(belt.Belt, target);
                MarkPortUsed(machine, portIndex);
                return;
            }

            if (node is TerminalVisual3D { Sim: { } terminal })
            {
                belt.Belt.Output = terminal;
                gameRoot.Network.RegisterFeed(belt.Belt, terminal);
                return;
            }
        }

        foreach (Node node in gameRoot.GetTree().GetNodesInGroup(BuildingGroups.PlacedBelts))
        {
            if (node is not BeltVisual3D other || other == belt) continue;
            if (other.GlobalPosition.DistanceTo(point) > SnapDistance) continue;

            gameRoot.Network.Connect(belt.Belt, other.Belt);
            return;
        }
    }

    private void ConnectInputEnd(BeltVisual3D belt, Vector3 point, GameRoot gameRoot)
    {
        foreach (Node node in gameRoot.GetTree().GetNodesInGroup(BuildingGroups.PlacedBuildings))
        {
            if (node is not MachineVisual3D { Sim: { } machine } machineVisual) continue;
            if (machineVisual.GlobalPosition.DistanceTo(point) > SnapDistance) continue;
            if (machine.Output is not null) continue; // already feeding something else

            machine.Output = belt.Belt;
            gameRoot.Network.RegisterFeed(machine, belt.Belt);
            return;
        }
    }

    private int? NextFreeInputPort(Machine machine)
    {
        _usedInputPorts.TryGetValue(machine, out HashSet<int>? used);
        for (int i = 0; i < machine.InputPortCount; i++)
            if (used is null || !used.Contains(i)) return i;
        return null;
    }

    private void MarkPortUsed(Machine machine, int index)
    {
        if (!_usedInputPorts.TryGetValue(machine, out HashSet<int>? used))
            _usedInputPorts[machine] = used = new HashSet<int>();
        used.Add(index);
    }
}
