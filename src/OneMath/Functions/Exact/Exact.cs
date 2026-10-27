using System;
using System.Numerics;

namespace RecursiveAI.OneMath;

/// <summary>The functions the exact fallback stage can evaluate.</summary>
internal enum Fn
{
    Exp, Exp2, Expm1, Log, Log2, Log10, Log1p,
    Sin, Cos, Tan, Asin, Acos, Atan, Atan2,
    Sinh, Cosh, Tanh, Pow, Cbrt, Hypot,
}

/// <summary>
/// The exact fallback stage: every function evaluated to about 500 bits with big-integer arithmetic, then rounded
/// once. It runs only when the fast double-double stage cannot decide the rounding (about 1 input in 2^35), when
/// the result is subnormal or at the overflow edge, or when a trigonometric argument is hopeless for the fast path.
/// Methods follow Brent's and Muller's textbook algorithms: argument reduction by multiples of ln 2 or pi/2,
/// repeated halving or square roots to shrink the series argument, then Taylor or atanh series.
/// </summary>
internal static class Exact
{
    private const int Guard = 16;

    // ----- constants (computed once, from integer arithmetic only) -----

    private static BF? s_pi;
    private static BF? s_ln2;
    private static BF? s_ln10;

    /// <summary>pi to about 1800 bits (Machin: 16 atan(1/5) - 4 atan(1/239)), enough to reduce any double.</summary>
    private static BF PiBig()
    {
        if (s_pi is { } p)
        {
            return p;
        }
        const int bits = 1900;
        BigInteger one = BigInteger.One << bits;
        BigInteger Atan(int inv)
        {
            BigInteger x = one / inv;
            BigInteger sum = x;
            BigInteger inv2 = (BigInteger)inv * inv;
            int n = 1;
            bool sub = true;
            while (!x.IsZero)
            {
                x /= inv2;
                n += 2;
                BigInteger term = x / n;
                sum = sub ? sum - term : sum + term;
                sub = !sub;
            }
            return sum;
        }
        BigInteger pi = 16 * Atan(5) - 4 * Atan(239);
        BF r = new(pi, -bits);
        s_pi = r;
        return r;
    }

    private static BF Pi() => PiBig().R();

    private static BF Ln2()
    {
        if (s_ln2 is { } v)
        {
            return v;
        }
        BF r = AtanhSeries(BF.Div(BF.One, BF.From(3))).Shl(1);
        s_ln2 = r;
        return r;
    }

    private static BF Ln10()
    {
        if (s_ln10 is { } v)
        {
            return v;
        }
        BF r = Log(BF.From(10));
        s_ln10 = r;
        return r;
    }

    // ----- series -----

    private static bool Negligible(BF term, BF sum) => term.IsZero || (!sum.IsZero && term.Top < sum.Top - BF.P - Guard);

    /// <summary>z * (1 + z^2/3 + z^4/5 + ...) = atanh(z) for |z| &lt; 1.</summary>
    private static BF AtanhSeries(BF z)
    {
        if (z.IsZero)
        {
            return BF.Zero;
        }
        BF w = (z * z).R();
        BF t = z;
        BF sum = z;
        for (int k = 1; ; k++)
        {
            t = (t * w).R();
            BF term = new(t.M / (2 * k + 1), t.E);
            if (Negligible(term, sum))
            {
                break;
            }
            sum += term;
        }
        return sum.R();
    }

    /// <summary>expm1(r) for small |r|: halve r 24 times, sum the Taylor series, then undo with e -> 2e + e^2.</summary>
    private static BF ExpM1Red(BF r)
    {
        if (r.IsZero)
        {
            return BF.Zero;
        }
        const int s = 24;
        BF u = r.Shl(-s);
        BF term = u;
        BF sum = u;
        for (int n = 2; ; n++)
        {
            term = (term * u).R();
            term = new BF(term.M / n, term.E);
            if (Negligible(term, sum))
            {
                break;
            }
            sum += term;
        }
        BF e = sum.R();
        for (int i = 0; i < s; i++)
        {
            e = (e.Shl(1) + e * e).R();
        }
        return e;
    }

    /// <summary>e^x for moderate |x| (callers keep |x| below 2^20).</summary>
    private static BF ExpBF(BF x)
    {
        if (x.IsZero)
        {
            return BF.One;
        }
        BigInteger k = BF.Div(x, Ln2()).RoundToInteger();
        BF r = (x - new BF(k, 0) * Ln2()).R();
        BF e = ExpM1Red(r);
        return (BF.One + e).R().Shl((int)k);
    }

