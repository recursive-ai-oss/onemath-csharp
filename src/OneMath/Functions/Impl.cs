using System;

namespace RecursiveAI.OneMath;

/// <summary>What a function front end decided: a final value, or a double-double value to round, with its exact fallback.</summary>
internal struct R
{
    public bool IsSpecial;
    public double V;      // the result when IsSpecial
    public DD M;          // otherwise value = M * 2^K ...
    public int K;
    public double Delta;  // ... to relative error Delta
    public Fn Fn;         // the exact fallback: Fn(A, B)
    public double A;
    public double B;
    public bool Neg;      // negate the final result (odd symmetry, odd powers)
    public bool ExactOnly;

    public static R Special(double v) => new() { IsSpecial = true, V = v };

    public static R Dd(DD m, int k, double delta, Fn fn, double a, double b, bool neg = false) =>
        new() { M = m, K = k, Delta = delta, Fn = fn, A = a, B = b, Neg = neg };

    public static R ViaExact(Fn fn, double a, double b, bool neg = false) =>
        new() { Fn = fn, A = a, B = b, Neg = neg, ExactOnly = true };

    /// <summary>The result as a correctly rounded double.</summary>
    public readonly double ToDouble()
    {
        if (IsSpecial)
        {
            return V;
        }
        double r;
        if (!ExactOnly && Rounding.ToDouble(M, K, Delta, out r))
        {
            return Neg ? -r : r;
        }
        BF v = Exact.Eval(Fn, A, B, out bool sticky);
        r = Exact.ToDouble(v, sticky);
        return Neg ? -r : r;
    }

    /// <summary>The result as a correctly rounded float32.</summary>
    public readonly float ToSingle()
    {
        if (IsSpecial)
        {
            return (float)V;
        }
        float r;
        if (!ExactOnly && K > -900 && K < 900 && Rounding.ToSingle(M.Scale(Pow2.Scale(1.0, K)), Delta, out r))
        {
            return Neg ? -r : r;
        }
        BF v = Exact.Eval(Fn, A, B, out bool sticky);
        r = Exact.ToSingle(v, sticky);
        return Neg ? -r : r;
    }
}

/// <summary>
/// Special-value handling (IEEE 754-2019 clause 9.2 and C99 Annex F) and the choice of evaluation for every function,
/// shared by the double and float32 fronts. Inputs arrive as doubles (float32 converts exactly). NaN results are always
/// the canonical <see cref="double.NaN"/>.
/// </summary>
internal static class Impl
{
    // Relative error bounds handed to the rounding test: about 2^-85, several bits above what was measured.
    private static readonly double s_d = Pow2.Of(-85);
    private static readonly double s_dPow = Pow2.Of(-80);
    private static readonly double s_tiny = Pow2.Of(-27);

    private static R Nan => R.Special(double.NaN);

    public static R Exp(double x)
    {
        if (double.IsNaN(x)) { return Nan; }
        if (x > 710.0) { return R.Special(double.PositiveInfinity); }
        if (x < -746.0) { return R.Special(0.0); }
        DD m = Cores.Exp(new DD(x), out int k);
        return R.Dd(m, k, s_d, Fn.Exp, x, 0);
    }

    public static R Exp2(double x)
    {
        if (double.IsNaN(x)) { return Nan; }
        if (x > 1100.0) { return R.Special(double.PositiveInfinity); }
        if (x < -1100.0) { return R.Special(0.0); }
        DD m = Cores.Exp2(x, out int k);
        return R.Dd(m, k, s_d, Fn.Exp2, x, 0);
    }

    public static R Expm1(double x)
    {
        if (double.IsNaN(x)) { return Nan; }
        if (x > 710.0) { return R.Special(double.PositiveInfinity); }
        if (x < -38.0) { return R.Special(-1.0); }
        if (Math.Abs(x) < Pow2.Of(-55)) { return R.Special(x); }
        DD m = Cores.ExpM1(x, out int k);
        return R.Dd(m, k, s_d, Fn.Expm1, x, 0);
    }

