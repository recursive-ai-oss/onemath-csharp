using System;
using System.IO;
using System.Text;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>
/// Runs every registered scenario and prints a manifest: one <c>name sha256</c> line per scenario in
/// ordinal name order, then a <c>manifest sha256</c> line hashing those lines. Two platforms agree
/// when their manifest lines are identical.
/// Options: <c>--threads N</c> (0 = all cores; default 1), <c>--filter PREFIX</c>, <c>--out FILE</c>, <c>--list</c>.
/// </summary>
public static class Program
{
    /// <summary>Entry point.</summary>
    public static int Main(string[] args)
    {
        int threads = 1;
        string filter = string.Empty;
        string? outFile = null;
        bool list = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--threads" when i + 1 < args.Length:
                    threads = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--filter" when i + 1 < args.Length:
                    filter = args[++i];
                    break;
                case "--out" when i + 1 < args.Length:
                    outFile = args[++i];
                    break;
                case "--list":
                    list = true;
                    break;
                default:
                    Console.Error.WriteLine($"unknown argument: {args[i]}");
                    return 2;
            }
        }
        if (threads <= 0)
        {
            threads = Math.Max(1, Environment.ProcessorCount);
        }

        var sb = new StringBuilder();
        foreach (var (name, run) in Scenarios.All())
        {
            if (!name.StartsWith(filter, StringComparison.Ordinal))
            {
                continue;
            }
            if (list)
            {
                sb.Append(name).Append('\n');
                continue;
            }
            var ctx = new ScenarioContext(threads);
            run(ctx);
            sb.Append(name).Append(' ').Append(Convert.ToHexStringLower(ctx.Finish())).Append('\n');
        }
        if (!list)
        {
            string lines = sb.ToString();
            sb.Append("manifest ").Append(Convert.ToHexStringLower(Sha256.Hash(Encoding.UTF8.GetBytes(lines)))).Append('\n');
        }
        if (outFile is not null)
        {
            File.WriteAllText(outFile, sb.ToString());
        }
        Console.Out.Write(sb.ToString());
        return 0;
    }
}
