using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RecursiveAI.OneMath;

/// <summary>
/// A 4x4 matrix of <see cref="float"/>, stored row-major in fields named <c>Mrc</c> (row r, column c).
/// Vectors are columns: <c>Transform(v)</c> computes M * v; translation sits in column 4.
/// Products accumulate left to right.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Matrix4x4F : IEquatable<Matrix4x4F>
{
    /// <summary>Row 1, column 1.</summary>
    public readonly float M11;

    /// <summary>Row 1, column 2.</summary>
    public readonly float M12;

    /// <summary>Row 1, column 3.</summary>
    public readonly float M13;

    /// <summary>Row 1, column 4.</summary>
    public readonly float M14;

    /// <summary>Row 2, column 1.</summary>
    public readonly float M21;

    /// <summary>Row 2, column 2.</summary>
    public readonly float M22;

    /// <summary>Row 2, column 3.</summary>
    public readonly float M23;

    /// <summary>Row 2, column 4.</summary>
    public readonly float M24;

    /// <summary>Row 3, column 1.</summary>
    public readonly float M31;

    /// <summary>Row 3, column 2.</summary>
    public readonly float M32;

    /// <summary>Row 3, column 3.</summary>
    public readonly float M33;

    /// <summary>Row 3, column 4.</summary>
    public readonly float M34;

    /// <summary>Row 4, column 1.</summary>
    public readonly float M41;

    /// <summary>Row 4, column 2.</summary>
    public readonly float M42;

    /// <summary>Row 4, column 3.</summary>
    public readonly float M43;

    /// <summary>Row 4, column 4.</summary>
    public readonly float M44;

    /// <summary>Creates a matrix from its entries in row-major order.</summary>
    public Matrix4x4F(
        float m11, float m12, float m13, float m14,
        float m21, float m22, float m23, float m24,
        float m31, float m32, float m33, float m34,
        float m41, float m42, float m43, float m44)
    {
        M11 = m11;
        M12 = m12;
        M13 = m13;
        M14 = m14;
        M21 = m21;
        M22 = m22;
        M23 = m23;
        M24 = m24;
        M31 = m31;
        M32 = m32;
        M33 = m33;
        M34 = m34;
        M41 = m41;
        M42 = m42;
        M43 = m43;
        M44 = m44;
    }

    /// <summary>The identity matrix.</summary>
    public static Matrix4x4F Identity => new(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);

    /// <summary>Creates a matrix whose columns are the given vectors.</summary>
    public static Matrix4x4F FromColumns(Vector4F c1, Vector4F c2, Vector4F c3, Vector4F c4) =>
        new(c1.X, c2.X, c3.X, c4.X, c1.Y, c2.Y, c3.Y, c4.Y, c1.Z, c2.Z, c3.Z, c4.Z, c1.W, c2.W, c3.W, c4.W);

    /// <summary>Creates a matrix whose rows are the given vectors.</summary>
    public static Matrix4x4F FromRows(Vector4F r1, Vector4F r2, Vector4F r3, Vector4F r4) =>
        new(r1.X, r1.Y, r1.Z, r1.W, r2.X, r2.Y, r2.Z, r2.W, r3.X, r3.Y, r3.Z, r3.W, r4.X, r4.Y, r4.Z, r4.W);

    /// <summary>Creates a translation matrix (the offset goes in column 4).</summary>
    public static Matrix4x4F CreateTranslation(Vector3F offset) =>
        new(1, 0, 0, offset.X, 0, 1, 0, offset.Y, 0, 0, 1, offset.Z, 0, 0, 0, 1);

    /// <summary>Creates a diagonal scale matrix.</summary>
    public static Matrix4x4F CreateScale(Vector3F scale) =>
        new(scale.X, 0, 0, 0, 0, scale.Y, 0, 0, 0, 0, scale.Z, 0, 0, 0, 0, 1);

    /// <summary>Row <paramref name="index"/> (0 to 3). Throws <see cref="ArgumentOutOfRangeException"/> otherwise.</summary>
    public Vector4F Row(int index) => index switch
    {
        0 => new(M11, M12, M13, M14),
        1 => new(M21, M22, M23, M24),
        2 => new(M31, M32, M33, M34),
        3 => new(M41, M42, M43, M44),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0 to 3."),
    };

    /// <summary>Column <paramref name="index"/> (0 to 3). Throws <see cref="ArgumentOutOfRangeException"/> otherwise.</summary>
    public Vector4F Column(int index) => index switch
    {
        0 => new(M11, M21, M31, M41),
        1 => new(M12, M22, M32, M42),
        2 => new(M13, M23, M33, M43),
        3 => new(M14, M24, M34, M44),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0 to 3."),
    };

    private static float Dot4(float a1, float b1, float a2, float b2, float a3, float b3, float a4, float b4) => a1 * b1 + a2 * b2 + a3 * b3 + a4 * b4;

    /// <summary>Matrix product <c>a * b</c>; entry (r, c) is <c>((a.Mr1*b.M1c + a.Mr2*b.M2c) + a.Mr3*b.M3c) + a.Mr4*b.M4c</c>.</summary>
    public static Matrix4x4F operator *(Matrix4x4F a, Matrix4x4F b) => new(
        Dot4(a.M11, b.M11, a.M12, b.M21, a.M13, b.M31, a.M14, b.M41),
        Dot4(a.M11, b.M12, a.M12, b.M22, a.M13, b.M32, a.M14, b.M42),
        Dot4(a.M11, b.M13, a.M12, b.M23, a.M13, b.M33, a.M14, b.M43),
        Dot4(a.M11, b.M14, a.M12, b.M24, a.M13, b.M34, a.M14, b.M44),
        Dot4(a.M21, b.M11, a.M22, b.M21, a.M23, b.M31, a.M24, b.M41),
        Dot4(a.M21, b.M12, a.M22, b.M22, a.M23, b.M32, a.M24, b.M42),
        Dot4(a.M21, b.M13, a.M22, b.M23, a.M23, b.M33, a.M24, b.M43),
        Dot4(a.M21, b.M14, a.M22, b.M24, a.M23, b.M34, a.M24, b.M44),
        Dot4(a.M31, b.M11, a.M32, b.M21, a.M33, b.M31, a.M34, b.M41),
        Dot4(a.M31, b.M12, a.M32, b.M22, a.M33, b.M32, a.M34, b.M42),
        Dot4(a.M31, b.M13, a.M32, b.M23, a.M33, b.M33, a.M34, b.M43),
        Dot4(a.M31, b.M14, a.M32, b.M24, a.M33, b.M34, a.M34, b.M44),
        Dot4(a.M41, b.M11, a.M42, b.M21, a.M43, b.M31, a.M44, b.M41),
        Dot4(a.M41, b.M12, a.M42, b.M22, a.M43, b.M32, a.M44, b.M42),
        Dot4(a.M41, b.M13, a.M42, b.M23, a.M43, b.M33, a.M44, b.M43),
        Dot4(a.M41, b.M14, a.M42, b.M24, a.M43, b.M34, a.M44, b.M44));

    /// <summary>Matrix-vector product <c>m * v</c>.</summary>
    public static Vector4F operator *(Matrix4x4F m, Vector4F v) => m.Transform(v);

    /// <summary>Entry-wise IEEE equality.</summary>
    public static bool operator ==(Matrix4x4F a, Matrix4x4F b) =>
        a.M11 == b.M11 && a.M12 == b.M12 && a.M13 == b.M13 && a.M14 == b.M14 &&
        a.M21 == b.M21 && a.M22 == b.M22 && a.M23 == b.M23 && a.M24 == b.M24 &&
        a.M31 == b.M31 && a.M32 == b.M32 && a.M33 == b.M33 && a.M34 == b.M34 &&
        a.M41 == b.M41 && a.M42 == b.M42 && a.M43 == b.M43 && a.M44 == b.M44;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(Matrix4x4F a, Matrix4x4F b) => !(a == b);

    /// <summary>Computes <c>M * v</c>; each component is <c>((Mr1*x + Mr2*y) + Mr3*z) + Mr4*w</c>.</summary>
    public Vector4F Transform(Vector4F v) => new(
        Dot4(M11, v.X, M12, v.Y, M13, v.Z, M14, v.W),
        Dot4(M21, v.X, M22, v.Y, M23, v.Z, M24, v.W),
        Dot4(M31, v.X, M32, v.Y, M33, v.Z, M34, v.W),
        Dot4(M41, v.X, M42, v.Y, M43, v.Z, M44, v.W));

    /// <summary>Transforms a point as <c>M * (x, y, z, 1)</c> and drops the last row (no perspective divide); the translation is added last.</summary>
    public Vector3F TransformPoint(Vector3F p) => new(
        M11 * p.X + M12 * p.Y + M13 * p.Z + M14,
        M21 * p.X + M22 * p.Y + M23 * p.Z + M24,
        M31 * p.X + M32 * p.Y + M33 * p.Z + M34);

    /// <summary>Transforms a direction as <c>M * (x, y, z, 0)</c> without the translation.</summary>
    public Vector3F TransformVector(Vector3F v) => new(
        M11 * v.X + M12 * v.Y + M13 * v.Z,
        M21 * v.X + M22 * v.Y + M23 * v.Z,
        M31 * v.X + M32 * v.Y + M33 * v.Z);

    /// <summary>The transposed matrix.</summary>
    public Matrix4x4F Transpose() => new(M11, M21, M31, M41, M12, M22, M32, M42, M13, M23, M33, M43, M14, M24, M34, M44);

    /// <summary>
    /// Determinant by Laplace expansion over the six 2x2 minors of rows 1-2 (s0..s5) and rows 3-4 (c0..c5):
    /// <c>s0*c5 - s1*c4 + s2*c3 + s3*c2 - s4*c1 + s5*c0</c>, summed left to right.
    /// </summary>
    public float Determinant()
    {
        Minors(out float s0, out float s1, out float s2, out float s3, out float s4, out float s5, out float c0, out float c1, out float c2, out float c3, out float c4, out float c5);
        return s0 * c5 - s1 * c4 + s2 * c3 + s3 * c2 - s4 * c1 + s5 * c0;
    }

    private void Minors(
        out float s0, out float s1, out float s2, out float s3, out float s4, out float s5,
        out float c0, out float c1, out float c2, out float c3, out float c4, out float c5)
    {
        s0 = M11 * M22 - M21 * M12;
        s1 = M11 * M23 - M21 * M13;
        s2 = M11 * M24 - M21 * M14;
        s3 = M12 * M23 - M22 * M13;
        s4 = M12 * M24 - M22 * M14;
        s5 = M13 * M24 - M23 * M14;
        c5 = M33 * M44 - M43 * M34;
        c4 = M32 * M44 - M42 * M34;
        c3 = M32 * M43 - M42 * M33;
        c2 = M31 * M44 - M41 * M34;
        c1 = M31 * M43 - M41 * M33;
        c0 = M31 * M42 - M41 * M32;
    }

    /// <summary>
    /// Inverts the matrix as the adjugate (built from the same 2x2 minors as <see cref="Determinant"/>) divided by the
    /// determinant, each entry a three-term sum divided by the determinant. Returns false and sets
    /// <paramref name="inverse"/> to the zero matrix when the determinant is zero or not finite.
    /// </summary>
    public bool TryInvert(out Matrix4x4F inverse)
    {
        Minors(out float s0, out float s1, out float s2, out float s3, out float s4, out float s5, out float c0, out float c1, out float c2, out float c3, out float c4, out float c5);
        float det = s0 * c5 - s1 * c4 + s2 * c3 + s3 * c2 - s4 * c1 + s5 * c0;
        if (det == 0 || !float.IsFinite(det))
        {
            inverse = default;
            return false;
        }
        inverse = new(
            (M22 * c5 - M23 * c4 + M24 * c3) / det,
            (-M12 * c5 + M13 * c4 - M14 * c3) / det,
            (M42 * s5 - M43 * s4 + M44 * s3) / det,
            (-M32 * s5 + M33 * s4 - M34 * s3) / det,
            (-M21 * c5 + M23 * c2 - M24 * c1) / det,
            (M11 * c5 - M13 * c2 + M14 * c1) / det,
            (-M41 * s5 + M43 * s2 - M44 * s1) / det,
            (M31 * s5 - M33 * s2 + M34 * s1) / det,
            (M21 * c4 - M22 * c2 + M24 * c0) / det,
            (-M11 * c4 + M12 * c2 - M14 * c0) / det,
            (M41 * s4 - M42 * s2 + M44 * s0) / det,
            (-M31 * s4 + M32 * s2 - M34 * s0) / det,
            (-M21 * c3 + M22 * c1 - M23 * c0) / det,
            (M11 * c3 - M12 * c1 + M13 * c0) / det,
            (-M41 * s3 + M42 * s1 - M43 * s0) / det,
            (M31 * s3 - M32 * s1 + M33 * s0) / det);
        return true;
    }

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public Matrix4x4D ToDouble() => new(
        (double)M11, (double)M12, (double)M13, (double)M14, (double)M21, (double)M22, (double)M23, (double)M24,
        (double)M31, (double)M32, (double)M33, (double)M34, (double)M41, (double)M42, (double)M43, (double)M44);

    /// <summary>Value equality that treats NaN as equal to itself (and +0 as equal to -0).</summary>
    public bool Equals(Matrix4x4F other) =>
        M11.Equals(other.M11) && M12.Equals(other.M12) && M13.Equals(other.M13) && M14.Equals(other.M14) &&
        M21.Equals(other.M21) && M22.Equals(other.M22) && M23.Equals(other.M23) && M24.Equals(other.M24) &&
        M31.Equals(other.M31) && M32.Equals(other.M32) && M33.Equals(other.M33) && M34.Equals(other.M34) &&
        M41.Equals(other.M41) && M42.Equals(other.M42) && M43.Equals(other.M43) && M44.Equals(other.M44);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Matrix4x4F m && Equals(m);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(
        Scalar.Mix(Scalar.Bits(M11), Scalar.Bits(M12), Scalar.Bits(M13), Scalar.Bits(M14)),
        Scalar.Mix(Scalar.Bits(M21), Scalar.Bits(M22), Scalar.Bits(M23), Scalar.Bits(M24)),
        Scalar.Mix(Scalar.Bits(M31), Scalar.Bits(M32), Scalar.Bits(M33), Scalar.Bits(M34)),
        Scalar.Mix(Scalar.Bits(M41), Scalar.Bits(M42), Scalar.Bits(M43), Scalar.Bits(M44)));

    /// <inheritdoc />
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"[{M11:R}, {M12:R}, {M13:R}, {M14:R}; {M21:R}, {M22:R}, {M23:R}, {M24:R}; {M31:R}, {M32:R}, {M33:R}, {M34:R}; {M41:R}, {M42:R}, {M43:R}, {M44:R}]");
}
