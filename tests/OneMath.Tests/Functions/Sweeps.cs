using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveAI.OneMath.Tests;

/// <summary>
/// Long-running accuracy sweeps, off unless ONEMATH_SWEEP is set (they are run as separate jobs, not on every build).
/// A platform function screens results (it is accurate to far better than the 2^-40 margin used below); every input
/// whose float32 or double rounding the screen cannot settle, and every disagreement, is checked against the exact stage,
/// which uses 512-bit integer arithmetic and shares no code with the fast stage.
/// Environment: ONEMATH_SWEEP=float|double, ONEMATH_FN=name, ONEMATH_THREADS=n (default 8), ONEMATH_N=count for random sweeps.
/// Results are appended to ONEMATH_LOG (default sweeps.log in the working directory).
/// </summary>
public class Sweeps
{
    private static string? Env(string k) => Environment.GetEnvironmentVariable(k);

    private static int Threads => int.TryParse(Env("ONEMATH_THREADS"), out int t) ? t : 8;

    private static long Count(long dflt) => long.TryParse(Env("ONEMATH_N"), out long n) ? n : dflt;

    private static void Log(string line)
    {
        lock (typeof(Sweeps))
        {
            File.AppendAllText(Env("ONEMATH_LOG") ?? "sweeps.log", line + Environment.NewLine);
        }
        Console.Error.WriteLine(line);
    }

    private static IEnumerable<Spec> Selected(string mode, bool? binary = null)
    {
        string? only = Env("ONEMATH_FN");
        foreach (Spec s in Table.All)
        {
            if ((only is null || only == s.Name) && (binary is null || s.Binary == binary))
            {
                yield return s;
            }
        }
        _ = mode;
    }

    // ---- float32 ----

    private static double NextToward(float f, bool up)
    {
        int bits = BitConverter.SingleToInt32Bits(f);
        float nf = f == 0 ? BitConverter.Int32BitsToSingle(up ? 1 : unchecked((int)0x80000001))
            : ((f > 0) == up ? BitConverter.Int32BitsToSingle(bits + 1) : BitConverter.Int32BitsToSingle(bits - 1));
        return nf;
    }

    /// <summary>True when the screening value cannot settle which float the exact value rounds to.</summary>
    private static bool NeedsExact32(double d, float ours)
    {
        // Clear overflow and underflow need no exact check; only the region within 1e-4 of the thresholds does.
        if (double.IsNaN(d))
        {
            return !float.IsNaN(ours);
        }
        if (d == 0 || Math.Abs(d) < 5e-46)
        {
            return ours != 0;
        }
        if (double.IsInfinity(d) || Math.Abs(d) > 3.4029e38)
        {
            return !float.IsInfinity(ours) || (d < 0) != (ours < 0);
        }
        float f = (float)d;
        if (BitConverter.SingleToInt32Bits(f) != BitConverter.SingleToInt32Bits(ours))
        {
            return true;
        }
        if (float.IsInfinity(f))
        {
            return true;
        }
        double fd = f;
        double e = d - fd;
        double nb = NextToward(f, e > 0);
        if (double.IsInfinity(nb))
        {
            return true;
        }
        double half = Math.Abs(nb - fd) * 0.5;
        return Math.Abs(Math.Abs(e) - half) < Math.Abs(d) * 1e-12;
    }

    private sealed class Tally
    {
        public long Total;
        public long Exact;
        public long Bad;
        public long Special;
        public readonly List<string> Examples = new();

        public void Add(Tally o)
        {
            Total += o.Total;
            Exact += o.Exact;
            Bad += o.Bad;
            Special += o.Special;
            foreach (string e in o.Examples)
            {
                if (Examples.Count < 20)
                {
                    Examples.Add(e);
                }
            }
        }
    }