    public static R Log(double x)
    {
        if (double.IsNaN(x) || x < 0.0) { return Nan; }
        if (x == 0.0) { return R.Special(double.NegativeInfinity); }
        if (double.IsPositiveInfinity(x)) { return R.Special(x); }
        if (x == 1.0) { return R.Special(0.0); }
        return R.Dd(Cores.Log(x), 0, s_d, Fn.Log, x, 0);
    }

    public static R Log2(double x)
    {
        if (double.IsNaN(x) || x < 0.0) { return Nan; }
        if (x == 0.0) { return R.Special(double.NegativeInfinity); }
        if (double.IsPositiveInfinity(x)) { return R.Special(x); }
        if (x == 1.0) { return R.Special(0.0); }
        return R.Dd(Cores.Log(x) * Consts.InvLn2, 0, s_d, Fn.Log2, x, 0);
    }

    public static R Log10(double x)
    {
        if (double.IsNaN(x) || x < 0.0) { return Nan; }
        if (x == 0.0) { return R.Special(double.NegativeInfinity); }
        if (double.IsPositiveInfinity(x)) { return R.Special(x); }
        if (x == 1.0) { return R.Special(0.0); }
        return R.Dd(Cores.Log(x) * Consts.InvLn10, 0, s_d, Fn.Log10, x, 0);
    }

    public static R Log1p(double x)
    {
        if (double.IsNaN(x) || x < -1.0) { return Nan; }
        if (x == -1.0) { return R.Special(double.NegativeInfinity); }
        if (double.IsPositiveInfinity(x)) { return R.Special(x); }
        if (Math.Abs(x) < Pow2.Of(-55)) { return R.Special(x); }
        return R.Dd(Cores.Log1p(x), 0, s_d, Fn.Log1p, x, 0);
    }

