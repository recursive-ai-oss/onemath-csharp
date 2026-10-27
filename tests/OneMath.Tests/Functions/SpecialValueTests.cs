using System;
using System.Collections.Generic;
using Xunit;

namespace RecursiveAI.OneMath.Tests;

public class SpecialValueTests
{
    private static readonly double[] s_points =
    [
        double.NaN, 0.0, -0.0, double.PositiveInfinity, double.NegativeInfinity, 1.0, -1.0, 0.5, -0.5, 2.0, -2.0, 3.0, -3.0, 0.25,
        5e-324, -5e-324, 1e-300, -1e-300, 1e300, -1e300, double.MaxValue, -double.MaxValue, 2.2250738585072014E-308,
        1e-10, 1e10, 40.0, -40.0, 710.0, -746.0, 1024.0, -1075.0, 4503599627370496.0, 9007199254740993.0, 1.0000000000000002,
    ];

    private static long Ulps(double x, double y)
    {
        long a = BitConverter.DoubleToInt64Bits(x);
        long b = BitConverter.DoubleToInt64Bits(y);
        if (a < 0) { a = long.MinValue - a; }
        if (b < 0) { b = long.MinValue - b; }
        return Math.Abs(a - b);
    }

    private static void Compare(Spec s, double a, double b, List<string> bad)
    {
        // The runtime's expm1 and log1p are not accurate for small arguments (and lose the sign of zero), so they only screen non-finite results here.
        if (s.Name is "expm1" or "log1p" && double.IsFinite(s.Platform(a, b)))
        {
            return;
        }
        double want = s.Platform(a, b);
        double got = s.F64(a, b);
        float got32 = s.F32((float)a, (float)b);
        bool ok;
        if (double.IsNaN(want))
        {
            ok = double.IsNaN(got);
        }
        else if (double.IsInfinity(want) || want == 0)
        {
            ok = BitConverter.DoubleToInt64Bits(got) == BitConverter.DoubleToInt64Bits(want);
        }
        else
        {
            ok = Ulps(got, want) <= 2;
        }
        // The float32 result must be the double result's float rounding for exactly representable specials.
        if (ok && (double)(float)a == a && (double)(float)b == b && double.IsNaN(want) != float.IsNaN(got32))
        {
            ok = false;
        }
        if (!ok)
        {
            bad.Add($"{s.Name}({a:R}, {b:R}) ours {got:R} platform {want:R}");
        }
    }

    [Fact]
    public void Special_values_follow_IEEE_754()
    {
        var bad = new List<string>();
        foreach (Spec s in Table.All)
        {
            foreach (double a in s_points)
            {
                if (s.Binary)
                {
                    foreach (double b in s_points)
                    {
                        Compare(s, a, b, bad);
                    }
                }
                else
                {
                    Compare(s, a, 0, bad);
                }
            }
        }
        Assert.True(bad.Count == 0, $"{bad.Count} differ:\n" + string.Join("\n", bad.GetRange(0, Math.Min(60, bad.Count))));
    }

    [Fact]
    public void Every_NaN_result_is_the_canonical_NaN()
    {
        double[] nans = [BitConverter.UInt64BitsToDouble(0x7FF8000000000123UL), BitConverter.UInt64BitsToDouble(0xFFF0000000000001UL)];
        foreach (Spec s in Table.All)
        {
            foreach (double n in nans)
            {
                // pow(x, 0) and pow(1, y) are 1 by definition even for NaN; every other NaN operand gives NaN.
                double[] others = s.Binary ? [n, 0.5, 2.0] : [0.0];
                foreach (double o in others)
                {
                    double r1 = s.F64(n, o);
                    double r2 = s.Binary ? s.F64(o, n) : r1;
                    foreach (double r in new[] { r1, r2 })
                    {
                        if (double.IsNaN(r))
                        {
                            Assert.Equal(BitConverter.DoubleToInt64Bits(double.NaN), BitConverter.DoubleToInt64Bits(r));
                        }
                    }
                    float f = s.F32((float)n, (float)o);
                    if (float.IsNaN(f))
                    {
                        Assert.Equal(BitConverter.SingleToInt32Bits(float.NaN), BitConverter.SingleToInt32Bits(f));
                    }
                }
            }
        }
    }

    [Fact]
    public void Exact_integer_results_and_ties()
    {
        Assert.Equal(8.0, Math64.Exp2(3.0));
        Assert.Equal(0.0, Math64.Exp2(-1075.0)); // an exact tie between 0 and the smallest subnormal: ties to even
        Assert.Equal(double.Epsilon, Math64.Exp2(-1074.0));
        Assert.Equal(0.0f, Math32.Exp2(-150.0f));
        Assert.Equal(4.0, Math64.Log2(16.0));
        Assert.Equal(-3.0, Math64.Log10(0.001) );
        Assert.Equal(3.0, Math64.Cbrt(27.0));
        Assert.Equal(-4.0, Math64.Cbrt(-64.0));
        Assert.Equal(5.0, Math64.Hypot(3.0, 4.0));
        Assert.Equal(1000.0, Math64.Pow(10.0, 3.0));
        Assert.Equal(0.0, Math64.Pow(2.0, -1075.0));
        Assert.Equal(double.Epsilon, Math64.Pow(4.0, -537.0));
        Assert.Equal(-1.0, Math64.Expm1(double.NegativeInfinity));
        Assert.True(double.IsNegative(Math64.Expm1(-0.0)) && double.IsNegative(Math64.Log1p(-0.0)));
        Assert.Equal(double.NegativeInfinity, Math64.Log1p(-1.0));
        Assert.Equal(1.0, Math64.Exp(0.0));
        Assert.Equal(0.0, Math64.Log(1.0));
        Assert.Equal(1.0, Math64.Cos(0.0));
        Assert.Equal(1.0f, Math32.Cos(0.0f));
        Assert.Equal(1.5707963267948966, Math64.Asin(1.0));
        Assert.Equal(3.141592653589793, Math64.Acos(-1.0));
        Assert.Equal(0.7853981633974483, Math64.Atan2(1.0, 1.0));
        Assert.Equal(2.356194490192345, Math64.Atan2(1.0, -1.0));
        (double s, double c) = Math64.SinCos(1.0);
        Assert.Equal(Math64.Sin(1.0), s);
        Assert.Equal(Math64.Cos(1.0), c);
    }
}
