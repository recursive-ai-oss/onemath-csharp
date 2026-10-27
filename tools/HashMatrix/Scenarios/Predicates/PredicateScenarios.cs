using System;
using System.Runtime.CompilerServices;
using RecursiveAI.OneMath.Predicates;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>Fixed input sets per predicate (random and near-degenerate); the output is the sign of each case.</summary>
internal static class PredicateScenarios
{
    private const int Cases = 20000;

    [ModuleInitializer]
    internal static void Init()
    {
        Scenarios.Register("predicates/orient2d", ctx => Run(ctx, PredicateKind.Orient2D, 101));
        Scenarios.Register("predicates/orient3d", ctx => Run(ctx, PredicateKind.Orient3D, 102));
        Scenarios.Register("predicates/incircle", ctx => Run(ctx, PredicateKind.InCircle, 103));
        Scenarios.Register("predicates/insphere", ctx => Run(ctx, PredicateKind.InSphere, 104));
        Scenarios.Register("predicates/segments", ctx =>
        {
            Span<double> p = stackalloc double[8];
            for (long i = 0; i < Cases; i++)
            {
                PredicateInputs.Fill(PredicateKind.InCircle, 105, i, p);
                ctx.Write((int)SegmentPredicates.Classify(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]));
            }
        });
        Scenarios.Register("predicates/expansions", ctx =>
        {
            // Exact products of differences of random doubles, written as component bit patterns.
            Span<double> p = stackalloc double[6];
            Span<double> d1 = stackalloc double[2];
            Span<double> d2 = stackalloc double[2];
            Span<double> h = stackalloc double[8];
            for (long i = 0; i < 2000; i++)
            {
                PredicateInputs.Fill(PredicateKind.Orient2D, 106, i, p);
                int n1 = Diff(p[0], p[1], d1);
                int n2 = Diff(p[2], p[3], d2);
                int n = ExpansionArithmetic.Product(d1[..n1], d2[..n2], h);
                ctx.Write(n);
                for (int k = 0; k < n; k++)
                {
                    ctx.Write(h[k]);
                }
            }
        });
    }

    private static int Diff(double a, double b, Span<double> d)
    {
        ExpansionArithmetic.TwoDiff(a, b, out double x, out double y);
        int n = 0;
        if (y != 0.0) { d[n++] = y; }
        if (x != 0.0) { d[n++] = x; }
        return n;
    }

    private static void Run(ScenarioContext ctx, PredicateKind kind, ulong seed)
    {
        Span<double> p = stackalloc double[PredicateInputs.Length(kind)];
        Span<byte> one = stackalloc byte[1];
        for (long i = 0; i < Cases; i++)
        {
            PredicateInputs.Fill(kind, seed, i, p);
            int s = kind switch
            {
                PredicateKind.Orient2D => ExactPredicates.Orient2D(p[0], p[1], p[2], p[3], p[4], p[5]),
                PredicateKind.Orient3D => ExactPredicates.Orient3D(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11]),
                PredicateKind.InCircle => ExactPredicates.InCircle(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]),
                _ => ExactPredicates.InSphere(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12], p[13], p[14]),
            };
            one[0] = (byte)(s + 1);
            ctx.Write(one);
        }
    }
}
