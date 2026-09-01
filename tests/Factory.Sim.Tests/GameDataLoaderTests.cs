using Factory.Sim.Data;
using Factory.Sim.Production;
using Factory.Sim.Tests.Support;

namespace Factory.Sim.Tests;

/// <summary>
/// Two kinds of coverage here, deliberately kept apart: tests that load the real, checked-in
/// production JSON (read-only — nothing here ever writes to /data), and tests that validate
/// error handling against small inline JSON fixtures that never touch disk.
/// </summary>
public class GameDataLoaderTests
{
    // ---- Loading the real, checked-in production data ----

    [Fact]
    public void LoadsAllFiveProductionItems()
    {
        ItemRegistry items = GameDataLoader.LoadItemsFromFile(RepoPaths.DataFile("items", "items.json"));

        Assert.Equal(5, items.All.Count);
        foreach (string key in new[] { "ferrite_ore", "copperite_ore", "ferrite_ingot", "copper_ingot", "assembly_core" })
            Assert.True(items.TryGet(key, out _), $"missing item '{key}'");
    }

    [Fact]
    public void LoadsAllFiveProductionRecipes()
    {
        GameData data = LoadProductionData();

        Assert.Equal(5, data.Recipes.All.Count);
        foreach (string id in new[] { "ferrite_extraction", "copperite_extraction", "ferrite_smelting", "copper_smelting", "assembly_core" })
            Assert.True(data.Recipes.TryGet(id, out _), $"missing recipe '{id}'");
    }

    [Fact]
    public void ExtractionRecipesHaveNoInputs()
    {
        GameData data = LoadProductionData();

        Assert.Empty(data.Recipes.Get("ferrite_extraction").Inputs);
        Assert.Empty(data.Recipes.Get("copperite_extraction").Inputs);
    }

    [Fact]
    public void ResolvesAllItemReferencesInAssemblyCoreRecipe()
    {
        GameData data = LoadProductionData();
        RecipeDefinition recipe = data.Recipes.Get("assembly_core");

        Assert.Equal(2, recipe.Inputs.Count);
        Assert.Equal(data.Items.Get("ferrite_ingot").Id, recipe.Inputs[0].Item);
        Assert.Equal(2, recipe.Inputs[0].Count);
        Assert.Equal(data.Items.Get("copper_ingot").Id, recipe.Inputs[1].Item);
        Assert.Equal(1, recipe.Inputs[1].Count);

        Assert.Single(recipe.Outputs);
        Assert.Equal(data.Items.Get("assembly_core").Id, recipe.Outputs[0].Item);
        Assert.Equal(1, recipe.Outputs[0].Count);
        Assert.Equal(120, recipe.DurationTicks);
    }

    [Theory]
    [InlineData("ferrite_extraction", 40)]
    [InlineData("copperite_extraction", 40)]
    [InlineData("ferrite_smelting", 60)]
    [InlineData("copper_smelting", 60)]
    [InlineData("assembly_core", 120)]
    public void ProductionRecipesHaveExpectedDurationTicks(string recipeId, int expectedTicks)
        => Assert.Equal(expectedTicks, LoadProductionData().Recipes.Get(recipeId).DurationTicks);

    private static GameData LoadProductionData()
        => GameDataLoader.LoadGameData(RepoPaths.DataFile("items", "items.json"), RepoPaths.DataFile("recipes", "recipes.json"));

    // ---- Validation, using inline fixtures (never touches the real data files) ----

    private const string OneValidItem = """{ "items": [ { "id": "a", "displayName": "A" } ] }""";

    [Fact]
    public void RejectsUnknownItemReferenceInRecipe()
    {
        ItemRegistry items = GameDataLoader.LoadItems(OneValidItem);
        const string badRecipes = """{ "recipes": [ { "id": "r1", "inputs": [ { "item": "does_not_exist", "count": 1 } ], "outputs": [ { "item": "a", "count": 1 } ], "durationTicks": 10 } ] }""";

        GameDataException ex = Assert.Throws<GameDataException>(() => GameDataLoader.LoadRecipes(badRecipes, items));
        Assert.Contains("does_not_exist", ex.Message);
    }

    [Fact]
    public void RejectsDuplicateItemIds()
    {
        const string json = """{ "items": [ { "id": "a", "displayName": "A" }, { "id": "a", "displayName": "A again" } ] }""";

        GameDataException ex = Assert.Throws<GameDataException>(() => GameDataLoader.LoadItems(json));
        Assert.Contains("'a'", ex.Message);
    }

