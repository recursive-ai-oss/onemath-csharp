using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace RecursiveAI.OneMath;

/// <summary>
/// A closed interval [<see cref="Lo"/>, <see cref="Hi"/>] of real numbers with <see cref="double"/> bounds, and the
/// outward-rounded arithmetic on it (Moore, "Interval Analysis", 1966; semantics after IEEE 1788-2015). Every operation
/// returns an interval that contains the exact result for every choice of points in the operands, so
/// <c>f(x)</c> computed by this library's own scalar function lies in the interval returned for any interval containing
/// <c>x</c>. This is the bound evaluation used to prune space in implicit-surface subdivision (Duff, SIGGRAPH 1992).
/// <para>
/// <b>Bounds.</b> A non-empty interval has <c>Lo &lt;= Hi</c>, <c>Lo &lt; +infinity</c> and <c>Hi &gt; -infinity</c>; a bound may
/// be infinite, meaning the interval is unbounded on that side (<see cref="Entire"/> is [-infinity, +infinity]); an infinite
/// bound is a limit, not a member. Zero bounds are stored canonically as <c>Lo = -0.0</c> and <c>Hi = +0.0</c>. The
/// <b>empty</b> interval (<see cref="Empty"/>, both bounds NaN) is the result of an operation whose domain the operand
/// misses; every operation returns <see cref="Empty"/> for an empty operand. <c>default(IntervalD)</c> is the point [0, 0].
/// </para>
/// <para>
/// <b>Rounding.</b> .NET has no rounding-mode control, so each bound is computed round-to-nearest and moved outward by
/// exact bit stepping only when the exact rounding error shows the true value lies beyond it (see <c>docs/intervals.md</c>);
/// results that are exactly representable are returned without widening. Elementary functions widen the correctly rounded
/// scalar result by one ulp. The type is deterministic on every platform.
/// </para>
/// <para>
/// <b>Zero.</b> An endpoint 0 stands for the real number 0. Division and negative powers treat a denominator of exactly
/// zero as a pole outside the domain (the enclosure still covers the infinite limits approached from the nonzero side);
/// <c>atan2</c> honours signed zeros.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial struct IntervalD : IEquatable<IntervalD>
{
    /// <summary>The lower bound; -infinity if unbounded below; NaN for <see cref="Empty"/>.</summary>
    [JsonInclude]
    public readonly double Lo;

    /// <summary>The upper bound; +infinity if unbounded above; NaN for <see cref="Empty"/>.</summary>
    [JsonInclude]
    public readonly double Hi;

    /// <summary>Creates the interval [<paramref name="lo"/>, <paramref name="hi"/>].</summary>
    /// <exception cref="ArgumentOutOfRangeException">A bound is NaN, <paramref name="lo"/> is +infinity or <paramref name="hi"/> is -infinity.</exception>
    /// <exception cref="ArgumentException"><paramref name="lo"/> is greater than <paramref name="hi"/>.</exception>
    [JsonConstructor]
    public IntervalD(double lo, double hi)
    {
        if (double.IsNaN(lo))
        {
            throw new ArgumentOutOfRangeException(nameof(lo), lo, "The lower bound must not be NaN; use IntervalD.Empty for the empty interval.");
        }
        if (double.IsNaN(hi))
        {
            throw new ArgumentOutOfRangeException(nameof(hi), hi, "The upper bound must not be NaN; use IntervalD.Empty for the empty interval.");
        }
        if (double.IsPositiveInfinity(lo))
        {
            throw new ArgumentOutOfRangeException(nameof(lo), lo, "The lower bound must be less than +infinity.");
        }
        if (double.IsNegativeInfinity(hi))
        {
            throw new ArgumentOutOfRangeException(nameof(hi), hi, "The upper bound must be greater than -infinity.");
        }
        if (lo > hi)
        {
            throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"The lower bound {lo:R} is greater than the upper bound {hi:R}."), nameof(lo));
        }
        Lo = lo == 0 ? double.NegativeZero : lo;
        Hi = hi == 0 ? 0.0 : hi;
    }

    /// <summary>Creates the point interval [<paramref name="value"/>, <paramref name="value"/>]. Throws <see cref="ArgumentOutOfRangeException"/> for NaN or an infinity.</summary>
    public IntervalD(double value) : this(value, value)
    {
    }

    // Trusted constructor: the caller guarantees lo <= hi (or both NaN for the empty interval).
    private IntervalD(double lo, double hi, bool trusted)
    {
        _ = trusted;
        Lo = lo == 0 ? double.NegativeZero : lo;
        Hi = hi == 0 ? 0.0 : hi;
    }

    internal static IntervalD Make(double lo, double hi) => new(lo, hi, true);

    /// <summary>The empty interval (both bounds NaN). It contains nothing, and every operation maps it to itself.</summary>
    public static IntervalD Empty => new(double.NaN, double.NaN, true);

    /// <summary>The interval [-infinity, +infinity] containing every real number.</summary>
    public static IntervalD Entire => new(double.NegativeInfinity, double.PositiveInfinity, true);

    /// <summary>True for <see cref="Empty"/>.</summary>
    [JsonIgnore]
    public bool IsEmpty => double.IsNaN(Lo);

    /// <summary>True when the interval is [-infinity, +infinity].</summary>
    [JsonIgnore]
    public bool IsEntire => double.IsNegativeInfinity(Lo) && double.IsPositiveInfinity(Hi);

    /// <summary>True when the interval is non-empty and both bounds are finite.</summary>
    [JsonIgnore]
    public bool IsBounded => double.IsFinite(Lo) && double.IsFinite(Hi);

    /// <summary>
    /// An upper bound on <c>Hi - Lo</c> (rounded up); +infinity if unbounded; NaN for <see cref="Empty"/>.
    /// </summary>
    [JsonIgnore]
    public double Width => IsEmpty ? double.NaN : IntervalRounding.AddUp(Hi, -Lo);

    /// <summary>
    /// A point inside the interval near its centre: <c>(Lo + Hi) / 2</c> without overflow; 0 for <see cref="Entire"/>;
    /// <c>±double.MaxValue</c> for a half-unbounded interval; NaN for <see cref="Empty"/>.
    /// </summary>
    [JsonIgnore]
    public double Midpoint
    {
        get
        {
            if (IsEmpty)
            {
                return double.NaN;
            }
            if (double.IsInfinity(Lo))
            {
                return double.IsInfinity(Hi) ? 0.0 : -double.MaxValue;
            }
            if (double.IsInfinity(Hi))
            {
                return double.MaxValue;
            }
            double s = Lo + Hi;
            return double.IsInfinity(s) ? (Lo * 0.5) + (Hi * 0.5) : s * 0.5;
        }
    }

    /// <summary>True when <paramref name="x"/> lies in the interval. False for NaN and for the empty interval.</summary>
    public bool Contains(double x) => Lo <= x && x <= Hi;

    /// <summary>True when every point of <paramref name="other"/> lies in this interval (the empty interval is contained in every interval).</summary>
    public bool Contains(IntervalD other) => other.IsEmpty || (Lo <= other.Lo && other.Hi <= Hi);

    /// <summary>True when the two intervals share at least one point.</summary>
    public bool Overlaps(IntervalD other) => !IsEmpty && !other.IsEmpty && Lo <= other.Hi && other.Lo <= Hi;

    /// <summary>The smallest interval containing both operands (an empty operand is ignored).</summary>
    public static IntervalD Hull(IntervalD a, IntervalD b)
    {
        if (a.IsEmpty)
        {
            return b;
        }
        if (b.IsEmpty)
        {
            return a;
        }
        return new(Scalar.Min(a.Lo, b.Lo), Scalar.Max(a.Hi, b.Hi), true);
    }

    /// <summary>The intersection of the operands; <see cref="Empty"/> when they do not overlap.</summary>
    public static IntervalD Intersect(IntervalD a, IntervalD b)
    {
        if (!a.Overlaps(b))
        {
            return Empty;
        }
        return new(Scalar.Max(a.Lo, b.Lo), Scalar.Min(a.Hi, b.Hi), true);
    }

    // ---- arithmetic ----------------------------------------------------------------------------------------------

    /// <summary>Sum. Each bound is the sum of the operands' bounds rounded outward; exact sums are not widened.</summary>
    public static IntervalD operator +(IntervalD a, IntervalD b) =>
        a.IsEmpty || b.IsEmpty ? Empty : new(IntervalRounding.AddDown(a.Lo, b.Lo), IntervalRounding.AddUp(a.Hi, b.Hi), true);

    /// <summary>Difference, rounded outward.</summary>
    public static IntervalD operator -(IntervalD a, IntervalD b) =>
        a.IsEmpty || b.IsEmpty ? Empty : new(IntervalRounding.AddDown(a.Lo, -b.Hi), IntervalRounding.AddUp(a.Hi, -b.Lo), true);

    /// <summary>Negation (exact).</summary>
    public static IntervalD operator -(IntervalD a) => a.IsEmpty ? Empty : new(-a.Hi, -a.Lo, true);

    /// <summary>Sum with a scalar. Throws if <paramref name="b"/> is NaN or infinite.</summary>
    public static IntervalD operator +(IntervalD a, double b) => a + new IntervalD(b);

    /// <summary>Sum with a scalar. Throws if <paramref name="a"/> is NaN or infinite.</summary>
    public static IntervalD operator +(double a, IntervalD b) => new IntervalD(a) + b;

    /// <summary>Difference with a scalar. Throws if <paramref name="b"/> is NaN or infinite.</summary>
    public static IntervalD operator -(IntervalD a, double b) => a - new IntervalD(b);

    /// <summary>Difference from a scalar. Throws if <paramref name="a"/> is NaN or infinite.</summary>
    public static IntervalD operator -(double a, IntervalD b) => new IntervalD(a) - b;

    /// <summary>
    /// Product, rounded outward. A zero factor times an infinite bound is 0 (the limit convention of IEEE 1788), so
    /// <c>[0, 0] * Entire = [0, 0]</c>.
    /// </summary>
    public static IntervalD operator *(IntervalD a, IntervalD b)
    {
        if (a.IsEmpty || b.IsEmpty)
        {
            return Empty;
        }
        double lo;
        double hi;
        if (a.Lo >= 0)
        {
            if (b.Lo >= 0)
            {
                lo = IntervalRounding.MulDown(a.Lo, b.Lo);
                hi = IntervalRounding.MulUp(a.Hi, b.Hi);
            }
            else if (b.Hi <= 0)
            {
                lo = IntervalRounding.MulDown(a.Hi, b.Lo);
                hi = IntervalRounding.MulUp(a.Lo, b.Hi);
            }
            else
            {
                lo = IntervalRounding.MulDown(a.Hi, b.Lo);
                hi = IntervalRounding.MulUp(a.Hi, b.Hi);
            }
        }
        else if (a.Hi <= 0)
        {
            if (b.Lo >= 0)
            {
                lo = IntervalRounding.MulDown(a.Lo, b.Hi);
                hi = IntervalRounding.MulUp(a.Hi, b.Lo);
            }
            else if (b.Hi <= 0)
            {
                lo = IntervalRounding.MulDown(a.Hi, b.Hi);
                hi = IntervalRounding.MulUp(a.Lo, b.Lo);
            }
            else
            {
                lo = IntervalRounding.MulDown(a.Lo, b.Hi);
                hi = IntervalRounding.MulUp(a.Lo, b.Lo);
            }
        }
        else if (b.Lo >= 0)
        {
            lo = IntervalRounding.MulDown(a.Lo, b.Hi);
            hi = IntervalRounding.MulUp(a.Hi, b.Hi);
        }
        else if (b.Hi <= 0)
        {
            lo = IntervalRounding.MulDown(a.Hi, b.Lo);
            hi = IntervalRounding.MulUp(a.Lo, b.Lo);
        }
        else
        {
            lo = Scalar.Min(IntervalRounding.MulDown(a.Lo, b.Hi), IntervalRounding.MulDown(a.Hi, b.Lo));
            hi = Scalar.Max(IntervalRounding.MulUp(a.Lo, b.Lo), IntervalRounding.MulUp(a.Hi, b.Hi));
        }
        return new(lo, hi, true);
    }

    /// <summary>Product with a scalar. Throws if <paramref name="b"/> is NaN or infinite.</summary>
    public static IntervalD operator *(IntervalD a, double b) => a * new IntervalD(b);

    /// <summary>Product with a scalar. Throws if <paramref name="a"/> is NaN or infinite.</summary>
    public static IntervalD operator *(double a, IntervalD b) => new IntervalD(a) * b;

    /// <summary>
    /// Quotient, rounded outward. When the divisor contains 0 the result is the hull of the quotients by its nonzero
    /// points: [0, 0] / d is [0, 0] unless d is exactly [0, 0]; a divisor [0, 0] gives <see cref="Empty"/>; a numerator
    /// containing 0 over a divisor containing 0 gives <see cref="Entire"/>; a one-signed numerator over a divisor with 0
    /// at an end gives a half-unbounded interval (<c>[1, 2] / [0, 4] = [0.25, +inf]</c>), and over a divisor with 0 inside
    /// gives <see cref="Entire"/>. (The hull form of the IEEE 1788-2015 division table.)
    /// </summary>
    public static IntervalD operator /(IntervalD a, IntervalD b)
    {
        if (a.IsEmpty || b.IsEmpty)
        {
            return Empty;
        }
        if (b.Lo == 0 && b.Hi == 0)
        {
            return Empty;
        }
        if (b.Lo > 0 || b.Hi < 0)
        {
            double lo;
            double hi;
            if (b.Lo > 0)
            {
                if (a.Lo >= 0)
                {
                    lo = IntervalRounding.DivDown(a.Lo, b.Hi);
                    hi = IntervalRounding.DivUp(a.Hi, b.Lo);
                }
                else if (a.Hi <= 0)
                {
                    lo = IntervalRounding.DivDown(a.Lo, b.Lo);
                    hi = IntervalRounding.DivUp(a.Hi, b.Hi);
                }
                else
                {
                    lo = IntervalRounding.DivDown(a.Lo, b.Lo);
                    hi = IntervalRounding.DivUp(a.Hi, b.Lo);
                }
            }
            else if (a.Lo >= 0)
            {
                lo = IntervalRounding.DivDown(a.Hi, b.Hi);
                hi = IntervalRounding.DivUp(a.Lo, b.Lo);
            }
            else if (a.Hi <= 0)
            {
                lo = IntervalRounding.DivDown(a.Hi, b.Lo);
                hi = IntervalRounding.DivUp(a.Lo, b.Hi);
            }
            else
            {
                lo = IntervalRounding.DivDown(a.Hi, b.Hi);
                hi = IntervalRounding.DivUp(a.Lo, b.Hi);
            }
            return new(lo, hi, true);
        }
        // The divisor contains zero (and is not [0, 0]).
        if (a.Lo == 0 && a.Hi == 0)
        {
            return new(0.0, 0.0, true);
        }
        if (a.Lo <= 0 && a.Hi >= 0)
        {
            return Entire;
        }
        if (b.Lo < 0 && b.Hi > 0)
        {
            return Entire;
        }
        if (a.Lo > 0)
        {
            // positive numerator
            return b.Lo == 0
                ? new(IntervalRounding.DivDown(a.Lo, b.Hi), double.PositiveInfinity, true)
                : new(double.NegativeInfinity, IntervalRounding.DivUp(a.Lo, b.Lo), true);
        }
        // negative numerator
        return b.Lo == 0
            ? new(double.NegativeInfinity, IntervalRounding.DivUp(a.Hi, b.Hi), true)
            : new(IntervalRounding.DivDown(a.Hi, b.Lo), double.PositiveInfinity, true);
    }

    /// <summary>Quotient by a scalar. Throws if <paramref name="b"/> is NaN or infinite; a zero scalar gives <see cref="Empty"/>.</summary>
    public static IntervalD operator /(IntervalD a, double b) => a / new IntervalD(b);

    /// <summary>Quotient of a scalar by an interval. Throws if <paramref name="a"/> is NaN or infinite.</summary>
    public static IntervalD operator /(double a, IntervalD b) => new IntervalD(a) / b;

    /// <summary>True when both bounds are equal (the empty interval equals itself; +0 equals -0).</summary>
    public static bool operator ==(IntervalD a, IntervalD b) => a.Equals(b);

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(IntervalD a, IntervalD b) => !a.Equals(b);

    /// <summary>Value equality; the empty interval equals itself and +0 equals -0.</summary>
    public bool Equals(IntervalD other) => Lo.Equals(other.Lo) && Hi.Equals(other.Hi);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is IntervalD i && Equals(i);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Scalar.Bits(Lo), Scalar.Bits(Hi));

    /// <inheritdoc />
    public override string ToString() =>
        IsEmpty ? "[empty]" : string.Create(CultureInfo.InvariantCulture, $"[{Lo:R}, {Hi:R}]");
}
