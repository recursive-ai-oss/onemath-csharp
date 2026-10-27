using BenchmarkDotNet.Running;

namespace RecursiveAI.OneMath.Bench;

internal static class Program
{
    private static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
