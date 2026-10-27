using System;
using RecursiveAI.OneMath.Physics;
using Xunit;

namespace RecursiveAI.OneMath.Tests.PhysicsTests;

public class SymmetricEigenTests
{
    private readonly ITestOutputHelper _out;

    public SymmetricEigenTests(ITestOutputHelper output) => _out = output;

    private static long CaseCount()
    {
        string? v = Environment.GetEnvironmentVariable("ONEMATH_PHYSICS_CASES");
        return v != null && long.TryParse(v, out long n) ? n : 1_000_000;
    }

    // Bounds in units of the unit roundoff u (2^-24 for float, 2^-53 for double), repeated in docs/physics.md:
    // max |V L V^T - A| / max|A| <= ReconBound u, and max |V^T V - I| and |det V - 1| <= OrthoBound u.
    internal const double ReconBound = 16;
    internal const double OrthoBound = 10;

    /// <summary>One random symmetric matrix as six doubles (upper triangle a11 a12 a13 a22 a23 a33); the caller rounds to the working precision.</summary>
    internal static double[] Generate(TestRng r, int kind, int minExp, int maxExp)
    {
        var m = new double[6];
        int[,] idx = { { 0, 1, 2 }, { 1, 3, 4 }, { 2, 4, 5 } };
        switch (kind)
        {
            case 0:
                for (int i = 0; i < 6; i++)
                {
                    m[i] = r.Signed();
                }
                break;
            case 1:
                // Entries of very different magnitude.
                for (int i = 0; i < 6; i++)
                {
                    m[i] = r.Signed() * Math.ScaleB(1.0, r.Int(61) - 30);
                }
                break;
            case 2:
            {
                // Rank one or two.
                int rank = 1 + r.Int(2);
                for (int k = 0; k < rank; k++)
                {
                    double x = r.Signed(), y = r.Signed(), z = r.Signed();
                    m[0] += x * x;
                    m[1] += x * y;
                    m[2] += x * z;
                    m[3] += y * y;
                    m[4] += y * z;
                    m[5] += z * z;
                }
                break;
            }
            case 3:
            case 6:
            {
                double[,] q = RandomFrame(r);
                double l0 = r.Signed();
                double l1, l2;
                if (kind == 3)
                {
                    // Repeated and nearly repeated eigenvalues.
                    double delta = Math.ScaleB(r.Unit(), -r.Int(52));
                    switch (r.Int(4))
                    {
                        case 0:
                            l1 = l0;
                            l2 = l0;
                            break;
                        case 1:
                            l1 = l0;
                            l2 = r.Signed();
                            break;
                        case 2:
                            l1 = l0 * (1 + delta);
                            l2 = r.Signed();
                            break;
                        default:
                            l1 = l0 * (1 + delta);
                            l2 = l0 * (1 - delta);
                            break;
                    }
                }
                else
                {
                    // Near-singular: eigenvalues spread over many orders of magnitude.
                    l0 = 1;
                    l1 = Math.ScaleB(r.Unit(), -r.Int(30));
                    l2 = Math.ScaleB(r.Unit(), -r.Int(60));
                }
                double[] l = { l0, l1, l2 };
                for (int i = 0; i < 3; i++)
                {
                    for (int j = i; j < 3; j++)
                    {
                        double s = 0;
                        for (int k = 0; k < 3; k++)
                        {
                            s += l[k] * q[i, k] * q[j, k];
                        }
                        m[idx[i, j]] = s;
                    }
                }
                break;
            }
            case 4:
                // Diagonal with zeros and repeats.
                m[0] = r.Int(3) == 0 ? 0 : r.Signed();
                m[3] = r.Int(3) == 0 ? m[0] : r.Signed();
                m[5] = r.Int(3) == 0 ? m[3] : r.Signed();
                break;
            case 5:
            {
                // Uniform entries at a random binary scale from tiny to huge.
                int e = minExp + r.Int(maxExp - minExp + 1);
                for (int i = 0; i < 6; i++)
                {
                    m[i] = Math.ScaleB(r.Signed(), e);
                }
                break;
            }
            default:
                // Small integers: many exact ties and degenerate patterns.
                for (int i = 0; i < 6; i++)
                {
                    m[i] = r.Int(5) - 2;
                }
                break;
        }
        return m;
    }

