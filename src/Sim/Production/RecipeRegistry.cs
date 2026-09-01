using System.Diagnostics.CodeAnalysis;

namespace Factory.Sim.Production;

/// <summary>
/// All known recipes for the currently loaded game data, resolvable by id. Built once by
/// <c>Factory.Sim.Data.GameDataLoader</c> and treated as read-only for the rest of the
/// session — nothing else constructs one.
/// </summary>
public sealed class RecipeRegistry
{
    private readonly Dictionary<string, RecipeDefinition> _byId;

    internal RecipeRegistry(IReadOnlyList<RecipeDefinition> recipes)
    {
        _byId = new Dictionary<string, RecipeDefinition>(recipes.Count);
        foreach (RecipeDefinition recipe in recipes)
            _byId[recipe.Id] = recipe;
    }

    public IReadOnlyCollection<RecipeDefinition> All => _byId.Values;

    public bool TryGet(string id, [NotNullWhen(true)] out RecipeDefinition? recipe)
        => _byId.TryGetValue(id, out recipe);

    /// <summary>Throwing form of <see cref="TryGet"/>, for call sites that expect the id to exist.</summary>
    public RecipeDefinition Get(string id)
        => TryGet(id, out RecipeDefinition? recipe)
            ? recipe
            : throw new KeyNotFoundException($"No recipe with id '{id}' in the recipe registry.");
}
