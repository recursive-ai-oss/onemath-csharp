using System;

namespace RecursiveAI.OneMath;

/// <summary>
/// Ziv's rounding test (Ziv 1991; Muller, <i>Elementary Functions</i>, ch. 10): given a value known to within a
/// stated relative error, decide whether every real number in that interval rounds to the same result.
/// </summary>
internal static class Rounding
{
    /// <summary>
    /// Rounds <c>m * 2^k</c> to a double if the whole interval <c>m (1 +- delta)</c> rounds the same way and the result
    /// is a normal double; otherwise returns false and the caller falls back to the exact stage.
    /// </summary>
    public static bool ToDouble(DD m, int k, double delta, out double r)
    {
        double hi = m.Hi;
        double lo = m.Lo;
        double err = Math.Abs(hi) * delta;
        double a = hi + (lo + err);
        double b = hi + (lo - err);
        r = a;
        if (a != b)
        {
            return false;
        }
        int eq = (int)((BitConverter.DoubleToInt64Bits(a) >> 52) & 0x7FF) - 1023;
        if (eq + k > 1023 || eq + k < -1022)
        {
            return false;
        }
        r = k == 0 ? a : Pow2.Scale(a, k);
        return true;
    }

    private static readonly double s_float2To128 = BitConverter.Int64BitsToDouble(0x47F0000000000000L);

    /// <summary>The float32 next to <paramref name="f"/> in the given direction; overflow reports 2^128 as a double.</summary>
    private static double Neighbour(float f, bool up, out float nf)
    {
        int bits = BitConverter.SingleToInt32Bits(f);
        if (f == 0.0f)
        {
            nf = BitConverter.Int32BitsToSingle(up ? 1 : unchecked((int)0x80000001));
        }
        else if ((f > 0.0f) == up)
        {
            nf = BitConverter.Int32BitsToSingle(bits + 1);
        }
        else
        {
            nf = BitConverter.Int32BitsToSingle(bits - 1);
        }
        if (float.IsInfinity(nf))
        {
            return nf > 0 ? s_float2To128 : -s_float2To128;
        }
        return nf;
    }

    /// <summary>
    /// Rounds the double-double <paramref name="v"/> (known to relative error <paramref name="delta"/>) to float32, comparing the
    /// offset from the nearest float to half the spacing to its neighbour; false when the offset is within the error
    /// of a rounding boundary, or the result would overflow.
    /// </summary>
    public static bool ToSingle(DD v, double delta, out float r)
    {
        double hi = v.Hi;
        float f = (float)hi;
        r = f;
        if (float.IsInfinity(f))
        {
            return false;
        }
        double fd = f;
        double d = (hi - fd) + v.Lo; // value - f, to within 2^-53 relative
        double ad = Math.Abs(d);
        double nbd = Neighbour(f, d > 0, out float nf);
        double half = Math.Abs(nbd - fd) * 0.5;
        double err = Math.Abs(hi) * delta + ad * 2.3e-16;
        if (ad < half - err)
        {
            return true;
        }
        if (ad > half + err)
        {
            if (float.IsInfinity(nf))
            {
                return false;
            }
            r = nf;
            return true;
        }
        return false;
    }
}
