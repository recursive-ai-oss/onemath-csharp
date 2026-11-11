using System;
using System.Collections.Generic;
using Xunit;

namespace RecursiveAI.OneMath.Tests;

public class IntervalFunctionTests
{
    private static IntervalD RandomInterval(Random r, bool wide)
    {
        double a = IntervalOracleTests.Pool(r, wide);
        double b;
        switch (r.Next(4))
        {
            case 0:
                b = a;
                break;
            case 1:
                b = a + (r.NextDouble() * 7);   // widths around and beyond one period
                break;
            case 2:
                b = Math.BitIncrement(a);
                break;
            default:
                b = IntervalOracleTests.Pool(r, wide);
                break;
        }
        if (!double.IsFinite(b))
        {
            b = a;
        }
        // Sometimes make a bound infinite.
        double lo = Math.Min(a, b), hi = Math.Max(a, b);
        int inf = r.Next(12);
        if (inf == 0)
        {
            lo = double.NegativeInfinity;
        }
        else if (inf == 1)
        {
            hi = double.PositiveInfinity;
        }
        return new IntervalD(lo, hi);
    }

    private static long Ulps(double a, double b) => Math.Abs(BitConverter.DoubleToInt64Bits(a) - BitConverter.DoubleToInt64Bits(b));

    private static void Check(string name, IntervalD result, double value, IntervalD input)
    {
        if (double.IsNaN(value))
        {
            return;
        }
        Assert.True(result.Contains(value), $"{name}({input}) = {value:R} not in {result}");
    }

    [Fact]
    public void Unary_functions_contain_every_sampled_scalar_result()
    {
        var r = new Random(201);
        int cases = 0;
        for (int i = 0; i < 40000; i++)
        {
            IntervalD x = RandomInterval(r, i % 2 == 1);
            IntervalD sin = IntervalD.Sin(x), cos = IntervalD.Cos(x), exp = IntervalD.Exp(x), log = IntervalD.Log(x);
            IntervalD sqrt = IntervalD.Sqrt(x), sq = IntervalD.Square(x), abs = IntervalD.Abs(x);
            Assert.True(sin.Lo >= -1 && sin.Hi <= 1 && cos.Lo >= -1 && cos.Hi <= 1);
            foreach (double v in IntervalOracleTests.Samples(x, r))
            {
                Check("sin", sin, Math64.Sin(v), x);
                Check("cos", cos, Math64.Cos(v), x);
                Check("exp", exp, Math64.Exp(v), x);
                if (v > 0)
                {
                    Check("log", log, Math64.Log(v), x);
                }
                if (v >= 0)
                {
                    Check("sqrt", sqrt, Math.Sqrt(v), x);
                }
                Check("square", sq, v * v, x);
                Check("abs", abs, Math.Abs(v), x);
                cases++;
            }
        }
        Console.WriteLine($"Unary_functions_contain_every_sampled_scalar_result: {cases} checks");
        Assert.True(cases > 300000);
    }

    [Fact]
    public void Sin_cos_near_extrema_and_quadrant_boundaries()
    {
        // Every interval between neighbours of k*pi/2 for k in a range, with the extremum inside or just outside.
        var r = new Random(202);
        int cases = 0;
        for (int k = -400; k <= 400; k++)
        {
            double c = k * (Math.PI / 2);
            for (int da = -3; da <= 3; da++)
            {
                for (int db = da; db <= 3; db++)
                {
                    double a = Step(c, da), b = Step(c, db);
                    var x = new IntervalD(a, b);
                    var s = IntervalD.Sin(x);
                    var co = IntervalD.Cos(x);
                    foreach (double v in IntervalOracleTests.Samples(x, r))
                    {
                        Check("sin", s, Math64.Sin(v), x);
                        Check("cos", co, Math64.Cos(v), x);
                        cases++;
                    }
                }
            }
        }
        // Wide: starts at many offsets, widths across the period.
        for (int i = 0; i < 20000; i++)
        {
            double a = (r.NextDouble() - 0.5) * 100;
            double w = r.NextDouble() * 8;
            var x = new IntervalD(a, a + w);
            var s = IntervalD.Sin(x);
            var co = IntervalD.Cos(x);
            for (int j = 0; j <= 40; j++)
            {
                double v = a + (w * j / 40);
                v = Math.Min(Math.Max(v, x.Lo), x.Hi);
                Check("sin", s, Math64.Sin(v), x);
                Check("cos", co, Math64.Cos(v), x);
                cases++;
            }
        }
        // A peak is reported exactly: an interval around pi/2 reaches 1.
        Assert.Equal(1.0, IntervalD.Sin(new IntervalD(1.5, 1.6)).Hi);
        Assert.Equal(-1.0, IntervalD.Sin(new IntervalD(4.6, 4.8)).Lo);
        Assert.Equal(1.0, IntervalD.Cos(new IntervalD(-0.1, 0.1)).Hi);
        Assert.Equal(-1.0, IntervalD.Cos(new IntervalD(3.0, 3.3)).Lo);
        // No false peak: a monotone stretch stays below 1.
        Assert.True(IntervalD.Sin(new IntervalD(0.1, 0.2)).Hi < 0.21);
        Console.WriteLine($"Sin_cos_near_extrema_and_quadrant_boundaries: {cases} checks");
        Assert.True(cases > 100000);
    }

    private static double Step(double v, int n)
    {
        for (int i = 0; i < Math.Abs(n); i++)
        {
            v = n > 0 ? Math.BitIncrement(v) : Math.BitDecrement(v);
        }
        return v;
    }

