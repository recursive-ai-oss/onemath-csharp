using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using RecursiveAI.OneMath.HashMatrix;
using RecursiveAI.OneMath.Predicates;
using Xunit;

namespace RecursiveAI.OneMath.Tests;

public class PredicateTests
{
    // Cases per predicate; the light worker job sets ONEMATH_PREDICATE_CASES=10000000.
    private static long CaseCount()
    {
        string? v = Environment.GetEnvironmentVariable("ONEMATH_PREDICATE_CASES");
        return v != null && long.TryParse(v, out long n) ? n : 100_000;
    }

    private static int Eval(PredicateKind kind, ReadOnlySpan<double> p) => kind switch
    {
        PredicateKind.Orient2D => ExactPredicates.Orient2D(p[0], p[1], p[2], p[3], p[4], p[5]),
        PredicateKind.Orient3D => ExactPredicates.Orient3D(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11]),
        PredicateKind.InCircle => ExactPredicates.InCircle(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]),
        _ => ExactPredicates.InSphere(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12], p[13], p[14]),
    };

    private static int Oracle(PredicateKind kind, ReadOnlySpan<double> p) => kind switch
    {
        PredicateKind.Orient2D => PredicateOracle.Orient2D(p),
        PredicateKind.Orient3D => PredicateOracle.Orient3D(p),
        PredicateKind.InCircle => PredicateOracle.InCircle(p),
        _ => PredicateOracle.InSphere(p),
    };

    [Theory]
    [InlineData(PredicateKind.Orient2D, 1)]
    [InlineData(PredicateKind.Orient3D, 2)]
    [InlineData(PredicateKind.InCircle, 3)]
    [InlineData(PredicateKind.InSphere, 4)]
    public void Matches_exact_rational_evaluation(PredicateKind kind, int seed)
    {
        long total = CaseCount();
        const int Chunk = 5000;
        long chunks = (total + Chunk - 1) / Chunk;
        long mismatches = 0, zeros = 0, pos = 0, neg = 0;
        string? first = null;
        Parallel.For(0, chunks, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8) }, c =>
        {
            Span<double> p = stackalloc double[PredicateInputs.Length(kind)];
            long z = 0, ps = 0, ng = 0;
            long end = Math.Min(total, (c + 1) * Chunk);
            for (long i = c * Chunk; i < end; i++)
            {
                PredicateInputs.Fill(kind, (ulong)seed * 1000 + 7, i, p);
                int got = Eval(kind, p);
                int want = Oracle(kind, p);
                if (got != want)
                {
                    if (Interlocked.Increment(ref mismatches) == 1)
                    {
                        first = $"case {i}: got {got}, want {want}, inputs {string.Join(",", Array.ConvertAll(p.ToArray(), x => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))}";
                    }
                }
                if (want == 0) { z++; } else if (want > 0) { ps++; } else { ng++; }
            }
            Interlocked.Add(ref zeros, z); Interlocked.Add(ref pos, ps); Interlocked.Add(ref neg, ng);
        });
        Assert.True(mismatches == 0, $"{mismatches} mismatches; first: {first}");
        // The inputs must actually reach the degenerate and both non-degenerate outcomes.
        Assert.True(zeros > total / 50, $"too few exactly-degenerate cases: {zeros}/{total}");
        Assert.True(pos > total / 20 && neg > total / 20, $"signs unbalanced: +{pos} -{neg}");
    }

    [Fact]
    public void Sign_conventions_on_simple_cases()
    {
        Assert.Equal(1, ExactPredicates.Orient2D(0, 0, 1, 0, 0, 1));   // counter-clockwise
        Assert.Equal(-1, ExactPredicates.Orient2D(0, 0, 0, 1, 1, 0));
        Assert.Equal(0, ExactPredicates.Orient2D(0, 0, 1, 1, 2, 2));
        // d below the plane of a, b, c (counter-clockwise seen from above): positive.
        Assert.Equal(1, ExactPredicates.Orient3D(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, -1));
        Assert.Equal(-1, ExactPredicates.Orient3D(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1));
        Assert.Equal(0, ExactPredicates.Orient3D(0, 0, 0, 1, 0, 0, 0, 1, 0, 5, 5, 0));
        // a, b, c counter-clockwise on the unit circle.
        Assert.Equal(1, ExactPredicates.InCircle(1, 0, 0, 1, -1, 0, 0, 0));    // inside
        Assert.Equal(-1, ExactPredicates.InCircle(1, 0, 0, 1, -1, 0, 3, 0));   // outside
        Assert.Equal(0, ExactPredicates.InCircle(1, 0, 0, 1, -1, 0, 0, -1));   // on
        // Positively oriented tetrahedron inscribed in the unit sphere.
        double[] t = { 1, 0, 0, -1, 0, 0, 0, 1, 0, 0, 0, 1 };
        // choose orientation so that Orient3D(a, b, c, d) > 0
        if (ExactPredicates.Orient3D(t[0], t[1], t[2], t[3], t[4], t[5], t[6], t[7], t[8], t[9], t[10], t[11]) < 0)
        {
            (t[0], t[3]) = (t[3], t[0]);
        }
        Assert.Equal(1, ExactPredicates.Orient3D(t[0], t[1], t[2], t[3], t[4], t[5], t[6], t[7], t[8], t[9], t[10], t[11]));
        Assert.Equal(1, ExactPredicates.InSphere(t[0], t[1], t[2], t[3], t[4], t[5], t[6], t[7], t[8], t[9], t[10], t[11], 0, 0, 0));
        Assert.Equal(-1, ExactPredicates.InSphere(t[0], t[1], t[2], t[3], t[4], t[5], t[6], t[7], t[8], t[9], t[10], t[11], 3, 0, 0));
        Assert.Equal(0, ExactPredicates.InSphere(t[0], t[1], t[2], t[3], t[4], t[5], t[6], t[7], t[8], t[9], t[10], t[11], 0, -1, 0));
    }

    [Fact]
    public void Classic_filter_breakers_are_exact()
    {
        // Points 1 ulp off a long line: the floating-point determinant is wrong or zero; the exact sign is not.
        double a = 0.5, b = 12.0, c = 24.0;
        for (int i = 0; i < 2000; i++)
        {
            double cx = Math.BitIncrement(c), cy = c;
            double ax = a, ay = a;
            double bx = b, by = b;
            var p = new[] { ax, ay, bx, by, cx, cy };
            Assert.Equal(PredicateOracle.Orient2D(p), ExactPredicates.Orient2D(ax, ay, bx, by, cx, cy));
            a = Math.BitIncrement(a); b += 1.0 / 1024; c += 1.0 / 4096;
        }
        // Large offset plus tiny spread.
        Assert.Equal(1, ExactPredicates.Orient2D(1e15, 1e15, 1e15 + 2, 1e15, 1e15, 1e15 + 1));
    }

    [Fact]
    public void Rejects_non_finite_and_out_of_range_when_exact_path_is_needed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.Orient2D(double.NaN, 0, 1, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.Orient2D(double.PositiveInfinity, 0, 1, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.Orient3D(0, 0, 0, 1, 0, 0, 0, double.NaN, 0, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.InCircle(0, 0, 1, 0, 0, 1, double.NegativeInfinity, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.InSphere(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, double.NaN, 0, 0));
        // Collinear points with a coordinate below 2^-140 need the exact path and are outside the range.
        Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.Orient2D(1e-300, 0, 2e-300, 0, 3e-300, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.Orient2D(1e100, 1e100, 2e100, 2e100, 3e100, 3e100));
        // Zero coordinates are fine.
        Assert.Equal(0, ExactPredicates.Orient2D(0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void Edge_of_supported_range_is_exact()
    {
        double lo = BitConverter.Int64BitsToDouble(((long)(1023 - 140)) << 52); // 2^-140
        double hi = BitConverter.Int64BitsToDouble(((long)(1023 + 200)) << 52); // 2^200
        double[] vals = { lo, lo * 1.5, hi, hi * 0.75, -lo, -hi, 0 };
        var rng = new Random(12345);
        for (int i = 0; i < 3000; i++)
        {
            var p = new double[15];
            for (int k = 0; k < 15; k++) { p[k] = vals[rng.Next(vals.Length)]; }
            Assert.Equal(PredicateOracle.Orient2D(p), ExactPredicates.Orient2D(p[0], p[1], p[2], p[3], p[4], p[5]));
            Assert.Equal(PredicateOracle.Orient3D(p), ExactPredicates.Orient3D(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11]));
            Assert.Equal(PredicateOracle.InCircle(p), ExactPredicates.InCircle(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]));
            Assert.Equal(PredicateOracle.InSphere(p), ExactPredicates.InSphere(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12], p[13], p[14]));
        }
    }

    // ---- expansions ----

    private static void AssertExpansionValid(ReadOnlySpan<double> e)
    {
        for (int i = 0; i < e.Length; i++)
        {
            Assert.NotEqual(0.0, e[i]);
        }
        for (int i = 0; i + 1 < e.Length; i++)
        {
            BigInteger lo = BigInteger.Abs(PredicateOracle.Big(e[i])), hi = BigInteger.Abs(PredicateOracle.Big(e[i + 1]));
            Assert.True(lo.GetBitLength() <= (long)TrailingZeros(hi), "components overlap or are out of order");
        }
    }

    private static int TrailingZeros(BigInteger v)
    {
        int n = 0;
        while ((v & 1) == 0) { v >>= 1; n++; }
        return n;
    }

    private static double Rand(Random r)
    {
        double m = r.NextDouble() * 2 - 1;
        int e = r.Next(-40, 41);
        double s = 1;
        for (int i = 0; i < Math.Abs(e); i++) { s = e > 0 ? s * 2 : s * 0.5; }
        return m * s;
    }

    private static double[] RandomExpansion(Random r, int terms)
    {
        var acc = Array.Empty<double>();
        for (int i = 0; i < terms; i++)
        {
            double a = Rand(r), b = Rand(r);
            ExpansionArithmetic.TwoProduct(a, b, out double x, out double y);
            var t = new double[acc.Length + 2];
            var piece = new[] { y, x };
            int n = ExpansionArithmetic.Sum(acc, piece[0] == 0 ? piece[1..] : piece, t);
            acc = t[..n];
        }
        return acc;
    }

    [Fact]
    public void Sum_scale_product_compare_are_exact_and_valid()
    {
        var r = new Random(99);
        for (int it = 0; it < 4000; it++)
        {
            var e = RandomExpansion(r, r.Next(0, 4));
            var f = RandomExpansion(r, r.Next(0, 4));
            double b = Rand(r);
            AssertExpansionValid(e);
            AssertExpansionValid(f);
            BigInteger be = PredicateOracle.Big(e), bf = PredicateOracle.Big(f);

            var h = new double[e.Length + f.Length];
            int n = ExpansionArithmetic.Sum(e, f, h);
            AssertExpansionValid(h.AsSpan(0, n));
            Assert.Equal(be + bf, PredicateOracle.Big(h.AsSpan(0, n)));

            n = ExpansionArithmetic.Difference(e, f, h);
            AssertExpansionValid(h.AsSpan(0, n));
            Assert.Equal(be - bf, PredicateOracle.Big(h.AsSpan(0, n)));
            Assert.Equal((be - bf).Sign, ExpansionArithmetic.Sign(h.AsSpan(0, n)));
            Assert.Equal((be - bf).Sign, ExpansionArithmetic.Compare(e, f));

            var s = new double[2 * e.Length];
            n = ExpansionArithmetic.Scale(e, b, s);
            AssertExpansionValid(s.AsSpan(0, n));
            Assert.Equal(be * PredicateOracle.Big(b), PredicateOracle.Big(s.AsSpan(0, n)) << 1074);
        }
    }

    [Fact]
    public void Product_is_exact()
    {
        var r = new Random(5);
        for (int it = 0; it < 1500; it++)
        {
            var e = RandomExpansion(r, r.Next(0, 3));
            var f = RandomExpansion(r, r.Next(0, 3));
            var h = new double[2 * e.Length * f.Length];
            int n = ExpansionArithmetic.Product(e, f, h);
            AssertExpansionValid(h.AsSpan(0, n));
            // Big() scales by 2^1074 once per factor, so the product carries 2^1074 more.
            Assert.Equal(PredicateOracle.Big(e) * PredicateOracle.Big(f), PredicateOracle.Big(h.AsSpan(0, n)) << 1074);
        }
    }

    [Fact]
    public void Two_sum_diff_product_are_exact()
    {
        var r = new Random(7);
        for (int i = 0; i < 20000; i++)
        {
            double a = Rand(r), b = Rand(r);
            ExpansionArithmetic.TwoSum(a, b, out double x, out double y);
            Assert.Equal(PredicateOracle.Big(a) + PredicateOracle.Big(b), PredicateOracle.Big(x) + PredicateOracle.Big(y));
            ExpansionArithmetic.TwoDiff(a, b, out x, out y);
            Assert.Equal(PredicateOracle.Big(a) - PredicateOracle.Big(b), PredicateOracle.Big(x) + PredicateOracle.Big(y));
            ExpansionArithmetic.TwoProduct(a, b, out x, out y);
            Assert.Equal(PredicateOracle.Big(a) * PredicateOracle.Big(b), (PredicateOracle.Big(x) + PredicateOracle.Big(y)) << 1074);
        }
    }

    // ---- segments ----

    private static SegmentRelation SegOracle(double[] p)
    {
        // Independent: exact orientations from BigInteger, interval logic on exact rational parameters.
        var a = (p[0], p[1]); var b = (p[2], p[3]); var c = (p[4], p[5]); var d = (p[6], p[7]);
        int O(double ax, double ay, double bx, double by, double cx, double cy) =>
            PredicateOracle.Orient2D(new[] { ax, ay, bx, by, cx, cy });
        bool OnSeg((double x, double y) q, (double x, double y) s, (double x, double y) t) =>
            O(s.x, s.y, t.x, t.y, q.x, q.y) == 0 &&
            q.x >= Math.Min(s.x, t.x) && q.x <= Math.Max(s.x, t.x) && q.y >= Math.Min(s.y, t.y) && q.y <= Math.Max(s.y, t.y);
        bool abPt = a == b, cdPt = c == d;
        if (abPt && cdPt) { return a == c ? SegmentRelation.Touching : SegmentRelation.Disjoint; }
        if (abPt) { return OnSeg(a, c, d) ? SegmentRelation.Touching : SegmentRelation.Disjoint; }
        if (cdPt) { return OnSeg(c, a, b) ? SegmentRelation.Touching : SegmentRelation.Disjoint; }
        int o1 = O(a.Item1, a.Item2, b.Item1, b.Item2, c.Item1, c.Item2), o2 = O(a.Item1, a.Item2, b.Item1, b.Item2, d.Item1, d.Item2);
        int o3 = O(c.Item1, c.Item2, d.Item1, d.Item2, a.Item1, a.Item2), o4 = O(c.Item1, c.Item2, d.Item1, d.Item2, b.Item1, b.Item2);
        if (o1 == 0 && o2 == 0)
        {
            // Collinear: count shared points among the four endpoints, and overlap via containment.
            bool aIn = OnSeg(a, c, d), bIn = OnSeg(b, c, d), cIn = OnSeg(c, a, b), dIn = OnSeg(d, a, b);
            if (!(aIn || bIn || cIn || dIn)) { return SegmentRelation.Disjoint; }
            // A single shared point happens only when the segments meet end to end.
            bool endToEnd = (a == c || a == d || b == c || b == d) && !(aIn && bIn) && !(cIn && dIn);
            if (endToEnd)
            {
                // Shared endpoint; overlap iff another endpoint lies strictly inside the other segment.
                var shared = (a == c || a == d) ? a : b;
                bool overlap = false;
                foreach (var q in new[] { a, b }) { if (q != shared && OnSeg(q, c, d)) { overlap = true; } }
                foreach (var q in new[] { c, d }) { if (q != shared && OnSeg(q, a, b)) { overlap = true; } }
                return overlap ? SegmentRelation.CollinearOverlap : SegmentRelation.Touching;
            }
            return SegmentRelation.CollinearOverlap;
        }
        if (o1 * o2 < 0 && o3 * o4 < 0) { return SegmentRelation.Proper; }
        if (OnSeg(c, a, b) || OnSeg(d, a, b) || OnSeg(a, c, d) || OnSeg(b, c, d)) { return SegmentRelation.Touching; }
        return SegmentRelation.Disjoint;
    }

    [Fact]
    public void Segment_classification_matches_oracle()
    {
        Span<double> q = stackalloc double[8];
        var counts = new int[4];
        for (long i = 0; i < 60000; i++)
        {
            PredicateInputs.Fill(PredicateKind.InCircle, 77, i, q);
            var p = q.ToArray();
            var got = SegmentPredicates.Classify(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]);
            Assert.Equal(SegOracle(p), got);
            // Symmetry under swapping segments and endpoints.
            Assert.Equal(got, SegmentPredicates.Classify(p[4], p[5], p[6], p[7], p[0], p[1], p[2], p[3]));
            Assert.Equal(got, SegmentPredicates.Classify(p[2], p[3], p[0], p[1], p[6], p[7], p[4], p[5]));
            counts[(int)got]++;
        }
        for (int k = 0; k < 4; k++)
        {
            Assert.True(counts[k] > 0, $"relation {(SegmentRelation)k} never produced");
        }
    }

    [Fact]
    public void Segment_known_cases()
    {
        Assert.Equal(SegmentRelation.Proper, SegmentPredicates.Classify(0, 0, 2, 2, 0, 2, 2, 0));
        Assert.Equal(SegmentRelation.Touching, SegmentPredicates.Classify(0, 0, 2, 2, 1, 1, 3, 0));   // T-junction
        Assert.Equal(SegmentRelation.Touching, SegmentPredicates.Classify(0, 0, 1, 1, 1, 1, 2, 2));   // end to end
        Assert.Equal(SegmentRelation.CollinearOverlap, SegmentPredicates.Classify(0, 0, 2, 2, 1, 1, 3, 3));
        Assert.Equal(SegmentRelation.Disjoint, SegmentPredicates.Classify(0, 0, 1, 1, 2, 2, 3, 3));
        Assert.Equal(SegmentRelation.Touching, SegmentPredicates.Classify(1, 1, 1, 1, 0, 0, 2, 2));   // point on segment
        Assert.Equal(SegmentRelation.Disjoint, SegmentPredicates.Classify(1, 1, 1, 1, 1, 2, 5, 5));
        Assert.Equal(SegmentRelation.CollinearOverlap, SegmentPredicates.Classify(0, 0, 3, 0, 0, 0, 3, 0)); // identical
        Assert.Throws<ArgumentOutOfRangeException>(() => SegmentPredicates.Classify(0, 0, 1, 1, double.NaN, 0, 1, 0));
    }

    [Fact]
    public void Float32_inputs_promote_exactly()
    {
        float a = 0.1f, b = 0.7f, c = 1.3f;
        Assert.Equal(
            PredicateOracle.Orient2D(new double[] { a, a, b, b, c, c }),
            ExactPredicates.Orient2D(a, a, b, b, c, c));
    }
}
