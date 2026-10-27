namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>SplitMix64 (Steele, Lea and Flood 2014): a tiny seeded generator whose output depends only on the seed.</summary>
internal sealed class TypesRng
{
    private static readonly double[] s_scales = { 1.0, 1.0 / 1024, 1024.0, 1.0 / 1048576, 1048576.0, 1.0, 16.0 };
    private ulong _state;

    internal TypesRng(ulong seed) => _state = seed;

    internal ulong NextUInt64()
    {
        ulong z = unchecked(_state += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A double in [0, 1) with 53 random bits.</summary>
    internal double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>A power-of-two magnitude between 2^-20 and 2^20.</summary>
    internal double Scale() => s_scales[NextUInt64() % (ulong)s_scales.Length];
}
