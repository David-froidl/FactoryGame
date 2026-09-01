namespace Factory.Sim.Tests.Support;

/// <summary>Locates the repo's checked-in /data files from wherever the test binaries land.</summary>
public static class RepoPaths
{
    public static string DataFile(params string[] relativeParts)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "FactoryGame.sln")))
            dir = Path.GetDirectoryName(dir);

        if (dir is null)
            throw new InvalidOperationException(
                "Could not locate repo root (FactoryGame.sln) from the test output directory.");

        var parts = new List<string> { dir, "data" };
        parts.AddRange(relativeParts);
        return Path.Combine(parts.ToArray());
    }
}
