using System.Diagnostics.CodeAnalysis;

namespace Factory.Sim.Items;

/// <summary>
/// All known item types for the currently loaded game data, resolvable by data key or by
/// runtime <see cref="ItemId"/>. Built once by <c>Factory.Sim.Data.GameDataLoader</c> and
/// treated as read-only for the rest of the session — nothing else constructs one.
/// </summary>
public sealed class ItemRegistry
{
    private readonly Dictionary<string, ItemDefinition> _byKey;
    private readonly Dictionary<ItemId, ItemDefinition> _byId;

    internal ItemRegistry(IReadOnlyList<ItemDefinition> items)
    {
        _byKey = new Dictionary<string, ItemDefinition>(items.Count);
        _byId = new Dictionary<ItemId, ItemDefinition>(items.Count);
        foreach (ItemDefinition item in items)
        {
            _byKey[item.Key] = item;
            _byId[item.Id] = item;
        }
    }

    public IReadOnlyCollection<ItemDefinition> All => _byKey.Values;

    public bool TryGet(string key, [NotNullWhen(true)] out ItemDefinition? item)
        => _byKey.TryGetValue(key, out item);

    /// <summary>Throwing form of <see cref="TryGet"/>, for call sites that expect the key to exist.</summary>
    public ItemDefinition Get(string key)
        => TryGet(key, out ItemDefinition? item)
            ? item
            : throw new KeyNotFoundException($"No item with key '{key}' in the item registry.");

    public ItemDefinition Resolve(ItemId id)
        => _byId.TryGetValue(id, out ItemDefinition? item)
            ? item
            : throw new KeyNotFoundException($"No item with id {id} in the item registry.");
}
