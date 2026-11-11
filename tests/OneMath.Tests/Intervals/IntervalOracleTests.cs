using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using Xunit.Sdk;

namespace RecursiveAI.OneMath.Tests;

/// <summary>An exact dyadic rational M * 2^E, used as the oracle for the outward-rounding tests (BigInteger arithmetic is exact).</summary>
public readonly struct Dyadic
{
    internal readonly BigInteger M;
    internal readonly int E;

    private Dyadic(BigInteger m, int e)
    {
        M = m;
        E = e;
    }

    internal static Dyadic From(double d)
    {
        long bits = BitConverter.DoubleToInt64Bits(d);
        bool neg = bits < 0;
        int exp = (int)((bits >> 52) & 0x7FF);
        long frac = bits & 0xFFFFFFFFFFFFFL;
        if (exp == 0x7FF)
        {
            throw new ArgumentException("finite only");
        }
        BigInteger m;
        int e;
        if (exp == 0)
        {
            m = frac;
            e = -1074;
        }
        else
        {
            m = frac | (1L << 52);
            e = exp - 1075;
        }
        return new Dyadic(neg ? -m : m, e);
    }

    internal static int Compare(Dyadic a, Dyadic b)
    {
        int e = Math.Min(a.E, b.E);
        return (a.M << (a.E - e)).CompareTo(b.M << (b.E - e));
    }

    public static Dyadic operator +(Dyadic a, Dyadic b)
    {
        int e = Math.Min(a.E, b.E);
        return new Dyadic((a.M << (a.E - e)) + (b.M << (b.E - e)), e);
    }

    public static Dyadic operator -(Dyadic a, Dyadic b) => a + new Dyadic(-b.M, b.E);

    public static Dyadic operator *(Dyadic a, Dyadic b) => new(a.M * b.M, a.E + b.E);
}

public class IntervalOracleTests
{
    private static readonly double[] s_special =
    {
        0.0, -0.0, 1, -1, 0.5, -0.5, 2, 3, 6.28, 6.2831853071795862, Math.PI, -Math.PI, Math.PI / 2, 2 * Math.PI,
        double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 2.2250738585072014E-308, 1e-300, 1e300, 1e154, 1e-154, 1e-271, 3e150,
    };

    internal static double Pool(Random r, bool wide)
    {
        switch (r.Next(wide ? 9 : 5))
        {
            case 0:
                return s_special[r.Next(s_special.Length)];
            case 1:
                return (r.NextDouble() - 0.5) * 20;
            case 2:
                return r.Next(-6, 7);
            case 3:
            {
                double k = r.Next(-1000000, 1000001);
                double v = k * (Math.PI / 2);
                int ulps = r.Next(-3, 4);
                for (int i = 0; i < Math.Abs(ulps); i++)
                {
                    v = ulps > 0 ? Math.BitIncrement(v) : Math.BitDecrement(v);
                }
                return v;
            }
            case 4:
                return (r.NextDouble() * 2 - 1) * Math.Pow(2, r.Next(-60, 60));
            default:
            {
                while (true)
                {
                    double d = BitConverter.Int64BitsToDouble(r.NextInt64());
                    if (double.IsFinite(d))
                    {
                        return d;
                    }
                }
            }
        }
    }

    private static bool LowerOk(double lo, Dyadic exact) => double.IsNegativeInfinity(lo) || Dyadic.Compare(Dyadic.From(lo), exact) <= 0;

    private static bool UpperOk(double hi, Dyadic exact) => double.IsPositiveInfinity(hi) || Dyadic.Compare(Dyadic.From(hi), exact) >= 0;

    // True when no double lies strictly between the bound and the exact value on the inside (the bound is the exact directed rounding).
    private static bool LowerTight(double lo, Dyadic exact) => double.IsNegativeInfinity(lo) || double.IsInfinity(Math.BitIncrement(lo)) || Dyadic.Compare(Dyadic.From(Math.BitIncrement(lo)), exact) > 0;

