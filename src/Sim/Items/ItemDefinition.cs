namespace Factory.Sim.Items;

/// <summary>
/// Static definition of one item type: what it is called and which <see cref="ItemId"/>
/// it resolves to at runtime. Always produced by <see cref="ItemRegistry"/> from loaded
/// data (see <c>Factory.Sim.Data.GameDataLoader</c>) — never hand-written per item in code.
/// </summary>
public sealed class ItemDefinition
{
    public ItemDefinition(string key, ItemId id, string displayName)
    {
        Key = key;
        Id = id;
        DisplayName = displayName;
    }

    /// <summary>Stable, human-authored identifier used in JSON data (e.g. "ferrite_ore").</summary>
    public string Key { get; }

    /// <summary>Runtime handle assigned at load time. Belts and machines store only this.</summary>
    public ItemId Id { get; }

    public string DisplayName { get; }

    public override string ToString() => $"{Key} ({DisplayName})";
}
