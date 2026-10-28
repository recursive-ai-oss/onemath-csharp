using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace RecursiveAI.OneMath;

/// <summary>
/// A 2x2 matrix of <see cref="double"/>, stored row-major in fields named <c>Mrc</c> (row r, column c).
/// Vectors are columns: <c>Transform(v)</c> computes M * v. Products accumulate left to right.
/// <para>
/// <b>Matrix convention (the opposite of System.Numerics).</b> Vectors are columns and <c>a * b</c> applies <c>b</c> first.
/// System.Numerics uses row vectors and <c>a * b</c> applies <c>a</c> first, although its field names <c>Mrc</c> are the same as
/// these. A matrix copied field by field from System.Numerics is therefore the transpose of the transform it was; transpose
/// when porting (<see cref="Matrix4x4D.FromSystemNumerics"/> does it for 4x4).
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Matrix2x2D : IEquatable<Matrix2x2D>
{
    /// <summary>Row 1, column 1.</summary>
    [JsonInclude]
    public readonly double M11;

    /// <summary>Row 1, column 2.</summary>
    [JsonInclude]
    public readonly double M12;

    /// <summary>Row 2, column 1.</summary>
    [JsonInclude]
    public readonly double M21;

    /// <summary>Row 2, column 2.</summary>
    [JsonInclude]
    public readonly double M22;

    /// <summary>Creates a matrix from its entries in row-major order.</summary>
    [JsonConstructor]
    public Matrix2x2D(double m11, double m12, double m21, double m22)
    {
        M11 = m11;
        M12 = m12;
        M21 = m21;
        M22 = m22;
    }

    /// <summary>The identity matrix.</summary>
    public static Matrix2x2D Identity => new(1, 0, 0, 1);

    /// <summary>Creates a matrix whose columns are the given vectors.</summary>
    public static Matrix2x2D FromColumns(Vector2D column0, Vector2D column1) => new(column0.X, column1.X, column0.Y, column1.Y);

    /// <summary>Creates a matrix whose rows are the given vectors.</summary>
    public static Matrix2x2D FromRows(Vector2D row0, Vector2D row1) => new(row0.X, row0.Y, row1.X, row1.Y);

    /// <summary>Creates a diagonal scale matrix.</summary>
    public static Matrix2x2D CreateScale(Vector2D scale) => new(scale.X, 0, 0, scale.Y);

    /// <summary>Row <paramref name="index"/> (0 or 1). Throws <see cref="ArgumentOutOfRangeException"/> otherwise.</summary>
    public Vector2D Row(int index) => index switch
    {
        0 => new(M11, M12),
        1 => new(M21, M22),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0 or 1."),
    };

    /// <summary>Column <paramref name="index"/> (0 or 1). Throws <see cref="ArgumentOutOfRangeException"/> otherwise.</summary>
    public Vector2D Column(int index) => index switch
    {
        0 => new(M11, M21),
        1 => new(M12, M22),
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0 or 1."),
    };

    /// <summary>Matrix product <c>a * b</c>; entry (r, c) is <c>a.Mr1*b.M1c + a.Mr2*b.M2c</c>.</summary>
    public static Matrix2x2D operator *(Matrix2x2D a, Matrix2x2D b) => new(
        a.M11 * b.M11 + a.M12 * b.M21,
        a.M11 * b.M12 + a.M12 * b.M22,
        a.M21 * b.M11 + a.M22 * b.M21,
        a.M21 * b.M12 + a.M22 * b.M22);

    /// <summary>Matrix-vector product <c>m * v</c>.</summary>
    public static Vector2D operator *(Matrix2x2D m, Vector2D v) => m.Transform(v);

    /// <summary>Each entry multiplied by <paramref name="s"/>.</summary>
    public static Matrix2x2D operator *(Matrix2x2D m, double s) => new(m.M11 * s, m.M12 * s, m.M21 * s, m.M22 * s);

    /// <summary>Entry-wise IEEE equality.</summary>
    public static bool operator ==(Matrix2x2D a, Matrix2x2D b) => a.M11 == b.M11 && a.M12 == b.M12 && a.M21 == b.M21 && a.M22 == b.M22;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(Matrix2x2D a, Matrix2x2D b) => !(a == b);

    /// <summary>Computes <c>M * v</c>: <c>(M11*x + M12*y, M21*x + M22*y)</c>.</summary>
    public Vector2D Transform(Vector2D v) => new(M11 * v.X + M12 * v.Y, M21 * v.X + M22 * v.Y);

    /// <summary>The transposed matrix.</summary>
    public Matrix2x2D Transposed() => new(M11, M21, M12, M22);

    /// <summary>Determinant, <c>M11*M22 - M12*M21</c>.</summary>
    public double Determinant() => M11 * M22 - M12 * M21;

    /// <summary>
    /// Inverts the matrix as the adjugate divided by the determinant (each entry a true division).
    /// Returns false and sets <paramref name="inverse"/> to the zero matrix when the determinant is zero or not finite.
    /// </summary>
    public bool TryInvert(out Matrix2x2D inverse)
    {
        double det = Determinant();
        if (det == 0 || !double.IsFinite(det))
        {
            inverse = default;
            return false;
        }
        inverse = new(M22 / det, -M12 / det, -M21 / det, M11 / det);
        return true;
    }

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public Matrix2x2F ToSingle() => new((float)M11, (float)M12, (float)M21, (float)M22);

    /// <summary>Value equality that treats NaN as equal to itself (and +0 as equal to -0).</summary>
    public bool Equals(Matrix2x2D other) => M11.Equals(other.M11) && M12.Equals(other.M12) && M21.Equals(other.M21) && M22.Equals(other.M22);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Matrix2x2D m && Equals(m);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Scalar.Bits(M11), Scalar.Bits(M12), Scalar.Bits(M21), Scalar.Bits(M22));

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"[{M11:R}, {M12:R}; {M21:R}, {M22:R}]");
}
