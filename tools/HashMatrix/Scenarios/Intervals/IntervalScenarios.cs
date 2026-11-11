using System;
using System.Runtime.CompilerServices;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>Hash-matrix scenarios for interval arithmetic: bounds are written by bit pattern, so outward rounding must agree on every platform.</summary>
internal static class IntervalScenarios
{
    private static readonly double[] s_special =
    {
        0.0, -0.0, 1, -1, 0.5, 2, 3, 6.28, Math.PI, -Math.PI, Math.PI / 2, 2 * Math.PI, double.MaxValue, -double.MaxValue,
        double.Epsilon, 2.2250738585072014E-308, 1e-300, 1e300, 1e154, 1e-154, 1e-271, 3e150,
    };

    [ModuleInitializer]
    internal static void Init()
    {
        Scenarios.Register("intervals/arith", Arith);
        Scenarios.Register("intervals/functions", Functions);
        Scenarios.Register("intervals/vector3", Vector3);
    }

    private static double Pick(TypesRng r)
    {
        switch (r.NextUInt64() % 6)
        {
            case 0:
                return s_special[r.NextUInt64() % (ulong)s_special.Length];
            case 1:
                return (r.NextDouble() - 0.5) * 20;
            case 2:
                return (long)(r.NextUInt64() % 13) - 6;
            case 3:
                return ((long)(r.NextUInt64() % 2000001) - 1000000) * (Math.PI / 2);
            case 4:
                return (r.NextDouble() * 2 - 1) * r.Scale();
            default:
                while (true)
                {
                    double d = BitConverter.Int64BitsToDouble((long)r.NextUInt64());
                    if (double.IsFinite(d))
                    {
                        return d;
                    }
                }
        }
    }

    private static IntervalD Pick2(TypesRng r)
    {
        double a = Pick(r), b = r.NextUInt64() % 4 == 0 ? a : (r.NextUInt64() % 2 == 0 ? a + (r.NextDouble() * 7) : Pick(r));
        if (!double.IsFinite(b))
        {
            b = a;
        }
        double lo = Math.Min(a, b), hi = Math.Max(a, b);
        ulong k = r.NextUInt64() % 16;
        if (k == 0)
        {
            lo = double.NegativeInfinity;
        }
        else if (k == 1)
        {
            hi = double.PositiveInfinity;
        }
        return new IntervalD(lo, hi);
    }

    private static void Put(ScenarioContext c, IntervalD i)
    {
        c.Write(i.Lo);
        c.Write(i.Hi);
    }

    private static void Arith(ScenarioContext c)
    {
        var r = new TypesRng(0x1A7E);
        for (int i = 0; i < 20000; i++)
        {
            IntervalD a = Pick2(r), b = Pick2(r);
            Put(c, a + b);
            Put(c, a - b);
            Put(c, a * b);
            Put(c, a / b);
            Put(c, IntervalD.Square(a));
            Put(c, IntervalD.Sqrt(a));
            Put(c, IntervalD.Abs(a));
            Put(c, IntervalD.Min(a, b));
            Put(c, IntervalD.Max(a, b));
            Put(c, IntervalD.Hull(a, b));
            Put(c, IntervalD.Intersect(a, b));
            c.Write(a.Width);
            c.Write(a.Midpoint);
        }
    }

    private static void Functions(ScenarioContext c)
    {
        var r = new TypesRng(0xF00D);
        for (int i = 0; i < 6000; i++)
        {
            IntervalD a = Pick2(r), b = Pick2(r);
            Put(c, IntervalD.Sin(a));
            Put(c, IntervalD.Cos(a));
            Put(c, IntervalD.Exp(a));
            Put(c, IntervalD.Log(a));
            Put(c, IntervalD.Atan2(a, b));
            Put(c, IntervalD.Pow(a, b));
            Put(c, IntervalD.Pow(a, (int)(r.NextUInt64() % 13) - 6));
        }
    }

    private static void Vector3(ScenarioContext c)
    {
        var r = new TypesRng(0xB0C5);
        for (int i = 0; i < 5000; i++)
        {
            var a = new Interval3D(Pick2(r), Pick2(r), Pick2(r));
            var b = new Interval3D(Pick2(r), Pick2(r), Pick2(r));
            Put(c, a.Length());
            Put(c, Interval3D.Dot(a, b));
            var s = a + b;
            Put(c, s.X);
            Put(c, s.Y);
            Put(c, s.Z);
            var m = a * 1.5;
            Put(c, m.X);
            Put(c, m.Y);
            Put(c, m.Z);
        }
    }
}
