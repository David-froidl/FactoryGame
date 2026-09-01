namespace Factory.Sim.Data;

/// <summary>
/// Thrown for any problem in authored game data (items/recipes JSON): a missing file,
/// malformed JSON, a duplicate id, an unresolvable reference, or an invalid value. Always
/// carries a message specific enough to fix the data without a debugger — see
/// <see cref="GameDataLoader"/> for the validation rules that raise this.
/// </summary>
public sealed class GameDataException : Exception
{
    public GameDataException(string message) : base(message) { }

    public GameDataException(string message, Exception innerException) : base(message, innerException) { }
}
