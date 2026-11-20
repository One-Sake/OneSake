using System.Security.Cryptography;
using System.Text;

namespace OneSake.Game.Randomness;

// All randomness of the game logic (shuffling, dice, random choices) goes through this,
// so a match can be played again exactly from its seed (see docs/bug-reports.md).
public interface IRandom
{
    // A number from 0 to maxExclusive - 1, without bias.
    int Next(int maxExclusive);
}

// PCG32 (pcg-random.org, "XSH RR" output): small, fast and fully deterministic.
// Not cryptographically secure - that is why every purpose gets its own stream, derived from the
// secret match seed with SHA-256. Seeing your own deck order never tells you anything about the
// opponent's deck or the dice, and the seed itself is only stored after the match.
public sealed class Pcg32 : IRandom
{
    private const ulong Multiplier = 6364136223846793005UL;

    private ulong _state;
    private readonly ulong _increment;

    public Pcg32(ulong state, ulong sequence)
    {
        _increment = (sequence << 1) | 1UL;
        _state = 0;
        NextUInt();
        _state += state;
        NextUInt();
    }

    // An independent stream for one purpose ("deck:1", "dice" ...) of a match.
    public static Pcg32 FromSeed(ulong seed, string stream)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{stream}"));
        return new Pcg32(BitConverter.ToUInt64(bytes, 0), BitConverter.ToUInt64(bytes, 8));
    }

    // A new secret seed for a match (only the server knows it until the match is over).
    public static ulong CreateSeed()
    {
        return BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
    }

    public uint NextUInt()
    {
        var old = _state;
        _state = unchecked(old * Multiplier + _increment);
        var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        var rotation = (int)(old >> 59);
        return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
    }

    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }

        // Rejection sampling: drop the values that would make small numbers more likely.
        var bound = (uint)maxExclusive;
        var threshold = (uint)(-(int)bound) % bound;
        while (true)
        {
            var value = NextUInt();
            if (value >= threshold)
            {
                return (int)(value % bound);
            }
        }
    }
}

public static class RandomExtensions
{
    // Fisher-Yates shuffle with the given random source.
    public static void Shuffle<T>(this IList<T> items, IRandom random)
    {
        for (var index = items.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = random.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }
}