    [Fact]
    public void Atan2_contains_scalar_results_including_cuts_and_poles()
    {
        var r = new Random(203);
        int cases = 0;
        for (int i = 0; i < 40000; i++)
        {
            bool wide = i % 2 == 1;
            IntervalD y = RandomInterval(r, wide), x = RandomInterval(r, wide);
            if (i % 5 == 0)
            {
                // Put the box on the negative x axis or the origin.
                y = new IntervalD(r.Next(2) == 0 ? -0.0 : -r.NextDouble(), r.NextDouble());
                double xl = -r.NextDouble() * 3;
                x = new IntervalD(xl, r.Next(2) == 0 ? 0.0 : Math.Min(0.0, xl + (r.NextDouble() * 3)));
            }
            IntervalD a = IntervalD.Atan2(y, x);
            foreach (double yv in IntervalOracleTests.Samples(y, r))
            {
                foreach (double xv in IntervalOracleTests.Samples(x, r))
                {
                    Check("atan2", a, Math64.Atan2(yv, xv), x);
                    cases++;
                }
            }
        }
        Console.WriteLine($"Atan2_contains_scalar_results_including_cuts_and_poles: {cases} checks");
        Assert.True(cases > 500000);
        // Cut and quadrant examples.
        Assert.True(IntervalD.Atan2(new IntervalD(-1, 1), new IntervalD(-2, -1)).Contains(Math.PI));
        Assert.True(IntervalD.Atan2(new IntervalD(-1, 1), new IntervalD(-2, -1)).Contains(-Math.PI));
        var q1 = IntervalD.Atan2(new IntervalD(1, 2), new IntervalD(1, 2));
        Assert.True(q1.Lo > 0.4 && q1.Hi < 1.2);
    }

    [Fact]
    public void Pow_contains_scalar_results_for_signs_and_integer_exponents()
    {
        var r = new Random(204);
        int cases = 0;
        for (int i = 0; i < 40000; i++)
        {
            IntervalD x = RandomInterval(r, false);
            IntervalD y;
            switch (i % 4)
            {
                case 0:
                    y = new IntervalD(r.Next(-6, 7));   // integer point
                    break;
                case 1:
                    y = new IntervalD(r.Next(-5, 2), r.Next(2, 6));   // contains integers
                    break;
                case 2:
                    y = new IntervalD((r.NextDouble() * 6) - 3);   // non-integer point
                    break;
                default:
                    y = RandomInterval(r, false);
                    break;
            }
            IntervalD p = IntervalD.Pow(x, y);
            foreach (double xv in IntervalOracleTests.Samples(x, r))
            {
                foreach (double yv in IntervalOracleTests.Samples(y, r))
                {
                    if (xv == 0 && yv < 0)
                    {
                        continue;   // pole: outside the domain
                    }
                    double v = Math64.Pow(xv, yv);
                    Check("pow", p, v, x);
                    cases++;
                }
            }
            if (y.Lo == y.Hi && y.Lo == Math.Floor(y.Lo) && Math.Abs(y.Lo) < 100)
            {
                IntervalD pi = IntervalD.Pow(x, (int)y.Lo);
                foreach (double xv in IntervalOracleTests.Samples(x, r))
                {
                    if (xv == 0 && y.Lo < 0)
                    {
                        continue;
                    }
                    Check("powInt", pi, Math64.Pow(xv, y.Lo), x);
                }
                Assert.True(pi == p);
            }
        }
        Console.WriteLine($"Pow_contains_scalar_results_for_signs_and_integer_exponents: {cases} checks");
        Assert.True(cases > 200000);
        // Known shapes.
        Assert.Equal(new IntervalD(0, 4), IntervalD.Pow(new IntervalD(-2, 1), 2));
        Assert.Equal(new IntervalD(-8, 1), IntervalD.Pow(new IntervalD(-2, 1), 3));
        Assert.True(IntervalD.Pow(new IntervalD(-2, -1), new IntervalD(0.5)).IsEmpty);
        Assert.True(IntervalD.Pow(new IntervalD(0, 0), -2).IsEmpty);
        Assert.True(IntervalD.Pow(new IntervalD(-1, 1), -1).IsEntire);
    }

    [Fact]
    public void Elementary_endpoints_are_within_one_ulp_per_side()
    {
        var r = new Random(205);
        double sum = 0;
        long n = 0;
        long worst = 0;
        for (int i = 0; i < 20000; i++)
        {
            double v = (r.NextDouble() - 0.5) * 20;
            var p = new IntervalD(v);
            foreach (IntervalD res in new[] { IntervalD.Sin(p), IntervalD.Cos(p), IntervalD.Exp(p), IntervalD.Atan2(p, new IntervalD(Math.Abs(v) + 0.5)), IntervalD.Pow(new IntervalD(Math.Abs(v) + 0.1), new IntervalD(1.5)) })
            {
                long u = Ulps(res.Lo, res.Hi);
                sum += u / 2.0;
                worst = Math.Max(worst, u);
                n++;
            }
            if (v > 0)
            {
                var l = IntervalD.Log(p);
                long u = Ulps(l.Lo, l.Hi);
                sum += u / 2.0;
                worst = Math.Max(worst, u);
                n++;
            }
        }
        double avg = sum / n;
        Console.WriteLine($"elementary point-input width: average {avg:F3} ulp per endpoint, worst total {worst} ulp");
        Assert.True(worst <= 2);
        Assert.True(avg <= 1.0);
    }
}
