using Factory.Sim.Production;

namespace Factory.Sim.Tests;

public class MilestoneTerminalTests
{
    private static readonly ItemId AssemblyCore = new(1);
    private static readonly ItemId FerriteIngot = new(2);

    private static MilestoneTerminal MakeTerminal(UnlockState? unlockState = null, int requiredCount = 10)
    {
        var definition = new MilestoneDefinition("belt_mk2_unlock", AssemblyCore, requiredCount, "belt_mk2");
        return new MilestoneTerminal(definition, unlockState ?? new UnlockState());
    }

    [Fact]
    public void DeliveringFewerThanRequiredDoesNotUnlock()
    {
        MilestoneTerminal terminal = MakeTerminal();

        for (int i = 0; i < 9; i++) Assert.True(terminal.TryAccept(AssemblyCore));

        Assert.False(terminal.IsThresholdMet);
        Assert.False(terminal.UnlockState.IsUnlocked("belt_mk2"));
    }

    [Fact]
    public void UnlockFiresOnExactlyTheRequiredDelivery()
    {
        MilestoneTerminal terminal = MakeTerminal(requiredCount: 10);

        for (int i = 0; i < 9; i++) terminal.TryAccept(AssemblyCore);
        Assert.False(terminal.UnlockState.IsUnlocked("belt_mk2"));

        terminal.TryAccept(AssemblyCore); // the 10th
        Assert.True(terminal.UnlockState.IsUnlocked("belt_mk2"));
        Assert.Equal(10, terminal.DeliveredCount);
        Assert.True(terminal.IsThresholdMet);
    }

    [Fact]
    public void UnlockEventFiresExactlyOnceEvenWithDeliveriesPastTheThreshold()
    {
        var unlockState = new UnlockState();
        MilestoneTerminal terminal = MakeTerminal(unlockState, requiredCount: 3);
        int fireCount = 0;
        unlockState.Unlocked += _ => fireCount++;

        for (int i = 0; i < 8; i++) terminal.TryAccept(AssemblyCore);

        Assert.Equal(1, fireCount);
        Assert.Equal(8, terminal.DeliveredCount); // keeps counting deliveries past the threshold
    }

    [Fact]
    public void DeliveredCountIsNotCappedAtRequiredCount()
    {
        MilestoneTerminal terminal = MakeTerminal(requiredCount: 2);

        for (int i = 0; i < 5; i++) terminal.TryAccept(AssemblyCore);

        Assert.Equal(5, terminal.DeliveredCount);
    }

    [Fact]
    public void RejectsWrongItemTypeAndDoesNotCountIt()
    {
        MilestoneTerminal terminal = MakeTerminal();

        Assert.False(terminal.CanAccept(FerriteIngot));
        Assert.False(terminal.TryAccept(FerriteIngot));
        Assert.Equal(0, terminal.DeliveredCount);
    }

    [Fact]
    public void TickIsANoOp()
    {
        MilestoneTerminal terminal = MakeTerminal();
        terminal.TryAccept(AssemblyCore);

        terminal.Tick();
        terminal.Tick();

        Assert.Equal(1, terminal.DeliveredCount);
        Assert.False(terminal.IsThresholdMet);
    }

    // ---- UnlockState ----

    [Fact]
    public void UnlockStateIsUnlockedIsFalseByDefault()
    {
        var unlockState = new UnlockState();
        Assert.False(unlockState.IsUnlocked("belt_mk2"));
    }

    [Fact]
    public void UnlockStateUnlockReturnsFalseWhenAlreadyUnlocked()
    {
        var unlockState = new UnlockState();
        Assert.True(unlockState.Unlock("belt_mk2"));
        Assert.False(unlockState.Unlock("belt_mk2"));
        Assert.True(unlockState.IsUnlocked("belt_mk2"));
    }

    [Fact]
    public void UnlockStateTracksMultipleIndependentUnlocksById()
    {
        var unlockState = new UnlockState();
        unlockState.Unlock("belt_mk2");

        Assert.True(unlockState.IsUnlocked("belt_mk2"));
        Assert.False(unlockState.IsUnlocked("belt_mk3"));
        Assert.Contains("belt_mk2", unlockState.All);
    }

    [Fact]
    public void TwoTerminalsSharingAnUnlockStateUnlockIndependently()
    {
        var unlockState = new UnlockState();
        MilestoneTerminal cores = MakeTerminal(unlockState, requiredCount: 2);
        var stoneDefinition = new MilestoneDefinition("other_unlock", FerriteIngot, 3, "other_thing");
        var ingots = new MilestoneTerminal(stoneDefinition, unlockState);

        cores.TryAccept(AssemblyCore);
        cores.TryAccept(AssemblyCore);

        Assert.True(unlockState.IsUnlocked("belt_mk2"));
        Assert.False(unlockState.IsUnlocked("other_thing"));

        ingots.TryAccept(FerriteIngot);
        ingots.TryAccept(FerriteIngot);
        ingots.TryAccept(FerriteIngot);

        Assert.True(unlockState.IsUnlocked("other_thing"));
    }

    // ---- Construction validation ----

    [Fact]
    public void MilestoneDefinitionRejectsNonPositiveRequiredCount()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new MilestoneDefinition("m", AssemblyCore, 0, "u"));

    [Fact]
    public void MilestoneDefinitionRejectsEmptyId()
        => Assert.Throws<ArgumentException>(() => new MilestoneDefinition("", AssemblyCore, 1, "u"));

    [Fact]
    public void MilestoneDefinitionRejectsEmptyUnlockId()
        => Assert.Throws<ArgumentException>(() => new MilestoneDefinition("m", AssemblyCore, 1, ""));

    [Fact]
    public void UnlockStateUnlockRejectsEmptyId()
        => Assert.Throws<ArgumentException>(() => new UnlockState().Unlock(""));
}
