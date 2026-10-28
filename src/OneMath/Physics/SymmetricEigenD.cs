using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace RecursiveAI.OneMath.Physics;

/// <summary>
/// The result of a symmetric 3x3 eigen decomposition: <c>A = V * diag(Values) * V^T</c>, where the columns of
/// <see cref="Vectors"/> are unit eigenvectors forming a right-handed orthonormal frame.
/// </summary>
public readonly struct EigenDecompositionD : IEquatable<EigenDecompositionD>
{
    /// <summary>Creates a result from its parts.</summary>
    [JsonConstructor]
    public EigenDecompositionD(Vector3D values, Matrix3x3D vectors)
    {
        Values = values;
        Vectors = vectors;
    }

    /// <summary>The eigenvalues in ascending order (X smallest). Equal eigenvalues keep the order in which the iteration left them.</summary>
    public Vector3D Values { get; }

    /// <summary>The eigenvectors as columns, in the order of <see cref="Values"/>; the determinant is +1.</summary>
    public Matrix3x3D Vectors { get; }

    /// <summary>Bitwise-style equality of both parts.</summary>
    public bool Equals(EigenDecompositionD other) => Values.Equals(other.Values) && Vectors.Equals(other.Vectors);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is EigenDecompositionD e && Equals(e);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Values.GetHashCode(), Vectors.GetHashCode());

    /// <summary>Equality of both parts.</summary>
    public static bool operator ==(EigenDecompositionD a, EigenDecompositionD b) => a.Equals(b);

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(EigenDecompositionD a, EigenDecompositionD b) => !a.Equals(b);
}

/// <summary>
/// Eigen decomposition of a symmetric 3x3 matrix by cyclic Jacobi rotations (Golub and Van Loan, Matrix Computations,
/// section 8.5, with Rutishauser's stable form of the rotation). Only IEEE-exact + - * / and square root are used,
/// in the order documented in docs/physics.md, so the bits are the same on every platform.
/// </summary>
public static class SymmetricEigenD
{
    // At most this many sweeps over the three pairs (0,1), (0,2), (1,2). The off-diagonal norm shrinks quadratically,
    // so a matrix of this size is converged after about six sweeps; the cap only bounds the work.
    private const int MaxSweeps = 16;

    private static readonly double s_bigRatio = Pow2(60);

    /// <summary>
    /// Decomposes the symmetric matrix <paramref name="matrix"/>. Only the upper triangle (M11, M12, M13, M22, M23, M33) is read;
    /// the mirrored entries must agree with it to within 64 machine epsilons of the largest entry.
    /// The matrix is scaled by a power of two so its largest entry lies in [1, 2) and the eigenvalues are scaled back, so
    /// the result is correct for any finite input whose eigenvalues do not overflow (they would become infinite).
    /// </summary>
    /// <remarks>
    /// Errors are relative to the largest entry of the matrix: <c>|V diag(L) V^T - A|</c> and <c>|V^T V - I|</c> stay within the
    /// bounds stated in docs/physics.md. Eigenvalues come out in ascending order; equal values keep the iteration's order.
    /// Each of the first two eigenvectors has its largest-magnitude component positive (lowest index on a tie); the third is
    /// signed so the frame is right-handed. A diagonal matrix returns its sorted diagonal and the corresponding signed axes.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">An entry is NaN or infinite.</exception>
    /// <exception cref="ArgumentException">The matrix is not symmetric.</exception>
    public static EigenDecompositionD Decompose(Matrix3x3D matrix)
    {
        GuardsD.Symmetric(matrix, nameof(matrix), nameof(matrix));

        double max = Math.Abs(matrix.M11);
        max = Max(max, Math.Abs(matrix.M12));
        max = Max(max, Math.Abs(matrix.M13));
        max = Max(max, Math.Abs(matrix.M22));
        max = Max(max, Math.Abs(matrix.M23));
        max = Max(max, Math.Abs(matrix.M33));
        if (max == 0)
        {
            return new EigenDecompositionD(default, Matrix3x3D.Identity);
        }

        // Scale by 2^-e in two exact steps so the largest entry is in [1, 2) and nothing below can overflow.
        int e = Exponent(max);
        int e1 = e >> 1;
        int e2 = e - e1;
        double down1 = Pow2(-e1);
        double down2 = Pow2(-e2);

        Span<double> a = stackalloc double[9];
        Span<double> v = stackalloc double[9];
        a[0] = matrix.M11 * down1 * down2;
        a[1] = matrix.M12 * down1 * down2;
        a[2] = matrix.M13 * down1 * down2;
        a[4] = matrix.M22 * down1 * down2;
        a[5] = matrix.M23 * down1 * down2;
        a[8] = matrix.M33 * down1 * down2;
        a[3] = a[1];
        a[6] = a[2];
        a[7] = a[5];
        v[0] = 1;
        v[4] = 1;
        v[8] = 1;

        for (int sweep = 0; sweep < MaxSweeps; sweep++)
        {
            bool rotated = Rotate(a, v, 0, 1);
            rotated |= Rotate(a, v, 0, 2);
            rotated |= Rotate(a, v, 1, 2);
            if (!rotated)
            {
                break;
            }
        }

        // Undo the scaling (again in two steps, both in the same direction, so no step overflows needlessly).
        double up1 = Pow2(e1);
        double up2 = Pow2(e2);
        Span<double> lambda = stackalloc double[3];
        lambda[0] = a[0] * up1 * up2;
        lambda[1] = a[4] * up1 * up2;
        lambda[2] = a[8] * up1 * up2;

        // Stable insertion sort of the three indices by eigenvalue, ascending; equal values keep their positions.
        Span<int> order = stackalloc int[3];
        order[0] = 0;
        order[1] = 1;
        order[2] = 2;
        for (int i = 1; i < 3; i++)
        {
            int key = order[i];
            int j = i - 1;
            while (j >= 0 && lambda[order[j]] > lambda[key])
            {
                order[j + 1] = order[j];
                j--;
            }
            order[j + 1] = key;
        }

        Vector3D c0 = Column(v, order[0]);
        Vector3D c1 = Column(v, order[1]);
        Vector3D c2 = Column(v, order[2]);
        c0 = LargestComponentPositive(c0);
        c1 = LargestComponentPositive(c1);
        // Right-handed: the third vector's sign is whatever makes (c0 x c1) . c2 positive.
        if (Vector3D.Dot(Vector3D.Cross(c0, c1), c2) < 0)
        {
            c2 = Negate(c2);
        }

        return new EigenDecompositionD(
            new Vector3D(lambda[order[0]], lambda[order[1]], lambda[order[2]]),
            Matrix3x3D.FromColumns(c0, c1, c2));
    }

