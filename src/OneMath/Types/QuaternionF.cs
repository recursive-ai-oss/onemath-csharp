using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RecursiveAI.OneMath;

/// <summary>
/// A rotation quaternion of <see cref="float"/> components, <c>X*i + Y*j + Z*k + W</c>, with the Hamilton product.
/// Rotation methods assume a unit quaternion; they do not renormalize. Constructors that take an angle are not
/// provided here because they need sine and cosine: build the half-angle sine and cosine with this library's
/// functions and pass them to <see cref="FromAxisSinCos"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct QuaternionF : IEquatable<QuaternionF>
{
    /// <summary>The X (i) component.</summary>
    public readonly float X;

    /// <summary>The Y (j) component.</summary>
    public readonly float Y;

    /// <summary>The Z (k) component.</summary>
    public readonly float Z;

    /// <summary>The scalar (real) component.</summary>
    public readonly float W;

    /// <summary>Creates a quaternion from its components.</summary>
    public QuaternionF(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>The identity rotation (0, 0, 0, 1).</summary>
    public static QuaternionF Identity => new(0, 0, 0, 1);

    /// <summary>The vector part (X, Y, Z).</summary>
    public Vector3F Vector => new(X, Y, Z);

    /// <summary>
    /// Builds the rotation about <paramref name="axis"/> (which must be a unit vector) whose half-angle has the given sine
    /// and cosine: <c>(axis * sinHalf, cosHalf)</c>. The result is not renormalized.
    /// </summary>
    public static QuaternionF FromAxisSinCos(Vector3F axis, float sinHalf, float cosHalf) =>
        new(axis.X * sinHalf, axis.Y * sinHalf, axis.Z * sinHalf, cosHalf);

    /// <summary>
    /// Builds a quaternion from a rotation matrix (the matrix must be orthonormal with determinant +1) by the trace method:
    /// the largest of the trace and the diagonal entries picks one of four formulas, ties going to the earlier branch
    /// (trace, then M11, then M22, then M33). The result is not renormalized and has non-negative W in the trace branch.
    /// </summary>
    public static QuaternionF FromMatrix(Matrix3x3F m)
    {
        float trace = m.M11 + m.M22 + m.M33;
        if (trace > 0)
        {
            float s = Scalar.Sqrt(trace + 1) * 2;
            return new((m.M32 - m.M23) / s, (m.M13 - m.M31) / s, (m.M21 - m.M12) / s, s / 4);
        }
        if (m.M11 > m.M22 && m.M11 > m.M33)
        {
            float s = Scalar.Sqrt(1 + m.M11 - m.M22 - m.M33) * 2;
            return new(s / 4, (m.M12 + m.M21) / s, (m.M13 + m.M31) / s, (m.M32 - m.M23) / s);
        }
        if (m.M22 > m.M33)
        {
            float s = Scalar.Sqrt(1 + m.M22 - m.M11 - m.M33) * 2;
            return new((m.M12 + m.M21) / s, s / 4, (m.M23 + m.M32) / s, (m.M13 - m.M31) / s);
        }
        else
        {
            float s = Scalar.Sqrt(1 + m.M33 - m.M11 - m.M22) * 2;
            return new((m.M13 + m.M31) / s, (m.M23 + m.M32) / s, s / 4, (m.M21 - m.M12) / s);
        }
    }

    /// <summary>
    /// Hamilton product <c>a * b</c> (apply <paramref name="b"/> first, then <paramref name="a"/>). Components:
    /// <c>X = ((a.W*b.X + a.X*b.W) + a.Y*b.Z) - a.Z*b.Y</c>, <c>Y = ((a.W*b.Y - a.X*b.Z) + a.Y*b.W) + a.Z*b.X</c>,
    /// <c>Z = ((a.W*b.Z + a.X*b.Y) - a.Y*b.X) + a.Z*b.W</c>, <c>W = ((a.W*b.W - a.X*b.X) - a.Y*b.Y) - a.Z*b.Z</c>.
    /// </summary>
    public static QuaternionF operator *(QuaternionF a, QuaternionF b) => new(
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

    /// <summary>Component-wise IEEE equality (note that q and -q are different values even though they represent one rotation).</summary>
    public static bool operator ==(QuaternionF a, QuaternionF b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(QuaternionF a, QuaternionF b) => !(a == b);

    /// <summary>Four-component dot product, <c>((a.X*b.X + a.Y*b.Y) + a.Z*b.Z) + a.W*b.W</c>.</summary>
    public static float Dot(QuaternionF a, QuaternionF b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    /// <summary>Squared norm, <c>((X*X + Y*Y) + Z*Z) + W*W</c>.</summary>
    public float LengthSquared() => X * X + Y * Y + Z * Z + W * W;

    /// <summary>Norm: the correctly rounded square root of <see cref="LengthSquared"/>.</summary>
    public float Length() => Scalar.Sqrt(LengthSquared());

    /// <summary>Each component divided by <see cref="Length"/>; returns <see cref="Identity"/> when the length is zero.</summary>
    public QuaternionF Normalized()
    {
        float len = Length();
        return len == 0 ? Identity : new(X / len, Y / len, Z / len, W / len);
    }

    /// <summary>The conjugate (-X, -Y, -Z, W); for a unit quaternion this is the inverse rotation.</summary>
    public QuaternionF Conjugate() => new(-X, -Y, -Z, W);

    /// <summary>
    /// The multiplicative inverse, the conjugate divided by <see cref="LengthSquared"/> (each component a true division).
    /// Returns false and sets <paramref name="inverse"/> to <see cref="Identity"/> when the squared norm is zero or not finite.
    /// </summary>
    public bool TryInvert(out QuaternionF inverse)
    {
        float n = LengthSquared();
        if (n == 0 || !float.IsFinite(n))
        {
            inverse = Identity;
            return false;
        }
        inverse = new(-X / n, -Y / n, -Z / n, W / n);
        return true;
    }

    /// <summary>Component-wise linear interpolation <c>a + (b - a) * t</c>, not normalized and not shortest-arc.</summary>
    public static QuaternionF Lerp(QuaternionF a, QuaternionF b, float t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t);

    /// <summary>
    /// Normalized linear interpolation along the shortest arc: when <c>Dot(a, b)</c> is negative, <paramref name="b"/> is negated
    /// first (a negative dot is the only test; exactly zero keeps <paramref name="b"/>), then <see cref="Lerp"/> and <see cref="Normalized"/> apply.
    /// </summary>
    public static QuaternionF NLerp(QuaternionF a, QuaternionF b, float t)
    {
        QuaternionF bb = Dot(a, b) < 0 ? new(-b.X, -b.Y, -b.Z, -b.W) : b;
        return Lerp(a, bb, t).Normalized();
    }

    /// <summary>
    /// Rotates a vector by this unit quaternion: with <c>t = 2 * (q.Vector x v)</c>, the result is <c>(v + W*t) + (q.Vector x t)</c>
    /// (cross products as in <see cref="Vector3F.Cross"/>).
    /// </summary>
    public Vector3F Rotate(Vector3F v)
    {
        Vector3F q = Vector;
        Vector3F t = Vector3F.Cross(q, v) * 2;
        return v + t * W + Vector3F.Cross(q, t);
    }

    /// <summary>
    /// The rotation matrix of this unit quaternion: with <c>xx = X*X</c>, <c>xy = X*Y</c>, <c>wx = W*X</c> and so on, the diagonal is
    /// <c>1 - 2*(yy + zz)</c>, <c>1 - 2*(xx + zz)</c>, <c>1 - 2*(xx + yy)</c> and each off-diagonal pair is <c>2*(xy -/+ wz)</c>, <c>2*(xz +/- wy)</c>, <c>2*(yz -/+ wx)</c>.
    /// </summary>
    public Matrix3x3F ToMatrix()
    {
        float xx = X * X, yy = Y * Y, zz = Z * Z;
        float xy = X * Y, xz = X * Z, yz = Y * Z;
        float wx = W * X, wy = W * Y, wz = W * Z;
        return new(
            1 - 2 * (yy + zz), 2 * (xy - wz), 2 * (xz + wy),
            2 * (xy + wz), 1 - 2 * (xx + zz), 2 * (yz - wx),
            2 * (xz - wy), 2 * (yz + wx), 1 - 2 * (xx + yy));
    }

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public QuaternionD ToDouble() => new((double)X, (double)Y, (double)Z, (double)W);

    /// <summary>Value equality that treats NaN as equal to itself (and +0 as equal to -0).</summary>
    public bool Equals(QuaternionF other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is QuaternionF q && Equals(q);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Scalar.Bits(X), Scalar.Bits(Y), Scalar.Bits(Z), Scalar.Bits(W));

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({X:R}, {Y:R}, {Z:R}, {W:R})");
}
