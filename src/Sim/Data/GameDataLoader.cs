using System.Text.Json;
using Factory.Sim.Items;
using Factory.Sim.Production;

namespace Factory.Sim.Data;

/// <summary>
/// Parses items.json / recipes.json into an <see cref="ItemRegistry"/> / <see cref="RecipeRegistry"/>.
///
/// This is the one place item and recipe data enters the sim — everything downstream
/// (machines, belts) only ever sees the validated, strongly-typed result. Validation is
/// strict and fails fast: a bad data file throws <see cref="GameDataException"/> naming
/// exactly what is wrong, rather than silently skipping a record or defaulting a value.
///
/// Stays in Factory.Sim (zero Godot dependencies), so the data pipeline itself is
/// dotnet-test-able. The Godot side only ever supplies an OS file path (already resolved
/// from res:// via ProjectSettings.GlobalizePath) and never touches parsing itself.
/// </summary>
public static class GameDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ---- File-based entry points (used by the Godot side once paths are resolved) ----

    public static GameData LoadGameData(string itemsPath, string recipesPath)
    {
        ItemRegistry items = LoadItemsFromFile(itemsPath);
        RecipeRegistry recipes = LoadRecipesFromFile(recipesPath, items);
        return new GameData(items, recipes);
    }

    public static ItemRegistry LoadItemsFromFile(string path)
        => LoadItems(ReadFile(path, "items"));

    public static RecipeRegistry LoadRecipesFromFile(string path, ItemRegistry items)
        => LoadRecipes(ReadFile(path, "recipes"), items);

    // ---- String-based entry points (the primary surface tests exercise directly) ----

    public static ItemRegistry LoadItems(string json)
    {
        ItemsFile file = Deserialize<ItemsFile>(json, "items");
        if (file.Items is null || file.Items.Count == 0)
            throw new GameDataException("items data must contain a non-empty \"items\" array.");

        var definitions = new List<ItemDefinition>(file.Items.Count);
        var seenKeys = new HashSet<string>();
        int nextId = 1; // 0 is reserved for ItemId.None.

        foreach (ItemDto dto in file.Items)
        {
            string key = RequireKey(dto.Id, "item", $"items[{definitions.Count}]");
            string displayName = RequireDisplayName(dto.DisplayName, key);

            if (!seenKeys.Add(key))
                throw new GameDataException($"Duplicate item id '{key}' in items data.");
            if (nextId > ushort.MaxValue)
                throw new GameDataException("Too many items: the item id space (ushort) is exhausted.");

            definitions.Add(new ItemDefinition(key, new ItemId((ushort)nextId), displayName));
            nextId++;
        }

        return new ItemRegistry(definitions);
    }

    public static RecipeRegistry LoadRecipes(string json, ItemRegistry items)
    {
        ArgumentNullException.ThrowIfNull(items);
        RecipesFile file = Deserialize<RecipesFile>(json, "recipes");
        if (file.Recipes is null || file.Recipes.Count == 0)
            throw new GameDataException("recipes data must contain a non-empty \"recipes\" array.");

        var definitions = new List<RecipeDefinition>(file.Recipes.Count);
        var seenIds = new HashSet<string>();

        foreach (RecipeDto dto in file.Recipes)
        {
            string id = RequireKey(dto.Id, "recipe", $"recipes[{definitions.Count}]");
            if (!seenIds.Add(id))
                throw new GameDataException($"Duplicate recipe id '{id}' in recipes data.");

            List<ItemStack> inputs = ResolveAmounts(dto.Inputs, items, id, "inputs");
            List<ItemStack> outputs = ResolveAmounts(dto.Outputs, items, id, "outputs");
            if (outputs.Count == 0)
                throw new GameDataException($"Recipe '{id}' has no outputs; a recipe must produce something.");

            int durationTicks = ResolveDurationTicks(dto, id);

            definitions.Add(new RecipeDefinition(id, inputs, outputs, durationTicks));
        }

        return new RecipeRegistry(definitions);
    }

    // ---- Helpers ----

    private static List<ItemStack> ResolveAmounts(List<ItemAmountDto>? dtos, ItemRegistry items, string recipeId, string field)
    {
        if (dtos is null) return new List<ItemStack>();

        var result = new List<ItemStack>(dtos.Count);
        foreach (ItemAmountDto dto in dtos)
        {
            string itemKey = RequireKey(dto.Item, "item reference", $"recipe '{recipeId}' {field}");
            if (!items.TryGet(itemKey, out ItemDefinition? item))
                throw new GameDataException($"Recipe '{recipeId}' {field} references unknown item '{itemKey}'.");
            if (dto.Count <= 0)
                throw new GameDataException(
                    $"Recipe '{recipeId}' {field} has a non-positive count ({dto.Count}) for item '{itemKey}'.");

            result.Add(new ItemStack(item.Id, dto.Count));
        }
        return result;
    }

    private static int ResolveDurationTicks(RecipeDto dto, string recipeId)
    {
        bool hasTicks = dto.DurationTicks.HasValue;
        bool hasSeconds = dto.DurationSeconds.HasValue;

        if (hasTicks == hasSeconds)
            throw new GameDataException(
                $"Recipe '{recipeId}' must specify exactly one of \"durationTicks\" or \"durationSeconds\".");

        if (hasTicks)
        {
            int ticks = dto.DurationTicks!.Value;
            if (ticks <= 0)
                throw new GameDataException($"Recipe '{recipeId}' has a non-positive durationTicks ({ticks}).");
            return ticks;
        }

        decimal seconds = dto.DurationSeconds!.Value;
        if (seconds <= 0)
            throw new GameDataException($"Recipe '{recipeId}' has a non-positive durationSeconds ({seconds}).");

        return SecondsToTicks(seconds, recipeId);
    }

    /// <summary>
    /// The one, central seconds-to-ticks conversion for authored data. Uses <c>decimal</c>
    /// (exact base-10 arithmetic, unlike <c>double</c>) so "3 seconds at 20 Hz" is checked
    /// for an exact whole-tick result instead of a float-precision near-match — the sim
    /// itself never does this conversion or compares fractional ticks.
    /// </summary>
    private static int SecondsToTicks(decimal seconds, string recipeId)
    {
        decimal ticks = seconds * SimConstants.TicksPerSecond;
        if (ticks != decimal.Truncate(ticks))
            throw new GameDataException(
                $"Recipe '{recipeId}' has durationSeconds={seconds}, which is not a whole number of ticks " +
                $"at {SimConstants.TicksPerSecond} ticks/second ({ticks} ticks). Use a value that divides evenly, " +
                "or specify durationTicks directly.");

        return (int)ticks;
    }

    private static string RequireKey(string? value, string kind, string context)
        => string.IsNullOrWhiteSpace(value)
            ? throw new GameDataException($"{context}: {kind} id must not be null or empty.")
            : value;

    private static string RequireDisplayName(string? value, string itemKey)
        => string.IsNullOrWhiteSpace(value)
            ? throw new GameDataException($"Item '{itemKey}' is missing a displayName.")
            : value;

    private static string ReadFile(string path, string kind)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new GameDataException($"Could not read {kind} data file at '{path}': {ex.Message}", ex);
        }
    }

    private static T Deserialize<T>(string json, string kind)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                   ?? throw new GameDataException($"{kind} data is empty or \"null\".");
        }
        catch (JsonException ex)
        {
            throw new GameDataException($"{kind} data is not valid JSON: {ex.Message}", ex);
        }
    }

    // ---- JSON shape (private: the public contract is the returned registries, not these DTOs) ----

    private sealed class ItemsFile
    {
        public List<ItemDto>? Items { get; set; }
    }

    private sealed class ItemDto
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
    }

    private sealed class RecipesFile
    {
        public List<RecipeDto>? Recipes { get; set; }
    }

    private sealed class RecipeDto
    {
        public string? Id { get; set; }
        public List<ItemAmountDto>? Inputs { get; set; }
        public List<ItemAmountDto>? Outputs { get; set; }
        public int? DurationTicks { get; set; }
        public decimal? DurationSeconds { get; set; }
    }

    private sealed class ItemAmountDto
    {
        public string? Item { get; set; }
        public int Count { get; set; }
    }
}
