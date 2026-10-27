using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RecursiveAI.OneMath;

/// <summary>A 3D vector of <see cref="double"/> components. Every operation uses only IEEE-exact <c>+ - * /</c> and square root, in the order its documentation states.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Vector3D : IEquatable<Vector3D>
{
    /// <summary>The X component.</summary>
    public readonly double X;

    /// <summary>The Y component.</summary>
    public readonly double Y;

    /// <summary>The Z component.</summary>
    public readonly double Z;

    /// <summary>Creates a vector from its components.</summary>
    public Vector3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>The zero vector.</summary>
    public static Vector3D Zero => default;

    /// <summary>The vector (1, 1, 1).</summary>
    public static Vector3D One => new(1, 1, 1);

    /// <summary>The vector (1, 0, 0).</summary>
    public static Vector3D UnitX => new(1, 0, 0);

    /// <summary>The vector (0, 1, 0).</summary>
    public static Vector3D UnitY => new(0, 1, 0);

    /// <summary>The vector (0, 0, 1).</summary>
    public static Vector3D UnitZ => new(0, 0, 1);

    /// <summary>Returns the component at <paramref name="index"/> (0 = X, 1 = Y, 2 = Z). Throws <see cref="ArgumentOutOfRangeException"/> for any other index.</summary>
    public double this[int index] => index switch
    {
        0 => X,
        1 => Y,
        2 => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0, 1 or 2."),
    };

    /// <summary>Component-wise sum.</summary>
    public static Vector3D operator +(Vector3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>Component-wise difference.</summary>
    public static Vector3D operator -(Vector3D a, Vector3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>Negation of each component.</summary>
    public static Vector3D operator -(Vector3D a) => new(-a.X, -a.Y, -a.Z);

    /// <summary>Component-wise product.</summary>
    public static Vector3D operator *(Vector3D a, Vector3D b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    /// <summary>Each component multiplied by <paramref name="s"/>.</summary>
    public static Vector3D operator *(Vector3D a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    /// <summary>Each component multiplied by <paramref name="s"/>.</summary>
    public static Vector3D operator *(double s, Vector3D a) => new(s * a.X, s * a.Y, s * a.Z);

    /// <summary>Each component divided by <paramref name="s"/> (a true division, not a multiplication by the reciprocal).</summary>
    public static Vector3D operator /(Vector3D a, double s) => new(a.X / s, a.Y / s, a.Z / s);

    /// <summary>Component-wise IEEE equality (NaN is unequal to itself; +0 equals -0).</summary>
    public static bool operator ==(Vector3D a, Vector3D b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(Vector3D a, Vector3D b) => !(a == b);

    /// <summary>Dot product, evaluated left to right as <c>(a.X*b.X + a.Y*b.Y) + a.Z*b.Z</c>.</summary>
    public static double Dot(Vector3D a, Vector3D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    /// <summary>Cross product: <c>(a.Y*b.Z - a.Z*b.Y, a.Z*b.X - a.X*b.Z, a.X*b.Y - a.Y*b.X)</c>, each product rounded separately.</summary>
    public static Vector3D Cross(Vector3D a, Vector3D b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    /// <summary>Squared length, <c>(X*X + Y*Y) + Z*Z</c>.</summary>
    public double LengthSquared() => X * X + Y * Y + Z * Z;

    /// <summary>Length: the correctly rounded square root of <see cref="LengthSquared"/>.</summary>
    public double Length() => Scalar.Sqrt(LengthSquared());

    /// <summary>Squared distance between two points, <c>(a - b).LengthSquared()</c>.</summary>
    public static double DistanceSquared(Vector3D a, Vector3D b) => (a - b).LengthSquared();

    /// <summary>Distance between two points, <c>(a - b).Length()</c>.</summary>
    public static double Distance(Vector3D a, Vector3D b) => (a - b).Length();

    /// <summary>Each component divided by <see cref="Length"/>. Returns <see cref="Zero"/> when the length is zero; other non-finite cases follow IEEE arithmetic.</summary>
    public Vector3D Normalized()
    {
        double len = Length();
        return len == 0 ? default : this / len;
    }

    /// <summary>Linear interpolation, <c>a + (b - a) * t</c> per component (t is not clamped).</summary>
    public static Vector3D Lerp(Vector3D a, Vector3D b, double t) => a + (b - a) * t;

    /// <summary>Component-wise minimum; where a component is NaN in either operand the result is <paramref name="b"/>'s.</summary>
    public static Vector3D Min(Vector3D a, Vector3D b) => new(Scalar.Min(a.X, b.X), Scalar.Min(a.Y, b.Y), Scalar.Min(a.Z, b.Z));

    /// <summary>Component-wise maximum; where a component is NaN in either operand the result is <paramref name="b"/>'s.</summary>
    public static Vector3D Max(Vector3D a, Vector3D b) => new(Scalar.Max(a.X, b.X), Scalar.Max(a.Y, b.Y), Scalar.Max(a.Z, b.Z));

    /// <summary>Component-wise absolute value.</summary>
    public Vector3D Abs() => new(Math.Abs(X), Math.Abs(Y), Math.Abs(Z));

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public Vector3F ToSingle() => new((float)X, (float)Y, (float)Z);

    /// <summary>Value equality that treats NaN as equal to itself (and +0 as equal to -0).</summary>
    public bool Equals(Vector3D other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Vector3D v && Equals(v);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Scalar.Bits(X), Scalar.Bits(Y), Scalar.Bits(Z));

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({X:R}, {Y:R}, {Z:R})");
}
