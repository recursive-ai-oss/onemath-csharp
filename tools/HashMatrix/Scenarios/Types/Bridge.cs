using System.Runtime.CompilerServices;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>Hash-matrix scenario for the Q31.32 bridge.</summary>
internal static class BridgeScenarios
{
    [ModuleInitializer]
    internal static void Init() => Scenarios.Register("types/bridge", Run);

    private static void Run(ScenarioContext c)
    {
        var r = new TypesRng(0x7007);
        // Boundaries first.
        long[] edges = { 0, 1, -1, FixedBridge.MaxRaw, -FixedBridge.MaxRaw, FixedBridge.MaxRaw - 1, 1L << 52, (1L << 24) + 1, (1L << 25) + 1 };
        foreach (long e in edges)
        {
            c.Write(FixedBridge.ToDouble(e));
            c.Write(FixedBridge.ToSingle(e));
        }
        for (int i = 0; i < 20000; i++)
        {
            // Random raw values of every bit length up to 53, including many float32 ties (few significant bits followed by zeros).
            int bits = 1 + (int)(r.NextUInt64() % 53);
            long raw = (long)(r.NextUInt64() >> (64 - bits));
            if ((i & 1) == 1)
            {
                raw = -raw;
            }
            double d = FixedBridge.ToDouble(raw);
            c.Write(d);
            c.Write(FixedBridge.ToSingle(raw));
            c.Write(FixedBridge.FromDouble(d));

            // An arbitrary double in range (not a multiple of 2^-32), to exercise the rounding.
            double v = (r.NextDouble() * 2 - 1) * 2097151.0 * (i % 3 == 0 ? 1.0 : 1.0 / 4096);
            c.Write(FixedBridge.FromDouble(v));
            c.Write(FixedBridge.FromSingle((float)v));
        }
        // Exact ties for the double to raw rounding.
        for (int k = -6; k <= 6; k++)
        {
            c.Write(FixedBridge.FromDouble(k * 0.5 / 4294967296.0));
        }
    }
}
