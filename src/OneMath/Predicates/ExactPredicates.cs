using System;

namespace RecursiveAI.OneMath.Predicates;

/// <summary>
/// Exact-sign geometric predicates on double coordinates (float32 values promote to double exactly).
/// <para>
/// Every method returns the sign (-1, 0 or +1) of a determinant evaluated <b>exactly</b>, as if with
/// infinite precision on the given doubles. Each first evaluates the determinant in ordinary
/// floating point with a rigorous error bound (Shewchuk 1997, "stage A") and returns when the
/// result is larger than the bound; otherwise it re-evaluates with exact expansion arithmetic
/// (<see cref="ExpansionArithmetic"/>). The sign is therefore the same as the exact one and identical on
/// every platform: only IEEE-exact <c>+ - *</c> are used, with no fused multiply-add.
/// </para>
/// <para>
/// <b>Input requirements.</b> All coordinates must be finite; NaN or infinity throws
/// <see cref="ArgumentOutOfRangeException"/>. Exactness is guaranteed for coordinates that are zero or
/// have magnitude in [2^-140, 2^200] (about 7e-43 .. 1.6e60); a nonzero coordinate outside that range
/// throws <see cref="ArgumentOutOfRangeException"/> whenever the exact path is needed. (A call the
/// floating-point filter can decide with a comfortable margin may succeed outside the range, and its
/// sign is still correct.) The bounds keep every product of up to five coordinates inside double's
/// normal range, so no product overflows or loses error bits. Real-world data (world coordinates within
/// ±2^21 at a resolution of 2^-32) lies well inside.
/// </para>
/// <para>
/// The methods never allocate on the filtered path. The exact path uses a per-thread scratch area.
/// </para>
/// </summary>
public static class ExactPredicates
{
    // Shewchuk 1997 Sec. 3: epsilon = 2^-53, half an ulp of 1.
    private const double Epsilon = 1.1102230246251565404e-16;

    // Filter constants: (3 + 16e)e, (7 + 56e)e, (10 + 96e)e, (16 + 224e)e. They bound the relative error of
    // the straightforward evaluation, as a multiple of the "permanent" (sum of absolute values of the terms).
    private const double Orient2DBound = (3.0 + 16.0 * Epsilon) * Epsilon;
    private const double Orient3DBound = (7.0 + 56.0 * Epsilon) * Epsilon;
    private const double InCircleBound = (10.0 + 96.0 * Epsilon) * Epsilon;
    private const double InSphereBound = (16.0 + 224.0 * Epsilon) * Epsilon;

    // Absolute slack that covers underflow of products far outside the supported range; it never matters inside it.
    private static readonly double Tiny = Pow2(-1000);

    // Supported magnitude range of nonzero coordinates on the exact path.
    private static readonly double MinMagnitude = Pow2(-140);
    private static readonly double MaxMagnitude = Pow2(200);

    // Exact power of two by repeated doubling or halving (both are exact in this range).
    private static double Pow2(int e)
    {
        double r = 1.0;
        for (int i = 0; i < Math.Abs(e); i++)
        {
            r = e > 0 ? r * 2.0 : r * 0.5;
        }
        return r;
    }

    /// <summary>
    /// Orientation of the triangle (a, b, c): +1 if the points turn counter-clockwise (c is left of the directed
    /// line a→b, in a y-up coordinate system), -1 if clockwise, 0 if collinear. Exactly the sign of
    /// (ax-cx)(by-cy) - (ay-cy)(bx-cx).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is NaN, infinite, or outside the supported range (see the class remarks).</exception>
    public static int Orient2D(double ax, double ay, double bx, double by, double cx, double cy)
    {
        double detleft = (ax - cx) * (by - cy);
        double detright = (ay - cy) * (bx - cx);
        double det = detleft - detright;
        double bound = Orient2DBound * (Math.Abs(detleft) + Math.Abs(detright)) + Tiny;
        if (det > bound)
        {
            return 1;
        }
        if (-det > bound)
        {
            return -1;
        }
        return Orient2DExact(ax, ay, bx, by, cx, cy);
    }

