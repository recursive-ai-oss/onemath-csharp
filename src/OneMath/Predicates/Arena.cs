using System;

namespace RecursiveAI.OneMath.Predicates;

/// <summary>
/// Per-thread bump allocator for the temporary expansions of the exact fallback paths. Which thread
/// owns which arena never influences a result. The area is allocated on the first exact evaluation,
/// so code that only takes the filtered fast path never pays for it.
/// </summary>
internal sealed class Arena
{
    // Largest working set is an in-sphere evaluation; 2^17 doubles (1 MiB) leaves ample headroom
    // (tests run it on inputs spanning the whole supported exponent range).
    private const int Capacity = 1 << 17;

    [ThreadStatic]
    private static Arena? t_instance;

    private readonly double[] _buf = new double[Capacity];
    private int _top;

    public static Arena Get() => t_instance ??= new Arena();

    public int Mark => _top;

    public void Release(int mark) => _top = mark;

    public Span<double> Alloc(int n)
    {
        if (n > Capacity - _top)
        {
            throw new InvalidOperationException("Exact-arithmetic scratch area exhausted; inputs are outside the supported coordinate range.");
        }
        Span<double> s = _buf.AsSpan(_top, n);
        _top += n;
        return s;
    }

    /// <summary>Shrinks the most recent allocation (starting at <paramref name="start"/>) to <paramref name="len"/> doubles.</summary>
    public void Trim(int start, int len) => _top = start + len;

    /// <summary>e * f into <paramref name="h"/> (capacity 2|e||f|), intermediates released before returning.</summary>
    public int Mul(ReadOnlySpan<double> e, ReadOnlySpan<double> f, Span<double> h)
    {
        int mark = _top;
        int len = 0;
        Span<double> t = Alloc(2 * e.Length);
        Span<double> s = Alloc(h.Length > 0 ? 2 * e.Length * f.Length : 0);
        for (int j = 0; j < f.Length; j++)
        {
            int tl = ExpansionArithmetic.Scale(e, f[j], t);
            if (tl == 0)
            {
                continue;
            }
            int sl = ExpansionArithmetic.Sum(h[..len], t[..tl], s);
            s[..sl].CopyTo(h);
            len = sl;
        }
        _top = mark;
        return len;
    }

    /// <summary>Allocates and returns e * f trimmed to its actual length.</summary>
    public Span<double> Product(ReadOnlySpan<double> e, ReadOnlySpan<double> f)
    {
        int start = _top;
        Span<double> h = Alloc(2 * e.Length * f.Length);
        int len = Mul(e, f, h);
        Trim(start, len);
        return _buf.AsSpan(start, len);
    }

    public Span<double> Add(ReadOnlySpan<double> e, ReadOnlySpan<double> f)
    {
        int start = _top;
        Span<double> h = Alloc(e.Length + f.Length);
        int len = ExpansionArithmetic.Sum(e, f, h);
        Trim(start, len);
        return _buf.AsSpan(start, len);
    }

    public Span<double> Sub(ReadOnlySpan<double> e, ReadOnlySpan<double> f)
    {
        int start = _top;
        Span<double> h = Alloc(e.Length + f.Length);
        int len = ExpansionArithmetic.Difference(e, f, h);
        Trim(start, len);
        return _buf.AsSpan(start, len);
    }

    /// <summary>The exact two-component expansion of a - b.</summary>
    public Span<double> Diff2(double a, double b)
    {
        ExpansionArithmetic.TwoDiff(a, b, out double x, out double y);
        int start = _top;
        Span<double> h = Alloc(2);
        int n = 0;
        if (y != 0.0)
        {
            h[n++] = y;
        }
        if (x != 0.0)
        {
            h[n++] = x;
        }
        Trim(start, n);
        return _buf.AsSpan(start, n);
    }

    /// <summary>The one-component expansion of a (empty for zero).</summary>
    public Span<double> One(double a)
    {
        int start = _top;
        Span<double> h = Alloc(1);
        int n = 0;
        if (a != 0.0)
        {
            h[n++] = a;
        }
        Trim(start, n);
        return _buf.AsSpan(start, n);
    }

    /// <summary>x*x + y*y (+ z*z when <paramref name="z"/> is non-null) as an expansion.</summary>
    public Span<double> Lift(ReadOnlySpan<double> x, ReadOnlySpan<double> y, ReadOnlySpan<double> z, bool useZ)
    {
        Span<double> s = Add(Product(x, x), Product(y, y));
        return useZ ? Add(s, Product(z, z)) : s;
    }

    /// <summary>p*s - q*r.</summary>
    public Span<double> Det2(ReadOnlySpan<double> p, ReadOnlySpan<double> q, ReadOnlySpan<double> r, ReadOnlySpan<double> s) =>
        Sub(Product(p, s), Product(q, r));

    /// <summary>Determinant of the 3x3 matrix with rows (a0 a1 a2), (b0 b1 b2), (c0 c1 c2), expanded along the first row.</summary>
    public Span<double> Det3(
        ReadOnlySpan<double> a0, ReadOnlySpan<double> a1, ReadOnlySpan<double> a2,
        ReadOnlySpan<double> b0, ReadOnlySpan<double> b1, ReadOnlySpan<double> b2,
        ReadOnlySpan<double> c0, ReadOnlySpan<double> c1, ReadOnlySpan<double> c2)
    {
        Span<double> m0 = Det2(b1, b2, c1, c2);
        Span<double> m1 = Det2(b0, b2, c0, c2);
        Span<double> m2 = Det2(b0, b1, c0, c1);
        Span<double> t = Sub(Product(a0, m0), Product(a1, m1));
        return Add(t, Product(a2, m2));
    }
}