    private static double[,] RandomFrame(TestRng r)
    {
        // Gram-Schmidt in plain double arithmetic: any nearly orthogonal frame will do for building test matrices.
        double[][] c = new double[3][];
        for (int i = 0; i < 3; i++)
        {
            double[] x = { r.Signed() + 0.01, r.Signed(), r.Signed() };
            for (int j = 0; j < i; j++)
            {
                double d = x[0] * c[j][0] + x[1] * c[j][1] + x[2] * c[j][2];
                for (int k = 0; k < 3; k++)
                {
                    x[k] -= d * c[j][k];
                }
            }
            double n = Math.Sqrt(x[0] * x[0] + x[1] * x[1] + x[2] * x[2]);
            for (int k = 0; k < 3; k++)
            {
                x[k] /= n;
            }
            c[i] = x;
        }
        var q = new double[3, 3];
        for (int i = 0; i < 3; i++)
        {
            for (int k = 0; k < 3; k++)
            {
                q[k, i] = c[i][k];
            }
        }
        return q;
    }

    private static void CheckConventions(double[] lambda, double[] v)
    {
        Assert.True(lambda[0] <= lambda[1] && lambda[1] <= lambda[2], "eigenvalues not ascending");
        for (int c = 0; c < 2; c++)
        {
            double best = Math.Abs(v[c]);
            int pick = 0;
            for (int row = 1; row < 3; row++)
            {
                if (Math.Abs(v[row * 3 + c]) > best)
                {
                    best = Math.Abs(v[row * 3 + c]);
                    pick = row;
                }
            }
            Assert.True(v[pick * 3 + c] > 0, "largest component of eigenvector " + c + " not positive");
        }
    }

    private static (double[] Lambda, double[] V) DecomposeD(double[] a)
    {
        var e = SymmetricEigenD.Decompose(new Matrix3x3D(a[0], a[1], a[2], a[1], a[3], a[4], a[2], a[4], a[5]));
        return (new[] { e.Values.X, e.Values.Y, e.Values.Z },
            new[] { e.Vectors.M11, e.Vectors.M12, e.Vectors.M13, e.Vectors.M21, e.Vectors.M22, e.Vectors.M23, e.Vectors.M31, e.Vectors.M32, e.Vectors.M33 });
    }

    private static (double[] Lambda, double[] V) DecomposeF(double[] a)
    {
        float[] f = { (float)a[0], (float)a[1], (float)a[2], (float)a[3], (float)a[4], (float)a[5] };
        var e = SymmetricEigenF.Decompose(new Matrix3x3F(f[0], f[1], f[2], f[1], f[3], f[4], f[2], f[4], f[5]));
        return (new double[] { e.Values.X, e.Values.Y, e.Values.Z },
            new double[] { e.Vectors.M11, e.Vectors.M12, e.Vectors.M13, e.Vectors.M21, e.Vectors.M22, e.Vectors.M23, e.Vectors.M31, e.Vectors.M32, e.Vectors.M33 });
    }

    private void Sweep(string label, Func<double[], (double[] Lambda, double[] V)> decompose, Func<double, double> round, double u, double denormMin, int minExp, int maxExp, ulong seed)
    {
        long cases = CaseCount();
        var rng = new TestRng(seed);
        double worstRecon = 0, worstOrtho = 0;
        long worstAt = -1;
        for (long n = 0; n < cases; n++)
        {
            int kind = (int)(n % 8);
            double[] m = Generate(rng, kind, minExp, maxExp);
            for (int i = 0; i < 6; i++)
            {
                m[i] = round(m[i]);
            }
            var (lam, v) = decompose(m);
            var (recon, ortho, det) = EigenOracle.Measure(m, lam, v, denormMin);
            double rr = recon / u;
            double oo = Math.Max(ortho, det) / u;
            if (!(rr <= worstRecon))
            {
                worstRecon = rr;
                worstAt = n;
            }
            worstOrtho = !(oo <= worstOrtho) ? oo : worstOrtho;
            Assert.True(rr <= ReconBound && oo <= OrthoBound, $"{label} case {n} (kind {kind}): reconstruction {rr} u, orthonormality {oo} u");
            CheckConventions(lam, v);
        }
        _out.WriteLine($"{label}: {cases} cases, worst reconstruction {worstRecon:G4} u (case {worstAt}), worst orthonormality/determinant {worstOrtho:G4} u");
    }

