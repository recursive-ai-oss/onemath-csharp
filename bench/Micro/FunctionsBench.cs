using System;
using BenchmarkDotNet.Attributes;

namespace RecursiveAI.OneMath.Bench;

/// <summary>
/// Throughput of the correctly rounded functions against the platform's <c>Math</c> and <c>MathF</c> on the same inputs
/// (each method evaluates 1024 inputs; divide by 1024 for per-call cost). Calls go through a delegate on both sides, so the
/// call overhead is identical and small next to the functions themselves.
/// Run: <c>dotnet run -c Release --project bench/Micro -- --filter '*FunctionsBench*'</c> (about 20 minutes for all 19 functions
/// with the default job; add <c>--job short</c> for a quick pass). Benchmarks belong on a quiet machine.
/// </summary>
[MemoryDiagnoser]
public class FunctionsBench
{
    private const int N = 1024;

    [Params("sin", "cos", "tan", "asin", "acos", "atan", "atan2", "exp", "exp2", "log", "log2", "log10", "pow", "cbrt", "hypot", "sinh", "cosh", "tanh", "expm1", "log1p")]
    public string Fn { get; set; } = "sin";

    private double[] _a = new double[N];
    private double[] _b = new double[N];
    private float[] _fa = new float[N];
    private float[] _fb = new float[N];
    private Func<double, double, double> _ours64 = null!;
    private Func<double, double, double> _plat64 = null!;
    private Func<float, float, float> _ours32 = null!;
    private Func<float, float, float> _plat32 = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(12345); // inputs only; results never depend on it
        double Lo(double lo, double hi) => lo + (hi - lo) * rng.NextDouble();
        double Lg(double lo, double hi) => Math.Pow(2.0, Lo(lo, hi));
        for (int i = 0; i < N; i++)
        {
            (double a, double b) = Fn switch
            {
                "sin" or "cos" or "tan" => (Lo(-100, 100), 0.0),
                "asin" or "acos" => (Lo(-1, 1), 0.0),
                "atan" => (Lo(-100, 100), 0.0),
                "atan2" => (Lo(-100, 100), Lo(-100, 100)),
                "exp" or "exp2" or "expm1" => (Lo(-20, 20), 0.0),
                "log" or "log2" or "log10" => (Lg(-20, 20), 0.0),
                "log1p" => (Lg(-20, 20) - 0.5, 0.0),
                "pow" => (Lg(-5, 5), Lo(-8, 8)),
                "cbrt" => (Lo(-1000, 1000), 0.0),
                "hypot" => (Lo(-100, 100), Lo(-100, 100)),
                _ => (Lo(-10, 10), 0.0),
            };
            _a[i] = a;
            _b[i] = b;
            _fa[i] = (float)a;
            _fb[i] = (float)b;
        }
        (_ours64, _plat64, _ours32, _plat32) = Fn switch
        {
            "sin" => (Wrap(Math64.Sin), Wrap(Math.Sin), Wrap(Math32.Sin), Wrap(MathF.Sin)),
            "cos" => (Wrap(Math64.Cos), Wrap(Math.Cos), Wrap(Math32.Cos), Wrap(MathF.Cos)),
            "tan" => (Wrap(Math64.Tan), Wrap(Math.Tan), Wrap(Math32.Tan), Wrap(MathF.Tan)),
            "asin" => (Wrap(Math64.Asin), Wrap(Math.Asin), Wrap(Math32.Asin), Wrap(MathF.Asin)),
            "acos" => (Wrap(Math64.Acos), Wrap(Math.Acos), Wrap(Math32.Acos), Wrap(MathF.Acos)),
            "atan" => (Wrap(Math64.Atan), Wrap(Math.Atan), Wrap(Math32.Atan), Wrap(MathF.Atan)),
            "atan2" => ((Func<double, double, double>)Math64.Atan2, Math.Atan2, Math32.Atan2, MathF.Atan2),
            "exp" => (Wrap(Math64.Exp), Wrap(Math.Exp), Wrap(Math32.Exp), Wrap(MathF.Exp)),
            "exp2" => (Wrap(Math64.Exp2), Wrap(x => Math.Pow(2.0, x)), Wrap(Math32.Exp2), Wrap(x => MathF.Pow(2f, x))),
            "expm1" => (Wrap(Math64.Expm1), Wrap(double.ExpM1), Wrap(Math32.Expm1), Wrap(float.ExpM1)),
            "log" => (Wrap(Math64.Log), Wrap(Math.Log), Wrap(Math32.Log), Wrap(MathF.Log)),
            "log2" => (Wrap(Math64.Log2), Wrap(Math.Log2), Wrap(Math32.Log2), Wrap(MathF.Log2)),
            "log10" => (Wrap(Math64.Log10), Wrap(Math.Log10), Wrap(Math32.Log10), Wrap(MathF.Log10)),
            "log1p" => (Wrap(Math64.Log1p), Wrap(double.LogP1), Wrap(Math32.Log1p), Wrap(float.LogP1)),
            "pow" => ((Func<double, double, double>)Math64.Pow, Math.Pow, Math32.Pow, MathF.Pow),
            "cbrt" => (Wrap(Math64.Cbrt), Wrap(Math.Cbrt), Wrap(Math32.Cbrt), Wrap(MathF.Cbrt)),
            "hypot" => ((Func<double, double, double>)Math64.Hypot, double.Hypot, Math32.Hypot, float.Hypot),
            "sinh" => (Wrap(Math64.Sinh), Wrap(Math.Sinh), Wrap(Math32.Sinh), Wrap(MathF.Sinh)),
            "cosh" => (Wrap(Math64.Cosh), Wrap(Math.Cosh), Wrap(Math32.Cosh), Wrap(MathF.Cosh)),
            "tanh" => (Wrap(Math64.Tanh), Wrap(Math.Tanh), Wrap(Math32.Tanh), Wrap(MathF.Tanh)),
            _ => throw new ArgumentException(Fn),
        };
    }

    private static Func<double, double, double> Wrap(Func<double, double> f) => (x, _) => f(x);

    private static Func<float, float, float> Wrap(Func<float, float> f) => (x, _) => f(x);

    [Benchmark(Description = "double: ours", OperationsPerInvoke = N)]
    public double Ours64() => Run(_ours64, _a, _b);

    [Benchmark(Description = "double: Math", OperationsPerInvoke = N)]
    public double Platform64() => Run(_plat64, _a, _b);

    [Benchmark(Description = "float: ours", OperationsPerInvoke = N)]
    public float Ours32() => Run(_ours32, _fa, _fb);

    [Benchmark(Description = "float: MathF", OperationsPerInvoke = N)]
    public float Platform32() => Run(_plat32, _fa, _fb);

    private static double Run(Func<double, double, double> f, double[] a, double[] b)
    {
        double s = 0;
        for (int i = 0; i < a.Length; i++)
        {
            s += f(a[i], b[i]);
        }
        return s;
    }

    private static float Run(Func<float, float, float> f, float[] a, float[] b)
    {
        float s = 0;
        for (int i = 0; i < a.Length; i++)
        {
            s += f(a[i], b[i]);
        }
        return s;
    }
}
