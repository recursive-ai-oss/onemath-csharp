using System;

namespace RecursiveAI.OneMath;

/// <summary>
/// The fast stage: table-driven evaluations that are accurate to about 2^-62 (relative) with plain double arithmetic and
/// a few error-free transformations, so the Ziv test in <see cref="Rounding"/> succeeds for all but about 1 input in
/// 2^8 to 2^9. The rest fall back to the slower double-double stage in <see cref="Cores"/>.
/// The pattern throughout (Tang 1989, 1990; Gal and Bachelis 1991; Muller, <i>Elementary Functions</i>, ch. 4-5):
/// reduce the argument to a small interval around a table point, add the table value (stored as a double-double),
/// and add a short polynomial correction whose terms are small enough that double rounding on them is negligible.
/// Error bounds per function are stated in docs/functions.md.
/// </summary>
internal static class Fast
{
    private const double InvLn2By64 = 92.33248261689366; // 64 / ln 2, only used to pick the table index
    private const double TwoOverPi = 0.6366197723675814;

    // ---------------------------------------------------------------- exp

    /// <summary>
    /// e^t for t = (th + tl): <c>m * 2^k</c> with m in about [0.99, 2). False when |t| is too large for the table scheme.
    /// </summary>
    public static bool Exp(DD t, out DD v, out int k)
    {
        v = default;
        k = 0;
        if (!(Math.Abs(t.Hi) < 700.0))
        {
            return false;
        }
        int n = (int)Math.Round(t.Hi * InvLn2By64);
        double dn = n;
        // r = t - n ln2/64 in double-double: n * part is exact for the first two parts.
        // t.Hi - n*part1 is exact (the operands are within a factor of two of each other, or n = 0); the second product is
        // exact too, so only the last two small terms are rounded, at 2^-60 of the remainder or below.
        double r1 = t.Hi - dn * FastTables.Ln2By64Part1;
        DD r = DD.TwoSum(r1, -(dn * FastTables.Ln2By64Part2));
        v = ExpTable(DD.QuickTwoSum(r.Hi, r.Lo + (t.Lo - dn * FastTables.Ln2By64Part3)), n & 63); // renormalised: ExpTable's polynomial uses only the high part
        k = n >> 6;
        return true;
    }

    /// <summary>2^x as <c>m * 2^k</c>.</summary>
    public static bool Exp2(double x, out DD v, out int k)
    {
        v = default;
        k = 0;
        if (!(Math.Abs(x) < 1000.0))
        {
            return false;
        }
        double nd = Math.Round(x * 64.0);
        int n = (int)nd;
        double f = x - nd * (1.0 / 64.0); // exact: |f| <= 1/128 and x has at most 53 bits
        DD r = Consts.Ln2 * f;
        v = ExpTable(r, n & 63);
        k = n >> 6;
        return true;
    }

    /// <summary>T_j * exp(r) for |r| &lt;= ln2/128 (+ a hair), T_j = 2^(j/64).</summary>
    private static DD ExpTable(DD r, int j)
    {
        double rh = r.Hi;
        double r2 = rh * rh;
        // exp(r) - 1 - r = r^2/2 + r^3/6 + r^4/24 + r^5/120 + r^6/720; the dropped r^7/5040 is below 2^-64.
        double tail = r2 * (0.5 + rh * (1.0 / 6.0 + rh * (1.0 / 24.0 + rh * (1.0 / 120.0 + rh * (1.0 / 720.0)))));
        double q = r.Lo + tail;
        double th = FastTables.ExpHi[j];
        double tl = FastTables.ExpLo[j];
        DD u = DD.TwoProd(th, rh);
        double small = u.Lo + th * q + tl * (1.0 + rh + q);
        DD s = DD.TwoSum(th, u.Hi);
        return DD.QuickTwoSum(s.Hi, s.Lo + small);
    }

    // ---------------------------------------------------------------- log