    private static bool UpperTight(double hi, Dyadic exact) => double.IsPositiveInfinity(hi) || double.IsInfinity(Math.BitDecrement(hi)) || Dyadic.Compare(Dyadic.From(Math.BitDecrement(hi)), exact) < 0;

    private static bool Safe(double a, double b) =>
        Math.Abs(a) < 1e100 && Math.Abs(b) < 1e100 && (Math.Abs(a) > 1e-100 || a == 0) && (Math.Abs(b) > 1e-100 || b == 0);

    [Fact]
    public void Add_sub_mul_are_exact_directed_roundings()
    {
        var r = new Random(101);
        int cases = 0, tight = 0, safeCases = 0;
        for (int i = 0; i < 60000; i++)
        {
            bool wide = i % 2 == 1;
            double a = Pool(r, wide), b = Pool(r, wide);
            var ia = new IntervalD(a);
            var ib = new IntervalD(b);
            for (int op = 0; op < 3; op++)
            {
                IntervalD res = op == 0 ? ia + ib : op == 1 ? ia - ib : ia * ib;
                Dyadic da = Dyadic.From(a), db = Dyadic.From(b);
                Dyadic exact = op == 0 ? da + db : op == 1 ? da - db : da * db;
                Assert.True(LowerOk(res.Lo, exact), $"op {op} {a:R} {b:R} lo {res.Lo:R}");
                Assert.True(UpperOk(res.Hi, exact), $"op {op} {a:R} {b:R} hi {res.Hi:R}");
                cases++;
                bool isSafe = op != 2 || Safe(a, b);
                bool t = LowerTight(res.Lo, exact) && UpperTight(res.Hi, exact);
                if (isSafe && !(double.IsInfinity(res.Lo) || double.IsInfinity(res.Hi)))
                {
                    safeCases++;
                    Assert.True(t, $"not tight: op {op} {a:R} {b:R} -> {res}");
                }
                if (t)
                {
                    tight++;
                }
            }
        }
        Console.WriteLine($"Add_sub_mul_are_exact_directed_roundings: {safeCases} checks");
        Console.WriteLine($"Add_sub_mul: {cases} point operations, {tight} exact directed (both bounds), {safeCases} in the tight-checked range");
        Assert.True(safeCases > 100000);
        Assert.True(tight > cases / 2);
    }

    [Fact]
    public void Interval_operands_contain_all_point_results()
    {
        var r = new Random(102);
        int cases = 0;
        for (int i = 0; i < 30000; i++)
        {
            double a1 = Pool(r, i % 2 == 1), a2 = Pool(r, i % 2 == 1), b1 = Pool(r, i % 2 == 1), b2 = Pool(r, i % 2 == 1);
            var a = new IntervalD(Math.Min(a1, a2), Math.Max(a1, a2));
            var b = new IntervalD(Math.Min(b1, b2), Math.Max(b1, b2));
            foreach (double x in Samples(a, r))
            {
                foreach (double y in Samples(b, r))
                {
                    Dyadic dx = Dyadic.From(x), dy = Dyadic.From(y);
                    var s = a + b;
                    Assert.True(LowerOk(s.Lo, dx + dy) && UpperOk(s.Hi, dx + dy));
                    var d = a - b;
                    Assert.True(LowerOk(d.Lo, dx - dy) && UpperOk(d.Hi, dx - dy));
                    var m = a * b;
                    Assert.True(LowerOk(m.Lo, dx * dy) && UpperOk(m.Hi, dx * dy), $"mul {a} {b} {x:R} {y:R} -> {m}");
                    if (y != 0)
                    {
                        // x / y compared exactly: q = x/y lies in [lo, hi] iff lo*y <= x <= hi*y (sign of y decides the direction).
                        var q = a / b;
                        Assert.True(QuotientInside(q, dx, dy, y > 0), $"div {a} / {b}, {x:R} / {y:R} -> {q}");
                    }
                    cases++;
                }
            }
        }
        Console.WriteLine($"Interval_operands_contain_all_point_results: {cases} checks");
        Assert.True(cases > 200000);
    }

