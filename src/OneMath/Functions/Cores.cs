using System;

namespace RecursiveAI.OneMath;

/// <summary>
/// Double-double evaluations of the elementary functions, accurate to about 2^-95 relative. They feed the rounding
/// test in <see cref="Rounding"/>. Series coefficients are exact rationals (1/n!, 1/(2k+1)) computed once with
/// double-double division, so no table depends on a platform function. High-order series terms are summed in plain
/// double because their size is below double-double resolution of the sum; the order of every sum is fixed.
/// </summary>
internal static class Cores
{
    private static readonly DD[] s_expC = new DD[10];   // 1/(n+1)!
    private static readonly DD[] s_sinC = new DD[15];   // (-1)^k/(2k+1)!
    private static readonly DD[] s_cosC = new DD[14];   // (-1)^(j+1)/(2j+2)!
    private static readonly DD[] s_atanhC = new DD[22]; // 1/(2k+1)
    private static readonly DD[] s_atanC = new DD[23];  // (-1)^k/(2k+1)

    static Cores()
    {
        s_expC[0] = new DD(1.0);
        for (int n = 1; n < s_expC.Length; n++)
        {
            s_expC[n] = s_expC[n - 1] / (double)(n + 1);
        }
        s_sinC[0] = new DD(1.0);
        for (int k = 1; k < s_sinC.Length; k++)
        {
            s_sinC[k] = -(s_sinC[k - 1] / (double)((2 * k) * (2 * k + 1)));
        }
        s_cosC[0] = new DD(-0.5);
        for (int j = 1; j < s_cosC.Length; j++)
        {
            s_cosC[j] = -(s_cosC[j - 1] / (double)((2 * j + 1) * (2 * j + 2)));
        }
        DD one = new(1.0);
        for (int k = 0; k < s_atanhC.Length; k++)
        {
            s_atanhC[k] = one / (double)(2 * k + 1);
        }
        for (int k = 0; k < s_atanC.Length; k++)
        {
            DD c = one / (double)(2 * k + 1);
            s_atanC[k] = (k & 1) == 0 ? c : -c;
        }
    }

    /// <summary>Horner evaluation of sum c[i] w^i: terms from nDD upward in plain double, the rest in double-double.</summary>
    private static DD Poly(DD[] c, int nDD, DD w)
    {
        double t = c[c.Length - 1].Hi;
        double wh = w.Hi;
        for (int i = c.Length - 2; i >= nDD; i--)
        {
            t = t * wh + c[i].Hi;
        }
        DD acc = new(t);
        for (int i = nDD - 1; i >= 0; i--)
        {
            acc = acc * w + c[i];
        }
        return acc;
    }

    // ---------------------------------------------------------------- exp family

    /// <summary>expm1(r) for |r| &lt;= 0.36: scale by 2^-8, short Taylor series, then e -> 2e + e^2 eight times.</summary>
    private static DD ExpM1Small(DD r)
    {
        DD u = r.Scale(1.0 / 256.0);
        DD e = u * Poly(s_expC, 5, u);
        for (int i = 0; i < 8; i++)
        {
            DD sq = e * e;
            e = new DD(e.Hi * 2.0, e.Lo * 2.0) + sq;
        }
        return e;
    }

    /// <summary>t - k ln 2 with ln 2 in three parts; the first two parts have 40 bits so k times them is exact.</summary>
    private static DD ReduceLn2(DD t, int k)
    {
        double dk = k;
        DD r = t + (-(dk * Consts.Ln2Part1));
        r += -(dk * Consts.Ln2Part2);
        r += -(dk * Consts.Ln2Part3);
        return r;
    }

    /// <summary>e^t as <c>m * 2^k</c>, with m in about [0.7, 1.42]. |t| must be below about 1100.</summary>
    public static DD Exp(DD t, out int k)
    {
        k = (int)Math.Round(t.Hi * Consts.InvLn2.Hi);
        return ExpM1Small(ReduceLn2(t, k)) + 1.0;
    }

    /// <summary>2^x as <c>m * 2^k</c> (|x| below 1100).</summary>
    public static DD Exp2(double x, out int k)
    {
        double kr = Math.Round(x);
        k = (int)kr;
        DD r = Consts.Ln2 * (x - kr);
        return ExpM1Small(r) + 1.0;
    }

