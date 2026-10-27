using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RecursiveAI.OneMath;

/// <summary>A 4D vector of <see cref="double"/> components. Every operation uses only IEEE-exact <c>+ - * /</c> and square root, in the order its documentation states.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Vector4D : IEquatable<Vector4D>
{
    /// <summary>The X component.</summary>
    public readonly double X;

    /// <summary>The Y component.</summary>
    public readonly double Y;

    /// <summary>The Z component.</summary>
    public readonly double Z;

    /// <summary>The W component.</summary>
    public readonly double W;

    /// <summary>Creates a vector from its components.</summary>
    public Vector4D(double x, double y, double z, double w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>Creates a vector from a 3D vector and a W component.</summary>
    public Vector4D(Vector3D xyz, double w)
    {
        X = xyz.X;
        Y = xyz.Y;
        Z = xyz.Z;
        W = w;
    }

    /// <summary>The zero vector.</summary>
    public static Vector4D Zero => default;

    /// <summary>The vector (1, 1, 1, 1).</summary>
    public static Vector4D One => new(1, 1, 1, 1);

    /// <summary>The X, Y and Z components as a 3D vector.</summary>
    public Vector3D XYZ => new(X, Y, Z);

    /// <summary>Returns the component at <paramref name="index"/> (0 = X, 1 = Y, 2 = Z, 3 = W). Throws <see cref="ArgumentOutOfRangeException"/> for any other index.</summary>
    public double this[int index] => index switch
    {
        0 => X,
        1 => Y,
        2 => Z,
        3 => W,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0 to 3."),
    };

    /// <summary>Component-wise sum.</summary>
    public static Vector4D operator +(Vector4D a, Vector4D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);

    /// <summary>Component-wise difference.</summary>
    public static Vector4D operator -(Vector4D a, Vector4D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);

    /// <summary>Negation of each component.</summary>
    public static Vector4D operator -(Vector4D a) => new(-a.X, -a.Y, -a.Z, -a.W);

    /// <summary>Component-wise product.</summary>
    public static Vector4D operator *(Vector4D a, Vector4D b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W);

    /// <summary>Each component multiplied by <paramref name="s"/>.</summary>
    public static Vector4D operator *(Vector4D a, double s) => new(a.X * s, a.Y * s, a.Z * s, a.W * s);

    /// <summary>Each component multiplied by <paramref name="s"/>.</summary>
    public static Vector4D operator *(double s, Vector4D a) => new(s * a.X, s * a.Y, s * a.Z, s * a.W);

    /// <summary>Each component divided by <paramref name="s"/> (a true division, not a multiplication by the reciprocal).</summary>
    public static Vector4D operator /(Vector4D a, double s) => new(a.X / s, a.Y / s, a.Z / s, a.W / s);

    /// <summary>Component-wise IEEE equality (NaN is unequal to itself; +0 equals -0).</summary>
    public static bool operator ==(Vector4D a, Vector4D b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(Vector4D a, Vector4D b) => !(a == b);

    /// <summary>Dot product, evaluated left to right as <c>((a.X*b.X + a.Y*b.Y) + a.Z*b.Z) + a.W*b.W</c>.</summary>
    public static double Dot(Vector4D a, Vector4D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    /// <summary>Squared length, <c>((X*X + Y*Y) + Z*Z) + W*W</c>.</summary>
    public double LengthSquared() => X * X + Y * Y + Z * Z + W * W;

    /// <summary>Length: the correctly rounded square root of <see cref="LengthSquared"/>.</summary>
    public double Length() => Scalar.Sqrt(LengthSquared());

    /// <summary>Each component divided by <see cref="Length"/>. Returns <see cref="Zero"/> when the length is zero; other non-finite cases follow IEEE arithmetic.</summary>
    public Vector4D Normalized()
    {
        double len = Length();
        return len == 0 ? default : this / len;
    }

    /// <summary>Linear interpolation, <c>a + (b - a) * t</c> per component (t is not clamped).</summary>
    public static Vector4D Lerp(Vector4D a, Vector4D b, double t) => a + (b - a) * t;

    /// <summary>Component-wise minimum; where a component is NaN in either operand the result is <paramref name="b"/>'s.</summary>
    public static Vector4D Min(Vector4D a, Vector4D b) =>
        new(Scalar.Min(a.X, b.X), Scalar.Min(a.Y, b.Y), Scalar.Min(a.Z, b.Z), Scalar.Min(a.W, b.W));

    /// <summary>Component-wise maximum; where a component is NaN in either operand the result is <paramref name="b"/>'s.</summary>
    public static Vector4D Max(Vector4D a, Vector4D b) =>
        new(Scalar.Max(a.X, b.X), Scalar.Max(a.Y, b.Y), Scalar.Max(a.Z, b.Z), Scalar.Max(a.W, b.W));

    /// <summary>Component-wise absolute value.</summary>
    public Vector4D Abs() => new(Math.Abs(X), Math.Abs(Y), Math.Abs(Z), Math.Abs(W));

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public Vector4F ToSingle() => new((float)X, (float)Y, (float)Z, (float)W);

    /// <summary>Value equality that treats NaN as equal to itself (and +0 as equal to -0).</summary>
    public bool Equals(Vector4D other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Vector4D v && Equals(v);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Scalar.Bits(X), Scalar.Bits(Y), Scalar.Bits(Z), Scalar.Bits(W));

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({X:R}, {Y:R}, {Z:R}, {W:R})");
}
