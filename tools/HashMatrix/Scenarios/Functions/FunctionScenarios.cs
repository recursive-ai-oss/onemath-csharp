using System;
using System.Runtime.CompilerServices;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>
/// One scenario per function and precision: a fixed pseudo-random input set built from integer arithmetic only
/// (no platform math decides an input), with every result's bit pattern hashed. Inputs cover each function's normal
/// range, the argument-reduction and overflow edges, and a few special values.
/// </summary>
internal static class FunctionScenarios
{
    private const int Count = 2000;

    private sealed class Gen
    {
        private ulong _s;

        public Gen(ulong seed) => _s = seed;

        public ulong Next()
        {
            ulong z = _s += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>A double with a uniformly chosen unbiased exponent in [lo, hi], random mantissa; negative when allowed.</summary>
        public double D(int lo, int hi, bool signed)
        {
            ulong r = Next();
            int e = lo + (int)((r >> 53) % (ulong)(hi - lo + 1));
            ulong bits = (r & 0xFFFFFFFFFFFFFUL) | ((ulong)(e + 1023) << 52);
            if (signed && ((r >> 52) & 1) != 0)
            {
                bits |= 1UL << 63;
            }
            return BitConverter.UInt64BitsToDouble(bits);
        }

        public float F(int lo, int hi, bool signed)
        {
            ulong r = Next();
            int e = lo + (int)((r >> 40) % (ulong)(hi - lo + 1));
            uint bits = (uint)(r & 0x7FFFFFUL) | (uint)((e + 127) << 23);
            if (signed && ((r >> 39) & 1) != 0)
            {
                bits |= 0x80000000U;
            }
            return BitConverter.UInt32BitsToSingle(bits);
        }
    }

    private static readonly double[] Specials =
    [
        double.NaN, 0.0, -0.0, double.PositiveInfinity, double.NegativeInfinity, 1.0, -1.0, 0.5, 5e-324, 1e-300, 1e300,
        710.0, -746.0, 1.5707963267948966, 3.141592653589793, 1e22, 6381956970095103.0, 40.0, 1.0000000000000002,
    ];

    private static void Add1(string name, int lo, int hi, bool signed, Func<double, double> f64, Func<float, float> f32, int f32Lo, int f32Hi)
    {
        Scenarios.Register($"functions/{name}/f64", ctx =>
        {
            var g = new Gen(0x6F6E656D61746800UL + (ulong)name.Length);
            for (int i = 0; i < Count; i++)
            {
                ctx.Write(f64(g.D(lo, hi, signed)));
            }
            foreach (double x in Specials)
            {
                ctx.Write(f64(x));
            }
        });
        Scenarios.Register($"functions/{name}/f32", ctx =>
        {
            var g = new Gen(0x6F6E656D61746801UL + (ulong)name.Length);
            for (int i = 0; i < Count; i++)
            {
                ctx.Write(f32(g.F(f32Lo, f32Hi, signed)));
            }
            foreach (double x in Specials)
            {
                ctx.Write(f32((float)x));
            }
        });
    }

    private static void Add2(string name, int lo, int hi, int lo2, int hi2, bool signed, Func<double, double, double> f64, Func<float, float, float> f32)
    {
        Scenarios.Register($"functions/{name}/f64", ctx =>
        {
            var g = new Gen(0x6F6E656D61746802UL + (ulong)name.Length);
            for (int i = 0; i < Count; i++)
            {
                double x = g.D(lo, hi, signed);
                double y = g.D(lo2, hi2, true);
                ctx.Write(f64(x, y));
            }
            foreach (double x in Specials)
            {
                ctx.Write(f64(x, 0.5));
                ctx.Write(f64(2.0, x));
            }
        });
        Scenarios.Register($"functions/{name}/f32", ctx =>
        {
            var g = new Gen(0x6F6E656D61746803UL + (ulong)name.Length);
            for (int i = 0; i < Count; i++)
            {
                float x = g.F(Math.Max(lo, -100), Math.Min(hi, 100), signed);
                float y = g.F(Math.Max(lo2, -100), Math.Min(hi2, 100), true);
                ctx.Write(f32(x, y));
            }
            foreach (double x in Specials)
            {
                ctx.Write(f32((float)x, 0.5f));
                ctx.Write(f32(2.0f, (float)x));
            }
        });
    }

    [ModuleInitializer]
    internal static void Init()
    {
        Add1("sin", -20, 60, true, Math64.Sin, Math32.Sin, -20, 60);
        Add1("cos", -20, 60, true, Math64.Cos, Math32.Cos, -20, 60);
        Add1("tan", -20, 60, true, Math64.Tan, Math32.Tan, -20, 60);
        Add1("asin", -30, -1, true, Math64.Asin, Math32.Asin, -30, -1);
        Add1("acos", -30, -1, true, Math64.Acos, Math32.Acos, -30, -1);
        Add1("atan", -30, 50, true, Math64.Atan, Math32.Atan, -30, 50);
        Add1("exp", -20, 9, true, Math64.Exp, Math32.Exp, -20, 6);
        Add1("exp2", -20, 10, true, Math64.Exp2, Math32.Exp2, -20, 7);
        Add1("expm1", -30, 9, true, Math64.Expm1, Math32.Expm1, -30, 6);
        Add1("log", -1000, 1000, false, Math64.Log, Math32.Log, -120, 120);
        Add1("log2", -1000, 1000, false, Math64.Log2, Math32.Log2, -120, 120);
        Add1("log10", -1000, 1000, false, Math64.Log10, Math32.Log10, -120, 120);
        Add1("log1p", -30, 40, false, Math64.Log1p, Math32.Log1p, -30, 40);
        Add1("cbrt", -1000, 1000, true, Math64.Cbrt, Math32.Cbrt, -120, 120);
        Add1("sinh", -30, 9, true, Math64.Sinh, Math32.Sinh, -30, 6);
        Add1("cosh", -30, 9, true, Math64.Cosh, Math32.Cosh, -30, 6);
        Add1("tanh", -30, 5, true, Math64.Tanh, Math32.Tanh, -30, 5);
        Add2("atan2", -20, 20, -20, 20, true, Math64.Atan2, Math32.Atan2);
        Add2("hypot", -300, 300, -300, 300, true, Math64.Hypot, Math32.Hypot);
        Add2("pow", -20, 20, -10, 5, false, Math64.Pow, Math32.Pow);
        Scenarios.Register("functions/sincos/f64", ctx =>
        {
            var g = new Gen(0x6F6E656D61746804UL);
            for (int i = 0; i < Count; i++)
            {
                (double s, double c) = Math64.SinCos(g.D(-20, 60, true));
                ctx.Write(s);
                ctx.Write(c);
            }
        });
        Scenarios.Register("functions/sincos/f32", ctx =>
        {
            var g = new Gen(0x6F6E656D61746805UL);
            for (int i = 0; i < Count; i++)
            {
                (float s, float c) = Math32.SinCos(g.F(-20, 60, true));
                ctx.Write(s);
                ctx.Write(c);
            }
        });
    }
}