    /// <summary>e^x - 1 as <c>m * 2^k</c>. For |x| below about 415 the exact value is formed (k = 0 on return).</summary>
    public static DD ExpM1(double x, out int k)
    {
        int kk = (int)Math.Round(x * Consts.InvLn2.Hi);
        DD e = ExpM1Small(ReduceLn2(new DD(x), kk));
        if (kk == 0)
        {
            k = 0;
            return e;
        }
        DD u = e + 1.0;
        if (kk > 600)
        {
            k = kk;
            return u;
        }
        k = 0;
        return u.Scale(Pow2.Scale(1.0, kk)) + (-1.0);
    }

    // ---------------------------------------------------------------- log family

    /// <summary>log(m) for m in about [0.7, 1.42] as 2 atanh((m-1)/(m+1)).</summary>
    private static DD LogNear1(DD m)
    {
        DD z = (m + (-1.0)) / (m + 1.0);
        DD w = z * z;
        DD acc = Poly(s_atanhC, 11, w);
        DD v = z * acc;
        return new DD(v.Hi * 2.0, v.Lo * 2.0);
    }

    /// <summary>e * ln 2 as a double-double (|e| below 4096).</summary>
    private static DD MulLn2(int e)
    {
        double de = e;
        DD s = DD.TwoSum(de * Consts.Ln2Part1, de * Consts.Ln2Part2);
        return s + de * Consts.Ln2Part3;
    }

    /// <summary>Natural log of a positive finite double (subnormals scaled first).</summary>
    public static DD Log(double x)
    {
        int e = 0;
        if (x < 2.2250738585072014E-308)
        {
            x *= 18014398509481984.0; // 2^54
            e = -54;
        }
        long bits = BitConverter.DoubleToInt64Bits(x);
        e += (int)((bits >> 52) & 0x7FF) - 1023;
        double m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
        if (m > 1.4142135623730951)
        {
            m *= 0.5;
            e++;
        }
        DD l = LogNear1(new DD(m));
        return e == 0 ? l : MulLn2(e) + l;
    }

    /// <summary>log(1 + x) for x &gt; -1.</summary>
    public static DD Log1p(double x)
    {
        if (Math.Abs(x) < 0.2)
        {
            // atanh form keeps full relative accuracy however small x is.
            DD z = new DD(x) / (new DD(2.0) + x);
            DD w = z * z;
            DD v = z * Poly(s_atanhC, 11, w);
            return new DD(v.Hi * 2.0, v.Lo * 2.0);
        }
        // 1 + x is exact as a double-double; split off the exponent.
        DD u = DD.TwoSum(1.0, x);
        long bits = BitConverter.DoubleToInt64Bits(u.Hi);
        int e = (int)((bits >> 52) & 0x7FF) - 1023;
        double sc = Pow2.Scale(1.0, -e);
        DD m = u.Scale(sc);
        if (m.Hi > 1.4142135623730951)
        {
            m = m.Scale(0.5);
            e++;
        }
        DD l = LogNear1(m);
        return e == 0 ? l : MulLn2(e) + l;
    }

    // ---------------------------------------------------------------- atan

    /// <summary>atan of a non-negative double-double.</summary>
    public static DD Atan(DD t)
    {
        bool inv = t.Hi > 1.0;
        DD u = inv ? new DD(1.0) / t : t;
        for (int i = 0; i < 2; i++)
        {
            // atan u = 2 atan(u / (1 + sqrt(1 + u^2)))
            DD s = DD.Sqrt((u * u) + 1.0);
            u = u / (s + 1.0);
        }
        DD w = u * u;
        DD v = u * Poly(s_atanC, 12, w);
        DD a = new(v.Hi * 4.0, v.Lo * 4.0);
        return inv ? Consts.PiOver2 - a : a;
    }

    // ---------------------------------------------------------------- trigonometric

    private static DD SinSeries(DD r)
    {
        DD w = r * r;
        return r * Poly(s_sinC, 8, w);
    }

    private static DD CosSeries(DD r)
    {
        DD w = r * r;
        return (w * Poly(s_cosC, 7, w)) + 1.0;
    }

    private static readonly ulong[] s_twoOverPi = Consts.TwoOverPi;

