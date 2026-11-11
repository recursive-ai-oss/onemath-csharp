using System;

namespace RecursiveAI.OneMath;

/// <summary>
/// Directed-rounding emulation for double arithmetic. .NET cannot change the FPU rounding mode, so each helper computes the
/// round-to-nearest result and then decides, from the exact sign of the rounding error, whether the true value lies above
/// or below it. The error is obtained with error-free transformations (Knuth's TwoSum; Dekker's product with Veltkamp
/// splitting; the exact residual of a division or square root), which use only IEEE-exact + - * and need no fused
/// multiply-add. When an error-free transformation could itself under- or overflow, the helper falls back to widening the
/// nearest result by one ulp, which always encloses the true value (the nearest result is within half an ulp of it).
/// Reference: Moore, Kearfott, Cloud, "Introduction to Interval Analysis" (2009); IEEE 1788-2015 for the semantics.
/// </summary>
internal static class IntervalRounding
{
    private const double Two500 = 3.2733906078961419e150;   // 2^500
    private const double Two900Inv = 1.3e-271;               // just above 2^-900 (smaller products take the one-ulp fallback)
    private const double Two450Inv = 3.6e-136;               // just above 2^-450
    private const double Split = 134217729.0;               // 2^27 + 1 (Veltkamp)

    // ---- addition ------------------------------------------------------------------------------------------------

    /// <summary>The greatest double not above the real sum a + b. Operands may be infinite but not opposite infinities.</summary>
    internal static double AddDown(double a, double b)
    {
        double s = a + b;
        if (double.IsFinite(s))
        {
            // Knuth TwoSum: e is the exact rounding error, a + b = s + e.
            double bb = s - a;
            double e = (a - (s - bb)) + (b - bb);
            return e < 0 ? Math.BitDecrement(s) : s;
        }
        if (double.IsNaN(s) || double.IsInfinity(a) || double.IsInfinity(b))
        {
            return s;
        }
        return s > 0 ? double.MaxValue : s;   // finite operands overflowed: the real sum exceeds MaxValue
    }

    /// <summary>The least double not below the real sum a + b.</summary>
    internal static double AddUp(double a, double b)
    {
        double s = a + b;
        if (double.IsFinite(s))
        {
            double bb = s - a;
            double e = (a - (s - bb)) + (b - bb);
            return e > 0 ? Math.BitIncrement(s) : s;
        }
        if (double.IsNaN(s) || double.IsInfinity(a) || double.IsInfinity(b))
        {
            return s;
        }
        return s < 0 ? -double.MaxValue : s;
    }

    // ---- multiplication ------------------------------------------------------------------------------------------

    /// <summary>Sign of the exact error a*b - p (p = a*b rounded), or 2 when the transformation is not safe from over/underflow.</summary>
    private static int ProductErrorSign(double a, double b, double p)
    {
        double ap = Math.Abs(p);
        if (Math.Abs(a) > Two500 || Math.Abs(b) > Two500 || !(ap >= Two900Inv))
        {
            return 2;
        }
        return Math.Sign(ProductError(a, b, p));
    }

    // Dekker's exact product error (1971), a*b = p + result, valid when nothing over- or underflows.
    private static double ProductError(double a, double b, double p)
    {
        double c = Split * a;
        double ah = c - (c - a);
        double al = a - ah;
        c = Split * b;
        double bh = c - (c - b);
        double bl = b - bh;
        return (((ah * bh) - p) + (ah * bl) + (al * bh)) + (al * bl);
    }

    /// <summary>The greatest double not above the real product a*b. A zero factor gives 0 even against an infinity (interval convention).</summary>
    internal static double MulDown(double a, double b)
    {
        if (a == 0 || b == 0)
        {
            return 0.0;
        }
        double p = a * b;
        if (double.IsInfinity(a) || double.IsInfinity(b))
        {
            return p;
        }
        if (double.IsInfinity(p))
        {
            return p > 0 ? double.MaxValue : p;
        }
        int s = ProductErrorSign(a, b, p);
        if (s == 2)
        {
            if (p == 0)
            {
                return (a > 0) == (b > 0) ? 0.0 : -double.Epsilon;   // the real product is tiny, nonzero, of the operands' sign
            }
            return Math.BitDecrement(p);
        }
        return s < 0 ? Math.BitDecrement(p) : p;
    }

