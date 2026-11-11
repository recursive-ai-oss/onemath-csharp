using System;
using System.Text.Json;
using Xunit;

namespace RecursiveAI.OneMath.Tests;

public class IntervalBasicTests
{
    private static IntervalD I(double lo, double hi) => new(lo, hi);

    [Fact]
    public void Construction_rejects_bad_bounds_with_typed_errors()
    {
        Assert.Equal("lo", Assert.Throws<ArgumentOutOfRangeException>(() => I(double.NaN, 1)).ParamName);
        Assert.Equal("hi", Assert.Throws<ArgumentOutOfRangeException>(() => I(1, double.NaN)).ParamName);
        Assert.Equal("lo", Assert.Throws<ArgumentException>(() => I(2, 1)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => I(double.PositiveInfinity, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => I(double.NegativeInfinity, double.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntervalD(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => I(0, 1) + double.PositiveInfinity);
        Assert.True(I(double.NegativeInfinity, double.PositiveInfinity).IsEntire);
        Assert.True(IntervalD.Empty.IsEmpty);
        Assert.False(IntervalD.Empty.Contains(0));
        Assert.Equal(0, BitConverter.DoubleToInt64Bits(I(-0.0, -0.0).Hi));
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(I(0.0, 1).Lo));
    }

    [Fact]
    public void Exact_operations_are_not_widened()
    {
        Assert.Equal(I(4, 6), I(1, 2) + I(3, 4));
        Assert.Equal(I(-3, -1), I(1, 2) - I(3, 4));
        Assert.Equal(I(3, 8), I(1, 2) * I(3, 4));
        Assert.Equal(I(-6, 6), I(-2, 3) * I(-2, 2));
        Assert.Equal(I(0.5, 2), I(1, 2) / I(1, 2));
        Assert.Equal(I(2, 4), IntervalD.Sqrt(I(4, 16)));
        Assert.Equal(I(0, 4), IntervalD.Sqrt(I(-5, 16)));
        Assert.True(IntervalD.Sqrt(I(-5, -1)).IsEmpty);
        Assert.Equal(I(1, 1), IntervalD.Exp(I(0, 0)));
        Assert.Equal(I(0, 0), IntervalD.Log(I(1, 1)));
        Assert.Equal(I(0, 0), IntervalD.Sin(I(0, 0)));
        Assert.Equal(I(1, 1), IntervalD.Cos(I(0, 0)));
        Assert.Equal(I(0, 3), IntervalD.Abs(I(-3, 2)));
        Assert.Equal(I(2, 3), IntervalD.Abs(I(-3, -2)));
        Assert.Equal(I(-3, 2), IntervalD.Min(I(-3, 5), I(1, 2)));
        Assert.Equal(I(1, 5), IntervalD.Max(I(-3, 5), I(1, 2)));
        Assert.Equal(I(-2, -1), -I(1, 2));
    }

    [Fact]
    public void Square_is_tighter_than_self_product()
    {
        var x = I(-2, 3);
        Assert.Equal(I(0, 9), IntervalD.Square(x));
        Assert.Equal(I(-6, 9), x * x);
        Assert.Equal(I(4, 9), IntervalD.Square(I(-3, -2)));
    }

    [Fact]
    public void Division_by_zero_containing_intervals()
    {
        Assert.True((I(1, 2) / I(0, 0)).IsEmpty);
        Assert.Equal(I(0, 0), I(0, 0) / I(-1, 1));
        Assert.True((I(-1, 1) / I(-1, 1)).IsEntire);
        Assert.True((I(1, 2) / I(-1, 1)).IsEntire);
        Assert.Equal(I(0.25, double.PositiveInfinity), I(1, 2) / I(0, 4));
        Assert.Equal(I(double.NegativeInfinity, -0.25), I(1, 2) / I(-4, 0));
        Assert.Equal(I(double.NegativeInfinity, -0.25), I(-2, -1) / I(0, 4));
        Assert.Equal(I(0.25, double.PositiveInfinity), I(-2, -1) / I(-4, 0));
        Assert.Equal(I(0, 0), I(0, 0) * IntervalD.Entire);
        var q = I(1, double.PositiveInfinity) / I(1, double.PositiveInfinity);
        Assert.True(q.Lo == 0 && double.IsPositiveInfinity(q.Hi));
    }

    [Fact]
    public void Empty_propagates_and_set_operations()
    {
        var e = IntervalD.Empty;
        Assert.True((e + I(1, 2)).IsEmpty && (I(1, 2) * e).IsEmpty && (e / I(1, 2)).IsEmpty && IntervalD.Sin(e).IsEmpty);
        Assert.Equal(I(1, 2), IntervalD.Hull(e, I(1, 2)));
        Assert.Equal(I(0, 5), IntervalD.Hull(I(0, 1), I(4, 5)));
        Assert.True(IntervalD.Intersect(I(0, 1), I(2, 3)).IsEmpty);
        Assert.Equal(I(1, 1), IntervalD.Intersect(I(0, 1), I(1, 3)));
        Assert.True(I(0, 4).Contains(I(1, 2)) && !I(1, 2).Contains(I(0, 4)) && I(0, 1).Contains(e));
        Assert.True(I(0, 1).Overlaps(I(1, 2)) && !I(0, 1).Overlaps(I(1.5, 2)));
    }

    [Fact]
    public void Width_and_midpoint()
    {
        Assert.Equal(3.0, I(1, 4).Width);
        Assert.True(I(1, 4).Width >= 3.0);
        Assert.Equal(2.5, I(1, 4).Midpoint);
        Assert.Equal(0.0, IntervalD.Entire.Midpoint);
        Assert.Equal(double.MaxValue, I(0, double.PositiveInfinity).Midpoint);
        Assert.Equal(0.0, I(-double.MaxValue, double.MaxValue).Midpoint);
        Assert.True(double.IsPositiveInfinity(IntervalD.Entire.Width));
        Assert.True(double.IsNaN(IntervalD.Empty.Width));
        double m = I(1e308, double.MaxValue).Midpoint;
        Assert.True(m >= 1e308 && m <= double.MaxValue);
    }

    [Fact]
    public void Atan2_cut_and_sectors()
    {
        var s = IntervalD.Atan2(I(-1, 1), I(-2, -1));
        Assert.True(s.Lo < -Math.PI && s.Hi > Math.PI);
        var u = IntervalD.Atan2(I(1, double.PositiveInfinity), I(1, double.PositiveInfinity));
        Assert.True(u.Lo == 0 && u.Hi > Math.PI / 2 && u.Hi < 1.6);
    }

    [Fact]
    public void Json_round_trip_of_non_empty_intervals()
    {
        var x = I(-1.25, double.MaxValue);
        string j = JsonSerializer.Serialize(x);
        Assert.Equal(x, JsonSerializer.Deserialize<IntervalD>(j));
        var b = new Interval3D(I(0, 1), I(-2, 3), I(4, 4));
        Assert.Equal(b, JsonSerializer.Deserialize<Interval3D>(JsonSerializer.Serialize(b)));
    }

    [Fact]
    public void Interval3D_operations_enclose_point_results()
    {
        var r = new Random(301);
        int cases = 0;
        for (int i = 0; i < 20000; i++)
        {
            Interval3D a = Box(r), b = Box(r);
            double s = (r.NextDouble() - 0.5) * 8;
            var sum = a + b;
            var diff = a - b;
            var scaled = a * s;
            var dot = Interval3D.Dot(a, b);
            var len = a.Length();
            var ab = Interval3D.Abs(a);
            var mn = Interval3D.Min(a, b);
            var mx = Interval3D.Max(a, b);
            for (int k = 0; k < 8; k++)
            {
                Vector3D p = Pick(a, r), q = Pick(b, r);
                Assert.True(sum.Contains(p + q) || OverflowRounding(p, q), $"sum {a} {b}");
                Assert.True(diff.Contains(p - q), $"diff {a} {b} {p} {q}");
                Assert.True(scaled.Contains(p * s), $"scale {a} {s} {p}");
                // The dot product rounds each term and the sum; compare with the exact value via a margin of the interval.
                double d = (p.X * q.X) + (p.Y * q.Y) + (p.Z * q.Z);
                Assert.True(dot.Contains(d) || dot.Width > 1e300, $"dot {a} {b} -> {dot} vs {d:R}");
                double l = Math.Sqrt((p.X * p.X) + (p.Y * p.Y) + (p.Z * p.Z));
                Assert.True(len.Contains(l), $"length {a} -> {len} vs {l:R}");
                Assert.True(ab.Contains(new Vector3D(Math.Abs(p.X), Math.Abs(p.Y), Math.Abs(p.Z))));
                Assert.True(mn.Contains(Vector3D.Min(p, q)) && mx.Contains(Vector3D.Max(p, q)));
                cases++;
            }
        }
        Console.WriteLine($"Interval3D_operations_enclose_point_results: {cases} checks");
        Assert.True(cases > 100000);
        Assert.True(Interval3D.FromBounds(new Bounds3D(new Vector3D(-1, -1, -1), new Vector3D(1, 2, 3))).Contains(new Vector3D(0, 2, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Interval3D().Equals(new Interval3D(new Vector3D(double.NaN, 0, 0))));
    }

    private static bool OverflowRounding(Vector3D p, Vector3D q) => !double.IsFinite((p + q).X) || !double.IsFinite((p + q).Y) || !double.IsFinite((p + q).Z);

    private static Interval3D Box(Random r) => new(Axis(r), Axis(r), Axis(r));

    private static IntervalD Axis(Random r)
    {
        double a = (r.NextDouble() - 0.5) * 20 * (r.Next(8) == 0 ? 1e100 : 1);
        double b = r.Next(4) == 0 ? a : a + (r.NextDouble() * 5);
        return new IntervalD(a, b);
    }

    private static Vector3D Pick(Interval3D b, Random r) => new(PickOne(b.X, r), PickOne(b.Y, r), PickOne(b.Z, r));

    private static double PickOne(IntervalD i, Random r) => r.Next(3) switch
    {
        0 => i.Lo,
        1 => i.Hi,
        _ => Math.Min(i.Hi, Math.Max(i.Lo, (i.Lo * (1 - r.NextDouble())) + (i.Hi * r.NextDouble()))),
    };
}