    /// <summary>
    /// Payne-Hanek reduction (Payne and Hanek 1983): x = q * pi/2 + r with |r| &lt;= pi/4 (plus a hair), from the binary
    /// expansion of 2/pi. Returns q mod 4; r is accurate to 2^-100 relative to r itself, even when r is about 2^-62.
    /// </summary>
    public static int Reduce(double x, out DD r)
    {
        double ax = Math.Abs(x);
        if (ax <= 0.7853981633974483)
        {
            r = new DD(x);
            return 0;
        }
        long bits = BitConverter.DoubleToInt64Bits(ax);
        int ex = (int)((bits >> 52) & 0x7FF);
        ulong m = ((ulong)bits & 0xFFFFFFFFFFFFFUL) | (1UL << 52);
        int e = ex - 1075;
        int j0 = e >= 2 ? (e - 2) / 64 : 0;
        int a = -(e - 64 * (j0 + 5)); // bit index in the product that carries weight 2^0
        Span<ulong> p = stackalloc ulong[8];
        p.Clear();
        for (int i = 0; i < 5; i++)
        {
            ulong hi = Math.BigMul(m, s_twoOverPi[j0 + i], out ulong lo);
            AddAt(p, 4 - i, lo);
            AddAt(p, 5 - i, hi);
        }
        int n = (int)(Bits64(p, a) & 3UL);
        ulong f0 = Bits64(p, a - 64);
        ulong f1 = Bits64(p, a - 128);
        ulong f2 = Bits64(p, a - 192);
        bool neg = (f0 >> 63) != 0;
        if (neg)
        {
            // fraction >= 1/2: use the next integer and the negative remainder (192-bit two's complement).
            f2 = ~f2 + 1;
            ulong c = f2 == 0 ? 1UL : 0UL;
            f1 = ~f1 + c;
            c = (c == 1 && f1 == 0) ? 1UL : 0UL;
            f0 = ~f0 + c;
            n++;
        }
        DD f = new(0.0);
        f += (double)(f2 & 0xFFFFFFFFUL) * 2.3283064365386963E-10 * 2.3283064365386963E-10 * 2.3283064365386963E-10 * 2.3283064365386963E-10 * 2.3283064365386963E-10 * 2.3283064365386963E-10;
        f += (double)(f2 >> 32) * 2.3283064365386963E-10 * 2.3283064365386963E-10 * 2.3283064365386963E-10 * 2.3283064365386963E-10 * 2.3283064365386963E-10;
        f += (double)(f1 & 0xFFFFFFFFUL) * 2.3283064365386963E-10 * 2.3283064365386963E-10 * 2.3283064365386963E-10 * 2.3283064365386963E-10;
        f += (double)(f1 >> 32) * 2.3283064365386963E-10 * 2.3283064365386963E-10 * 2.3283064365386963E-10;
        f += (double)(f0 & 0xFFFFFFFFUL) * 2.3283064365386963E-10 * 2.3283064365386963E-10;
        f += (double)(f0 >> 32) * 2.3283064365386963E-10;
        DD rr = f * Consts.PiOver2;
        if (neg)
        {
            rr = -rr;
        }
        int q = n & 3;
        if (x < 0)
        {
            q = (4 - q) & 3;
            rr = -rr;
        }
        r = rr;
        return q;
    }

    private static void AddAt(Span<ulong> p, int idx, ulong v)
    {
        ulong s = p[idx] + v;
        bool carry = s < v;
        p[idx] = s;
        for (int i = idx + 1; carry && i < p.Length; i++)
        {
            p[i]++;
            carry = p[i] == 0;
        }
    }

    /// <summary>The 64 bits of the little-endian multi-limb integer starting at bit <paramref name="low"/>.</summary>
    private static ulong Bits64(ReadOnlySpan<ulong> p, int low)
    {
        int w = low >> 6;
        int b = low & 63;
        ulong v = p[w] >> b;
        if (b != 0)
        {
            v |= p[w + 1] << (64 - b);
        }
        return v;
    }

    /// <summary>sin(x) for any finite x with |x| above the tiny-argument shortcut.</summary>
    public static DD Sin(double x)
    {
        int q = Reduce(x, out DD r);
        switch (q)
        {
            case 0: return SinSeries(r);
            case 1: return CosSeries(r);
            case 2: return -SinSeries(r);
            default: return -CosSeries(r);
        }
    }

    public static DD Cos(double x)
    {
        int q = Reduce(x, out DD r);
        switch (q)
        {
            case 0: return CosSeries(r);
            case 1: return -SinSeries(r);
            case 2: return -CosSeries(r);
            default: return SinSeries(r);
        }
    }