    /// <summary>
    /// One Jacobi rotation in the (p, q) plane, p &lt; q. Returns false when the off-diagonal entry is negligible next to both
    /// diagonal entries (it is then set to zero without rotating).
    /// </summary>
    private static bool Rotate(Span<double> a, Span<double> v, int p, int q)
    {
        int pq = p * 3 + q;
        double apq = a[pq];
        int pp = p * 4;
        int qq = q * 4;
        double app = a[pp];
        double aqq = a[qq];

        // Negligible: adding it to either diagonal entry changes neither (the textbook "small enough to drop" test).
        double g = Math.Abs(apq) * 2;
        if (apq == 0 || (Math.Abs(app) + g == Math.Abs(app) && Math.Abs(aqq) + g == Math.Abs(aqq)))
        {
            a[pq] = 0;
            a[q * 3 + p] = 0;
            return false;
        }

        // Rutishauser: t = tan(angle) is the smaller root of t^2 + 2*theta*t - 1 = 0, theta = (aqq - app) / (2 apq).
        double d = aqq - app;
        double t;
        if (Math.Abs(d) > s_bigRatio * g)
        {
            // |theta| is huge; the root is 1 / (2 theta) = apq / d, and squaring theta could overflow.
            t = apq / d;
        }
        else
        {
            double theta = d / (apq * 2);
            double root = Scalar.Sqrt(theta * theta + 1);
            t = 1 / (Math.Abs(theta) + root);
            if (theta < 0)
            {
                t = -t;
            }
        }
        double c = 1 / Scalar.Sqrt(t * t + 1);
        double s = t * c;
        double tau = s / (1 + c);

        // The diagonal changes by +-t*apq and the pivot becomes exactly zero.
        double shift = t * apq;
        a[pp] = app - shift;
        a[qq] = aqq + shift;
        a[pq] = 0;
        a[q * 3 + p] = 0;

        int r = 3 - p - q;
        double arp = a[r * 3 + p];
        double arq = a[r * 3 + q];
        double nrp = arp - s * (arq + tau * arp);
        double nrq = arq + s * (arp - tau * arq);
        a[r * 3 + p] = nrp;
        a[p * 3 + r] = nrp;
        a[r * 3 + q] = nrq;
        a[q * 3 + r] = nrq;

        for (int k = 0; k < 3; k++)
        {
            double vkp = v[k * 3 + p];
            double vkq = v[k * 3 + q];
            v[k * 3 + p] = vkp - s * (vkq + tau * vkp);
            v[k * 3 + q] = vkq + s * (vkp - tau * vkq);
        }
        return true;
    }

    private static Vector3D Column(ReadOnlySpan<double> v, int c) => new(v[c], v[3 + c], v[6 + c]);

    // 0 - x rather than -x so a zero component stays +0.
    private static Vector3D Negate(Vector3D x) => new(0 - x.X, 0 - x.Y, 0 - x.Z);

    private static Vector3D LargestComponentPositive(Vector3D x)
    {
        double ax = Math.Abs(x.X);
        double ay = Math.Abs(x.Y);
        double az = Math.Abs(x.Z);
        double pick = x.X;
        double best = ax;
        if (ay > best)
        {
            best = ay;
            pick = x.Y;
        }
        if (az > best)
        {
            pick = x.Z;
        }
        return pick < 0 ? Negate(x) : x;
    }

    private static double Max(double a, double b) => a > b ? a : b;

    /// <summary>The binary exponent of a positive finite value (2^e &lt;= m &lt; 2^(e+1)), subnormals included.</summary>
    private static int Exponent(double m)
    {
        int adjust = 0;
        if (m < 2.2250738585072014E-308)
        {
            m *= Pow2(100);
            adjust = 100;
        }
        return (int)((BitConverter.DoubleToInt64Bits(m) >> 52) & 0x7FF) - 1023 - adjust;
    }

    /// <summary>Exactly 2^e for e in the normal range of the type.</summary>
    internal static double Pow2(int e) => BitConverter.Int64BitsToDouble((long)(e + 1023) << 52);
}
