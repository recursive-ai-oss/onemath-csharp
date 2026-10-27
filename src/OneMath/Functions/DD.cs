using System;
using System.Runtime.CompilerServices;

namespace RecursiveAI.OneMath;

/// <summary>
/// An unevaluated sum of two doubles, <c>Hi + Lo</c> with |Lo| &lt;= ulp(Hi)/2, giving about 106 bits.
/// Built only from IEEE-exact + - * / and sqrt, so every platform computes the same bits. Algorithms: Knuth and
/// Dekker error-free transformations (see Muller et al., <i>Handbook of Floating-Point Arithmetic</i>, ch. 4).
/// There is no fused multiply-add anywhere: products are made exact with Dekker's splitting.
/// </summary>
internal readonly struct DD
{
    public readonly double Hi;
    public readonly double Lo;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DD(double hi, double lo)
    {
        Hi = hi;
        Lo = lo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DD(double hi)
    {
        Hi = hi;
        Lo = 0.0;
    }

    // Dekker's splitting constant 2^27 + 1. Valid while |a| < 2^996.
    private const double SplitFactor = 134217729.0;

    /// <summary>Exact sum of two doubles (Knuth).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DD TwoSum(double a, double b)
    {
        double s = a + b;
        double bb = s - a;
        double e = (a - (s - bb)) + (b - bb);
        return new DD(s, e);
    }

    /// <summary>Exact sum when |a| &gt;= |b| (or a == 0) (Dekker).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DD QuickTwoSum(double a, double b)
    {
        double s = a + b;
        return new DD(s, b - (s - a));
    }

    /// <summary>Exact product of two doubles (Dekker/Veltkamp splitting, no fused multiply-add).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DD TwoProd(double a, double b)
    {
        double p = a * b;
        double t = SplitFactor * a;
        double ah = t - (t - a);
        double al = a - ah;
        t = SplitFactor * b;
        double bh = t - (t - b);
        double bl = b - bh;
        double e = ((ah * bh - p) + ah * bl + al * bh) + al * bl;
        return new DD(p, e);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DD operator -(DD a) => new(-a.Hi, -a.Lo);

    /// <summary>Sum with relative error below 2^-103 (accurate variant, no cancellation loss).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DD operator +(DD a, DD b)
    {
        DD s = TwoSum(a.Hi, b.Hi);
        DD t = TwoSum(a.Lo, b.Lo);
        double s2 = s.Lo + t.Hi;
        DD u = QuickTwoSum(s.Hi, s2);
        double u2 = u.Lo + t.Lo;
        return QuickTwoSum(u.Hi, u2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DD operator +(DD a, double b)
    {
        DD s = TwoSum(a.Hi, b);
        double s2 = s.Lo + a.Lo;
        return QuickTwoSum(s.Hi, s2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DD operator -(DD a, DD b) => a + (-b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DD operator -(DD a, double b) => a + (-b);

    /// <summary>Product with relative error below 2^-103.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DD operator *(DD a, DD b)
    {
        DD p = TwoProd(a.Hi, b.Hi);
        double p2 = p.Lo + (a.Hi * b.Lo + a.Lo * b.Hi);
        return QuickTwoSum(p.Hi, p2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DD operator *(DD a, double b)
    {
        DD p = TwoProd(a.Hi, b);
        double p2 = p.Lo + a.Lo * b;
        return QuickTwoSum(p.Hi, p2);
    }

    /// <summary>Quotient with relative error below 2^-102 (long division with three quotient digits).</summary>
    public static DD operator /(DD a, DD b)
    {
        double d1 = a.Hi / b.Hi;
        DD r = a - b * d1;
        double d2 = r.Hi / b.Hi;
        r -= b * d2;
        double d3 = r.Hi / b.Hi;
        DD q = QuickTwoSum(d1, d2);
        return q + d3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DD operator /(DD a, double b)
    {
        double d1 = a.Hi / b;
        DD p = TwoProd(d1, b);
        double r = ((a.Hi - p.Hi) - p.Lo) + a.Lo;
        double d2 = r / b;
        return QuickTwoSum(d1, d2);
    }

    /// <summary>Square root with relative error below 2^-102 (one Karp-Markstein correction). Requires a &gt; 0.</summary>
    public static DD Sqrt(DD a)
    {
        double s = Math.Sqrt(a.Hi);
        DD p = TwoProd(s, s);
        double r = ((a.Hi - p.Hi) - p.Lo) + a.Lo;
        double s2 = r / (2.0 * s);
        return QuickTwoSum(s, s2);
    }

    /// <summary>Multiplies by 2^k for a power of two given as a double (exact unless the result leaves the normal range).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DD Scale(double pow2) => new(Hi * pow2, Lo * pow2);
}

/// <summary>Exact powers of two built from bit patterns (no <c>ScaleB</c>, no platform call).</summary>
internal static class Pow2
{
    /// <summary>2^k for -1022 &lt;= k &lt;= 1023.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Of(int k) => BitConverter.Int64BitsToDouble((long)(k + 1023) << 52);

    /// <summary>
    /// x * 2^k, exact whenever the result is a normal number (|k| up to 2046 is split into two exact factors).
    /// </summary>
    public static double Scale(double x, int k)
    {
        if (k >= -1022 && k <= 1023)
        {
            return x * Of(k);
        }
        int k1 = k / 2;
        return x * Of(k1) * Of(k - k1);
    }
}