    [Fact]
    public void RandomSweepDouble() =>
        Sweep("double", DecomposeD, x => x, 1.0 / 9007199254740992.0, double.Epsilon, -1060, 1020, 0xD0B1E);

    [Fact]
    public void RandomSweepSingle() =>
        Sweep("float", DecomposeF, x => (float)x, 1.0 / 16777216.0, float.Epsilon, -140, 125, 0xF10A7);

    [Fact]
    public void DiagonalSortsAndKeepsAxes()
    {
        var e = SymmetricEigenD.Decompose(new Matrix3x3D(3, 0, 0, 0, 1, 0, 0, 0, 2));
        Assert.Equal(new Vector3D(1, 2, 3), e.Values);
        Assert.Equal(Matrix3x3D.FromColumns(new(0, 1, 0), new(0, 0, 1), new(1, 0, 0)), e.Vectors);
        var f = SymmetricEigenF.Decompose(new Matrix3x3F(3, 0, 0, 0, 1, 0, 0, 0, 2));
        Assert.Equal(new Vector3F(1, 2, 3), f.Values);
        Assert.Equal(Matrix3x3F.FromColumns(new(0, 1, 0), new(0, 0, 1), new(1, 0, 0)), f.Vectors);
    }

    [Fact]
    public void NegativeDiagonalHasPositiveLargestComponents()
    {
        var e = SymmetricEigenD.Decompose(new Matrix3x3D(-1, 0, 0, 0, -5, 0, 0, 0, 2));
        Assert.Equal(new Vector3D(-5, -1, 2), e.Values);
        Assert.Equal(Matrix3x3D.FromColumns(new(0, 1, 0), new(1, 0, 0), new(0, 0, -1)), e.Vectors);
    }

    [Fact]
    public void RepeatedEigenvaluesKeepIterationOrder()
    {
        var two = SymmetricEigenD.Decompose(new Matrix3x3D(2, 0, 0, 0, 2, 0, 0, 0, 5));
        Assert.Equal(new Vector3D(2, 2, 5), two.Values);
        Assert.Equal(Matrix3x3D.Identity, two.Vectors);
        var two2 = SymmetricEigenD.Decompose(new Matrix3x3D(5, 0, 0, 0, 2, 0, 0, 0, 2));
        Assert.Equal(new Vector3D(2, 2, 5), two2.Values);
        Assert.Equal(Matrix3x3D.FromColumns(new(0, 1, 0), new(0, 0, 1), new(1, 0, 0)), two2.Vectors);
        var three = SymmetricEigenD.Decompose(new Matrix3x3D(7, 0, 0, 0, 7, 0, 0, 0, 7));
        Assert.Equal(new Vector3D(7, 7, 7), three.Values);
        Assert.Equal(Matrix3x3D.Identity, three.Vectors);
        // A coupled block with a repeated eigenvalue: [[2,1,0],[1,2,0],[0,0,3]] has 1, 3, 3.
        var e = SymmetricEigenD.Decompose(new Matrix3x3D(2, 1, 0, 1, 2, 0, 0, 0, 3));
        Assert.Equal(1, e.Values.X, 1e-15);
        Assert.Equal(3, e.Values.Y, 1e-15);
        Assert.Equal(3, e.Values.Z, 1e-15);
    }

    [Fact]
    public void ZeroMatrixIsIdentityFrame()
    {
        var e = SymmetricEigenD.Decompose(default);
        Assert.Equal(default, e.Values);
        Assert.Equal(Matrix3x3D.Identity, e.Vectors);
    }

    [Fact]
    public void RankDeficientAllOnes()
    {
        var e = SymmetricEigenD.Decompose(new Matrix3x3D(1, 1, 1, 1, 1, 1, 1, 1, 1));
        Assert.Equal(0, e.Values.X, 1e-15);
        Assert.Equal(0, e.Values.Y, 1e-15);
        Assert.Equal(3, e.Values.Z, 1e-15);
        double s = 1 / Math.Sqrt(3);
        Assert.Equal(s, e.Vectors.M13, 1e-15);
        Assert.Equal(s, e.Vectors.M23, 1e-15);
        Assert.Equal(s, e.Vectors.M33, 1e-15);
    }

