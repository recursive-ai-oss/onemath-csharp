using System;

namespace RecursiveAI.OneMath.Predicates;

/// <summary>
/// Exact floating-point expansion arithmetic (the internal engine under <see cref="ExactPredicates"/> and <see cref="SegmentPredicates"/>; not public API because its preconditions are not checked).
/// <para>
/// An <i>expansion</i> is a sequence of doubles whose exact sum is the value it represents. Every
/// expansion this class produces is <i>zero-eliminated</i> (no zero component), <i>nonoverlapping</i>
/// (the bits of no two components overlap) and sorted by increasing magnitude, so its sign is the sign
/// of its last component and the empty expansion is zero. Inputs must satisfy the same three
/// properties; a single double is always a valid one-component expansion (zero is the empty one).
/// </para>
/// <para>
/// The methods follow Priest (1991), Dekker (1971), Knuth's TwoSum and Shewchuk, "Adaptive Precision
/// Floating-Point Arithmetic and Fast Robust Geometric Predicates" (1997). They use only IEEE-exact
/// <c>+ - *</c> on double with round-to-nearest-even; products are split by Dekker's method, so no
/// fused multiply-add is involved and results are identical on every platform.
/// </para>
/// <para>
/// Exactness holds as long as no intermediate value overflows and no error term underflows: operands
/// whose components lie within about 2^-500 .. 2^500 (or are zero) are safe for sums and scaling;
/// products need the sum of exponent ranges to stay inside double's range. Operations do not check this.
/// </para>
/// </summary>
internal static class ExpansionArithmetic
{
    // 2^27 + 1: splits a 53-bit significand into two 26-bit halves (Dekker 1971; Shewchuk 1997, Sec. 2.2).
    private const double Splitter = 134217729.0;

    /// <summary>Exact sum of two doubles: <c>a + b == x + y</c> with <c>x = fl(a + b)</c> (Knuth's TwoSum, no magnitude precondition).</summary>
    public static void TwoSum(double a, double b, out double x, out double y)
    {
        x = a + b;
        double bVirtual = x - a;
        double aVirtual = x - bVirtual;
        y = (a - aVirtual) + (b - bVirtual);
    }

    /// <summary>Exact difference of two doubles: <c>a - b == x + y</c> with <c>x = fl(a - b)</c>.</summary>
    public static void TwoDiff(double a, double b, out double x, out double y)
    {
        x = a - b;
        double bVirtual = a - x;
        double aVirtual = x + bVirtual;
        y = (a - aVirtual) + (bVirtual - b);
    }

    // Dekker's FastTwoSum: valid when |a| >= |b| or a == 0 (or the exponent of a is not below b's).
    private static void FastTwoSum(double a, double b, out double x, out double y)
    {
        x = a + b;
        y = b - (x - a);
    }

    private static void Split(double a, out double hi, out double lo)
    {
        double c = Splitter * a;
        double abig = c - a;
        hi = c - abig;
        lo = a - hi;
    }

    /// <summary>
    /// Exact product of two doubles: <c>a * b == x + y</c> with <c>x = fl(a * b)</c> (Dekker's TwoProduct by splitting).
    /// Requires |a|, |b| below 2^995 and that the error term does not underflow (|a * b| above about 2^-969).
    /// </summary>
    public static void TwoProduct(double a, double b, out double x, out double y)
    {
        x = a * b;
        Split(a, out double ahi, out double alo);
        Split(b, out double bhi, out double blo);
        double err1 = x - ahi * bhi;
        double err2 = err1 - alo * bhi;
        double err3 = err2 - ahi * blo;
        y = alo * blo - err3;
    }

    /// <summary>
    /// Writes the exact sum <c>e + f</c> to <paramref name="h"/> and returns its length.
    /// <paramref name="h"/> must hold at least <c>e.Length + f.Length</c> doubles and must not overlap the inputs.
    /// </summary>
    public static int Sum(ReadOnlySpan<double> e, ReadOnlySpan<double> f, Span<double> h)
    {
        if (h.Length < e.Length + f.Length)
        {
            throw new ArgumentException("Output span must hold e.Length + f.Length components.", nameof(h));
        }
        e.CopyTo(h);
        int len = e.Length;
        for (int j = 0; j < f.Length; j++)
        {
            len = Grow(h, len, f[j]);
        }
        return len;
    }

    /// <summary>Writes the exact difference <c>e - f</c> to <paramref name="h"/> (same size rule as <see cref="Sum"/>).</summary>
    public static int Difference(ReadOnlySpan<double> e, ReadOnlySpan<double> f, Span<double> h)
    {
        if (h.Length < e.Length + f.Length)
        {
            throw new ArgumentException("Output span must hold e.Length + f.Length components.", nameof(h));
        }
        e.CopyTo(h);
        int len = e.Length;
        for (int j = 0; j < f.Length; j++)
        {
            len = Grow(h, len, -f[j]);
        }
        return len;
    }

