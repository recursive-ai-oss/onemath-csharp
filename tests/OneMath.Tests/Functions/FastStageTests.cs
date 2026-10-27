using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveAI.OneMath.Tests;

/// <summary>
/// The fast stage's stated error bound, checked against the double-double stage (about 2^-100 accurate) on many inputs.
/// A fast value must be within a quarter of the bound handed to the rounding test, so a bound violated by the true
/// worst case would have to exceed the measured maximum by more than a factor of four.
/// </summary>
public class FastStageTests
{
    private static R Fast(string n, double a, double b) => n switch
    {
        "sin" => Impl.Sin(a), "cos" => Impl.Cos(a), "tan" => Impl.Tan(a), "asin" => Impl.Asin(a), "acos" => Impl.Acos(a),
        "atan" => Impl.Atan(a), "atan2" => Impl.Atan2(a, b), "exp" => Impl.Exp(a), "exp2" => Impl.Exp2(a), "expm1" => Impl.Expm1(a),
        "log" => Impl.Log(a), "log2" => Impl.Log2(a), "log10" => Impl.Log10(a), "pow" => Impl.Pow(a, b),
        "sinh" => Impl.Sinh(a), "cosh" => Impl.Cosh(a), "tanh" => Impl.Tanh(a),
        _ => throw new ArgumentException(n),
    };

    private static R Slow(string n, double a, double b) => Impl.ByFn(Table.Get(n).Fn, a, b);

    internal static double Value(R r) => (r.M.Hi + r.M.Lo) * 1.0;

    [Fact]
    public void Fast_stage_stays_within_its_error_bound()
    {
        long n = long.TryParse(Environment.GetEnvironmentVariable("ONEMATH_N"), out long nn) ? nn : 200_000;
        var lines = new List<string>();
        var bad = new List<string>();
        foreach (string name in new[] { "sin", "cos", "tan", "asin", "acos", "atan", "atan2", "exp", "exp2", "expm1", "log", "log2", "log10", "pow", "sinh", "cosh", "tanh" })
        {
            Spec s = Table.Get(name);
            double worst = -200;
            long used = 0;
            double worstMargin = -200;
            object gate = new();
            Parallel.For(0, 16, c =>
            {
                var rng = new Rng(0xFA57UL + (ulong)c * 977UL + (ulong)name.Length);
                double lw = -200;
                long lu = 0;
                double lb = -200;
                for (long i = 0; i < n / 16; i++)
                {
                    (double a, double b) = s.Gen(rng);
                    if (name == "pow" && (i & 1) == 0) { b = Math.Round(b); }
                    if (!FunctionTests.InDomain(s, a, b)) { continue; }
                    R f = Fast(name, a, b);
                    if (f.IsSpecial || !f.Fast) { continue; }
                    R sl = Slow(name, a, b);
                    if (sl.IsSpecial || sl.Fast) { continue; }
                    // Compare m * 2^k as exact BigFloat differences only when cheap: both are double-doubles.
                    DD fm = f.M;
                    DD sm = sl.M;
                    if (f.K != sl.K) { continue; }
                    DD d = fm - sm;
                    double rel = Math.Abs(d.Hi) / Math.Abs(sm.Hi);
                    double bits = rel == 0 ? -200 : Math.Log2(rel);
                    lu++;
                    double margin = bits - Math.Log2(f.Delta); // how far below this sample's own bound the error sits
                    if (margin > lb) { lb = margin; }
                    if (bits > lw) { lw = bits; }
                }
                lock (gate) { worst = Math.Max(worst, lw); used += lu; worstMargin = Math.Max(worstMargin, lb); }
            });
            lines.Add($"fast {name}: samples {used}, max relative error 2^{worst:F1}, closest approach to its bound 2^{worstMargin:F1} of it");
            if (worstMargin > -2)
            {
                bad.Add(lines[^1]);
            }
        }
        File.WriteAllLines(Environment.GetEnvironmentVariable("ONEMATH_LOG") ?? "fast-stage.log", lines);
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }
}