    private static int Orient2DExact(double ax, double ay, double bx, double by, double cx, double cy)
    {
        CheckRange(ax, ay, bx, by, cx, cy);
        Arena ar = Arena.Get();
        int mark = ar.Mark;
        try
        {
            // ax(by-cy) + bx(cy-ay) + cx(ay-by): the translation-free expansion of the same determinant.
            Span<double> t0 = ar.Product(ar.One(ax), ar.Diff2(by, cy));
            Span<double> t1 = ar.Product(ar.One(bx), ar.Diff2(cy, ay));
            Span<double> t2 = ar.Product(ar.One(cx), ar.Diff2(ay, by));
            return ExpansionArithmetic.Sign(ar.Add(ar.Add(t0, t1), t2));
        }
        finally
        {
            ar.Release(mark);
        }
    }

    /// <summary>
    /// Orientation of the tetrahedron (a, b, c, d): +1 if d lies below the plane through a, b, c (the side from
    /// which a, b, c appear clockwise; equivalently a, b, c seen from above run counter-clockwise), -1 if above,
    /// 0 if coplanar. Exactly the sign of the determinant of the rows a-d, b-d, c-d.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is NaN, infinite, or outside the supported range.</exception>
    public static int Orient3D(
        double ax, double ay, double az,
        double bx, double by, double bz,
        double cx, double cy, double cz,
        double dx, double dy, double dz)
    {
        double adx = ax - dx, bdx = bx - dx, cdx = cx - dx;
        double ady = ay - dy, bdy = by - dy, cdy = cy - dy;
        double adz = az - dz, bdz = bz - dz, cdz = cz - dz;

        double bdxcdy = bdx * cdy, cdxbdy = cdx * bdy;
        double cdxady = cdx * ady, adxcdy = adx * cdy;
        double adxbdy = adx * bdy, bdxady = bdx * ady;

        double det = adz * (bdxcdy - cdxbdy) + bdz * (cdxady - adxcdy) + cdz * (adxbdy - bdxady);
        double permanent =
            (Math.Abs(bdxcdy) + Math.Abs(cdxbdy)) * Math.Abs(adz) +
            (Math.Abs(cdxady) + Math.Abs(adxcdy)) * Math.Abs(bdz) +
            (Math.Abs(adxbdy) + Math.Abs(bdxady)) * Math.Abs(cdz);
        double bound = Orient3DBound * permanent + Tiny;
        if (det > bound)
        {
            return 1;
        }
        if (-det > bound)
        {
            return -1;
        }
        return Orient3DExact(ax, ay, az, bx, by, bz, cx, cy, cz, dx, dy, dz);
    }

    private static int Orient3DExact(
        double ax, double ay, double az, double bx, double by, double bz,
        double cx, double cy, double cz, double dx, double dy, double dz)
    {
        CheckRange(ax, ay, az, bx, by, bz, cx, cy, cz, dx, dy, dz);
        Arena ar = Arena.Get();
        int mark = ar.Mark;
        try
        {
            return ExpansionArithmetic.Sign(ar.Det3(
                ar.Diff2(ax, dx), ar.Diff2(ay, dy), ar.Diff2(az, dz),
                ar.Diff2(bx, dx), ar.Diff2(by, dy), ar.Diff2(bz, dz),
                ar.Diff2(cx, dx), ar.Diff2(cy, dy), ar.Diff2(cz, dz)));
        }
        finally
        {
            ar.Release(mark);
        }
    }

    /// <summary>
    /// In-circle test: for a, b, c in counter-clockwise order, +1 if d lies strictly inside the circle through
    /// them, -1 if strictly outside, 0 if on it. If a, b, c are clockwise the signs are reversed; if they are
    /// collinear the result is the sign of the determinant, which has no circle meaning. Exactly the sign of
    /// the determinant of the rows (a-d, |a-d|²), (b-d, |b-d|²), (c-d, |c-d|²).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is NaN, infinite, or outside the supported range.</exception>
    public static int InCircle(
        double ax, double ay, double bx, double by,
        double cx, double cy, double dx, double dy)
    {
        double adx = ax - dx, bdx = bx - dx, cdx = cx - dx;
        double ady = ay - dy, bdy = by - dy, cdy = cy - dy;

        double bdxcdy = bdx * cdy, cdxbdy = cdx * bdy;
        double alift = adx * adx + ady * ady;
        double cdxady = cdx * ady, adxcdy = adx * cdy;
        double blift = bdx * bdx + bdy * bdy;
        double adxbdy = adx * bdy, bdxady = bdx * ady;
        double clift = cdx * cdx + cdy * cdy;

        double det = alift * (bdxcdy - cdxbdy) + blift * (cdxady - adxcdy) + clift * (adxbdy - bdxady);
        double permanent =
            (Math.Abs(bdxcdy) + Math.Abs(cdxbdy)) * alift +
            (Math.Abs(cdxady) + Math.Abs(adxcdy)) * blift +
            (Math.Abs(adxbdy) + Math.Abs(bdxady)) * clift;
        double bound = InCircleBound * permanent + Tiny;
        if (det > bound)
        {
            return 1;
        }
        if (-det > bound)
        {
            return -1;
        }
        return InCircleExact(ax, ay, bx, by, cx, cy, dx, dy);
    }