    /// <summary>
    /// Natural log of a positive finite double. False when x is within 1/16 of 1 (the table scheme loses relative accuracy
    /// there; the slow stage handles it) .
    /// </summary>
    public static bool Log(double x, out DD v)
    {
        v = default;
        if (Math.Abs(x - 1.0) < 0.0625)
        {
            return false;
        }
        int e = 0;
        if (x < 2.2250738585072014E-308)
        {
            x *= 18014398509481984.0; // 2^54
            e = -54;
        }
        long bits = BitConverter.DoubleToInt64Bits(x);
        e += (int)((bits >> 52) & 0x7FF) - 1023;
        int j = (int)((bits >> 45) & 0x7F);
        double m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
        // r = m * inv - 1, exactly, as a double-double: m = c (1 + r) with c = 1 / inv.
        DD p = DD.TwoProd(m, FastTables.LogInv[j]);
        DD r = DD.TwoSum(p.Hi - 1.0, p.Lo);
        double rh = r.Hi;
        double r2 = rh * rh;
        // log(1 + r) - r = -r^2/2 + r^3/3 - ... + r^7/7; the dropped r^8/8 is below 2^-66.
        double tail = r2 * (-0.5 + rh * (1.0 / 3.0 + rh * (-0.25 + rh * (0.2 + rh * (-1.0 / 6.0 + rh * (1.0 / 7.0))))));
        DD s = new DD(FastTables.LogHi[j], FastTables.LogLo[j]);
        if (e != 0)
        {
            s = Cores.MulLn2(e) + s;
        }
        s = s + rh;
        v = s + (r.Lo + tail);
        return true;
    }

    // ---------------------------------------------------------------- sin and cos

    /// <summary>
    /// Reduces |x| &lt;= 2^19 to r = x - q pi/2 with |r| &lt;= pi/4 (Cody and Waite 1980, with three 33-bit parts of pi/2 so each
    /// k times a part is exact, and a double remainder). Returns q mod 4.
    /// </summary>
    private static int ReduceSmall(double x, out DD r)
    {
        double kd = Math.Round(x * TwoOverPi);
        double k = kd;
        r = DD.TwoSum(x, -(k * FastTables.PiO2Part1));
        r = r + (-(k * FastTables.PiO2Part2));
        r = r + (-(k * FastTables.PiO2Part3));
        r = r + (-(k * FastTables.PiO2Part4));
        return (int)((long)kd & 3L);
    }

    /// <summary>sin(r) and cos(r) for a double-double |r| &lt;= pi/4 + 2^-20 using the table of j/128.</summary>
    private static void SinCosSmall(DD r, bool wantSin, bool wantCos, out DD s, out DD c)
    {
        bool neg = r.Hi < 0;
        double ah = neg ? -r.Hi : r.Hi;
        double al = neg ? -r.Lo : r.Lo;
        int j = (int)(ah * 128.0 + 0.5);
        if (j > 101)
        {
            j = 101;
        }
        double bh = ah - j * (1.0 / 128.0); // exact
        double b2 = bh * bh;
        // sin b - b = -b^3/6 + b^5/120 - b^7/5040 and cos b - 1 = -b^2/2 + b^4/24 - b^6/720, |b| <= 2^-8 (+ a hair).
        double sbTail = bh * b2 * (-1.0 / 6.0 + b2 * (1.0 / 120.0 + b2 * (-1.0 / 5040.0)));
        double cb1 = b2 * (-0.5 + b2 * (1.0 / 24.0 + b2 * (-1.0 / 720.0))) - bh * al; // the last term is the effect of the low part of r
        double sh = FastTables.SinHi[j];
        double sl = FastTables.SinLo[j];
        double ch = FastTables.CosHi[j];
        double cl = FastTables.CosLo[j];
        double sbx = sbTail + al; // sin b - bh, to the accuracy that matters
        s = default;
        c = default;
        if (wantSin)
        {
            // sin(a) = S cos b + C sin b
            DD p = DD.TwoProd(ch, bh);
            double small = p.Lo + ch * sbx + cl * bh + sh * cb1 + sl;
            DD t = DD.TwoSum(sh, p.Hi);
            DD v = DD.QuickTwoSum(t.Hi, t.Lo + small);
            s = neg ? -v : v;
        }
        if (wantCos)
        {
            // cos(a) = C cos b - S sin b
            DD p = DD.TwoProd(sh, bh);
            double small = ch * cb1 + cl - (p.Lo + sh * sbx + sl * bh);
            DD t = DD.TwoSum(ch, -p.Hi);
            c = DD.QuickTwoSum(t.Hi, t.Lo + small);
        }
    }