    // Shewchuk 1997, Alg. 3 (Grow-Expansion) with zero elimination, in place: adds b to h[0..len).
    // The new length is at most len + 1.
    private static int Grow(Span<double> h, int len, double b)
    {
        double q = b;
        int n = 0;
        for (int i = 0; i < len; i++)
        {
            TwoSum(q, h[i], out q, out double hh);
            if (hh != 0.0)
            {
                h[n++] = hh;
            }
        }
        if (q != 0.0)
        {
            h[n++] = q;
        }
        return n;
    }

    /// <summary>
    /// Writes the exact product <c>e * b</c> to <paramref name="h"/> and returns its length
    /// (Shewchuk 1997, Alg. 6 Scale-Expansion). <paramref name="h"/> must hold at least <c>2 * e.Length</c> doubles.
    /// </summary>
    public static int Scale(ReadOnlySpan<double> e, double b, Span<double> h)
    {
        if (h.Length < 2 * e.Length)
        {
            throw new ArgumentException("Output span must hold 2 * e.Length components.", nameof(h));
        }
        if (e.Length == 0 || b == 0.0)
        {
            return 0;
        }
        int n = 0;
        TwoProduct(e[0], b, out double q, out double lo);
        if (lo != 0.0)
        {
            h[n++] = lo;
        }
        for (int i = 1; i < e.Length; i++)
        {
            TwoProduct(e[i], b, out double t, out double tt);
            TwoSum(q, tt, out double s, out double hh);
            if (hh != 0.0)
            {
                h[n++] = hh;
            }
            FastTwoSum(t, s, out q, out hh);
            if (hh != 0.0)
            {
                h[n++] = hh;
            }
        }
        if (q != 0.0)
        {
            h[n++] = q;
        }
        return n;
    }

    /// <summary>
    /// Writes the exact product <c>e * f</c> to <paramref name="h"/> and returns its length: the sum of
    /// <c>e * f[j]</c> over the components of <c>f</c>. <paramref name="h"/> must hold at least
    /// <c>2 * e.Length * f.Length</c> doubles. Uses a small per-thread scratch area for intermediates.
    /// </summary>
    public static int Product(ReadOnlySpan<double> e, ReadOnlySpan<double> f, Span<double> h)
    {
        if (h.Length < 2 * e.Length * f.Length)
        {
            throw new ArgumentException("Output span must hold 2 * e.Length * f.Length components.", nameof(h));
        }
        Arena arena = Arena.Get();
        int mark = arena.Mark;
        try
        {
            return arena.Mul(e, f, h);
        }
        finally
        {
            arena.Release(mark);
        }
    }

    /// <summary>Sign (-1, 0, +1) of an expansion: the sign of its last (largest) component.</summary>
    public static int Sign(ReadOnlySpan<double> e)
    {
        if (e.Length == 0)
        {
            return 0;
        }
        double top = e[^1];
        return top > 0.0 ? 1 : top < 0.0 ? -1 : 0;
    }

    /// <summary>Exact comparison of two expansions: the sign of <c>e - f</c>.</summary>
    public static int Compare(ReadOnlySpan<double> e, ReadOnlySpan<double> f)
    {
        Arena arena = Arena.Get();
        int mark = arena.Mark;
        try
        {
            Span<double> d = arena.Alloc(e.Length + f.Length);
            int n = Difference(e, f, d);
            return Sign(d[..n]);
        }
        finally
        {
            arena.Release(mark);
        }
    }

    /// <summary>
    /// Approximation of the value: the components summed from smallest to largest. Within one rounding
    /// error of the last component's neighbourhood; use <see cref="Sign"/> for exact decisions.
    /// </summary>
    public static double Estimate(ReadOnlySpan<double> e)
    {
        double q = 0.0;
        for (int i = 0; i < e.Length; i++)
        {
            q += e[i];
        }
        return q;
    }

    /// <summary>Writes <c>-e</c> to <paramref name="h"/> (at least <c>e.Length</c> doubles) and returns its length.</summary>
    public static int Negate(ReadOnlySpan<double> e, Span<double> h)
    {
        if (h.Length < e.Length)
        {
            throw new ArgumentException("Output span must hold e.Length components.", nameof(h));
        }
        for (int i = 0; i < e.Length; i++)
        {
            h[i] = -e[i];
        }
        return e.Length;
    }
}