    public static R Sin(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) { return Nan; }
        if (Math.Abs(x) < s_tiny) { return R.Special(x); }
        return R.Dd(Cores.Sin(x), 0, s_d, Fn.Sin, x, 0);
    }

    public static R Cos(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) { return Nan; }
        if (Math.Abs(x) < s_tiny) { return R.Special(1.0); }
        return R.Dd(Cores.Cos(x), 0, s_d, Fn.Cos, x, 0);
    }

    public static void SinCos(double x, out R sin, out R cos)
    {
        if (double.IsNaN(x) || double.IsInfinity(x))
        {
            sin = Nan;
            cos = Nan;
            return;
        }
        if (Math.Abs(x) < s_tiny)
        {
            sin = R.Special(x);
            cos = R.Special(1.0);
            return;
        }
        Cores.SinCos(x, out DD s, out DD c);
        sin = R.Dd(s, 0, s_d, Fn.Sin, x, 0);
        cos = R.Dd(c, 0, s_d, Fn.Cos, x, 0);
    }

    public static R Tan(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) { return Nan; }
        if (Math.Abs(x) < s_tiny) { return R.Special(x); }
        return R.Dd(Cores.Tan(x), 0, s_d, Fn.Tan, x, 0);
    }

    public static R Asin(double x)
    {
        if (double.IsNaN(x) || Math.Abs(x) > 1.0) { return Nan; }
        if (Math.Abs(x) < s_tiny) { return R.Special(x); }
        if (Math.Abs(x) == 1.0)
        {
            return R.Dd(x < 0 ? -Consts.PiOver2 : Consts.PiOver2, 0, s_d, Fn.Asin, x, 0);
        }
        return R.Dd(Cores.Asin(x), 0, s_d, Fn.Asin, x, 0);
    }

    public static R Acos(double x)
    {
        if (double.IsNaN(x) || Math.Abs(x) > 1.0) { return Nan; }
        if (x == 1.0) { return R.Special(0.0); }
        if (x == -1.0) { return R.Dd(Consts.Pi, 0, s_d, Fn.Acos, x, 0); }
        return R.Dd(Cores.Acos(x), 0, s_d, Fn.Acos, x, 0);
    }

    public static R Atan(double x)
    {
        if (double.IsNaN(x)) { return Nan; }
        if (Math.Abs(x) < s_tiny) { return R.Special(x); }
        if (double.IsInfinity(x))
        {
            return R.Dd(x < 0 ? -Consts.PiOver2 : Consts.PiOver2, 0, s_d, Fn.Atan, x, 0);
        }
        DD a = Cores.Atan(new DD(Math.Abs(x)));
        return R.Dd(x < 0 ? -a : a, 0, s_d, Fn.Atan, x, 0);
    }

    private static int Exponent(double v) => (int)((BitConverter.DoubleToInt64Bits(v) >> 52) & 0x7FF) - 1023;

    public static R Atan2(double y, double x)
    {
        if (double.IsNaN(x) || double.IsNaN(y)) { return Nan; }
        bool yneg = double.IsNegative(y);
        bool xneg = double.IsNegative(x);
        DD pi = Consts.Pi;
        if (y == 0.0)
        {
            if (!xneg) { return R.Special(y); }
            return R.Dd(yneg ? -pi : pi, 0, s_d, Fn.Atan2, y, x);
        }
        DD pio2 = Consts.PiOver2;
        if (x == 0.0)
        {
            return R.Dd(yneg ? -pio2 : pio2, 0, s_d, Fn.Atan2, y, x);
        }
        if (double.IsInfinity(x))
        {
            if (double.IsInfinity(y))
            {
                DD q = xneg ? pi * 0.75 : pi * 0.25;
                return R.Dd(yneg ? -q : q, 0, s_d, Fn.Atan2, y, x);
            }
            if (!xneg) { return R.Special(yneg ? -0.0 : 0.0); }
            return R.Dd(yneg ? -pi : pi, 0, s_d, Fn.Atan2, y, x);
        }
        if (double.IsInfinity(y))
        {
            return R.Dd(yneg ? -pio2 : pio2, 0, s_d, Fn.Atan2, y, x);
        }
        double ay = Math.Abs(y);
        double ax = Math.Abs(x);
        int ey = Exponent(ay < 2.2250738585072014E-308 ? ay * 18014398509481984.0 : ay) - (ay < 2.2250738585072014E-308 ? 54 : 0);
        int ex = Exponent(ax < 2.2250738585072014E-308 ? ax * 18014398509481984.0 : ax) - (ax < 2.2250738585072014E-308 ? 54 : 0);
        int d = ey - ex;
        if (d > 900 || d < -900)
        {
            return R.ViaExact(Fn.Atan2, y, x);
        }
        int emax = Math.Max(ey, ex);
        double sc = Pow2.Scale(1.0, -emax);
        double sy = ay * sc;
        double sx = ax * sc;
        DD a;
        if (sy <= sx)
        {
            a = Cores.Atan(new DD(sy) / new DD(sx));
        }
        else
        {
            a = Consts.PiOver2 - Cores.Atan(new DD(sx) / new DD(sy));
        }
        if (xneg)
        {
            a = pi - a;
        }
        if (Math.Abs(a.Hi) < Pow2.Of(-900))
        {
            return R.ViaExact(Fn.Atan2, y, x);
        }
        return R.Dd(yneg ? -a : a, 0, s_d, Fn.Atan2, y, x);
    }

    public static R Sinh(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || x == 0.0) { return R.Special(x); }
        double ax = Math.Abs(x);
        if (ax < s_tiny) { return R.Special(x); }
        bool neg = x < 0;
        if (ax > 711.0) { return R.Special(neg ? double.NegativeInfinity : double.PositiveInfinity); }
        if (ax < 35.0)
        {
            return R.Dd(Cores.Sinh(ax), 0, s_d, Fn.Sinh, ax, 0, neg);
        }
        DD m = Cores.Exp(new DD(ax), out int k);
        return R.Dd(m, k - 1, s_d, Fn.Sinh, ax, 0, neg);
    }

    public static R Cosh(double x)
    {
        if (double.IsNaN(x)) { return Nan; }
        double ax = Math.Abs(x);
        if (double.IsInfinity(ax) || ax > 711.0) { return R.Special(double.PositiveInfinity); }
        if (ax < s_tiny) { return R.Special(1.0); }
        if (ax < 35.0)
        {
            return R.Dd(Cores.Cosh(ax), 0, s_d, Fn.Cosh, ax, 0);
        }
        DD m = Cores.Exp(new DD(ax), out int k);
        return R.Dd(m, k - 1, s_d, Fn.Cosh, ax, 0);
    }

    public static R Tanh(double x)
    {
        if (double.IsNaN(x) || x == 0.0) { return R.Special(x); }
        double ax = Math.Abs(x);
        bool neg = x < 0;
        if (ax < s_tiny) { return R.Special(x); }
        if (ax >= 22.0) { return R.Special(neg ? -1.0 : 1.0); }
        return R.Dd(Cores.Tanh(ax), 0, s_d, Fn.Tanh, ax, 0, neg);
    }

    public static R Cbrt(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || x == 0.0) { return R.Special(x); }
        double ax = Math.Abs(x);
        DD m = Cores.Cbrt(ax, out int k);
        return R.Dd(m, k, s_d, Fn.Cbrt, ax, 0, x < 0);
    }

    public static R Hypot(double x, double y)
    {
        if (double.IsInfinity(x) || double.IsInfinity(y)) { return R.Special(double.PositiveInfinity); }
        if (double.IsNaN(x) || double.IsNaN(y)) { return Nan; }
        if (x == 0.0) { return R.Special(Math.Abs(y)); }
        if (y == 0.0) { return R.Special(Math.Abs(x)); }
        DD m = Cores.Hypot(x, y, out int k);
        return R.Dd(m, k, s_d, Fn.Hypot, x, y);
    }

    private static bool IsInteger(double y) => Math.Abs(y) >= 9007199254740992.0 || y == Math.Floor(y);

    private static bool IsOddInteger(double y) => Math.Abs(y) < 9007199254740992.0 && y == Math.Floor(y) && (((long)y) & 1L) != 0;

    public static R Pow(double x, double y)
    {
        if (y == 0.0 || x == 1.0) { return R.Special(1.0); }
        if (double.IsNaN(x) || double.IsNaN(y)) { return Nan; }
        double ax = Math.Abs(x);
        if (double.IsInfinity(y))
        {
            if (ax == 1.0) { return R.Special(1.0); }
            return R.Special((ax < 1.0) == (y > 0) ? 0.0 : double.PositiveInfinity);
        }
        bool oddY = IsOddInteger(y);
        if (double.IsInfinity(x))
        {
            if (x > 0) { return R.Special(y > 0 ? double.PositiveInfinity : 0.0); }
            if (y > 0) { return R.Special(oddY ? double.NegativeInfinity : double.PositiveInfinity); }
            return R.Special(oddY ? -0.0 : 0.0);
        }
        if (x == 0.0)
        {
            bool negZero = double.IsNegative(x);
            if (y < 0) { return R.Special(negZero && oddY ? double.NegativeInfinity : double.PositiveInfinity); }
            return R.Special(negZero && oddY ? -0.0 : 0.0);
        }
        if (x < 0 && !IsInteger(y)) { return Nan; }
        bool neg = x < 0 && oddY;
        // Operations that IEEE arithmetic already rounds correctly.
        if (y == 1.0) { return R.Special(x); }
        if (y == 2.0) { return R.Special(x * x); }
        if (y == -1.0) { return R.Special(1.0 / x); }
        if (y == 0.5 && x > 0) { return R.Special(Math.Sqrt(x)); }
        DD t = Cores.Log(ax) * y;
        if (t.Hi > 710.0) { return R.Special(neg ? double.NegativeInfinity : double.PositiveInfinity); }
        if (t.Hi < -746.0) { return R.Special(neg ? -0.0 : 0.0); }
        DD m = Cores.Exp(t, out int k);
        return R.Dd(m, k, s_dPow, Fn.Pow, ax, y, neg);
    }
}
