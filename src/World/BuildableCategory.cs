using Factory.Sim.Production;

namespace FactoryGame.World;

/// <summary>
/// The three shapes a <see cref="BuildableResource"/> can take. Extractor, smelter and
/// assembler are all <see cref="Machine"/> — they differ only in which recipe and (for
/// extractors) which ore type their data names, never in category.
/// </summary>
public enum BuildableCategory
{
    Machine,
    Belt,
    Terminal,
}
