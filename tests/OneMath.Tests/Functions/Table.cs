using System;
using System.Collections.Generic;
using System.Numerics;

namespace RecursiveAI.OneMath.Tests;

/// <summary>One function under test, with the black-box platform function used only to pre-screen results.</summary>
internal sealed record Spec(
    string Name,
    Fn Fn,
    bool Binary,
    Func<double, double, double> Platform,
    Func<float, float, float> F32,
    Func<double, double, double> F64,
    Func<Rng, (double, double)> Gen);

/// <summary>SplitMix64: a small seeded generator so every sweep is reproducible.</summary>
internal sealed class Rng
{
    private ulong _s;

    public Rng(ulong seed) => _s = seed;

    public ulong Next()
    {
        ulong z = _s += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform in [0, 1).</summary>
    public double Unit() => (Next() >> 11) * (1.0 / 9007199254740992.0);

    public double Uniform(double lo, double hi) => lo + (hi - lo) * Unit();

    /// <summary>2^u for u uniform in [lo, hi): log-uniform magnitudes.</summary>
    public double LogU(double lo, double hi) => Math.Pow(2.0, Uniform(lo, hi));

    public double Sign() => (Next() & 1) == 0 ? 1.0 : -1.0;
}

internal static class Table
{
    private static double Cbrt(double x) => Math.Cbrt(x);

    public static readonly Spec[] All =
    [
        new("sin", Fn.Sin, false, (x, _) => Math.Sin(x), (x, _) => Math32.Sin(x), (x, _) => Math64.Sin(x), TrigGen),
        new("cos", Fn.Cos, false, (x, _) => Math.Cos(x), (x, _) => Math32.Cos(x), (x, _) => Math64.Cos(x), TrigGen),
        new("tan", Fn.Tan, false, (x, _) => Math.Tan(x), (x, _) => Math32.Tan(x), (x, _) => Math64.Tan(x), TrigGen),
        new("asin", Fn.Asin, false, (x, _) => Math.Asin(x), (x, _) => Math32.Asin(x), (x, _) => Math64.Asin(x), UnitGen),
        new("acos", Fn.Acos, false, (x, _) => Math.Acos(x), (x, _) => Math32.Acos(x), (x, _) => Math64.Acos(x), UnitGen),
        new("atan", Fn.Atan, false, (x, _) => Math.Atan(x), (x, _) => Math32.Atan(x), (x, _) => Math64.Atan(x), r => (r.Sign() * r.LogU(-40, 60), 0)),
        new("atan2", Fn.Atan2, true, Math.Atan2, Math32.Atan2, Math64.Atan2, r => (r.Sign() * r.LogU(-30, 30), r.Sign() * r.LogU(-30, 30))),
        new("exp", Fn.Exp, false, (x, _) => Math.Exp(x), (x, _) => Math32.Exp(x), (x, _) => Math64.Exp(x), r => (r.Unit() < 0.5 ? r.Uniform(-745, 709) : r.Uniform(-4, 4), 0)),
        new("exp2", Fn.Exp2, false, (x, _) => Math.Pow(2.0, x), (x, _) => Math32.Exp2(x), (x, _) => Math64.Exp2(x), r => (r.Unit() < 0.5 ? r.Uniform(-1074, 1023) : r.Uniform(-5, 5), 0)),
        new("expm1", Fn.Expm1, false, (x, _) => double.ExpM1(x), (x, _) => Math32.Expm1(x), (x, _) => Math64.Expm1(x), r => (r.Unit() < 0.5 ? r.Uniform(-40, 709) : r.Sign() * r.LogU(-50, 0), 0)),
        new("log", Fn.Log, false, (x, _) => Math.Log(x), (x, _) => Math32.Log(x), (x, _) => Math64.Log(x), LogGen),
        new("log2", Fn.Log2, false, (x, _) => Math.Log2(x), (x, _) => Math32.Log2(x), (x, _) => Math64.Log2(x), LogGen),
        new("log10", Fn.Log10, false, (x, _) => Math.Log10(x), (x, _) => Math32.Log10(x), (x, _) => Math64.Log10(x), LogGen),
        new("log1p", Fn.Log1p, false, (x, _) => double.LogP1(x), (x, _) => Math32.Log1p(x), (x, _) => Math64.Log1p(x), r => (r.Unit() < 0.5 ? r.Sign() * r.LogU(-50, -1) : r.LogU(-1, 60), 0)),
        new("cbrt", Fn.Cbrt, false, (x, _) => Cbrt(x), (x, _) => Math32.Cbrt(x), (x, _) => Math64.Cbrt(x), r => (r.Sign() * r.LogU(-1074, 1023), 0)),
        new("hypot", Fn.Hypot, true, (x, y) => double.Hypot(x, y), Math32.Hypot, Math64.Hypot, r => (r.Sign() * r.LogU(-500, 500), r.Sign() * r.LogU(-500, 500))),
        new("pow", Fn.Pow, true, Math.Pow, Math32.Pow, Math64.Pow, r => (r.LogU(-30, 30), r.Uniform(-30, 30))),
        new("sinh", Fn.Sinh, false, (x, _) => Math.Sinh(x), (x, _) => Math32.Sinh(x), (x, _) => Math64.Sinh(x), HypGen),
        new("cosh", Fn.Cosh, false, (x, _) => Math.Cosh(x), (x, _) => Math32.Cosh(x), (x, _) => Math64.Cosh(x), HypGen),
        new("tanh", Fn.Tanh, false, (x, _) => Math.Tanh(x), (x, _) => Math32.Tanh(x), (x, _) => Math64.Tanh(x), r => (r.Unit() < 0.6 ? r.Uniform(-12, 12) : r.Sign() * r.LogU(-40, 3), 0)),
    ];

    public static Spec Get(string name) => Array.Find(All, s => s.Name == name) ?? throw new ArgumentException(name);

    private static (double, double) TrigGen(Rng r)
    {
        double c = r.Unit();
        if (c < 0.4) { return (r.Uniform(-12, 12), 0); }
        if (c < 0.9) { return (r.Sign() * r.LogU(-30, 30), 0); }
        return (r.Sign() * r.LogU(30, 1000), 0);
    }

    private static (double, double) UnitGen(Rng r)
    {
        double c = r.Unit();
        if (c < 0.5) { return (r.Uniform(-1, 1), 0); }
        if (c < 0.8) { return (r.Sign() * (1 - r.LogU(-40, -1)), 0); }
        return (r.Sign() * r.LogU(-40, -1), 0);
    }

    private static (double, double) LogGen(Rng r) => r.Unit() < 0.6 ? (r.LogU(-1074, 1023), 0) : (1.0 + r.Uniform(-0.3, 0.4), 0);

    private static (double, double) HypGen(Rng r) => (r.Unit() < 0.6 ? r.Uniform(-40, 40) : r.Sign() * r.LogU(-40, 9.47), 0);

    // ---- exact reference helpers (tests may use the exact stage as the high-precision reference) ----

    public static double ExactD(Fn fn, double a, double b, bool neg = false)
    {
        BF v = Exact.Eval(fn, a, b, out bool sticky);
        double r = Exact.ToDouble(v, sticky);
        return neg ? -r : r;
    }

    public static float ExactF(Fn fn, double a, double b)
    {
        BF v = Exact.Eval(fn, a, b, out bool sticky);
        return Exact.ToSingle(v, sticky);
    }
}
