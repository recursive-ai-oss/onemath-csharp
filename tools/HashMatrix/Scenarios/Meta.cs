using System.Runtime.CompilerServices;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>Scenarios that check the runner itself.</summary>
internal static class MetaScenarios
{
    [ModuleInitializer]
    internal static void Init()
    {
        // Fixed bit patterns, including signed zero, infinity, NaN and a subnormal.
        Scenarios.Register("meta/bits", ctx =>
        {
            ctx.Write(1);
            ctx.Write(-1L);
            ctx.Write(-0.0f);
            ctx.Write(float.PositiveInfinity);
            ctx.Write(float.NaN);
            ctx.Write(double.Epsilon);
            ctx.Write(0.1);
        });
    }
}