    private static BF Expm1BF(BF x)
    {
        if (x.IsZero)
        {
            return x;
        }
        if (x.Top <= 0)
        {
            return ExpM1Red(x);
        }
        return (ExpBF(x) - BF.One).R();
    }

    /// <summary>Natural log of a positive BF.</summary>
    private static BF Log(BF x)
    {
        long e = x.Top - 1;
        BF f = x.Shl((int)-e); // in [1, 2)
        if (Compare(f, BF.From(1.4142135623730951)) > 0)
        {
            f = f.Shl(-1);
            e++;
        }
        const int s = 8;
        BF g = f;
        for (int i = 0; i < s; i++)
        {
            g = BF.Sqrt(g);
        }
        BF z = BF.Div(g - BF.One, g + BF.One);
        BF lm = AtanhSeries(z).Shl(s + 1);
        if (e == 0)
        {
            return lm;
        }
        return (lm + new BF(e, 0) * Ln2()).R();
    }

    private static BF Log1pBF(BF x)
    {
        if (x.IsZero)
        {
            return x;
        }
        if (x.Top <= -2)
        {
            // |x| < 1/4: atanh form is relative-accurate however small x is.
            BF z = BF.Div(x, BF.From(2) + x);
            return AtanhSeries(z).Shl(1);
        }
        return Log((BF.One + x).R());
    }

    private static int Compare(BF a, BF b) => (a - b).Sign;

    private static void SinCos(BF x, out BF sin, out BF cos)
    {
        BF pio2 = PiBig().Shl(-1);
        // k = nearest integer to x / (pi/2), by exact integer division (the quotient can have 1000 bits).
        BigInteger num = x.M;
        BigInteger den = pio2.M;
        int sh = x.E - pio2.E;
        if (sh >= 0)
        {
            num <<= sh;
        }
        else
        {
            den <<= -sh;
        }
        BigInteger k = num.Sign >= 0
            ? BigInteger.Divide(2 * num + den, 2 * den)
            : -BigInteger.Divide(2 * -num + den, 2 * den);
        BF r = (x - new BF(k, 0) * pio2).R();
        BF w = (r * r).R();
        // sin r = r - r^3/3! + ...
        BF t = r;
        BF s = r;
        for (int n = 1; !t.IsZero; n++)
        {
            t = (t * w).R();
            t = new BF(-t.M / ((2 * n) * (2L * n + 1)), t.E);
            if (Negligible(t, s))
            {
                break;
            }
            s += t;
        }
        BF c = BF.One;
        t = BF.One;
        for (int n = 1; ; n++)
        {
            t = (t * w).R();
            t = new BF(-t.M / ((2L * n - 1) * (2L * n)), t.E);
            if (Negligible(t, c))
            {
                break;
            }
            c += t;
        }
        s = s.R();
        c = c.R();
        int q = (int)(((k % 4) + 4) % 4);
        switch (q)
        {
            case 0: sin = s; cos = c; break;
            case 1: sin = c; cos = -s; break;
            case 2: sin = -s; cos = -c; break;
            default: sin = -c; cos = s; break;
        }
    }

    /// <summary>atan of a nonnegative BF.</summary>
    private static BF AtanPos(BF x)
    {
        if (x.IsZero)
        {
            return x;
        }
        if (Compare(x, BF.One) > 0)
        {
            return (Pi().Shl(-1) - AtanPos(BF.Div(BF.One, x))).R();
        }
        const int s = 6;
        BF u = x;
        for (int i = 0; i < s; i++)
        {
            // atan u = 2 atan(u / (1 + sqrt(1 + u^2)))
            u = BF.Div(u, BF.One + BF.Sqrt((BF.One + u * u).R()));
        }
        BF w = (u * u).R();
        BF t = u;
        BF sum = u;
        for (int k = 1; ; k++)
        {
            t = (t * w).R();
            BF term = new((k % 2 == 0 ? t.M : -t.M) / (2 * k + 1), t.E);
            if (Negligible(term, sum))
            {
                break;
            }
            sum += term;
        }
        return sum.R().Shl(s);
    }

    private static BF Atan(BF x) => x.Sign < 0 ? -AtanPos(-x) : AtanPos(x);

    // ----- evaluation -----