    [Fact]
    public void RejectsDuplicateRecipeIds()
    {
        ItemRegistry items = GameDataLoader.LoadItems(OneValidItem);
        const string json = """{ "recipes": [ { "id": "r1", "inputs": [], "outputs": [ { "item": "a", "count": 1 } ], "durationTicks": 10 }, { "id": "r1", "inputs": [], "outputs": [ { "item": "a", "count": 1 } ], "durationTicks": 20 } ] }""";

        GameDataException ex = Assert.Throws<GameDataException>(() => GameDataLoader.LoadRecipes(json, items));
        Assert.Contains("'r1'", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveRecipeAmounts(int badCount)
    {
        ItemRegistry items = GameDataLoader.LoadItems(OneValidItem);
        string json = $$"""{ "recipes": [ { "id": "r1", "inputs": [ { "item": "a", "count": {{badCount}} } ], "outputs": [ { "item": "a", "count": 1 } ], "durationTicks": 10 } ] }""";

        Assert.Throws<GameDataException>(() => GameDataLoader.LoadRecipes(json, items));
    }

    [Fact]
    public void RejectsNonPositiveDurationTicks()
    {
        ItemRegistry items = GameDataLoader.LoadItems(OneValidItem);
        const string json = """{ "recipes": [ { "id": "r1", "inputs": [], "outputs": [ { "item": "a", "count": 1 } ], "durationTicks": 0 } ] }""";

        Assert.Throws<GameDataException>(() => GameDataLoader.LoadRecipes(json, items));
    }

    [Fact]
    public void RejectsDurationSecondsThatDoNotDivideEvenlyIntoTicks()
    {
        ItemRegistry items = GameDataLoader.LoadItems(OneValidItem);
        const string json = """{ "recipes": [ { "id": "r1", "inputs": [], "outputs": [ { "item": "a", "count": 1 } ], "durationSeconds": 0.13 } ] }""";

        Assert.Throws<GameDataException>(() => GameDataLoader.LoadRecipes(json, items));
    }

    [Fact]
    public void ConvertsExactDurationSecondsToTicks()
    {
        ItemRegistry items = GameDataLoader.LoadItems(OneValidItem);
        const string json = """{ "recipes": [ { "id": "r1", "inputs": [], "outputs": [ { "item": "a", "count": 1 } ], "durationSeconds": 3 } ] }""";

        RecipeRegistry recipes = GameDataLoader.LoadRecipes(json, items);
        Assert.Equal(60, recipes.Get("r1").DurationTicks);
    }

    [Fact]
    public void RejectsRecipeWithBothDurationFieldsSet()
    {
        ItemRegistry items = GameDataLoader.LoadItems(OneValidItem);
        const string json = """{ "recipes": [ { "id": "r1", "inputs": [], "outputs": [ { "item": "a", "count": 1 } ], "durationTicks": 10, "durationSeconds": 1 } ] }""";

        Assert.Throws<GameDataException>(() => GameDataLoader.LoadRecipes(json, items));
    }

    [Fact]
    public void RejectsRecipeWithNoDurationField()
    {
        ItemRegistry items = GameDataLoader.LoadItems(OneValidItem);
        const string json = """{ "recipes": [ { "id": "r1", "inputs": [], "outputs": [ { "item": "a", "count": 1 } ] } ] }""";

        Assert.Throws<GameDataException>(() => GameDataLoader.LoadRecipes(json, items));
    }

    [Fact]
    public void RejectsRecipeWithNoOutputs()
    {
        ItemRegistry items = GameDataLoader.LoadItems(OneValidItem);
        const string json = """{ "recipes": [ { "id": "r1", "inputs": [], "outputs": [], "durationTicks": 10 } ] }""";

        Assert.Throws<GameDataException>(() => GameDataLoader.LoadRecipes(json, items));
    }

    [Fact]
    public void RejectsEmptyOrWhitespaceItemId()
    {
        const string json = """{ "items": [ { "id": "   ", "displayName": "X" } ] }""";

        Assert.Throws<GameDataException>(() => GameDataLoader.LoadItems(json));
    }

    [Fact]
    public void RejectsMissingItemsFile()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".json");

        GameDataException ex = Assert.Throws<GameDataException>(() => GameDataLoader.LoadItemsFromFile(missingPath));
        Assert.Contains("Could not read", ex.Message);
    }

    [Fact]
    public void RejectsInvalidJson()
        => Assert.Throws<GameDataException>(() => GameDataLoader.LoadItems("{ this is not valid json"));

    // ---- Registry lookup behaviour ----

    [Fact]
    public void ItemRegistryGetThrowsForUnknownKey()
    {
        ItemRegistry items = GameDataLoader.LoadItems(OneValidItem);
        Assert.Throws<KeyNotFoundException>(() => items.Get("nope"));
    }

    [Fact]
    public void RecipeRegistryGetThrowsForUnknownId()
    {
        ItemRegistry items = GameDataLoader.LoadItems(OneValidItem);
        const string json = """{ "recipes": [ { "id": "r1", "inputs": [], "outputs": [ { "item": "a", "count": 1 } ], "durationTicks": 5 } ] }""";
        RecipeRegistry recipes = GameDataLoader.LoadRecipes(json, items);

        Assert.Throws<KeyNotFoundException>(() => recipes.Get("nope"));
    }
}