    private static bool QuotientInside(IntervalD q, Dyadic x, Dyadic y, bool yPositive)
    {
        if (q.IsEmpty)
        {
            return false;
        }
        // lo <= x/y  <=>  lo*y <= x (y > 0) or lo*y >= x (y < 0)
        bool lowerOk = double.IsNegativeInfinity(q.Lo) || Cmp(Dyadic.From(q.Lo) * y, x, yPositive) <= 0;
        bool upperOk = double.IsPositiveInfinity(q.Hi) || Cmp(Dyadic.From(q.Hi) * y, x, yPositive) >= 0;
        return lowerOk && upperOk;
    }

    private static int Cmp(Dyadic loTimesY, Dyadic x, bool yPositive) => yPositive ? Dyadic.Compare(loTimesY, x) : -Dyadic.Compare(loTimesY, x);

    [Fact]
    public void Division_and_sqrt_are_exact_directed_roundings()
    {
        var r = new Random(103);
        int safeCases = 0;
        for (int i = 0; i < 60000; i++)
        {
            bool wide = i % 2 == 1;
            double a = Pool(r, wide), b = Pool(r, wide);
            if (b != 0)
            {
                var q = new IntervalD(a) / new IntervalD(b);
                Assert.True(QuotientInside(q, Dyadic.From(a), Dyadic.From(b), b > 0), $"div {a:R} {b:R} -> {q}");
                if (!wide && a != 0 && Safe(a, b) && double.IsFinite(q.Lo) && double.IsFinite(q.Hi))
                {
                    // 1 ulp at most between the bounds in the safe range, and exact when the quotient is exact.
                    Assert.True(q.Hi == q.Lo || Math.BitIncrement(q.Lo) == q.Hi, $"div width {a:R} {b:R} -> {q}");
                    safeCases++;
                }
            }
            if (a >= 0)
            {
                var s = IntervalD.Sqrt(new IntervalD(a));
                Dyadic da = Dyadic.From(a);
                // lo^2 <= a <= hi^2
                Assert.True(Dyadic.Compare(Dyadic.From(s.Lo) * Dyadic.From(s.Lo), da) <= 0, $"sqrt lo {a:R}");
                Assert.True(double.IsPositiveInfinity(s.Hi) || Dyadic.Compare(Dyadic.From(s.Hi) * Dyadic.From(s.Hi), da) >= 0, $"sqrt hi {a:R}");
                Assert.True(s.Lo == s.Hi || Math.BitIncrement(s.Lo) == s.Hi || (!Safe(a, 1) && Math.BitIncrement(Math.BitIncrement(s.Lo)) >= s.Hi), $"sqrt width {a:R} {s}");
            }
        }
        Console.WriteLine($"Division_and_sqrt_are_exact_directed_roundings: {safeCases} checks");
        Assert.True(safeCases > 5000);
    }

    internal static IEnumerable<double> Samples(IntervalD i, Random r)
    {
        if (i.IsEmpty)
        {
            yield break;
        }
        double lo = double.IsNegativeInfinity(i.Lo) ? -double.MaxValue : i.Lo;
        double hi = double.IsPositiveInfinity(i.Hi) ? double.MaxValue : i.Hi;
        yield return lo;
        yield return hi;
        yield return i.Midpoint;
        yield return Math.BitIncrement(lo) <= hi ? Math.BitIncrement(lo) : lo;
        yield return Math.BitDecrement(hi) >= lo ? Math.BitDecrement(hi) : hi;
        for (int k = 0; k < 3; k++)
        {
            double t = r.NextDouble();
            double v = (lo * (1 - t)) + (hi * t);
            if (double.IsFinite(v) && v >= lo && v <= hi)
            {
                yield return v;
            }
        }
        if (i.Contains(0.0))
        {
            yield return 0.0;
            yield return -0.0;
        }
        if (i.Contains(1.0))
        {
            yield return 1.0;
        }
        if (i.Contains(-1.0))
        {
            yield return -1.0;
        }
    }
}
