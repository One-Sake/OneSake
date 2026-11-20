using System.Security.Cryptography;

namespace OneSake.Server.Match;

// Rolls one six-sided die. The server decides every roll, so no client can cheat.
public interface IDiceRoller
{
    int Roll();
}

public sealed class CryptoDiceRoller : IDiceRoller
{
    public static CryptoDiceRoller Instance { get; } = new();

    public int Roll() => RandomNumberGenerator.GetInt32(1, 7);
}

// Default for matches: the dice come from the match seed (own stream), so a recorded match
// rolls the same dice when it is played again. The seed stays secret on the server.
public sealed class SeededDiceRoller : IDiceRoller
{
    private readonly OneSake.Game.Randomness.IRandom _random;

    public SeededDiceRoller(OneSake.Game.Randomness.IRandom random)
    {
        _random = random;
    }

    public int Roll() => _random.Next(6) + 1;
}

// Development/test hook: returns the given values in order, then falls back to real rolls.
// Example config (Development only): "Match": { "FixedDiceRolls": [3, 3, 5, 2] } -> tie, then 5:2.
public sealed class FixedDiceRoller : IDiceRoller
{
    private readonly Queue<int> _values;

    public FixedDiceRoller(IEnumerable<int> values)
    {
        _values = new Queue<int>(values.Where(value => value is >= 1 and <= 6));
    }

    public int Roll() => _values.Count > 0 ? _values.Dequeue() : CryptoDiceRoller.Instance.Roll();
}