    /// <summary>
    /// Evaluates fn to about 500 bits. Arguments must be finite and inside the function's domain, with special
    /// values already handled by the caller. <paramref name="sticky"/> is true when the value is known to lie
    /// strictly above the returned (truncated) one, which settles exact-tie rounding for the integer-root functions.
    /// </summary>
    public static BF Eval(Fn fn, double a, double b, out bool sticky)
    {
        sticky = false;
        BF x = BF.From(a);
        switch (fn)
        {
            case Fn.Exp: return ExpBF(x);
            case Fn.Exp2:
                // Integer arguments are exact powers of two (and may sit exactly on a subnormal rounding tie).
                if (a == Math.Floor(a))
                {
                    return new BF(1, (int)a);
                }
                return ExpBF((x * Ln2()).R());
            case Fn.Expm1: return Expm1BF(x);
            case Fn.Log: return Log(x);
            case Fn.Log2: return BF.Div(Log(x), Ln2());
            case Fn.Log10: return BF.Div(Log(x), Ln10());
            case Fn.Log1p: return Log1pBF(x);
            case Fn.Sin: SinCos(x, out BF s0, out _); return s0;
            case Fn.Cos: SinCos(x, out _, out BF c0); return c0;
            case Fn.Tan:
                SinCos(x, out BF s1, out BF c1);
                return BF.Div(s1, c1);
            case Fn.Atan: return Atan(x);
            case Fn.Asin:
            {
                BF one = BF.One;
                if (Compare(x.Sign < 0 ? -x : x, one) == 0)
                {
                    return x.Sign < 0 ? -Pi().Shl(-1) : Pi().Shl(-1);
                }
                BF den = BF.Sqrt((one - x * x).R());
                return Atan(BF.Div(x, den));
            }
            case Fn.Acos:
            {
                if (Compare(x, -BF.One) == 0)
                {
                    return Pi();
                }
                BF q = BF.Div(BF.One - x, BF.One + x);
                return AtanPos(BF.Sqrt(q)).Shl(1);
            }
            case Fn.Atan2:
            {
                // a = y, b = x, both finite and nonzero here.
                BF y = x;
                BF xx = BF.From(b);
                BF ay = y.Sign < 0 ? -y : y;
                BF ax = xx.Sign < 0 ? -xx : xx;
                BF at = AtanPos(BF.Div(ay, ax));
                BF r = xx.Sign < 0 ? (Pi() - at).R() : at;
                return y.Sign < 0 ? -r : r;
            }
            case Fn.Sinh:
            {
                if (x.Top > 0)
                {
                    BF u = ExpBF(x);
                    return (u - BF.Div(BF.One, u)).R().Shl(-1);
                }
                BF e = Expm1BF(x);
                BF f = BF.Div(e, BF.One + e);
                return (e + f).R().Shl(-1);
            }
            case Fn.Cosh:
            {
                BF u = x.IsZero ? BF.One : ExpBF(x);
                return (u + BF.Div(BF.One, u)).R().Shl(-1);
            }
            case Fn.Tanh:
            {
                BF e = Expm1BF(x.Shl(1));
                return BF.Div(e, e + BF.From(2));
            }
            case Fn.Pow:
            {
                // x > 0 here; the caller applies the sign for negative bases.
                if (IsPowerOfTwo(a, out int j))
                {
                    // 2^j raised to y is exact whenever j*y is an integer (and may sit exactly on a subnormal tie).
                    BF jy = BF.From(j) * BF.From(b);
                    if (jy.IsZero || jy.E >= 0 || jy.Top - 0 > 0 && (jy.M & ((BigInteger.One << -jy.E) - 1)).IsZero)
                    {
                        BigInteger ji = jy.E >= 0 ? jy.M << jy.E : jy.M >> -jy.E;
                        return ji > 5000 ? new BF(1, 5000) : ji < -5000 ? new BF(1, -5000) : new BF(1, (int)ji);
                    }
                }
                BF t = (BF.From(b) * Log(x)).R();
                if (!t.IsZero && t.Top > 12)
                {
                    return t.Sign > 0 ? new BF(1, 3000) : new BF(1, -3000);
                }
                return ExpBF(t);
            }
            case Fn.Cbrt: return CbrtExact(a, out sticky);
            case Fn.Hypot: return HypotExact(a, b, out sticky);
            default: throw new ArgumentOutOfRangeException(nameof(fn));
        }
    }

