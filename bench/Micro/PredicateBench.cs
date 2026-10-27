using System;
using BenchmarkDotNet.Attributes;
using RecursiveAI.OneMath.Predicates;

namespace RecursiveAI.OneMath.Bench;

/// <summary>
/// Filtered fast path versus the exact fallback for each predicate. "Filtered" inputs are decided by the
/// floating-point filter; "Exact" inputs are exactly degenerate (collinear, coplanar, cocircular, cospherical),
/// so they always take the expansion path.
/// </summary>
[MemoryDiagnoser]
public class PredicateBench
{
    private const int N = 1024;
    private readonly double[] _o2f = new double[N * 6], _o2e = new double[N * 6];
    private readonly double[] _o3f = new double[N * 12], _o3e = new double[N * 12];
    private readonly double[] _icf = new double[N * 8], _ice = new double[N * 8];
    private readonly double[] _isf = new double[N * 15], _ise = new double[N * 15];

    [GlobalSetup]
    public void Setup()
    {
        var r = new System.Random(1);
        double U() => r.NextDouble() * 2 - 1;
        for (int i = 0; i < N; i++)
        {
            for (int k = 0; k < 6; k++) { _o2f[i * 6 + k] = U(); }
            for (int k = 0; k < 12; k++) { _o3f[i * 12 + k] = U(); }
            for (int k = 0; k < 8; k++) { _icf[i * 8 + k] = U(); }
            for (int k = 0; k < 15; k++) { _isf[i * 15 + k] = U(); }

            // Exactly degenerate: small integers on a line, a plane, a circle, a sphere.
            int s = r.Next(1, 6);
            Set(_o2e, i * 6, 0, 0, s, 2 * s, 3 * s, 6 * s);
            Set(_o3e, i * 12, 0, 0, 0, s, 0, 0, 0, s, 0, 2 * s, 3 * s, 0);
            Set(_ice, i * 8, 5 * s, 0, 0, 5 * s, -5 * s, 0, 3 * s, 4 * s);
            Set(_ise, i * 15, 3 * s, 0, 0, -3 * s, 0, 0, 0, 3 * s, 0, 0, 0, 3 * s, 0, 0, -3 * s);
        }
    }

    private static void Set(double[] a, int at, params double[] v)
    {
        for (int i = 0; i < v.Length; i++) { a[at + i] = v[i]; }
    }

    [Benchmark(OperationsPerInvoke = N)]
    public int Orient2DFiltered() { int t = 0; for (int i = 0; i < N; i++) { var p = _o2f.AsSpan(i * 6); t += ExactPredicates.Orient2D(p[0], p[1], p[2], p[3], p[4], p[5]); } return t; }

    [Benchmark(OperationsPerInvoke = N)]
    public int Orient2DExact() { int t = 0; for (int i = 0; i < N; i++) { var p = _o2e.AsSpan(i * 6); t += ExactPredicates.Orient2D(p[0], p[1], p[2], p[3], p[4], p[5]); } return t; }

    [Benchmark(OperationsPerInvoke = N)]
    public int Orient3DFiltered() { int t = 0; for (int i = 0; i < N; i++) { var p = _o3f.AsSpan(i * 12); t += ExactPredicates.Orient3D(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11]); } return t; }

    [Benchmark(OperationsPerInvoke = N)]
    public int Orient3DExact() { int t = 0; for (int i = 0; i < N; i++) { var p = _o3e.AsSpan(i * 12); t += ExactPredicates.Orient3D(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11]); } return t; }

    [Benchmark(OperationsPerInvoke = N)]
    public int InCircleFiltered() { int t = 0; for (int i = 0; i < N; i++) { var p = _icf.AsSpan(i * 8); t += ExactPredicates.InCircle(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]); } return t; }

    [Benchmark(OperationsPerInvoke = N)]
    public int InCircleExact() { int t = 0; for (int i = 0; i < N; i++) { var p = _ice.AsSpan(i * 8); t += ExactPredicates.InCircle(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]); } return t; }

    [Benchmark(OperationsPerInvoke = N)]
    public int InSphereFiltered() { int t = 0; for (int i = 0; i < N; i++) { var p = _isf.AsSpan(i * 15); t += ExactPredicates.InSphere(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12], p[13], p[14]); } return t; }

    [Benchmark(OperationsPerInvoke = N)]
    public int InSphereExact() { int t = 0; for (int i = 0; i < N; i++) { var p = _ise.AsSpan(i * 15); t += ExactPredicates.InSphere(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12], p[13], p[14]); } return t; }
}