    /// <summary>sin(x) for 2^-30 &lt;= |x| &lt;= 2^19, or false.</summary>
    public static bool Sin(double x, out DD v)
    {
        v = default;
        if (!(Math.Abs(x) <= 524288.0))
        {
            return false;
        }
        int q = ReduceSmall(x, out DD r);
        if ((q & 1) == 0)
        {
            SinCosSmall(r, true, false, out DD s, out _);
            v = q == 0 ? s : -s;
        }
        else
        {
            SinCosSmall(r, false, true, out _, out DD c);
            v = q == 1 ? c : -c;
        }
        return true;
    }

    public static bool Cos(double x, out DD v)
    {
        v = default;
        if (!(Math.Abs(x) <= 524288.0))
        {
            return false;
        }
        int q = ReduceSmall(x, out DD r);
        if ((q & 1) == 0)
        {
            SinCosSmall(r, false, true, out _, out DD c);
            v = q == 0 ? c : -c;
        }
        else
        {
            SinCosSmall(r, true, false, out DD s, out _);
            v = q == 1 ? -s : s;
        }
        return true;
    }

    public static bool SinCos(double x, out DD sin, out DD cos)
    {
        sin = default;
        cos = default;
        if (!(Math.Abs(x) <= 524288.0))
        {
            return false;
        }
        int q = ReduceSmall(x, out DD r);
        SinCosSmall(r, true, true, out DD s, out DD c);
        switch (q)
        {
            case 0: sin = s; cos = c; break;
            case 1: sin = c; cos = -s; break;
            case 2: sin = -s; cos = -c; break;
            default: sin = -c; cos = s; break;
        }
        return true;
    }

    public static bool Tan(double x, out DD v)
    {
        v = default;
        if (!(Math.Abs(x) <= 524288.0))
        {
            return false;
        }
        int q = ReduceSmall(x, out DD r);
        SinCosSmall(r, true, true, out DD s, out DD c);
        v = (q & 1) == 0 ? Div(s, c) : -Div(c, s);
        return true;
    }

    /// <summary>a / b to about 2^-100 relative (one correction step).</summary>
    private static DD Div(DD a, DD b)
    {
        double t = a.Hi / b.Hi;
        DD p = DD.TwoProd(t, b.Hi);
        double rem = (((a.Hi - p.Hi) - p.Lo) + a.Lo) - t * b.Lo;
        return DD.QuickTwoSum(t, rem / b.Hi);
    }

    // ---------------------------------------------------------------- atan

