namespace FactoryGame.Building;

/// <summary>Outcome of <c>PlacementValidator.Validate</c>: whether a placement is allowed, and if not, a player-facing reason.</summary>
public readonly struct PlacementResult
{
    private PlacementResult(bool isValid, string reason)
    {
        IsValid = isValid;
        Reason = reason;
    }

    public bool IsValid { get; }

    /// <summary>Empty when <see cref="IsValid"/> is true.</summary>
    public string Reason { get; }

    public static PlacementResult Valid() => new(true, "");

    public static PlacementResult Invalid(string reason) => new(false, reason);
}
