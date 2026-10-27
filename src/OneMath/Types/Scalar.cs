using System;

namespace RecursiveAI.OneMath;

/// <summary>
/// Scalar helpers shared by the types. Only IEEE-exact operations are used. Square root is correctly
/// rounded in both precisions; the float form rounds the double square root of the float, which equals
/// the correctly rounded float square root because double carries more than 2p+2 significand bits
/// (Figueroa, "When is double rounding innocuous?", 1995).
/// </summary>
internal static class Scalar
{
    internal static float Sqrt(float x) => (float)Math.Sqrt(x);

    internal static double Sqrt(double x) => Math.Sqrt(x);

    // Min and Max return the second operand when the comparison is false (so a NaN in either
    // operand yields b), which is the same on every platform.
    internal static float Min(float a, float b) => a < b ? a : b;

    internal static double Min(double a, double b) => a < b ? a : b;

    internal static float Max(float a, float b) => a > b ? a : b;

    internal static double Max(double a, double b) => a > b ? a : b;

    // Hash helpers: consistent with Equals (+0 and -0 hash alike, all NaNs hash alike). They exist
    // only so the types work as collection keys; no result depends on them.
    internal static int Bits(float x) => x == 0 ? 0 : float.IsNaN(x) ? 0x7fc00000 : BitConverter.SingleToInt32Bits(x);

    internal static int Bits(double x)
    {
        if (x == 0)
        {
            return 0;
        }
        if (double.IsNaN(x))
        {
            return 0x7ff80000;
        }
        long b = BitConverter.DoubleToInt64Bits(x);
        return unchecked((int)b ^ (int)(b >> 32));
    }

    internal static int Mix(int a, int b) => unchecked((a * -1640531535) ^ b);

    internal static int Mix(int a, int b, int c) => Mix(Mix(a, b), c);

    internal static int Mix(int a, int b, int c, int d) => Mix(Mix(a, b, c), d);
}