    /// <summary>atan of a non-negative double-double, by the table of atan(j/64) and the subtraction formula.</summary>
    public static DD Atan(DD t)
    {
        bool inv = t.Hi > 1.0;
        DD u = inv ? new DD(1.0) / t : t;
        int j = (int)(u.Hi * 64.0 + 0.5);
        DD b;
        if (j == 0)
        {
            b = u;
        }
        else
        {
            double c = j * (1.0 / 64.0);
            DD num = u + (-c);
            DD den = (u * c) + 1.0;
            b = num / den;
        }
        double bh = b.Hi;
        double b2 = bh * bh;
        // atan b - b = -b^3/3 + b^5/5 - b^7/7 + b^9/9 with |b| <= 2^-7; the dropped b^11/11 is below 2^-80.
        double tail = bh * b2 * (-1.0 / 3.0 + b2 * (0.2 + b2 * (-1.0 / 7.0 + b2 * (1.0 / 9.0))));
        DD a;
        if (j == 0)
        {
            a = DD.QuickTwoSum(bh, b.Lo + tail);
        }
        else
        {
            DD s = DD.TwoSum(FastTables.AtanHi[j], bh);
            a = DD.QuickTwoSum(s.Hi, s.Lo + (FastTables.AtanLo[j] + b.Lo + tail));
        }
        return inv ? Consts.PiOver2 - a : a;
    }

    // ---------------------------------------------------------------- exp-based

    /// <summary>e^x - 1 for 0.5 &lt;= |x| &lt; 700: exp from the table, minus 1 (no cancellation worse than one bit).</summary>
    public static bool Expm1(double x, out DD v, out int k)
    {
        v = default;
        k = 0;
        if (!(Math.Abs(x) >= 0.5) || !Exp(new DD(x), out DD u, out int ku))
        {
            return false;
        }
        if (ku > 600)
        {
            v = u;
            k = ku;
            return true;
        }
        v = u.Scale(Pow2.Scale(1.0, ku)) + (-1.0);
        return true;
    }

    /// <summary>e^ax as an unscaled double-double for 0 &lt;= ax &lt; 35 (the result is below 2^51).</summary>
    private static bool ExpSmall(double ax, out DD u)
    {
        u = default;
        if (!Exp(new DD(ax), out DD m, out int k))
        {
            return false;
        }
        u = m.Scale(Pow2.Of(k));
        return true;
    }

    /// <summary>sinh(ax) for 0.5 &lt;= ax &lt; 35 as (u - 1/u) / 2.</summary>
    public static bool Sinh(double ax, out DD v)
    {
        v = default;
        if (!(ax >= 0.5 && ax < 35.0) || !ExpSmall(ax, out DD u))
        {
            return false;
        }
        v = (u - Div(new DD(1.0), u)).Scale(0.5);
        return true;
    }

    /// <summary>cosh(ax) for 0 &lt;= ax &lt; 35 as (u + 1/u) / 2.</summary>
    public static bool Cosh(double ax, out DD v)
    {
        v = default;
        if (!(ax >= 0.0 && ax < 35.0) || !ExpSmall(ax, out DD u))
        {
            return false;
        }
        v = (u + Div(new DD(1.0), u)).Scale(0.5);
        return true;
    }

    /// <summary>tanh(ax) for 0.5 &lt;= ax &lt; 22 as (e - 1) / (e + 1) with e = exp(2 ax).</summary>
    public static bool Tanh(double ax, out DD v)
    {
        v = default;
        if (!(ax >= 0.5 && ax < 22.0) || !ExpSmall(2.0 * ax, out DD e))
        {
            return false;
        }
        v = Div(e + (-1.0), e + 1.0);
        return true;
    }

    /// <summary>
    /// x^y for positive x as <c>m * 2^k</c> through exp(y log x). The relative error grows with |y log x|, reported in
    /// <paramref name="delta"/>; false when x is near 1 or |y log x| is 64 or more.
    /// </summary>
    public static bool Pow(double x, double y, out DD v, out int k, out double delta)
    {
        v = default;
        k = 0;
        delta = 0;
        if (!Log(x, out DD l))
        {
            return false;
        }
        DD t = l * y;
        double at = Math.Abs(t.Hi);
        if (!(at < 64.0) || !Exp(t, out v, out k))
        {
            return false;
        }
        // log is good to about 2^-63 relative, so t carries |t| * 2^-63; the exp stage adds 2^-62 (margins in docs/functions.md).
        delta = 2.0 / 4611686018427387904.0 + at * (1.0 / 4611686018427387904.0);
        return true;
    }
}
