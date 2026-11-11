using System;

namespace RecursiveAI.OneMath;

// Elementary functions on intervals. The scalar functions in Math64 are correctly rounded, so the true value at any point
// is within one ulp of the scalar result; every endpoint value is therefore moved one ulp outward, except where the scalar
// result is exactly the real value (a known special argument).
public readonly partial struct IntervalD
{
    private static readonly double s_piUp = Math.BitIncrement(Math.PI);          // the double above the nearest double to pi: >= pi
    private static readonly double s_halfPiUp = Math.BitIncrement(Math.PI / 2);  // >= pi/2

    private static double Dec(double v) => Math.BitDecrement(v);

    private static double Inc(double v) => Math.BitIncrement(v);

    /// <summary>Absolute value (exact): the set { |x| : x in the interval }.</summary>
    public static IntervalD Abs(IntervalD x)
    {
        if (x.IsEmpty)
        {
            return Empty;
        }
        if (x.Lo >= 0)
        {
            return x;
        }
        if (x.Hi <= 0)
        {
            return new(-x.Hi, -x.Lo, true);
        }
        return new(0.0, Scalar.Max(-x.Lo, x.Hi), true);
    }

    /// <summary>Componentwise minimum: { min(a, b) } over the operands (exact).</summary>
    public static IntervalD Min(IntervalD a, IntervalD b) =>
        a.IsEmpty || b.IsEmpty ? Empty : new(Scalar.Min(a.Lo, b.Lo), Scalar.Min(a.Hi, b.Hi), true);

    /// <summary>Componentwise maximum: { max(a, b) } over the operands (exact).</summary>
    public static IntervalD Max(IntervalD a, IntervalD b) =>
        a.IsEmpty || b.IsEmpty ? Empty : new(Scalar.Max(a.Lo, b.Lo), Scalar.Max(a.Hi, b.Hi), true);

    /// <summary>
    /// The set { x * x }. Tighter than <c>x * x</c>, which treats the two factors as independent and gives a negative lower
    /// bound when the interval contains 0. The result is never below 0.
    /// </summary>
    public static IntervalD Square(IntervalD x)
    {
        if (x.IsEmpty)
        {
            return Empty;
        }
        if (x.Lo >= 0)
        {
            return new(IntervalRounding.MulDown(x.Lo, x.Lo), IntervalRounding.MulUp(x.Hi, x.Hi), true);
        }
        if (x.Hi <= 0)
        {
            return new(IntervalRounding.MulDown(x.Hi, x.Hi), IntervalRounding.MulUp(x.Lo, x.Lo), true);
        }
        double m = Scalar.Max(-x.Lo, x.Hi);
        return new(0.0, IntervalRounding.MulUp(m, m), true);
    }

    /// <summary>
    /// The square root restricted to the domain: { sqrt(x) : x in the interval, x &gt;= 0 }. <see cref="Empty"/> when the
    /// interval lies below 0; a negative lower bound is clipped to 0. Rounded outward.
    /// </summary>
    public static IntervalD Sqrt(IntervalD x)
    {
        if (x.IsEmpty || x.Hi < 0)
        {
            return Empty;
        }
        double lo = x.Lo <= 0 ? 0.0 : IntervalRounding.SqrtDown(x.Lo);
        return new(lo, IntervalRounding.SqrtUp(x.Hi), true);
    }

    /// <summary>
    /// The exponential. Monotone, so the bounds are the scalar values at the endpoints widened one ulp; exp(0) = 1 and the
    /// limits exp(-inf) = 0, exp(+inf) = +inf are exact. The lower bound is never negative.
    /// </summary>
    public static IntervalD Exp(IntervalD x)
    {
        if (x.IsEmpty)
        {
            return Empty;
        }
        double lo = double.IsNegativeInfinity(x.Lo) ? 0.0 : x.Lo == 0 ? 1.0 : Scalar.Max(0.0, Dec(Math64.Exp(x.Lo)));
        double hi = double.IsPositiveInfinity(x.Hi) ? double.PositiveInfinity : x.Hi == 0 ? 1.0 : Inc(Math64.Exp(x.Hi));
        return new(lo, hi, true);
    }

    /// <summary>
    /// The natural logarithm restricted to its domain x &gt; 0. <see cref="Empty"/> when the interval lies at or below 0
    /// (the pole at exactly 0 is outside the domain); a lower bound at or below 0 gives a lower bound of -infinity.
    /// log(1) = 0 is exact.
    /// </summary>
    public static IntervalD Log(IntervalD x)
    {
        if (x.IsEmpty || x.Hi <= 0)
        {
            return Empty;
        }
        double lo = x.Lo <= 0 ? double.NegativeInfinity : x.Lo == 1 ? 0.0 : Dec(Math64.Log(x.Lo));
        double hi = double.IsPositiveInfinity(x.Hi) ? double.PositiveInfinity : x.Hi == 1 ? 0.0 : Inc(Math64.Log(x.Hi));
        return new(lo, hi, true);
    }

    /// <summary>
    /// The sine. The result is exactly [-1, 1] when the interval is unbounded or wider than 6.28; otherwise the quadrant of
    /// each endpoint, which the signs of the correctly rounded sine and cosine determine exactly (no double is a multiple of
    /// pi/2), tells which extrema (+1 at pi/2 + 2 pi k, -1 at 3 pi/2 + 2 pi k) the interval crosses, so no period reduction
    /// of a constructed value is needed. Between extrema the sine is monotone and the bounds are the endpoint values widened
    /// one ulp (sin(0) = 0 is exact); the result is clamped to [-1, 1].
    /// </summary>
    public static IntervalD Sin(IntervalD x) => SinCos(x, true);

    /// <summary>The cosine; same method and guarantees as <see cref="Sin(IntervalD)"/> (cos(0) = 1 is exact).</summary>
    public static IntervalD Cos(IntervalD x) => SinCos(x, false);

    private static IntervalD SinCos(IntervalD x, bool sine)
    {
        if (x.IsEmpty)
        {
            return Empty;
        }
        if (!x.IsBounded)
        {
            return new(-1.0, 1.0, true);
        }
        double w = x.Width;
        if (!(w <= 6.28))
        {
            return new(-1.0, 1.0, true);
        }
        var (sa, ca) = Math64.SinCos(x.Lo);
        var (sb, cb) = Math64.SinCos(x.Hi);
        int qa = Quadrant(sa, ca);
        int qb = Quadrant(sb, cb);
        // Boundaries crossed, counted counter-clockwise from the quadrant of Lo. Boundary j separates quadrant j from j+1:
        // 0 = pi/2 (sin max), 1 = pi (cos min), 2 = 3pi/2 (sin min), 3 = 2pi (cos max). Within one quadrant the interval
        // either stays inside it (narrower than pi/2) or goes all the way round (wider than 3pi/2); 3 separates them.
        int crossed = (qb - qa) & 3;
        if (crossed == 0 && w > 3.0)
        {
            crossed = 4;
        }
        bool hitMax = false;
        bool hitMin = false;
        for (int k = 0; k < crossed; k++)
        {
            int j = (qa + k) & 3;
            if (sine)
            {
                hitMax |= j == 0;
                hitMin |= j == 2;
            }
            else
            {
                hitMax |= j == 3;
                hitMin |= j == 1;
            }
        }
        double ya = sine ? sa : ca;
        double yb = sine ? sb : cb;
        // sin(0) = 0 and cos(0) = 1 are exact; every other value is within one ulp of the scalar result.
        double loA = x.Lo == 0 ? ya : Dec(ya);
        double hiA = x.Lo == 0 ? ya : Inc(ya);
        double loB = x.Hi == 0 ? yb : Dec(yb);
        double hiB = x.Hi == 0 ? yb : Inc(yb);
        double lo = hitMin ? -1.0 : Scalar.Max(-1.0, Scalar.Min(loA, loB));
        double hi = hitMax ? 1.0 : Scalar.Min(1.0, Scalar.Max(hiA, hiB));
        return new(lo, hi, true);
    }

    private static int Quadrant(double sin, double cos)
    {
        bool s = !(sin < 0);
        bool c = !(cos < 0);
        return s ? (c ? 0 : 1) : (c ? 3 : 2);
    }

    /// <summary>
    /// The two-argument arctangent of (<paramref name="y"/>, <paramref name="x"/>), as <see cref="Math64.Atan2"/>
    /// (range [-pi, pi], branch cut along the negative x axis). Signed zeros are honoured: if <paramref name="y"/> contains
    /// 0 and <paramref name="x"/> reaches 0 or below, the result is [-pi, pi] widened one ulp (both cut sides, and the
    /// origin, are reachable). Elsewhere atan2 is continuous and monotone in each argument, so the extremes are at the
    /// corners; if any bound is infinite the result is the bounding half-plane sector instead.
    /// </summary>
    public static IntervalD Atan2(IntervalD y, IntervalD x)
    {
        if (y.IsEmpty || x.IsEmpty)
        {
            return Empty;
        }
        bool yHasZero = y.Lo <= 0 && y.Hi >= 0;
        if (yHasZero && x.Lo <= 0)
        {
            return new(-s_piUp, s_piUp, true);
        }
        if (!y.IsBounded || !x.IsBounded)
        {
            double lo;
            double hi;
            if (yHasZero)
            {
                lo = -s_halfPiUp;   // x > 0: the open right half plane
                hi = s_halfPiUp;
            }
            else if (y.Lo > 0)
            {
                lo = 0.0;
                hi = x.Lo >= 0 ? s_halfPiUp : s_piUp;
                if (x.Hi <= 0)
                {
                    lo = Dec(Math.PI / 2);
                }
            }
            else
            {
                hi = 0.0;
                lo = x.Lo >= 0 ? -s_halfPiUp : -s_piUp;
                if (x.Hi <= 0)
                {
                    hi = Inc(-(Math.PI / 2));
                }
            }
            return new(lo, hi, true);
        }
        double minV = double.PositiveInfinity;
        double maxV = double.NegativeInfinity;
        for (int i = 0; i < 2; i++)
        {
            double yc = i == 0 ? y.Lo : y.Hi;
            for (int j = 0; j < 2; j++)
            {
                double xc = j == 0 ? x.Lo : x.Hi;
                double v = Math64.Atan2(yc, xc);
                bool exact = yc == 0;   // here x > 0, so atan2(0, x) = 0
                double lo = exact ? v : Dec(v);
                double hi = exact ? v : Inc(v);
                minV = Scalar.Min(minV, lo);
                maxV = Scalar.Max(maxV, hi);
            }
        }
        return new(Scalar.Max(minV, -s_piUp), Scalar.Min(maxV, s_piUp), true);
    }

    /// <summary>
    /// The power x<sup>n</sup> for an integer exponent (the set { x^n }). Odd positive powers are monotone; even powers are
    /// monotone in |x|; negative powers divide, with the zero cases of <see cref="op_Division(IntervalD, IntervalD)"/>
    /// (a pole at exactly 0 is outside the domain, so <c>[0, 0]</c> to a negative power is <see cref="Empty"/>). n = 0 gives
    /// [1, 1]. Powers 1 to 4 chain directed multiplications (exact when representable); other bounds are the correctly rounded scalar powers at the endpoints widened one ulp; powers of 0, 1 and -1 are exact.
    /// </summary>
    public static IntervalD Pow(IntervalD x, int n) => x.IsEmpty ? Empty : PowInteger(x, n);

    private static bool PowExact(double a) => a == 0 || a == 1 || a == -1 || double.IsInfinity(a);

    // Powers 1..4 are chained directed multiplications, so a power that is exactly representable (2^3, 1.5^2) is not widened;
    // every other power is the correctly rounded scalar result widened one ulp.
    private static double PowDown(double a, double n)
    {
        if (n >= 1 && n <= 4)
        {
            int k = (int)n;
            return a >= 0 ? Chain(a, k, true) : (k & 1) == 0 ? Chain(-a, k, true) : -Chain(-a, k, false);
        }
        double v = Math64.Pow(a, n);
        return PowExact(a) ? v : Dec(v);
    }

    private static double PowUp(double a, double n)
    {
        if (n >= 1 && n <= 4)
        {
            int k = (int)n;
            return a >= 0 ? Chain(a, k, false) : (k & 1) == 0 ? Chain(-a, k, false) : -Chain(-a, k, true);
        }
        double v = Math64.Pow(a, n);
        return PowExact(a) ? v : Inc(v);
    }

    // m >= 0: m^k rounded down or up at every step (monotone in a non-negative base, so the result bounds the real power).
    private static double Chain(double m, int k, bool down)
    {
        double r = m;
        for (int i = 1; i < k; i++)
        {
            r = down ? IntervalRounding.MulDown(r, m) : IntervalRounding.MulUp(r, m);
        }
        return r;
    }

    // n is an integer-valued double with |n| < 2^53 (or the int exponent).
    private static IntervalD PowInteger(IntervalD x, double n)
    {
        if (n == 0)
        {
            return new(1.0, 1.0, true);
        }
        bool odd = Math.Abs(n % 2.0) == 1.0;
        if (n > 0)
        {
            if (odd)
            {
                return new(PowDown(x.Lo, n), PowUp(x.Hi, n), true);
            }
            IntervalD m = Abs(x);
            return new(Scalar.Max(0.0, PowDown(m.Lo, n)), PowUp(m.Hi, n), true);
        }
        // negative exponent
        if (x.Lo > 0)
        {
            return new(Scalar.Max(0.0, PowDown(x.Hi, n)), PowUp(x.Lo, n), true);
        }
        if (x.Hi < 0)
        {
            return odd
                ? new(PowDown(x.Hi, n), Scalar.Min(0.0, PowUp(x.Lo, n)), true)
                : new(Scalar.Max(0.0, PowDown(x.Lo, n)), PowUp(x.Hi, n), true);
        }
        // x contains 0
        if (x.Lo == 0 && x.Hi == 0)
        {
            return Empty;
        }
        if (!odd)
        {
            double big = Scalar.Max(-x.Lo, x.Hi);
            return new(Scalar.Max(0.0, PowDown(big, n)), double.PositiveInfinity, true);
        }
        if (x.Lo < 0 && x.Hi > 0)
        {
            return Entire;
        }
        return x.Lo == 0
            ? new(Scalar.Max(0.0, PowDown(x.Hi, n)), double.PositiveInfinity, true)
            : new(double.NegativeInfinity, Scalar.Min(0.0, PowUp(x.Lo, n)), true);
    }

    /// <summary>
    /// The power x<sup>y</sup> as <see cref="Math64.Pow"/> computes it, over the points where the scalar result is a real
    /// number. A point exponent that is an integer uses the exact odd/even logic of <see cref="Pow(IntervalD, int)"/>.
    /// Otherwise: for x &gt;= 0, x<sup>y</sup> = exp(y ln x) is bilinear in (y, ln x), so its extremes are at the four
    /// corners; for x &lt; 0 the scalar is real only at integer y, and if the exponent interval contains an integer the
    /// magnitude |x|<sup>y</sup> bounds the result in both signs. Parts of the domain with no real result (x &lt; 0 and no
    /// integer in y) add nothing, so the result may be <see cref="Empty"/>. Poles at x = 0 are outside the domain
    /// (x = 0 with y &lt; 0 contributes +infinity). Bounds are widened one ulp except at x = 0, 1, an infinity, or y = 0, 1.
    /// </summary>
    public static IntervalD Pow(IntervalD x, IntervalD y)
    {
        if (x.IsEmpty || y.IsEmpty)
        {
            return Empty;
        }
        if (y.Lo == y.Hi && double.IsFinite(y.Lo) && Math.Floor(y.Lo) == y.Lo)
        {
            return PowInteger(x, y.Lo);
        }
        IntervalD result = Empty;
        if (x.Hi >= 0)
        {
            result = PowNonNegative(Make(Scalar.Max(x.Lo, 0.0), x.Hi), y);
        }
        if (x.Lo < 0 && Math.Ceiling(y.Lo) <= y.Hi)
        {
            // Negative bases reach only integer exponents; the sign alternates, so bound the magnitude both ways.
            IntervalD mag = PowNonNegative(Make(x.Hi < 0 ? -x.Hi : 0.0, -x.Lo), y);
            result = Hull(result, Make(-mag.Hi, mag.Hi));
        }
        return result;
    }

    // x.Lo >= 0 (possibly the zero endpoint), y any non-empty interval.
    private static IntervalD PowNonNegative(IntervalD x, IntervalD y)
    {
        double lo = double.PositiveInfinity;
        double hi = double.NegativeInfinity;
        for (int i = 0; i < 2; i++)
        {
            double xc = i == 0 ? (x.Lo == 0 ? 0.0 : x.Lo) : x.Hi;   // +0, so pow(0, y < 0) is +infinity
            for (int j = 0; j < 2; j++)
            {
                double yc = j == 0 ? y.Lo : y.Hi;
                double v = Math64.Pow(xc, yc);
                bool exact = xc == 0 || xc == 1 || yc == 0 || yc == 1 || double.IsPositiveInfinity(xc);
                lo = Scalar.Min(lo, exact ? v : Dec(v));
                hi = Scalar.Max(hi, exact ? v : Inc(v));
            }
        }
        return new(Scalar.Max(0.0, lo), hi, true);
    }
}
