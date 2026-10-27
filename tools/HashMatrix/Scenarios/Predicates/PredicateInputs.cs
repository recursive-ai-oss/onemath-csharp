using System;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>Which predicate a generated input set is for.</summary>
public enum PredicateKind
{
    /// <summary>3 points in 2D (6 doubles).</summary>
    Orient2D,
    /// <summary>4 points in 3D (12 doubles).</summary>
    Orient3D,
    /// <summary>4 points in 2D (8 doubles).</summary>
    InCircle,
    /// <summary>5 points in 3D (15 doubles).</summary>
    InSphere,
}

/// <summary>
/// Deterministic generator of predicate inputs: random, small-integer, wide-exponent, large-offset, and
/// near-degenerate sets (collinear, coplanar, cocircular and cospherical points nudged by a few ulps).
/// Uses only integer arithmetic, exact double operations and <see cref="Math.BitIncrement"/>, so every platform
/// produces the same sets.
/// </summary>
public static class PredicateInputs
{
    /// <summary>Number of doubles a case of this kind needs.</summary>
    public static int Length(PredicateKind kind) => kind switch
    {
        PredicateKind.Orient2D => 6,
        PredicateKind.Orient3D => 12,
        PredicateKind.InCircle => 8,
        _ => 15,
    };

    private struct Rng
    {
        private ulong _s;
        public Rng(ulong seed) => _s = seed;

        // SplitMix64 (Steele, Lea and Flood 2014).
        public ulong Next()
        {
            ulong z = _s += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public int Int(int n) => (int)(Next() % (ulong)n);

        public double Unit() => (Next() >> 11) * (1.0 / 9007199254740992.0); // [0, 1)

        public double Signed() => Unit() * 2.0 - 1.0;
    }

    /// <summary>Fills <paramref name="p"/> (length <see cref="Length"/>) with case number <paramref name="index"/> of the stream <paramref name="seed"/>.</summary>
    public static void Fill(PredicateKind kind, ulong seed, long index, Span<double> p)
    {
        int dim = kind is PredicateKind.Orient2D or PredicateKind.InCircle ? 2 : 3;
        int count = p.Length / dim;
        var rng = new Rng(seed * 0x2545F4914F6CDD1DUL + (ulong)index * 0x9E3779B97F4A7C15UL + 1);
        int mode = rng.Int(10);
        switch (mode)
        {
            case 0:
                for (int i = 0; i < p.Length; i++) { p[i] = rng.Signed(); }
                break;
            case 1:
                for (int i = 0; i < p.Length; i++) { p[i] = rng.Int(17) - 8; }
                break;
            case 2:
                for (int i = 0; i < p.Length; i++) { p[i] = Pow2(rng.Int(41) - 20) * rng.Signed(); }
                break;
            case 3:
                {
                    double b = Pow2(rng.Int(40)) * rng.Signed();
                    for (int i = 0; i < p.Length; i++) { p[i] = b + rng.Signed() * 0.0009765625; }
                    break;
                }
            default:
                Structured(kind, ref rng, p, dim, count);
                break;
        }
        // Rescale by an exact power of two now and then: degeneracy is preserved, exponents move.
        if (rng.Int(4) == 0)
        {
            double s = Pow2(rng.Int(61) - 30);
            for (int i = 0; i < p.Length; i++) { p[i] *= s; }
        }
    }

    private static double Pow2(int e)
    {
        double r = 1.0;
        for (int i = 0; i < Math.Abs(e); i++) { r = e > 0 ? r * 2.0 : r * 0.5; }
        return r;
    }

    private static double Nudge(double x, int ulps)
    {
        if (x == 0.0) { return x; } // stepping off zero would give a subnormal, outside the supported range
        for (; ulps > 0; ulps--) { x = Math.BitIncrement(x); }
        for (; ulps < 0; ulps++) { x = Math.BitDecrement(x); }
        return x;
    }