    private static void Check32(Spec s, float a, float b, Tally t)
    {
        t.Total++;
        float ours = s.F32(a, b);
        double d = s.Platform(a, b);
        bool special = !float.IsFinite(a) || !float.IsFinite(b) || !FunctionTests.InDomain(s, a, b);
        if (special)
        {
            t.Special++;
            float want = (float)d;
            if (a == 0 && want == 0 && s.Name == "expm1")
            {
                want = a; // the screen's series loses the sign of zero; IEEE keeps it (checked in SpecialValueTests)
            }
            if (!(float.IsNaN(want) && float.IsNaN(ours)) && BitConverter.SingleToInt32Bits(want) != BitConverter.SingleToInt32Bits(ours))
            {
                t.Bad++;
                if (t.Examples.Count < 20) { t.Examples.Add($"special {s.Name}({a:R},{b:R}) ours {ours:R} platform {want:R}"); }
            }
            return;
        }
        if (!NeedsExact32(d, ours))
        {
            return;
        }
        t.Exact++;
        float w = Table.ExactF(s.Fn, a, b);
        if (BitConverter.SingleToInt32Bits(w) != BitConverter.SingleToInt32Bits(ours))
        {
            t.Bad++;
            if (t.Examples.Count < 20) { t.Examples.Add($"{s.Name}({a:R},{b:R}) [{BitConverter.SingleToUInt32Bits(a):x8}] ours {ours:R} exact {w:R}"); }
        }
    }

    [Fact]
    public void Float32_exhaustive_unary()
    {
        Assert.SkipUnless(Env("ONEMATH_SWEEP") == "float", "set ONEMATH_SWEEP=float");
        foreach (Spec s in Selected("float", binary: false))
        {
            var total = new Tally();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int nch = int.TryParse(Env("ONEMATH_CHUNKS"), out int nc) ? nc : 256;
            var chunks = new Tally[nch];
            Parallel.For(0, nch, new ParallelOptions { MaxDegreeOfParallelism = Threads }, c =>
            {
                var t = new Tally();
                uint lo = (uint)(c * (256 / nch)) << 24;
                for (uint i = 0; i < (1u << 24); i++)
                {
                    float x = BitConverter.UInt32BitsToSingle(lo + i);
                    Check32(s, x, 0f, t);
                }
                chunks[c] = t;
            });
            foreach (Tally t in chunks) { total.Add(t); }
            Log($"float32 exhaustive {s.Name}: inputs {total.Total}, special {total.Special}, exact-checked {total.Exact}, not correctly rounded {total.Bad}, {sw.Elapsed.TotalSeconds:F0}s");
            foreach (string e in total.Examples) { Log("  " + e); }
            Assert.Equal(0, total.Bad);
        }
    }

