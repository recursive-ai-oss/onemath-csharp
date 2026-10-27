using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Xunit;

namespace RecursiveAI.OneMath.Tests;

public class FunctionTests
{
    private static string KatPath() => Path.Combine(AppContext.BaseDirectory, "functions", "kat.txt");

    [Fact]
    public void Known_answers_are_bit_exact()
    {
        var bad = new List<string>();
        int n = 0;
        foreach (string line in File.ReadLines(KatPath()))
        {
            string[] p = line.Split(' ');
            Spec s = Table.Get(p[0]);
            if (p[1] == "f64")
            {
                double a = BitConverter.UInt64BitsToDouble(ulong.Parse(p[2], NumberStyles.HexNumber));
                double b = BitConverter.UInt64BitsToDouble(ulong.Parse(p[3], NumberStyles.HexNumber));
                ulong want = ulong.Parse(p[4], NumberStyles.HexNumber);
                ulong got = BitConverter.DoubleToUInt64Bits(s.F64(a, b));
                if (got != want)
                {
                    bad.Add($"{p[0]}({a:R}, {b:R}) f64 want {want:x16} got {got:x16}");
                }
            }
            else
            {
                float a = BitConverter.UInt32BitsToSingle(uint.Parse(p[2], NumberStyles.HexNumber));
                float b = BitConverter.UInt32BitsToSingle(uint.Parse(p[3], NumberStyles.HexNumber));
                uint want = uint.Parse(p[4], NumberStyles.HexNumber);
                uint got = BitConverter.SingleToUInt32Bits(s.F32(a, b));
                if (got != want)
                {
                    bad.Add($"{p[0]}({a:R}, {b:R}) f32 want {want:x8} got {got:x8}");
                }
            }
            n++;
        }
        Assert.True(n > 3000);
        Assert.True(bad.Count == 0, $"{bad.Count} of {n} differ:\n" + string.Join("\n", bad.GetRange(0, Math.Min(40, bad.Count))));
    }

    /// <summary>The fast stage and the exact stage must agree on every input; this exercises both on the same points.</summary>
    [Fact]
    public void Fast_stage_agrees_with_exact_stage()
    {
        var bad = new List<string>();
        foreach (Spec s in Table.All)
        {
            var rng = new Rng(0xABCDEFUL + (ulong)s.Name.Length * 7919UL + (ulong)s.Name[0]);
            for (int i = 0; i < 1500; i++)
            {
                (double a, double b) = s.Gen(rng);
                if (s.Name == "pow" && i % 3 == 0)
                {
                    b = Math.Round(b);
                }
                double got = s.F64(a, b);
                double want = ExactRef(s, a, b, out bool ok);
                if (ok && BitConverter.DoubleToInt64Bits(got) != BitConverter.DoubleToInt64Bits(want))
                {
                    bad.Add($"{s.Name}({a:R},{b:R}) f64 got {got:R} want {want:R}");
                }
                float fa = (float)a;
                float fb = (float)b;
                if (float.IsFinite(fa) && float.IsFinite(fb))
                {
                    float g32 = s.F32(fa, fb);
                    float w32 = ExactRef32(s, fa, fb, out bool ok32);
                    if (ok32 && BitConverter.SingleToInt32Bits(g32) != BitConverter.SingleToInt32Bits(w32))
                    {
                        bad.Add($"{s.Name}({fa:R},{fb:R}) f32 got {g32:R} want {w32:R}");
                    }
                }
            }
        }
        Assert.True(bad.Count == 0, $"{bad.Count} differ:\n" + string.Join("\n", bad.GetRange(0, Math.Min(40, bad.Count))));
    }

    /// <summary>Exact-stage reference for in-domain finite inputs; ok is false where the function has special handling.</summary>
    internal static double ExactRef(Spec s, double a, double b, out bool ok)
    {
        ok = InDomain(s, a, b);
        return ok ? Table.ExactD(s.Fn, a, b) : 0;
    }

    internal static float ExactRef32(Spec s, float a, float b, out bool ok)
    {
        ok = InDomain(s, a, b);
        return ok ? Table.ExactF(s.Fn, a, b) : 0;
    }

    internal static bool InDomain(Spec s, double a, double b)
    {
        if (!double.IsFinite(a) || !double.IsFinite(b))
        {
            return false;
        }
        switch (s.Name)
        {
            case "log": case "log2": case "log10": return a > 0;
            case "log1p": return a > -1;
            case "asin": case "acos": return Math.Abs(a) < 1;
            case "atan2": case "hypot": return a != 0 && b != 0;
            case "pow": return a > 0 && Math.Abs(b) < 1e6 && Math.Abs(b * Math.Log(a)) < 4000;
            case "exp": case "exp2": case "expm1": case "sinh": case "cosh": return Math.Abs(a) < 5000;
            case "sin": case "cos": case "tan": return a != 0;
            case "cbrt": case "tanh": case "atan": return a != 0;
            default: return true;
        }
    }
}
