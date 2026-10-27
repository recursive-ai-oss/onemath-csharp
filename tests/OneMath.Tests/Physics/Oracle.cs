using System;

namespace RecursiveAI.OneMath.Tests.PhysicsTests;

/// <summary>A double-double number (hi + lo, about 106 bits) built from error-free transformations; a test oracle.</summary>
internal readonly struct Dd
{
    internal readonly double Hi;
    internal readonly double Lo;

    internal Dd(double hi, double lo)
    {
        Hi = hi;
        Lo = lo;
    }

    internal static Dd From(double x) => new(x, 0);

    internal static Dd TwoSum(double a, double b)
    {
        double s = a + b;
        double bb = s - a;
        return new Dd(s, (a - (s - bb)) + (b - bb));
    }

    // Dekker's split, so no fused multiply-add is needed; inputs here stay far below overflow.
    private static void Split(double a, out double hi, out double lo)
    {
        double t = 134217729.0 * a;
        hi = t - (t - a);
        lo = a - hi;
    }

    internal static Dd TwoProd(double a, double b)
    {
        double p = a * b;
        Split(a, out double ah, out double al);
        Split(b, out double bh, out double bl);
        double err = ((ah * bh - p) + ah * bl + al * bh) + al * bl;
        return new Dd(p, err);
    }

    public static Dd operator +(Dd a, Dd b)
    {
        Dd s = TwoSum(a.Hi, b.Hi);
        Dd t = TwoSum(a.Lo, b.Lo);
        double lo = s.Lo + t.Hi;
        Dd u = TwoSum(s.Hi, lo);
        lo = t.Lo + u.Lo;
        return TwoSum(u.Hi, lo);
    }

    public static Dd operator -(Dd a) => new(-a.Hi, -a.Lo);

    public static Dd operator -(Dd a, Dd b) => a + (-b);

    public static Dd operator *(Dd a, Dd b)
    {
        Dd p = TwoProd(a.Hi, b.Hi);
        double lo = p.Lo + (a.Hi * b.Lo + a.Lo * b.Hi);
        return TwoSum(p.Hi, lo);
    }

    internal double Abs => Math.Abs(Hi + Lo);
}

/// <summary>Seeded generator for the tests (SplitMix64).</summary>
internal sealed class TestRng
{
    private ulong _s;

    internal TestRng(ulong seed) => _s = seed;

    internal ulong Next()
    {
        ulong z = unchecked(_s += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform in [0, 1) with 53 bits.</summary>
    internal double Unit() => (Next() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>Uniform in [-1, 1).</summary>
    internal double Signed() => Unit() * 2 - 1;

    internal int Int(int n) => (int)(Next() % (ulong)n);
}

/// <summary>Reconstruction and orthonormality measurements of a decomposition against the double-double oracle.</summary>
internal static class EigenOracle
{
    /// <summary>
    /// Returns (reconstruction, orthonormality, handedness error) for A (upper triangle a11 a12 a13 a22 a23 a33),
    /// eigenvalues lambda and eigenvector columns v (row-major 3x3), all widened to double. Both errors are divided
    /// by the largest entry of A, so multiply by 1/u to get units of the working precision's unit roundoff.
    /// </summary>
    internal static (double Recon, double Ortho, double Det) Measure(double[] a, double[] lambda, double[] v, double denormMin)
    {
        // Scale by a power of two so the double-double products cannot overflow; exact for both sides.
        double max = 0;
        foreach (double x in a)
        {
            max = Math.Max(max, Math.Abs(x));
        }
        int e = max == 0 ? 0 : Math.ILogB(max);
        double[] aa = new double[6];
        for (int i = 0; i < 6; i++)
        {
            aa[i] = Math.ScaleB(a[i], -e);
        }
        double[] ll = new double[3];
        for (int i = 0; i < 3; i++)
        {
            ll[i] = Math.ScaleB(lambda[i], -e);
        }
        double scale = max == 0 ? 1 : Math.ScaleB(max, -e);

        int[,] idx = { { 0, 1, 2 }, { 1, 3, 4 }, { 2, 4, 5 } };
        double recon = 0;
        double ortho = 0;
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Dd sum = default;
                Dd o = default;
                for (int k = 0; k < 3; k++)
                {
                    Dd vik = Dd.From(v[i * 3 + k]);
                    Dd vjk = Dd.From(v[j * 3 + k]);
                    sum += vik * Dd.From(ll[k]) * vjk;
                    // V^T V entry (i, j) uses columns i and j.
                    o += Dd.From(v[k * 3 + i]) * Dd.From(v[k * 3 + j]);
                }
                Dd diff = sum - Dd.From(aa[idx[i, j]]);
                recon = Math.Max(recon, diff.Abs);
                Dd od = o - Dd.From(i == j ? 1 : 0);
                ortho = Math.Max(ortho, od.Abs);
            }
        }
        // Determinant of V (row-major v) by cofactor expansion along row 0, in double-double.
        Dd Vv(int k) => Dd.From(v[k]);
        Dd det = Vv(0) * (Vv(4) * Vv(8) - Vv(5) * Vv(7)) - Vv(1) * (Vv(3) * Vv(8) - Vv(5) * Vv(6)) + Vv(2) * (Vv(3) * Vv(7) - Vv(4) * Vv(6));
        double detErr = (det - Dd.From(1)).Abs;
        // Eigenvalues that land in the subnormal range are quantized by the format itself (at most half a denormal step each,
        // a few steps through the products); that part of the error is not the algorithm's, so it is removed from the figure.
        double floor = Math.ScaleB(4 * denormMin, -e);
        return (Math.Max(0, recon - floor) / scale, ortho, detErr);
    }
}