    private static int InCircleExact(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
    {
        CheckRange(ax, ay, bx, by, cx, cy, dx, dy);
        Arena ar = Arena.Get();
        int mark = ar.Mark;
        try
        {
            Span<double> adx = ar.Diff2(ax, dx), ady = ar.Diff2(ay, dy);
            Span<double> bdx = ar.Diff2(bx, dx), bdy = ar.Diff2(by, dy);
            Span<double> cdx = ar.Diff2(cx, dx), cdy = ar.Diff2(cy, dy);
            Span<double> alift = ar.Lift(adx, ady, default, false);
            Span<double> blift = ar.Lift(bdx, bdy, default, false);
            Span<double> clift = ar.Lift(cdx, cdy, default, false);
            // Rows (x, y, lift) of a, b, c; the same determinant the filter evaluates.
            return ExpansionArithmetic.Sign(ar.Det3(
                adx, ady, alift,
                bdx, bdy, blift,
                cdx, cdy, clift));
        }
        finally
        {
            ar.Release(mark);
        }
    }

    /// <summary>
    /// In-sphere test: for a, b, c, d with <see cref="Orient3D"/>(a, b, c, d) &gt; 0, +1 if e lies strictly inside the
    /// sphere through them, -1 if strictly outside, 0 if on it. If the orientation of a, b, c, d is negative the
    /// signs are reversed; if the four points are coplanar the result is the determinant's sign, with no sphere meaning.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is NaN, infinite, or outside the supported range.</exception>
    public static int InSphere(
        double ax, double ay, double az,
        double bx, double by, double bz,
        double cx, double cy, double cz,
        double dx, double dy, double dz,
        double ex, double ey, double ez)
    {
        double aex = ax - ex, bex = bx - ex, cex = cx - ex, dex = dx - ex;
        double aey = ay - ey, bey = by - ey, cey = cy - ey, dey = dy - ey;
        double aez = az - ez, bez = bz - ez, cez = cz - ez, dez = dz - ez;

        // 2x2 minors of the (x, y) columns for every pair of rows (rows i, j: xi*yj - xj*yi).
        double ab = aex * bey - bex * aey, abp = Math.Abs(aex * bey) + Math.Abs(bex * aey);
        double bc = bex * cey - cex * bey, bcp = Math.Abs(bex * cey) + Math.Abs(cex * bey);
        double cd = cex * dey - dex * cey, cdp = Math.Abs(cex * dey) + Math.Abs(dex * cey);
        double da = dex * aey - aex * dey, dap = Math.Abs(dex * aey) + Math.Abs(aex * dey);
        double ac = aex * cey - cex * aey, acp = Math.Abs(aex * cey) + Math.Abs(cex * aey);
        double bd = bex * dey - dex * bey, bdp = Math.Abs(bex * dey) + Math.Abs(dex * bey);

        // 3x3 minors of the (x, y, z) columns for each triple of rows, expanded along z.
        double abc = aez * bc - bez * ac + cez * ab;
        double bcd = bez * cd - cez * bd + dez * bc;
        double cda = cez * da + dez * ac + aez * cd;
        double dab = dez * ab + aez * bd + bez * da;
        double abcp = Math.Abs(aez) * bcp + Math.Abs(bez) * acp + Math.Abs(cez) * abp;
        double bcdp = Math.Abs(bez) * cdp + Math.Abs(cez) * bdp + Math.Abs(dez) * bcp;
        double cdap = Math.Abs(cez) * dap + Math.Abs(dez) * acp + Math.Abs(aez) * cdp;
        double dabp = Math.Abs(dez) * abp + Math.Abs(aez) * bdp + Math.Abs(bez) * dap;

        double alift = aex * aex + aey * aey + aez * aez;
        double blift = bex * bex + bey * bey + bez * bez;
        double clift = cex * cex + cey * cey + cez * cez;
        double dlift = dex * dex + dey * dey + dez * dez;

        double det = (dlift * abc - clift * dab) + (blift * cda - alift * bcd);
        double permanent = (dlift * abcp + clift * dabp) + (blift * cdap + alift * bcdp);
        double bound = InSphereBound * permanent + Tiny;
        if (det > bound)
        {
            return InSphereSign;
        }
        if (-det > bound)
        {
            return -InSphereSign;
        }
        return InSphereExact(ax, ay, az, bx, by, bz, cx, cy, cz, dx, dy, dz, ex, ey, ez);
    }

    // Sign relating the determinant evaluated above (Laplace expansion along the lift column, rows a, b, c, d
    // translated by e) to the documented convention (positive: e inside for positively oriented a, b, c, d).
    private const int InSphereSign = 1;

    private static int InSphereExact(
        double ax, double ay, double az, double bx, double by, double bz,
        double cx, double cy, double cz, double dx, double dy, double dz,
        double ex, double ey, double ez)
    {
        CheckRange(ax, ay, az, bx, by, bz, cx, cy, cz, dx, dy, dz, ex, ey, ez);
        Arena ar = Arena.Get();
        int mark = ar.Mark;
        try
        {
            Span<double> aex = ar.Diff2(ax, ex), aey = ar.Diff2(ay, ey), aez = ar.Diff2(az, ez);
            Span<double> bex = ar.Diff2(bx, ex), bey = ar.Diff2(by, ey), bez = ar.Diff2(bz, ez);
            Span<double> cex = ar.Diff2(cx, ex), cey = ar.Diff2(cy, ey), cez = ar.Diff2(cz, ez);
            Span<double> dex = ar.Diff2(dx, ex), dey = ar.Diff2(dy, ey), dez = ar.Diff2(dz, ez);
            Span<double> alift = ar.Lift(aex, aey, aez, true);
            Span<double> blift = ar.Lift(bex, bey, bez, true);
            Span<double> clift = ar.Lift(cex, cey, cez, true);
            Span<double> dlift = ar.Lift(dex, dey, dez, true);
            // Same cofactor expansion as the filter: det = dlift*|abc| - clift*|dab| + blift*|cda| - alift*|bcd|,
            // each |xyz| the 3x3 determinant of the rows in the order named (a cyclic or even permutation of them).
            Span<double> abc = ar.Det3(aex, aey, aez, bex, bey, bez, cex, cey, cez);
            Span<double> bcd = ar.Det3(bex, bey, bez, cex, cey, cez, dex, dey, dez);
            Span<double> cda = ar.Det3(cex, cey, cez, dex, dey, dez, aex, aey, aez);
            Span<double> dab = ar.Det3(dex, dey, dez, aex, aey, aez, bex, bey, bez);
            Span<double> p = ar.Sub(ar.Product(dlift, abc), ar.Product(clift, dab));
            Span<double> q = ar.Sub(ar.Product(blift, cda), ar.Product(alift, bcd));
            return InSphereSign * ExpansionArithmetic.Sign(ar.Add(p, q));
        }
        finally
        {
            ar.Release(mark);
        }
    }

    // Throws unless every value is finite and either zero or of magnitude in [2^-140, 2^200].
    private static void CheckRange(double v0, double v1, double v2, double v3, double v4, double v5)
    {
        Check(v0); Check(v1); Check(v2); Check(v3); Check(v4); Check(v5);
    }

    private static void CheckRange(double v0, double v1, double v2, double v3, double v4, double v5, double v6, double v7)
    {
        CheckRange(v0, v1, v2, v3, v4, v5); Check(v6); Check(v7);
    }

    private static void CheckRange(
        double v0, double v1, double v2, double v3, double v4, double v5,
        double v6, double v7, double v8, double v9, double v10, double v11)
    {
        CheckRange(v0, v1, v2, v3, v4, v5); CheckRange(v6, v7, v8, v9, v10, v11);
    }

    private static void CheckRange(
        double v0, double v1, double v2, double v3, double v4, double v5,
        double v6, double v7, double v8, double v9, double v10, double v11,
        double v12, double v13, double v14)
    {
        CheckRange(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11); Check(v12); Check(v13); Check(v14);
    }

    internal static void Check(double v)
    {
        double m = Math.Abs(v);
        if (!(m <= MaxMagnitude) || (m != 0.0 && m < MinMagnitude))
        {
            throw new ArgumentOutOfRangeException(
                nameof(v),
                v,
                double.IsFinite(v)
                    ? "Predicate coordinates must be zero or have magnitude in [2^-140, 2^200]; got " + v.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "."
                    : "Predicate coordinates must be finite; got " + v.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        }
    }
}
