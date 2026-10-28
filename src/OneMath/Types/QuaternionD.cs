using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace RecursiveAI.OneMath;

/// <summary>
/// A rotation quaternion of <see cref="double"/> components, <c>X*i + Y*j + Z*k + W</c>, with the Hamilton product.
/// Rotation methods assume a unit quaternion; they do not renormalize. <see cref="CreateFromAxisAngle"/> builds the half-angle sine
/// and cosine with this library's functions and calls <see cref="CreateFromAxisSinCos"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct QuaternionD : IEquatable<QuaternionD>
{
    /// <summary>The X (i) component.</summary>
    [JsonInclude]
    public readonly double X;

    /// <summary>The Y (j) component.</summary>
    [JsonInclude]
    public readonly double Y;

    /// <summary>The Z (k) component.</summary>
    [JsonInclude]
    public readonly double Z;

    /// <summary>The scalar (real) component.</summary>
    [JsonInclude]
    public readonly double W;

    /// <summary>Creates a quaternion from its components.</summary>
    [JsonConstructor]
    public QuaternionD(double x, double y, double z, double w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>The identity rotation (0, 0, 0, 1).</summary>
    public static QuaternionD Identity => new(0, 0, 0, 1);

    /// <summary>The vector part (X, Y, Z).</summary>
    [JsonIgnore]
    public Vector3D Vector => new(X, Y, Z);

    /// <summary>
    /// Builds the rotation about <paramref name="axis"/> (which must be a unit vector) whose half-angle has the given sine
    /// and cosine: <c>(axis * sinHalf, cosHalf)</c>. The result is not renormalized.
    /// </summary>
    public static QuaternionD CreateFromAxisSinCos(Vector3D axis, double sinHalf, double cosHalf) =>
        new(axis.X * sinHalf, axis.Y * sinHalf, axis.Z * sinHalf, cosHalf);

    /// <summary>
    /// Builds a quaternion from a rotation matrix (the matrix must be orthonormal with determinant +1) by the trace method:
    /// the largest of the trace and the diagonal entries picks one of four formulas, ties going to the earlier branch
    /// (trace, then M11, then M22, then M33). The result is not renormalized and has non-negative W in the trace branch.
    /// </summary>
    public static QuaternionD CreateFromRotationMatrix(Matrix3x3D m)
    {
        double trace = m.M11 + m.M22 + m.M33;
        if (trace > 0)
        {
            double s = Scalar.Sqrt(trace + 1) * 2;
            return new((m.M32 - m.M23) / s, (m.M13 - m.M31) / s, (m.M21 - m.M12) / s, s / 4);
        }
        if (m.M11 > m.M22 && m.M11 > m.M33)
        {
            double s = Scalar.Sqrt(1 + m.M11 - m.M22 - m.M33) * 2;
            return new(s / 4, (m.M12 + m.M21) / s, (m.M13 + m.M31) / s, (m.M32 - m.M23) / s);
        }
        if (m.M22 > m.M33)
        {
            double s = Scalar.Sqrt(1 + m.M22 - m.M11 - m.M33) * 2;
            return new((m.M12 + m.M21) / s, s / 4, (m.M23 + m.M32) / s, (m.M13 - m.M31) / s);
        }
        else
        {
            double s = Scalar.Sqrt(1 + m.M33 - m.M11 - m.M22) * 2;
            return new((m.M13 + m.M31) / s, (m.M23 + m.M32) / s, s / 4, (m.M21 - m.M12) / s);
        }
    }

    /// <summary>
    /// Hamilton product <c>a * b</c> (apply <paramref name="b"/> first, then <paramref name="a"/>). Components:
    /// <c>X = ((a.W*b.X + a.X*b.W) + a.Y*b.Z) - a.Z*b.Y</c>, <c>Y = ((a.W*b.Y - a.X*b.Z) + a.Y*b.W) + a.Z*b.X</c>,
    /// <c>Z = ((a.W*b.Z + a.X*b.Y) - a.Y*b.X) + a.Z*b.W</c>, <c>W = ((a.W*b.W - a.X*b.X) - a.Y*b.Y) - a.Z*b.Z</c>.
    /// </summary>
    public static QuaternionD operator *(QuaternionD a, QuaternionD b) => new(
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

    /// <summary>Component-wise IEEE equality (note that q and -q are different values even though they represent one rotation).</summary>
    public static bool operator ==(QuaternionD a, QuaternionD b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(QuaternionD a, QuaternionD b) => !(a == b);

    /// <summary>Four-component dot product, <c>((a.X*b.X + a.Y*b.Y) + a.Z*b.Z) + a.W*b.W</c>.</summary>
    public static double Dot(QuaternionD a, QuaternionD b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    /// <summary>Squared norm, <c>((X*X + Y*Y) + Z*Z) + W*W</c>.</summary>
    public double LengthSquared() => X * X + Y * Y + Z * Z + W * W;

    /// <summary>Norm: the correctly rounded square root of <see cref="LengthSquared"/>.</summary>
    public double Length() => Scalar.Sqrt(LengthSquared());

    /// <summary>Each component divided by <see cref="Length"/>; returns <see cref="Identity"/> when the length is zero.</summary>
    public QuaternionD Normalized()
    {
        double len = Length();
        return len == 0 ? Identity : new(X / len, Y / len, Z / len, W / len);
    }

    /// <summary>The conjugate (-X, -Y, -Z, W); for a unit quaternion this is the inverse rotation.</summary>
    public QuaternionD Conjugated() => new(-X, -Y, -Z, W);

    /// <summary>
    /// The multiplicative inverse, the conjugate divided by <see cref="LengthSquared"/> (each component a true division).
    /// Returns false and sets <paramref name="inverse"/> to <see cref="Identity"/> when the squared norm is zero or not finite.
    /// </summary>
    public bool TryInvert(out QuaternionD inverse)
    {
        double n = LengthSquared();
        if (n == 0 || !double.IsFinite(n))
        {
            inverse = Identity;
            return false;
        }
        inverse = new(-X / n, -Y / n, -Z / n, W / n);
        return true;
    }

    /// <summary>Component-wise linear interpolation <c>a + (b - a) * t</c>, not normalized and not shortest-arc.</summary>
    public static QuaternionD Lerp(QuaternionD a, QuaternionD b, double t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t);

    /// <summary>
    /// Normalized linear interpolation along the shortest arc: when <c>Dot(a, b)</c> is negative, <paramref name="b"/> is negated
    /// first (a negative dot is the only test; exactly zero keeps <paramref name="b"/>), then <see cref="Lerp"/> and <see cref="Normalized"/> apply.
    /// </summary>
    public static QuaternionD NLerp(QuaternionD a, QuaternionD b, double t)
    {
        QuaternionD bb = Dot(a, b) < 0 ? new(-b.X, -b.Y, -b.Z, -b.W) : b;
        return Lerp(a, bb, t).Normalized();
    }

    /// <summary>
    /// Rotates a vector by this unit quaternion: with <c>t = 2 * (q.Vector x v)</c>, the result is <c>(v + W*t) + (q.Vector x t)</c>
    /// (cross products as in <see cref="Vector3D.Cross"/>).
    /// </summary>
    public Vector3D Rotate(Vector3D v)
    {
        Vector3D q = Vector;
        Vector3D t = Vector3D.Cross(q, v) * 2;
        return v + t * W + Vector3D.Cross(q, t);
    }

    /// <summary>
    /// The rotation matrix of this unit quaternion: with <c>xx = X*X</c>, <c>xy = X*Y</c>, <c>wx = W*X</c> and so on, the diagonal is
    /// <c>1 - 2*(yy + zz)</c>, <c>1 - 2*(xx + zz)</c>, <c>1 - 2*(xx + yy)</c> and each off-diagonal pair is <c>2*(xy -/+ wz)</c>, <c>2*(xz +/- wy)</c>, <c>2*(yz -/+ wx)</c>.
    /// </summary>
    public Matrix3x3D ToMatrix()
    {
        double xx = X * X, yy = Y * Y, zz = Z * Z;
        double xy = X * Y, xz = X * Z, yz = Y * Z;
        double wx = W * X, wy = W * Y, wz = W * Z;
        return new(
            1 - 2 * (yy + zz), 2 * (xy - wz), 2 * (xz + wy),
            2 * (xy + wz), 1 - 2 * (xx + zz), 2 * (yz - wx),
            2 * (xz - wy), 2 * (yz + wx), 1 - 2 * (xx + yy));
    }

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public QuaternionF ToSingle() => new((float)X, (float)Y, (float)Z, (float)W);

    /// <summary>Value equality that treats NaN as equal to itself (and +0 as equal to -0).</summary>
    public bool Equals(QuaternionD other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is QuaternionD q && Equals(q);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Scalar.Bits(X), Scalar.Bits(Y), Scalar.Bits(Z), Scalar.Bits(W));

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({X:R}, {Y:R}, {Z:R}, {W:R})");

    /// <summary>
    /// Builds the rotation of <paramref name="radians"/> about <paramref name="axis"/> (which must be a unit vector; counter-clockwise
    /// seen from the tip of the axis). Defined exactly as <c>CreateFromAxisSinCos(axis, Math64.SinCos(radians / 2))</c>: the half-angle
    /// sine and cosine come from this library's correctly rounded functions, so the bits are the same everywhere. Not renormalized.
    /// </summary>
    public static QuaternionD CreateFromAxisAngle(Vector3D axis, double radians)
    {
        (double sin, double cos) = Math64.SinCos(radians / 2);
        return CreateFromAxisSinCos(axis, sin, cos);
    }

    /// <summary>
    /// Spherical linear interpolation along the shortest arc between unit quaternions. Evaluation order: <c>d = Dot(a, b)</c>; when
    /// <c>d</c> is negative <paramref name="b"/> is negated and <c>d</c> becomes <c>-d</c> (exactly zero keeps <paramref name="b"/>).
    /// When <c>d</c> is above 0.9995 the quaternions are nearly parallel and the result is <c>Lerp(a, b, t).Normalized()</c>.
    /// Otherwise <c>theta = Math64.Acos(d)</c>, <c>s = Math64.Sin(theta)</c>, <c>wa = Math64.Sin((1 - t) * theta) / s</c>,
    /// <c>wb = Math64.Sin(t * theta) / s</c> and each component is <c>a.c * wa + b.c * wb</c> (product, product, then one sum).
    /// Inputs are assumed to be unit length; the result is not renormalized on the spherical branch. <paramref name="t"/> is not clamped.
    /// </summary>
    public static QuaternionD Slerp(QuaternionD a, QuaternionD b, double t)
    {
        double d = Dot(a, b);
        QuaternionD bb = b;
        if (d < 0)
        {
            bb = new(-b.X, -b.Y, -b.Z, -b.W);
            d = -d;
        }
        if (d > 0.9995)
        {
            return Lerp(a, bb, t).Normalized();
        }
        double theta = Math64.Acos(d);
        double sinTheta = Math64.Sin(theta);
        double wa = Math64.Sin((1 - t) * theta) / sinTheta;
        double wb = Math64.Sin(t * theta) / sinTheta;
        return new(a.X * wa + bb.X * wb, a.Y * wa + bb.Y * wb, a.Z * wa + bb.Z * wb, a.W * wa + bb.W * wb);
    }

    /// <summary>Converts from a <see cref="System.Numerics.Quaternion"/> (same component meaning and Hamilton product; no transposition needed). Exact.</summary>
    public static QuaternionD FromSystemNumerics(System.Numerics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);

    /// <summary>Converts to a <see cref="System.Numerics.Quaternion"/>. Narrows each component to float32 (round to nearest even).</summary>
    public System.Numerics.Quaternion ToSystemNumerics() => new((float)X, (float)Y, (float)Z, (float)W);
}
