using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace RecursiveAI.OneMath;

/// <summary>
/// A 3x3 matrix of <see cref="double"/>, stored row-major in fields named <c>Mrc</c> (row r, column c).
/// Vectors are columns: <c>Transform(v)</c> computes M * v. Products accumulate left to right.
/// <para>
/// <b>Matrix convention (the opposite of System.Numerics).</b> Vectors are columns and <c>a * b</c> applies <c>b</c> first.
/// System.Numerics uses row vectors and <c>a * b</c> applies <c>a</c> first, although its field names <c>Mrc</c> are the same as
/// these. A matrix copied field by field from System.Numerics is therefore the transpose of the transform it was; transpose
/// when porting (<see cref="Matrix4x4D.FromSystemNumerics"/> does it for 4x4).
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Matrix3x3D : IEquatable<Matrix3x3D>
{
    /// <summary>Row 1, column 1.</summary>
    [JsonInclude]
    public readonly double M11;

    /// <summary>Row 1, column 2.</summary>
    [JsonInclude]
    public readonly double M12;

    /// <summary>Row 1, column 3.</summary>
    [JsonInclude]
    public readonly double M13;

    /// <summary>Row 2, column 1.</summary>
    [JsonInclude]
    public readonly double M21;

    /// <summary>Row 2, column 2.</summary>
    [JsonInclude]
    public readonly double M22;

    /// <summary>Row 2, column 3.</summary>
    [JsonInclude]
    public readonly double M23;

    /// <summary>Row 3, column 1.</summary>
    [JsonInclude]
    public readonly double M31;

    /// <summary>Row 3, column 2.</summary>
    [JsonInclude]
    public readonly double M32;

    /// <summary>Row 3, column 3.</summary>
    [JsonInclude]
    public readonly double M33;

    /// <summary>Creates a matrix from its entries in row-major order.</summary>
    [JsonConstructor]
    public Matrix3x3D(double m11, double m12, double m13, double m21, double m22, double m23, double m31, double m32, double m33)
    {
        M11 = m11;
        M12 = m12;
        M13 = m13;
        M21 = m21;
        M22 = m22;
        M23 = m23;
        M31 = m31;
        M32 = m32;
        M33 = m33;
    }

    /// <summary>The identity matrix.</summary>
    public static Matrix3x3D Identity => new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    /// <summary>Creates a matrix whose columns are the given vectors.</summary>
    public static Matrix3x3D FromColumns(Vector3D column0, Vector3D column1, Vector3D column2) =>
        new(column0.X, column1.X, column2.X, column0.Y, column1.Y, column2.Y, column0.Z, column1.Z, column2.Z);

    /// <summary>Creates a matrix whose rows are the given vectors.</summary>
    public static Matrix3x3D FromRows(Vector3D row0, Vector3D row1, Vector3D row2) =>
        new(row0.X, row0.Y, row0.Z, row1.X, row1.Y, row1.Z, row2.X, row2.Y, row2.Z);

    /// <summary>Creates a diagonal scale matrix.</summary>
    public static Matrix3x3D CreateScale(Vector3D scale) => new(scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z);

    /// <summary>Row <paramref name="index"/> (0 to 2). Throws <see cref="ArgumentOutOfRangeException"/> otherwise.</summary>
    public Vector3D Row(int index) => index switch
    {
        0 => new(M11, M12, M13),
        1 => new(M21, M22, M23),
        2 => new(M31, M32, M33),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0 to 2."),
    };

    /// <summary>Column <paramref name="index"/> (0 to 2). Throws <see cref="ArgumentOutOfRangeException"/> otherwise.</summary>
    public Vector3D Column(int index) => index switch
    {
        0 => new(M11, M21, M31),
        1 => new(M12, M22, M32),
        2 => new(M13, M23, M33),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0 to 2."),
    };

    /// <summary>Matrix product <c>a * b</c>; entry (r, c) is <c>(a.Mr1*b.M1c + a.Mr2*b.M2c) + a.Mr3*b.M3c</c>.</summary>
    public static Matrix3x3D operator *(Matrix3x3D a, Matrix3x3D b) => new(
        a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
        a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
        a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,
        a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
        a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
        a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,
        a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
        a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
        a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33);

    /// <summary>Matrix-vector product <c>m * v</c>.</summary>
    public static Vector3D operator *(Matrix3x3D m, Vector3D v) => m.Transform(v);

    /// <summary>Each entry multiplied by <paramref name="s"/>.</summary>
    public static Matrix3x3D operator *(Matrix3x3D m, double s) =>
        new(m.M11 * s, m.M12 * s, m.M13 * s, m.M21 * s, m.M22 * s, m.M23 * s, m.M31 * s, m.M32 * s, m.M33 * s);

    /// <summary>Entry-wise IEEE equality.</summary>
    public static bool operator ==(Matrix3x3D a, Matrix3x3D b) =>
        a.M11 == b.M11 && a.M12 == b.M12 && a.M13 == b.M13 &&
        a.M21 == b.M21 && a.M22 == b.M22 && a.M23 == b.M23 &&
        a.M31 == b.M31 && a.M32 == b.M32 && a.M33 == b.M33;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(Matrix3x3D a, Matrix3x3D b) => !(a == b);

    /// <summary>Computes <c>M * v</c>; each component is <c>(Mr1*x + Mr2*y) + Mr3*z</c>.</summary>
    public Vector3D Transform(Vector3D v) => new(
        M11 * v.X + M12 * v.Y + M13 * v.Z,
        M21 * v.X + M22 * v.Y + M23 * v.Z,
        M31 * v.X + M32 * v.Y + M33 * v.Z);

    /// <summary>The transposed matrix.</summary>
    public Matrix3x3D Transposed() => new(M11, M21, M31, M12, M22, M32, M13, M23, M33);

    /// <summary>Determinant by cofactor expansion along row 1: <c>(M11*C11 + M12*C12) + M13*C13</c>, with each cofactor Cij a difference of two products.</summary>
    public double Determinant()
    {
        double c11 = M22 * M33 - M23 * M32;
        double c12 = M23 * M31 - M21 * M33;
        double c13 = M21 * M32 - M22 * M31;
        return M11 * c11 + M12 * c12 + M13 * c13;
    }

    /// <summary>
    /// Inverts the matrix as the adjugate divided by the determinant (each entry a true division); the cofactors
    /// and the determinant are the ones <see cref="Determinant"/> uses. Returns false and sets
    /// <paramref name="inverse"/> to the zero matrix when the determinant is zero or not finite.
    /// </summary>
    public bool TryInvert(out Matrix3x3D inverse)
    {
        double c11 = M22 * M33 - M23 * M32;
        double c12 = M23 * M31 - M21 * M33;
        double c13 = M21 * M32 - M22 * M31;
        double det = M11 * c11 + M12 * c12 + M13 * c13;
        if (det == 0 || !double.IsFinite(det))
        {
            inverse = default;
            return false;
        }
        double c21 = M13 * M32 - M12 * M33;
        double c22 = M11 * M33 - M13 * M31;
        double c23 = M12 * M31 - M11 * M32;
        double c31 = M12 * M23 - M13 * M22;
        double c32 = M13 * M21 - M11 * M23;
        double c33 = M11 * M22 - M12 * M21;
        inverse = new(c11 / det, c21 / det, c31 / det, c12 / det, c22 / det, c32 / det, c13 / det, c23 / det, c33 / det);
        return true;
    }

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public Matrix3x3F ToSingle() => new(
        (float)M11, (float)M12, (float)M13, (float)M21, (float)M22, (float)M23, (float)M31, (float)M32, (float)M33);

    /// <summary>Value equality that treats NaN as equal to itself (and +0 as equal to -0).</summary>
    public bool Equals(Matrix3x3D other) =>
        M11.Equals(other.M11) && M12.Equals(other.M12) && M13.Equals(other.M13) &&
        M21.Equals(other.M21) && M22.Equals(other.M22) && M23.Equals(other.M23) &&
        M31.Equals(other.M31) && M32.Equals(other.M32) && M33.Equals(other.M33);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Matrix3x3D m && Equals(m);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(
        Scalar.Mix(Scalar.Bits(M11), Scalar.Bits(M12), Scalar.Bits(M13)),
        Scalar.Mix(Scalar.Bits(M21), Scalar.Bits(M22), Scalar.Bits(M23)),
        Scalar.Mix(Scalar.Bits(M31), Scalar.Bits(M32), Scalar.Bits(M33)));

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"[{M11:R}, {M12:R}, {M13:R}; {M21:R}, {M22:R}, {M23:R}; {M31:R}, {M32:R}, {M33:R}]");

    /// <summary>
    /// Rotation of <paramref name="radians"/> about the X axis, counter-clockwise seen from +X (column vectors, right-handed):
    /// rows <c>(1, 0, 0)</c>, <c>(0, c, -s)</c>, <c>(0, s, c)</c> with <c>(s, c) = Math64.SinCos(radians)</c>.
    /// </summary>
    public static Matrix3x3D CreateRotationX(double radians)
    {
        (double s, double c) = Math64.SinCos(radians);
        return new(1, 0, 0, 0, c, -s, 0, s, c);
    }

    /// <summary>
    /// Rotation of <paramref name="radians"/> about the Y axis (column vectors, right-handed): rows <c>(c, 0, s)</c>, <c>(0, 1, 0)</c>,
    /// <c>(-s, 0, c)</c> with <c>(s, c) = Math64.SinCos(radians)</c>.
    /// </summary>
    public static Matrix3x3D CreateRotationY(double radians)
    {
        (double s, double c) = Math64.SinCos(radians);
        return new(c, 0, s, 0, 1, 0, -s, 0, c);
    }

    /// <summary>
    /// Rotation of <paramref name="radians"/> about the Z axis (column vectors, right-handed): rows <c>(c, -s, 0)</c>, <c>(s, c, 0)</c>,
    /// <c>(0, 0, 1)</c> with <c>(s, c) = Math64.SinCos(radians)</c>.
    /// </summary>
    public static Matrix3x3D CreateRotationZ(double radians)
    {
        (double s, double c) = Math64.SinCos(radians);
        return new(c, -s, 0, s, c, 0, 0, 0, 1);
    }

    /// <summary>The rotation matrix of a unit quaternion; the same as <see cref="QuaternionD.ToMatrix"/>.</summary>
    public static Matrix3x3D CreateFromQuaternion(QuaternionD rotation) => rotation.ToMatrix();
}
