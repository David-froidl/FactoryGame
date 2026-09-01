using Factory.Sim.Items;
using Factory.Sim.Production;

namespace Factory.Sim.Data;

/// <summary>The full set of loaded, validated game data for one session.</summary>
public sealed class GameData
{
    public GameData(ItemRegistry items, RecipeRegistry recipes)
    {
        Items = items;
        Recipes = recipes;
    }

    public ItemRegistry Items { get; }

    public RecipeRegistry Recipes { get; }
}
