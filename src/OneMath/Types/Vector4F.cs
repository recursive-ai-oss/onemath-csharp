using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RecursiveAI.OneMath;

/// <summary>A 4D vector of <see cref="float"/> components. Every operation uses only IEEE-exact <c>+ - * /</c> and square root, in the order its documentation states.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Vector4F : IEquatable<Vector4F>
{
    /// <summary>The X component.</summary>
    public readonly float X;

    /// <summary>The Y component.</summary>
    public readonly float Y;

    /// <summary>The Z component.</summary>
    public readonly float Z;

    /// <summary>The W component.</summary>
    public readonly float W;

    /// <summary>Creates a vector from its components.</summary>
    public Vector4F(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>Creates a vector from a 3D vector and a W component.</summary>
    public Vector4F(Vector3F xyz, float w)
    {
        X = xyz.X;
        Y = xyz.Y;
        Z = xyz.Z;
        W = w;
    }

    /// <summary>The zero vector.</summary>
    public static Vector4F Zero => default;

    /// <summary>The vector (1, 1, 1, 1).</summary>
    public static Vector4F One => new(1, 1, 1, 1);

    /// <summary>The X, Y and Z components as a 3D vector.</summary>
    public Vector3F XYZ => new(X, Y, Z);

    /// <summary>Returns the component at <paramref name="index"/> (0 = X, 1 = Y, 2 = Z, 3 = W). Throws <see cref="ArgumentOutOfRangeException"/> for any other index.</summary>
    public float this[int index] => index switch
    {
        0 => X,
        1 => Y,
        2 => Z,
        3 => W,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0 to 3."),
    };

    /// <summary>Component-wise sum.</summary>
    public static Vector4F operator +(Vector4F a, Vector4F b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);

    /// <summary>Component-wise difference.</summary>
    public static Vector4F operator -(Vector4F a, Vector4F b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);

    /// <summary>Negation of each component.</summary>
    public static Vector4F operator -(Vector4F a) => new(-a.X, -a.Y, -a.Z, -a.W);

    /// <summary>Component-wise product.</summary>
    public static Vector4F operator *(Vector4F a, Vector4F b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W);

    /// <summary>Each component multiplied by <paramref name="s"/>.</summary>
    public static Vector4F operator *(Vector4F a, float s) => new(a.X * s, a.Y * s, a.Z * s, a.W * s);

    /// <summary>Each component multiplied by <paramref name="s"/>.</summary>
    public static Vector4F operator *(float s, Vector4F a) => new(s * a.X, s * a.Y, s * a.Z, s * a.W);

    /// <summary>Each component divided by <paramref name="s"/> (a true division, not a multiplication by the reciprocal).</summary>
    public static Vector4F operator /(Vector4F a, float s) => new(a.X / s, a.Y / s, a.Z / s, a.W / s);

    /// <summary>Component-wise IEEE equality (NaN is unequal to itself; +0 equals -0).</summary>
    public static bool operator ==(Vector4F a, Vector4F b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(Vector4F a, Vector4F b) => !(a == b);

    /// <summary>Dot product, evaluated left to right as <c>((a.X*b.X + a.Y*b.Y) + a.Z*b.Z) + a.W*b.W</c>.</summary>
    public static float Dot(Vector4F a, Vector4F b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    /// <summary>Squared length, <c>((X*X + Y*Y) + Z*Z) + W*W</c>.</summary>
    public float LengthSquared() => X * X + Y * Y + Z * Z + W * W;

    /// <summary>Length: the correctly rounded square root of <see cref="LengthSquared"/>.</summary>
    public float Length() => Scalar.Sqrt(LengthSquared());

    /// <summary>Each component divided by <see cref="Length"/>. Returns <see cref="Zero"/> when the length is zero; other non-finite cases follow IEEE arithmetic.</summary>
    public Vector4F Normalized()
    {
        float len = Length();
        return len == 0 ? default : this / len;
    }

    /// <summary>Linear interpolation, <c>a + (b - a) * t</c> per component (t is not clamped).</summary>
    public static Vector4F Lerp(Vector4F a, Vector4F b, float t) => a + (b - a) * t;

    /// <summary>Component-wise minimum; where a component is NaN in either operand the result is <paramref name="b"/>'s.</summary>
    public static Vector4F Min(Vector4F a, Vector4F b) =>
        new(Scalar.Min(a.X, b.X), Scalar.Min(a.Y, b.Y), Scalar.Min(a.Z, b.Z), Scalar.Min(a.W, b.W));

    /// <summary>Component-wise maximum; where a component is NaN in either operand the result is <paramref name="b"/>'s.</summary>
    public static Vector4F Max(Vector4F a, Vector4F b) =>
        new(Scalar.Max(a.X, b.X), Scalar.Max(a.Y, b.Y), Scalar.Max(a.Z, b.Z), Scalar.Max(a.W, b.W));

    /// <summary>Component-wise absolute value.</summary>
    public Vector4F Abs() => new(Math.Abs(X), Math.Abs(Y), Math.Abs(Z), Math.Abs(W));

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public Vector4D ToDouble() => new((double)X, (double)Y, (double)Z, (double)W);

    /// <summary>Value equality that treats NaN as equal to itself (and +0 as equal to -0).</summary>
    public bool Equals(Vector4F other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Vector4F v && Equals(v);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Scalar.Bits(X), Scalar.Bits(Y), Scalar.Bits(Z), Scalar.Bits(W));

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({X:R}, {Y:R}, {Z:R}, {W:R})");
}