    private static bool IsPowerOfTwo(double d, out int exp)
    {
        BF v = BF.From(d);
        BigInteger m = v.M;
        exp = 0;
        if (m.Sign <= 0 || (m & (m - 1)) != 0)
        {
            return false;
        }
        exp = (int)(v.E + m.GetBitLength() - 1);
        return true;
    }

    private static void Decompose(double d, out BigInteger m, out int e)
    {
        BF v = BF.From(Math.Abs(d));
        m = v.M;
        e = v.E;
    }

    private static BF HypotExact(double a, double b, out bool sticky)
    {
        Decompose(a, out BigInteger ma, out int ea);
        Decompose(b, out BigInteger mb, out int eb);
        int e0 = Math.Min(ea, eb);
        BigInteger n = (ma * ma << (2 * (ea - e0))) + (mb * mb << (2 * (eb - e0)));
        int t = (int)Math.Max(0, 2 * 160 - (long)n.GetBitLength());
        t += t & 1;
        BigInteger nn = n << t;
        BigInteger r = BF.ISqrt(nn);
        sticky = r * r != nn;
        return new BF(r, e0 - t / 2);
    }

    private static BF CbrtExact(double a, out bool sticky)
    {
        Decompose(a, out BigInteger m, out int e);
        int t = (int)Math.Max(0, 3 * 160 - (long)m.GetBitLength());
        while (((e - t) % 3) != 0)
        {
            t++;
        }
        BigInteger n = m << t;
        BigInteger r = BF.ICbrt(n);
        sticky = r * r * r != n;
        BF v = new(r, (e - t) / 3);
        return a < 0 ? -v : v;
    }

    // ----- final rounding -----

    /// <summary>Rounds to the nearest double (ties to even; ties decided upward if <paramref name="sticky"/>), with subnormals and overflow.</summary>
    public static double ToDouble(BF v, bool sticky)
    {
        if (v.IsZero)
        {
            return 0.0;
        }
        bool neg = v.Sign < 0;
        BigInteger m = BigInteger.Abs(v.M);
        long bl = (long)m.GetBitLength();
        long q = Math.Max(v.E + bl - 53, -1074);
        BigInteger mant = RoundShift(m, v.E, q, sticky);
        if (mant.GetBitLength() > 53)
        {
            mant >>= 1;
            q++;
        }
        long bits;
        long ml = (long)mant;
        if (ml == 0)
        {
            bits = 0;
        }
        else if (ml < (1L << 52))
        {
            bits = ml; // subnormal: q is -1074 here
        }
        else
        {
            long be = q + 1075;
            bits = be >= 2047 ? 0x7FF0000000000000L : (be << 52) | (ml - (1L << 52));
        }
        double r = BitConverter.Int64BitsToDouble(bits);
        return neg ? -r : r;
    }

    /// <summary>Rounds to the nearest float32 (same conventions as <see cref="ToDouble"/>).</summary>
    public static float ToSingle(BF v, bool sticky)
    {
        if (v.IsZero)
        {
            return 0.0f;
        }
        bool neg = v.Sign < 0;
        BigInteger m = BigInteger.Abs(v.M);
        long bl = (long)m.GetBitLength();
        long q = Math.Max(v.E + bl - 24, -149);
        BigInteger mant = RoundShift(m, v.E, q, sticky);
        if (mant.GetBitLength() > 24)
        {
            mant >>= 1;
            q++;
        }
        int ml = (int)mant;
        int bits;
        if (ml == 0)
        {
            bits = 0;
        }
        else if (ml < (1 << 23))
        {
            bits = ml;
        }
        else
        {
            long be = q + 150;
            bits = be >= 255 ? 0x7F800000 : (int)((be << 23) | (long)(ml - (1 << 23)));
        }
        float r = BitConverter.Int32BitsToSingle(bits);
        return neg ? -r : r;
    }

    /// <summary>Integer mantissa of m * 2^e at ulp exponent q, rounded to nearest even.</summary>
    private static BigInteger RoundShift(BigInteger m, int e, long q, bool sticky)
    {
        long shift = q - e;
        if (shift <= 0)
        {
            return m << (int)(-shift);
        }
        if (shift > m.GetBitLength() + 1)
        {
            return BigInteger.Zero;
        }
        BigInteger mant = m >> (int)shift;
        BigInteger rem = m - (mant << (int)shift);
        BigInteger half = BigInteger.One << (int)(shift - 1);
        int c = rem.CompareTo(half);
        if (c > 0 || (c == 0 && (sticky || !mant.IsEven)))
        {
            mant += 1;
        }
        return mant;
    }
}
