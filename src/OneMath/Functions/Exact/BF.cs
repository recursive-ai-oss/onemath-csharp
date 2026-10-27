using System;
using System.Numerics;

namespace RecursiveAI.OneMath;

/// <summary>
/// A binary floating-point number <c>M * 2^E</c> with an unbounded integer mantissa, used by the exact fallback
/// stage. All arithmetic is integer arithmetic on <see cref="BigInteger"/>, so it is identical on every platform.
/// Addition and multiplication are exact; <see cref="R"/> truncates the mantissa to <see cref="P"/> bits, and
/// division and square root return <see cref="P"/> bits (truncated toward zero).
/// </summary>
internal readonly struct BF
{
    /// <summary>Working precision in bits. Far above the ~2^-120 worst cases published for the elementary functions (Lefevre and Muller).</summary>
    public const int P = 512;

    public readonly BigInteger M;
    public readonly int E;

    public BF(BigInteger m, int e)
    {
        M = m;
        E = e;
    }

    public static readonly BF Zero = new(BigInteger.Zero, 0);
    public static readonly BF One = new(BigInteger.One, 0);

    public bool IsZero => M.IsZero;
    public int Sign => M.Sign;

    /// <summary>Exponent <c>t</c> with 2^(t-1) &lt;= |value| &lt; 2^t (value must be nonzero).</summary>
    public long Top => (long)BigInteger.Abs(M).GetBitLength() + E;

    public static BF From(double d)
    {
        long bits = BitConverter.DoubleToInt64Bits(d);
        bool neg = bits < 0;
        int ex = (int)((bits >> 52) & 0x7FF);
        long frac = bits & 0xFFFFFFFFFFFFFL;
        if (ex == 0)
        {
            return new BF(neg ? -frac : frac, -1074);
        }
        long m = frac | (1L << 52);
        return new BF(neg ? -m : m, ex - 1075);
    }

    public static BF From(long v) => new(v, 0);

    public static BF operator -(BF a) => new(-a.M, a.E);

    public static BF operator +(BF a, BF b)
    {
        if (a.M.IsZero)
        {
            return b;
        }
        if (b.M.IsZero)
        {
            return a;
        }
        if (a.E == b.E)
        {
            return new BF(a.M + b.M, a.E);
        }
        return a.E < b.E ? new BF(a.M + (b.M << (b.E - a.E)), a.E) : new BF((a.M << (a.E - b.E)) + b.M, b.E);
    }

    public static BF operator -(BF a, BF b) => a + (-b);

    public static BF operator *(BF a, BF b) => new(a.M * b.M, a.E + b.E);

    /// <summary>Multiplies by 2^k exactly.</summary>
    public BF Shl(int k) => new(M, E + k);

    /// <summary>Truncates the mantissa to P bits toward zero.</summary>
    public BF R()
    {
        long bl = (long)BigInteger.Abs(M).GetBitLength();
        if (bl <= P)
        {
            return this;
        }
        int sh = (int)(bl - P);
        BigInteger m = M.Sign >= 0 ? M >> sh : -((-M) >> sh);
        return new BF(m, E + sh);
    }

    public static BF Div(BF a, BF b)
    {
        if (a.M.IsZero)
        {
            return Zero;
        }
        long ba = (long)BigInteger.Abs(a.M).GetBitLength();
        long bb = (long)BigInteger.Abs(b.M).GetBitLength();
        int s = (int)Math.Max(0, P + 2 + bb - ba);
        BigInteger q = BigInteger.Divide(a.M << s, b.M);
        return new BF(q, a.E - b.E - s).R();
    }

    public static BF Sqrt(BF a)
    {
        long bl = (long)a.M.GetBitLength();
        int t = (int)Math.Max(0, 2 * (P + 2) - bl);
        if (((a.E - t) & 1) != 0)
        {
            t++;
        }
        BigInteger r = ISqrt(a.M << t);
        return new BF(r, (a.E - t) / 2).R();
    }

    /// <summary>Floor of the square root of n &gt;= 0.</summary>
    public static BigInteger ISqrt(BigInteger n)
    {
        if (n.IsZero)
        {
            return n;
        }
        BigInteger x = BigInteger.One << (int)((n.GetBitLength() + 1) / 2);
        while (true)
        {
            BigInteger y = (x + n / x) >> 1;
            if (y >= x)
            {
                return x;
            }
            x = y;
        }
    }

    /// <summary>Floor of the cube root of n &gt;= 0.</summary>
    public static BigInteger ICbrt(BigInteger n)
    {
        if (n.IsZero)
        {
            return n;
        }
        BigInteger x = BigInteger.One << (int)((n.GetBitLength() + 2) / 3 + 1);
        while (true)
        {
            BigInteger y = (2 * x + n / (x * x)) / 3;
            if (y >= x)
            {
                return x;
            }
            x = y;
        }
    }

    /// <summary>Nearest integer (ties away from zero), as a BigInteger.</summary>
    public BigInteger RoundToInteger()
    {
        if (E >= 0)
        {
            return M << E;
        }
        BigInteger half = BigInteger.One << (-E - 1);
        return M.Sign >= 0 ? (M + half) >> -E : -((-M + half) >> -E);
    }
}