    [Fact]
    public void KnownTridiagonalEigenvalues()
    {
        var e = SymmetricEigenD.Decompose(new Matrix3x3D(2, -1, 0, -1, 2, -1, 0, -1, 2));
        double r2 = Math.Sqrt(2);
        Assert.Equal(2 - r2, e.Values.X, 1e-15);
        Assert.Equal(2, e.Values.Y, 1e-15);
        Assert.Equal(2 + r2, e.Values.Z, 1e-15);
    }

    [Fact]
    public void PowerOfTwoScalingCommutesBitForBit()
    {
        var rng = new TestRng(0x5CA1E);
        for (int n = 0; n < 2000; n++)
        {
            double[] m = Generate(rng, n % 8, 0, 0);
            var a = new Matrix3x3D(m[0], m[1], m[2], m[1], m[3], m[4], m[2], m[4], m[5]);
            var base0 = SymmetricEigenD.Decompose(a);
            foreach (int k in new[] { -900, -300, 7, 600, 900 })
            {
                double s = Math.ScaleB(1.0, k);
                var scaled = SymmetricEigenD.Decompose(a * s);
                Assert.Equal(base0.Vectors, scaled.Vectors);
                Assert.Equal(base0.Values * s, scaled.Values);
            }
        }
    }

    [Fact]
    public void ExtremeScalesStayAccurate()
    {
        double[][] cases =
        {
            new[] { 1e300, 2e299, -3e299, 5e300, 1e299, -2e300 },
            new[] { 1e-300, 2e-301, -3e-301, 5e-300, 1e-301, -2e-300 },
            new[] { double.Epsilon * 3, double.Epsilon, 0, double.Epsilon * 5, double.Epsilon * 2, double.Epsilon * 4 },
            new[] { double.MaxValue / 8, double.MaxValue / 16, 0, double.MaxValue / 8, 0, double.MaxValue / 8 },
        };
        foreach (double[] m in cases)
        {
            var (lam, v) = DecomposeD(m);
            var (recon, ortho, det) = EigenOracle.Measure(m, lam, v, double.Epsilon);
            Assert.True(recon <= ReconBound / 9007199254740992.0 && ortho <= OrthoBound / 9007199254740992.0 && det <= OrthoBound / 9007199254740992.0);
        }
        // Float subnormals.
        var fs = SymmetricEigenF.Decompose(new Matrix3x3F(3e-45f, 1e-45f, 0, 1e-45f, 5e-45f, 1e-45f, 0, 1e-45f, 2e-45f));
        Assert.True(float.IsFinite(fs.Values.X) && fs.Values.X <= fs.Values.Y && fs.Values.Y <= fs.Values.Z);
    }

    [Fact]
    public void HostileInputIsRejectedWithTypedErrors()
    {
        var nan = Assert.Throws<ArgumentOutOfRangeException>(() => SymmetricEigenD.Decompose(new Matrix3x3D(1, 0, double.NaN, 0, 1, 0, double.NaN, 0, 1)));
        Assert.Contains("matrix.M13", nan.Message);
        Assert.Contains("NaN", nan.Message);
        var inf = Assert.Throws<ArgumentOutOfRangeException>(() => SymmetricEigenF.Decompose(new Matrix3x3F(1, 0, 0, 0, float.PositiveInfinity, 0, 0, 0, 1)));
        Assert.Contains("matrix.M22", inf.Message);
        var asym = Assert.Throws<ArgumentException>(() => SymmetricEigenD.Decompose(new Matrix3x3D(1, 2, 0, 3, 1, 0, 0, 0, 1)));
        Assert.Contains("not symmetric", asym.Message);
        Assert.Equal("matrix", asym.ParamName);
    }

    [Fact]
    public void ResultIsDeterministicAcrossCalls()
    {
        var a = new Matrix3x3D(4, 1, -2, 1, 2, 0, -2, 0, 3);
        Assert.Equal(SymmetricEigenD.Decompose(a), SymmetricEigenD.Decompose(a));
    }
}
