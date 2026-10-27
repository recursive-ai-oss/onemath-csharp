using System;
using System.Numerics;

namespace RecursiveAI.OneMath.Tests;

/// <summary>Exact rational evaluation of the predicates' determinants with BigInteger, independent of the library code.</summary>
internal static class PredicateOracle
{
    // Every double is an integer multiple of 2^-1074; scaling all inputs by 2^1074 keeps determinant signs.
    public static BigInteger Big(double x)
    {
        long bits = BitConverter.DoubleToInt64Bits(x);
        bool neg = bits < 0;
        int exp = (int)((bits >> 52) & 0x7FF);
        long mant = bits & 0xFFFFFFFFFFFFFL;
        if (exp == 0x7FF)
        {
            throw new ArgumentException("not finite");
        }
        BigInteger v;
        if (exp == 0)
        {
            v = mant;
        }
        else
        {
            v = (BigInteger)(mant | (1L << 52)) << (exp - 1);
        }
        return neg ? -v : v;
    }

    public static BigInteger Big(ReadOnlySpan<double> e)
    {
        BigInteger s = BigInteger.Zero;
        foreach (double d in e)
        {
            s += Big(d);
        }
        return s;
    }

    public static int Orient2D(ReadOnlySpan<double> p)
    {
        var a = Big(p[0]); var b = Big(p[1]); var c = Big(p[2]); var d = Big(p[3]); var e = Big(p[4]); var f = Big(p[5]);
        return ((a - e) * (d - f) - (b - f) * (c - e)).Sign;
    }

    public static BigInteger Det(BigInteger[][] m)
    {
        int n = m.Length;
        if (n == 1)
        {
            return m[0][0];
        }
        BigInteger sum = BigInteger.Zero;
        for (int j = 0; j < n; j++)
        {
            var minor = new BigInteger[n - 1][];
            for (int i = 1; i < n; i++)
            {
                var row = new BigInteger[n - 1];
                for (int k = 0, c = 0; k < n; k++)
                {
                    if (k != j) { row[c++] = m[i][k]; }
                }
                minor[i - 1] = row;
            }
            BigInteger t = m[0][j] * Det(minor);
            sum += (j & 1) == 0 ? t : -t;
        }
        return sum;
    }

    public static int Orient3D(ReadOnlySpan<double> p)
    {
        var m = new BigInteger[3][];
        for (int i = 0; i < 3; i++)
        {
            m[i] = new[] { Big(p[3 * i]) - Big(p[9]), Big(p[3 * i + 1]) - Big(p[10]), Big(p[3 * i + 2]) - Big(p[11]) };
        }
        return Det(m).Sign;
    }

    public static int InCircle(ReadOnlySpan<double> p)
    {
        var m = new BigInteger[3][];
        for (int i = 0; i < 3; i++)
        {
            var x = Big(p[2 * i]) - Big(p[6]);
            var y = Big(p[2 * i + 1]) - Big(p[7]);
            m[i] = new[] { x, y, x * x + y * y };
        }
        return Det(m).Sign;
    }

    public static int InSphere(ReadOnlySpan<double> p)
    {
        var m = new BigInteger[4][];
        for (int i = 0; i < 4; i++)
        {
            var x = Big(p[3 * i]) - Big(p[12]);
            var y = Big(p[3 * i + 1]) - Big(p[13]);
            var z = Big(p[3 * i + 2]) - Big(p[14]);
            m[i] = new[] { x, y, z, x * x + y * y + z * z };
        }
        return Det(m).Sign;
    }
}