    /// <summary>The least double not below the real product a*b.</summary>
    internal static double MulUp(double a, double b)
    {
        if (a == 0 || b == 0)
        {
            return 0.0;
        }
        double p = a * b;
        if (double.IsInfinity(a) || double.IsInfinity(b))
        {
            return p;
        }
        if (double.IsInfinity(p))
        {
            return p < 0 ? -double.MaxValue : p;
        }
        int s = ProductErrorSign(a, b, p);
        if (s == 2)
        {
            if (p == 0)
            {
                return (a > 0) == (b > 0) ? double.Epsilon : 0.0;
            }
            return Math.BitIncrement(p);
        }
        return s > 0 ? Math.BitIncrement(p) : p;
    }

    // ---- division ------------------------------------------------------------------------------------------------

    // Sign of (a/b - q) where q = a/b rounded, or 2 when unsafe. a/b - q = (a - q*b)/b and a - q*b is computed exactly.
    private static int QuotientErrorSign(double a, double b, double q)
    {
        double aq = Math.Abs(q);
        if (aq > Two500 || Math.Abs(b) > Two500 || !(aq * Math.Abs(b) >= Two900Inv))
        {
            return 2;
        }
        double p = q * b;
        double e = ProductError(q, b, p);
        double r = (a - p) - e;            // a - p is exact because p is within an ulp of a
        int sr = r > 0 ? 1 : r < 0 ? -1 : 0;
        return b > 0 ? sr : -sr;
    }

    /// <summary>The greatest double not above the real quotient a/b for b != 0. a/infinity is 0 (the interval limit).</summary>
    internal static double DivDown(double a, double b)
    {
        if (a == 0 || double.IsInfinity(b))
        {
            return 0.0;
        }
        double q = a / b;
        if (double.IsInfinity(a))
        {
            return q;
        }
        if (double.IsInfinity(q))
        {
            return q > 0 ? double.MaxValue : q;
        }
        int s = QuotientErrorSign(a, b, q);
        if (s == 2)
        {
            if (q == 0)
            {
                return (a > 0) == (b > 0) ? 0.0 : -double.Epsilon;
            }
            return Math.BitDecrement(q);
        }
        return s < 0 ? Math.BitDecrement(q) : q;
    }

    /// <summary>The least double not below the real quotient a/b for b != 0.</summary>
    internal static double DivUp(double a, double b)
    {
        if (a == 0 || double.IsInfinity(b))
        {
            return 0.0;
        }
        double q = a / b;
        if (double.IsInfinity(a))
        {
            return q;
        }
        if (double.IsInfinity(q))
        {
            return q < 0 ? -double.MaxValue : q;
        }
        int s = QuotientErrorSign(a, b, q);
        if (s == 2)
        {
            if (q == 0)
            {
                return (a > 0) == (b > 0) ? double.Epsilon : 0.0;
            }
            return Math.BitIncrement(q);
        }
        return s > 0 ? Math.BitIncrement(q) : q;
    }

    // ---- square root ---------------------------------------------------------------------------------------------

    /// <summary>The greatest double not above the real square root of a (a >= 0).</summary>
    internal static double SqrtDown(double a)
    {
        if (a == 0 || double.IsPositiveInfinity(a))
        {
            return a;
        }
        double q = Math.Sqrt(a);
        if (q < Two450Inv || q > Two500)
        {
            return Math.BitDecrement(q);
        }
        double p = q * q;
        double r = (a - p) - ProductError(q, q, p);   // sqrt(a) - q has the sign of a - q^2
        return r < 0 ? Math.BitDecrement(q) : q;
    }

    /// <summary>The least double not below the real square root of a (a >= 0).</summary>
    internal static double SqrtUp(double a)
    {
        if (a == 0 || double.IsPositiveInfinity(a))
        {
            return a;
        }
        double q = Math.Sqrt(a);
        if (q < Two450Inv || q > Two500)
        {
            return Math.BitIncrement(q);
        }
        double p = q * q;
        double r = (a - p) - ProductError(q, q, p);
        return r > 0 ? Math.BitIncrement(q) : q;
    }
}