    public static void SinCos(double x, out DD sin, out DD cos)
    {
        int q = Reduce(x, out DD r);
        DD s = SinSeries(r);
        DD c = CosSeries(r);
        switch (q)
        {
            case 0: sin = s; cos = c; break;
            case 1: sin = c; cos = -s; break;
            case 2: sin = -s; cos = -c; break;
            default: sin = -c; cos = s; break;
        }
    }

    public static DD Tan(double x)
    {
        int q = Reduce(x, out DD r);
        DD s = SinSeries(r);
        DD c = CosSeries(r);
        return (q & 1) == 0 ? s / c : -(c / s);
    }

    /// <summary>asin(x) for 0 &lt; |x| &lt; 1 as atan(x / sqrt(1 - x^2)).</summary>
    public static DD Asin(double x)
    {
        double ax = Math.Abs(x);
        DD den = DD.Sqrt(new DD(1.0) - DD.TwoProd(ax, ax));
        DD a = Atan(new DD(ax) / den);
        return x < 0 ? -a : a;
    }

    /// <summary>acos(x) for -1 &lt; x &lt; 1 as 2 atan(sqrt((1-x)/(1+x))); no cancellation near 1 or -1.</summary>
    public static DD Acos(double x)
    {
        DD q = DD.TwoSum(1.0, -x) / DD.TwoSum(1.0, x);
        DD a = Atan(DD.Sqrt(q));
        return new DD(a.Hi * 2.0, a.Lo * 2.0);
    }

    // ---------------------------------------------------------------- roots

    /// <summary>Cube root of a positive finite double as <c>m * 2^k</c>: Newton in double, one double-double step.</summary>
    public static DD Cbrt(double x, out int k)
    {
        int e = 0;
        if (x < 2.2250738585072014E-308)
        {
            x *= 18014398509481984.0; // 2^54 = (2^18)^3
            e = -54;
        }
        long bits = BitConverter.DoubleToInt64Bits(x);
        int ex = (int)((bits >> 52) & 0x7FF) - 1023 + e;
        double m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);
        int q = ex >= 0 ? ex / 3 : -((2 - ex) / 3);
        int rem = ex - 3 * q; // 0..2
        m *= rem == 0 ? 1.0 : (rem == 1 ? 2.0 : 4.0); // m in [1, 8)
        double y = 1.0 + (m - 1.0) * (1.0 / 3.5);
        for (int i = 0; i < 6; i++)
        {
            y = y - (y * y * y - m) / (3.0 * y * y);
        }
        DD y2 = DD.TwoProd(y, y);
        DD y3 = y2 * y;
        DD res = y3 + (-m);
        double corr = res.Hi / (3.0 * y2.Hi);
        k = q;
        return DD.QuickTwoSum(y, -corr);
    }

    /// <summary>sqrt(a^2 + b^2) for finite nonzero a and b as <c>m * 2^k</c>, scaled first so nothing overflows.</summary>
    public static DD Hypot(double a, double b, out int k)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        double big = Math.Max(a, b);
        int e = (int)((BitConverter.DoubleToInt64Bits(big) >> 52) & 0x7FF) - 1023;
        double sc = Pow2.Scale(1.0, -e);
        a *= sc;
        b *= sc;
        DD s = DD.TwoProd(a, a) + DD.TwoProd(b, b);
        k = e;
        return DD.Sqrt(s);
    }

    // ---------------------------------------------------------------- hyperbolic

    /// <summary>sinh x for |x| &lt; 35 as (E + E/(1+E))/2, E = expm1(x); sums never cancel.</summary>
    public static DD Sinh(double x)
    {
        DD e = ExpM1(x, out _);
        DD s = e + (e / (e + 1.0));
        return s.Scale(0.5);
    }

    /// <summary>cosh x for |x| &lt; 35 as (u + 1/u)/2, u = e^x.</summary>
    public static DD Cosh(double x)
    {
        DD u = ExpM1(x, out _) + 1.0;
        DD s = u + (new DD(1.0) / u);
        return s.Scale(0.5);
    }

    /// <summary>tanh x for |x| &lt; 22 as E/(E+2) with E = expm1(2x).</summary>
    public static DD Tanh(double x)
    {
        DD e = ExpM1(2.0 * x, out _);
        return e / (e + 2.0);
    }

    // ---------------------------------------------------------------- pow

    /// <summary>x^y for positive finite x as <c>m * 2^k</c>; |y log x| must be below about 1000.</summary>
    public static DD Pow(double x, double y, out int k)
    {
        DD t = Log(x) * y;
        return Exp(t, out k);
    }
}
