using System;

namespace RecursiveAI.OneMath;

/// <summary>
/// Deterministic elementary functions on <see cref="float"/>. Every function is correctly rounded for every one of the 2^32 inputs (checked exhaustively), so results are identical on every platform. See docs/functions.md.
/// </summary>
public static class Math32
{
    /// <summary>Sine of <paramref name="x"/> in radians. ±0 gives ±0; ±∞ and NaN give NaN. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Sin(float x) => Impl.Sin(x).ToSingle();

    /// <summary>Cosine of <paramref name="x"/> in radians. ±∞ and NaN give NaN. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Cos(float x) => Impl.Cos(x).ToSingle();

    /// <summary>Tangent of <paramref name="x"/> in radians. ±0 gives ±0; ±∞ and NaN give NaN. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Tan(float x) => Impl.Tan(x).ToSingle();

    /// <summary>Arcsine in radians, in [-π/2, π/2]. |x| &gt; 1 and NaN give NaN; ±0 gives ±0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Asin(float x) => Impl.Asin(x).ToSingle();

    /// <summary>Arccosine in radians, in [0, π]. |x| &gt; 1 and NaN give NaN; acos(1) is +0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Acos(float x) => Impl.Acos(x).ToSingle();

    /// <summary>Arctangent in radians, in [-π/2, π/2]. ±0 gives ±0; ±∞ gives ±π/2. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Atan(float x) => Impl.Atan(x).ToSingle();

    /// <summary>e<sup>x</sup>. +∞ gives +∞, −∞ gives +0; overflow gives +∞, underflow rounds to a subnormal or +0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Exp(float x) => Impl.Exp(x).ToSingle();

    /// <summary>2<sup>x</sup>. +∞ gives +∞, −∞ gives +0; overflow gives +∞, underflow rounds to a subnormal or +0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Exp2(float x) => Impl.Exp2(x).ToSingle();

    /// <summary>e<sup>x</sup> − 1, accurate for small x. ±0 gives ±0; −∞ gives −1; +∞ gives +∞. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Expm1(float x) => Impl.Expm1(x).ToSingle();

    /// <summary>Natural logarithm. Negative input and NaN give NaN; ±0 gives −∞; +∞ gives +∞; log(1) is +0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Log(float x) => Impl.Log(x).ToSingle();

    /// <summary>Base-2 logarithm. Negative input and NaN give NaN; ±0 gives −∞; +∞ gives +∞; log2(1) is +0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Log2(float x) => Impl.Log2(x).ToSingle();

    /// <summary>Base-10 logarithm. Negative input and NaN give NaN; ±0 gives −∞; +∞ gives +∞; log10(1) is +0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Log10(float x) => Impl.Log10(x).ToSingle();

    /// <summary>log(1 + x), accurate for small x. x &lt; −1 and NaN give NaN; −1 gives −∞; ±0 gives ±0. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Log1p(float x) => Impl.Log1p(x).ToSingle();

    /// <summary>Real cube root, defined for negative x. ±0 and ±∞ are returned unchanged; NaN gives NaN. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Cbrt(float x) => Impl.Cbrt(x).ToSingle();

    /// <summary>Hyperbolic sine. ±0 and ±∞ are returned unchanged; overflow gives ±∞. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Sinh(float x) => Impl.Sinh(x).ToSingle();

    /// <summary>Hyperbolic cosine. ±∞ gives +∞; overflow gives +∞. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Cosh(float x) => Impl.Cosh(x).ToSingle();

    /// <summary>Hyperbolic tangent. ±0 is returned unchanged; ±∞ gives ±1. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Tanh(float x) => Impl.Tanh(x).ToSingle();

    /// <summary>Angle in radians, in [-π, π], of the point (<paramref name="x"/>, <paramref name="y"/>), with the zero and infinity cases of IEEE 754-2019 9.2 (atan2(±0, −0) is ±π, atan2(±0, +0) is ±0). Returns NaN if either argument is NaN. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Atan2(float y, float x) => Impl.Atan2(y, x).ToSingle();

    /// <summary><paramref name="x"/> raised to <paramref name="y"/>, with the special cases of IEEE 754-2019 9.2.1 (pow(x, ±0) and pow(1, y) are 1 even for NaN; a negative finite base with a non-integer exponent gives NaN). Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Pow(float x, float y) => Impl.Pow(x, y).ToSingle();

    /// <summary>√(x² + y²) without intermediate overflow. Any infinite argument gives +∞, even if the other is NaN; otherwise NaN gives NaN. Correctly rounded; NaN results are always the canonical NaN.</summary>
    public static float Hypot(float x, float y) => Impl.Hypot(x, y).ToSingle();

    /// <summary>Sine and cosine of <paramref name="x"/> in radians, sharing one argument reduction. Each is correctly rounded.</summary>
    public static (float Sin, float Cos) SinCos(float x)
    {
        Impl.SinCos(x, out R s, out R c);
        return (s.ToSingle(), c.ToSingle());
    }
}