    private static void Structured(PredicateKind kind, ref Rng rng, Span<double> p, int dim, int count)
    {
        // Variant: 0 exact grid degeneracy, 1..3 floating-point construction nudged by -2..2 ulps.
        int variant = rng.Int(4);
        int ulps = variant == 0 ? 0 : rng.Int(5) - 2;
        double offset = rng.Int(3) == 0 ? Pow2(rng.Int(30)) * rng.Signed() : 0.0;
        switch (kind)
        {
            case PredicateKind.Orient2D:
                {
                    if (variant == 0)
                    {
                        double ax = rng.Int(41) - 20, ay = rng.Int(41) - 20, bx = rng.Int(41) - 20, by = rng.Int(41) - 20;
                        int k = rng.Int(9) - 4;
                        Set(p, 0, ax, ay); Set(p, 2, bx, by); Set(p, 4, ax + k * (bx - ax), ay + k * (by - ay));
                    }
                    else
                    {
                        double ax = rng.Signed(), ay = rng.Signed(), bx = rng.Signed(), by = rng.Signed();
                        double t = rng.Signed() * 2.0;
                        Set(p, 0, ax, ay); Set(p, 2, bx, by); Set(p, 4, ax + t * (bx - ax), ay + t * (by - ay));
                        int which = 4 + rng.Int(2);
                        p[which] = Nudge(p[which], ulps);
                    }
                    break;
                }
            case PredicateKind.Orient3D:
                {
                    if (variant == 0)
                    {
                        double[] a = { rng.Int(21) - 10, rng.Int(21) - 10, rng.Int(21) - 10 };
                        double[] u = { rng.Int(9) - 4, rng.Int(9) - 4, rng.Int(9) - 4 };
                        double[] v = { rng.Int(9) - 4, rng.Int(9) - 4, rng.Int(9) - 4 };
                        int s = rng.Int(7) - 3, t = rng.Int(7) - 3;
                        for (int i = 0; i < 3; i++)
                        {
                            p[i] = a[i]; p[3 + i] = a[i] + u[i]; p[6 + i] = a[i] + v[i];
                            p[9 + i] = a[i] + s * u[i] + t * v[i];
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 9; i++) { p[i] = rng.Signed(); }
                        double s = rng.Signed() * 2.0, t = rng.Signed() * 2.0;
                        for (int i = 0; i < 3; i++)
                        {
                            p[9 + i] = p[i] + s * (p[3 + i] - p[i]) + t * (p[6 + i] - p[i]);
                        }
                        int which = 9 + rng.Int(3);
                        p[which] = Nudge(p[which], ulps);
                    }
                    break;
                }
            case PredicateKind.InCircle:
                {
                    // Integer points on one circle: the 16 symmetric images of (m²-n², 2mn), radius m²+n².
                    int m = rng.Int(6) + 2, n = rng.Int(m - 1) + 1;
                    double u = m * m - n * n, v = 2 * m * n;
                    double cxo = rng.Int(11) - 5, cyo = rng.Int(11) - 5;
                    for (int i = 0; i < 4; i++)
                    {
                        int sym = rng.Int(8);
                        double x = (sym & 1) == 0 ? u : v, y = (sym & 1) == 0 ? v : u;
                        if ((sym & 2) != 0) { x = -x; }
                        if ((sym & 4) != 0) { y = -y; }
                        Set(p, 2 * i, cxo + x, cyo + y);
                    }
                    if (variant != 0)
                    {
                        // Approximate circle instead: points from the rational parametrisation, rounded.
                        for (int i = 0; i < 4; i++)
                        {
                            double t = rng.Signed() * 4.0, w = 1.0 + t * t;
                            Set(p, 2 * i, (1.0 - t * t) / w, 2.0 * t / w);
                        }
                        int which = rng.Int(8);
                        p[which] = Nudge(p[which], ulps);
                    }
                    break;
                }
            default:
                {
                    // Integer points on one sphere: coordinate permutations and sign flips of a base triple.
                    int bx = rng.Int(7), by = rng.Int(7), bz = rng.Int(7);
                    double[] c = { rng.Int(7) - 3, rng.Int(7) - 3, rng.Int(7) - 3 };
                    for (int i = 0; i < 5; i++)
                    {
                        int perm = rng.Int(6), sg = rng.Int(8);
                        double[] x = perm switch
                        {
                            0 => new double[] { bx, by, bz },
                            1 => new double[] { bx, bz, by },
                            2 => new double[] { by, bx, bz },
                            3 => new double[] { by, bz, bx },
                            4 => new double[] { bz, bx, by },
                            _ => new double[] { bz, by, bx },
                        };
                        for (int k = 0; k < 3; k++)
                        {
                            p[3 * i + k] = c[k] + (((sg >> k) & 1) != 0 ? -x[k] : x[k]);
                        }
                    }
                    if (variant != 0)
                    {
                        // Approximate sphere: normalised random directions, nudged.
                        for (int i = 0; i < 5; i++)
                        {
                            double x = rng.Signed(), y = rng.Signed(), z = rng.Signed();
                            double len = Math.Sqrt(x * x + y * y + z * z);
                            if (len == 0.0) { len = 1.0; }
                            p[3 * i] = x / len; p[3 * i + 1] = y / len; p[3 * i + 2] = z / len;
                        }
                        int which = rng.Int(15);
                        p[which] = Nudge(p[which], ulps);
                    }
                    break;
                }
        }
        if (offset != 0.0)
        {
            // Large common offset (rounded): keeps near-degeneracy while stressing cancellation.
            for (int i = 0; i < p.Length; i++) { p[i] += offset; }
        }
        _ = dim; _ = count;
    }

    private static void Set(Span<double> p, int at, double x, double y)
    {
        p[at] = x;
        p[at + 1] = y;
    }
}
