using System;

namespace RecursiveAI.OneMath;

/// <summary>
/// Deterministic elementary functions on <see cref="double"/>. Every function is correctly rounded (the IEEE nearest result, ties to even, for every input), so results are identical on every platform and equal to any other correctly rounded implementation. Only IEEE-exact + - * / and sqrt decide a result: no platform math, no fused multiply-add. See docs/functions.md for the method and how correct rounding is established.
/// </summary>
public static class Math64
{
    /// <summary>Sine of <paramref name="x"/> in radians. ±0 gives ±0; ±∞ and NaN give NaN. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Sin(double x) => Impl.Sin(x).ToDouble();

    /// <summary>Cosine of <paramref name="x"/> in radians. ±∞ and NaN give NaN. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Cos(double x) => Impl.Cos(x).ToDouble();

    /// <summary>Tangent of <paramref name="x"/> in radians. ±0 gives ±0; ±∞ and NaN give NaN. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Tan(double x) => Impl.Tan(x).ToDouble();

    /// <summary>Arcsine in radians, in [-π/2, π/2]. |x| &gt; 1 and NaN give NaN; ±0 gives ±0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Asin(double x) => Impl.Asin(x).ToDouble();

    /// <summary>Arccosine in radians, in [0, π]. |x| &gt; 1 and NaN give NaN; acos(1) is +0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Acos(double x) => Impl.Acos(x).ToDouble();

    /// <summary>Arctangent in radians, in [-π/2, π/2]. ±0 gives ±0; ±∞ gives ±π/2. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Atan(double x) => Impl.Atan(x).ToDouble();

    /// <summary>e<sup>x</sup>. +∞ gives +∞, −∞ gives +0; overflow gives +∞, underflow rounds to a subnormal or +0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Exp(double x) => Impl.Exp(x).ToDouble();

    /// <summary>2<sup>x</sup>. +∞ gives +∞, −∞ gives +0; overflow gives +∞, underflow rounds to a subnormal or +0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Exp2(double x) => Impl.Exp2(x).ToDouble();

    /// <summary>e<sup>x</sup> − 1, accurate for small x. ±0 gives ±0; −∞ gives −1; +∞ gives +∞. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Expm1(double x) => Impl.Expm1(x).ToDouble();

    /// <summary>Natural logarithm. Negative input and NaN give NaN; ±0 gives −∞; +∞ gives +∞; log(1) is +0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Log(double x) => Impl.Log(x).ToDouble();

    /// <summary>Base-2 logarithm. Negative input and NaN give NaN; ±0 gives −∞; +∞ gives +∞; log2(1) is +0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Log2(double x) => Impl.Log2(x).ToDouble();

    /// <summary>Base-10 logarithm. Negative input and NaN give NaN; ±0 gives −∞; +∞ gives +∞; log10(1) is +0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Log10(double x) => Impl.Log10(x).ToDouble();

    /// <summary>log(1 + x), accurate for small x. x &lt; −1 and NaN give NaN; −1 gives −∞; ±0 gives ±0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Log1p(double x) => Impl.Log1p(x).ToDouble();

    /// <summary>Real cube root, defined for negative x. ±0 and ±∞ are returned unchanged; NaN gives NaN. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Cbrt(double x) => Impl.Cbrt(x).ToDouble();

    /// <summary>Hyperbolic sine. ±0 and ±∞ are returned unchanged; overflow gives ±∞. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Sinh(double x) => Impl.Sinh(x).ToDouble();

    /// <summary>Hyperbolic cosine. ±∞ gives +∞; overflow gives +∞. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Cosh(double x) => Impl.Cosh(x).ToDouble();

    /// <summary>Hyperbolic tangent. ±0 is returned unchanged; ±∞ gives ±1. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Tanh(double x) => Impl.Tanh(x).ToDouble();

    /// <summary>Angle in radians, in [-π, π], of the point (<paramref name="x"/>, <paramref name="y"/>), with the zero and infinity cases of IEEE 754-2019 9.2 (atan2(±0, −0) is ±π, atan2(±0, +0) is ±0). Returns NaN if either argument is NaN. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Atan2(double y, double x) => Impl.Atan2(y, x).ToDouble();

    /// <summary><paramref name="x"/> raised to <paramref name="y"/>, with the special cases of IEEE 754-2019 9.2.1 (pow(x, ±0) and pow(1, y) are 1 even for NaN; a negative finite base with a non-integer exponent gives NaN). Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Pow(double x, double y) => Impl.Pow(x, y).ToDouble();

    /// <summary>√(x² + y²) without intermediate overflow. Any infinite argument gives +∞, even if the other is NaN; otherwise NaN gives NaN. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static double Hypot(double x, double y) => Impl.Hypot(x, y).ToDouble();

    /// <summary>Sine and cosine of <paramref name="x"/> in radians, sharing one argument reduction. Each is correctly rounded.</summary>
    public static (double Sin, double Cos) SinCos(double x)
    {
        Impl.SinCos(x, out R s, out R c);
        return (s.ToDouble(), c.ToDouble());
    }
}