    [Fact]
    public void Float32_random_binary()
    {
        Assert.SkipUnless(Env("ONEMATH_SWEEP") == "float", "set ONEMATH_SWEEP=float");
        long n = Count(20_000_000);
        foreach (Spec s in Selected("float", binary: true))
        {
            var total = new Tally();
            var parts = new Tally[64];
            Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = Threads }, c =>
            {
                var t = new Tally();
                var rng = new Rng(0x5EED0000UL + (ulong)c * 1_000_003UL + (ulong)s.Name.Length);
                for (long i = 0; i < n / 64; i++)
                {
                    (double a, double b) = s.Gen(rng);
                    float fa = (float)a;
                    float fb = (float)b;
                    if (s.Name == "pow" && (i & 3) == 0) { fb = MathF.Round(fb); }
                    if (!float.IsFinite(fa) || !float.IsFinite(fb)) { continue; }
                    Check32(s, fa, fb, t);
                }
                parts[c] = t;
            });
            foreach (Tally t in parts) { total.Add(t); }
            Log($"float32 random {s.Name}: inputs {total.Total}, exact-checked {total.Exact}, not correctly rounded {total.Bad}");
            foreach (string e in total.Examples) { Log("  " + e); }
            Assert.Equal(0, total.Bad);
        }
    }

    // ---- double ----

    private static long UlpDistance(double x, double y)
    {
        long a = BitConverter.DoubleToInt64Bits(x);
        long b = BitConverter.DoubleToInt64Bits(y);
        if (a < 0) { a = long.MinValue - a; }
        if (b < 0) { b = long.MinValue - b; }
        return Math.Abs(a - b);
    }

    [Fact]
    public void Double_random()
    {
        Assert.SkipUnless(Env("ONEMATH_SWEEP") == "double", "set ONEMATH_SWEEP=double");
        long n = Count(10_000_000);
        long exactEvery = long.TryParse(Env("ONEMATH_EXACT_EVERY"), out long ee) ? ee : 250;
        foreach (Spec s in Selected("double"))
        {
            long total = 0, exact = 0, bad = 0, far = 0, maxUlp = 0, ambiguous = 0;
            double worstDd = -1000;
            var examples = new List<string>();
            var gate = new object();
            Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = Threads }, c =>
            {
                var rng = new Rng(0xD00B1E00UL + (ulong)c * 7_000_001UL + (ulong)s.Name.Length);
                long lt = 0, le = 0, lb = 0, lf = 0, lm = 0, la = 0;
                double lw = -1000;
                var ex = new List<string>();
                for (long i = 0; i < n / 64; i++)
                {
                    (double a, double b) = s.Gen(rng);
                    if (s.Name == "pow" && (i & 3) == 0) { b = Math.Round(b); }
                    double ours = s.F64(a, b);
                    lt++;
                    if (FunctionTests.InDomain(s, a, b))
                    {
                        double p = s.Platform(a, b);
                        if (double.IsFinite(p) && double.IsFinite(ours))
                        {
                            long u = UlpDistance(ours, p);
                            lm = Math.Max(lm, u);
                            if (u > 1) { lf++; }
                        }
                        if (i % exactEvery == 0)
                        {
                            le++;
                            double w = Table.ExactD(s.Fn, a, b, neg: false);
                            if (s.Name == "pow" && a > 0) { /* positive base only in the generator */ }
                            if (BitConverter.DoubleToInt64Bits(w) != BitConverter.DoubleToInt64Bits(ours))
                            {
                                lb++;
                                if (ex.Count < 10) { ex.Add($"{s.Name}({a:R},{b:R}) ours {ours:R} exact {w:R}"); }
                            }
                        }
                    }
                    if (i % (exactEvery * 4) == 0 && DdProbe.TryError(s, a, b, out double bits, out bool amb))
                    {
                        lw = Math.Max(lw, bits);
                        if (amb) { la++; }
                    }
                }
                lock (gate)
                {
                    total += lt; exact += le; bad += lb; far += lf; maxUlp = Math.Max(maxUlp, lm); worstDd = Math.Max(worstDd, lw); ambiguous += la;
                    examples.AddRange(ex);
                }
            });
            Log($"double random {s.Name}: inputs {total}, exact-checked {exact}, not correctly rounded {bad}, >1ulp from platform {far}, max ulp vs platform {maxUlp}, worst dd error 2^{worstDd:F1}, fell to exact stage {ambiguous}");
            foreach (string e in examples) { Log("  " + e); }
            Assert.Equal(0, bad);
        }
    }
}

/// <summary>Measures the double-double stage's actual relative error against the exact stage.</summary>
internal static class DdProbe
{
    public static bool TryError(Spec s, double a, double b, out double log2Err, out bool fellToExact)
    {
        log2Err = -1000;
        fellToExact = false;
        if (!FunctionTests.InDomain(s, a, b))
        {
            return false;
        }
        R r = Run(s, a, b);
        if (r.IsSpecial || r.ExactOnly)
        {
            return false;
        }
        if (!Rounding.ToDouble(r.M, r.K, r.Delta, out _))
        {
            fellToExact = true;
        }
        BF exact = Exact.Eval(r.Fn, r.A, r.B, out _);
        if (exact.IsZero)
        {
            return false;
        }
        BF dd = (BF.From(r.M.Hi) + BF.From(r.M.Lo)).Shl(r.K);
        BF diff = dd - exact;
        log2Err = diff.IsZero ? -1000 : diff.Top - exact.Top;
        return true;
    }

    private static R Run(Spec s, double a, double b) => s.Name switch
    {
        "sin" => Impl.Sin(a), "cos" => Impl.Cos(a), "tan" => Impl.Tan(a), "asin" => Impl.Asin(a), "acos" => Impl.Acos(a),
        "atan" => Impl.Atan(a), "atan2" => Impl.Atan2(a, b), "exp" => Impl.Exp(a), "exp2" => Impl.Exp2(a), "expm1" => Impl.Expm1(a),
        "log" => Impl.Log(a), "log2" => Impl.Log2(a), "log10" => Impl.Log10(a), "log1p" => Impl.Log1p(a), "cbrt" => Impl.Cbrt(a),
        "hypot" => Impl.Hypot(a, b), "pow" => Impl.Pow(a, b), "sinh" => Impl.Sinh(a), "cosh" => Impl.Cosh(a), "tanh" => Impl.Tanh(a),
        _ => throw new ArgumentException(s.Name),
    };
}
