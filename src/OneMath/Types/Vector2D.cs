using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace RecursiveAI.OneMath;

/// <summary>A 2D vector of <see cref="double"/> components. Every operation uses only IEEE-exact <c>+ - * /</c> and square root, in the order its documentation states.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Vector2D : IEquatable<Vector2D>
{
    /// <summary>The X component.</summary>
    [JsonInclude]
    public readonly double X;

    /// <summary>The Y component.</summary>
    [JsonInclude]
    public readonly double Y;

    /// <summary>Creates a vector from its components.</summary>
    [JsonConstructor]
    public Vector2D(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <summary>The zero vector.</summary>
    public static Vector2D Zero => default;

    /// <summary>The vector (1, 1).</summary>
    public static Vector2D One => new(1, 1);

    /// <summary>The vector (1, 0).</summary>
    public static Vector2D UnitX => new(1, 0);

    /// <summary>The vector (0, 1).</summary>
    public static Vector2D UnitY => new(0, 1);

    /// <summary>Returns the component at <paramref name="index"/> (0 = X, 1 = Y). Throws <see cref="ArgumentOutOfRangeException"/> for any other index.</summary>
    public double this[int index] => index switch
    {
        0 => X,
        1 => Y,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0 or 1."),
    };

    /// <summary>Component-wise sum.</summary>
    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>Component-wise difference.</summary>
    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>Negation of each component.</summary>
    public static Vector2D operator -(Vector2D a) => new(-a.X, -a.Y);

    /// <summary>Component-wise product.</summary>
    public static Vector2D operator *(Vector2D a, Vector2D b) => new(a.X * b.X, a.Y * b.Y);

    /// <summary>Each component multiplied by <paramref name="s"/>.</summary>
    public static Vector2D operator *(Vector2D a, double s) => new(a.X * s, a.Y * s);

    /// <summary>Each component multiplied by <paramref name="s"/>.</summary>
    public static Vector2D operator *(double s, Vector2D a) => new(s * a.X, s * a.Y);

    /// <summary>Each component divided by <paramref name="s"/> (a true division, not a multiplication by the reciprocal).</summary>
    public static Vector2D operator /(Vector2D a, double s) => new(a.X / s, a.Y / s);

    /// <summary>Component-wise IEEE equality (NaN is unequal to itself; +0 equals -0).</summary>
    public static bool operator ==(Vector2D a, Vector2D b) => a.X == b.X && a.Y == b.Y;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(Vector2D a, Vector2D b) => !(a == b);

    /// <summary>Dot product, evaluated as <c>a.X*b.X + a.Y*b.Y</c>.</summary>
    public static double Dot(Vector2D a, Vector2D b) => a.X * b.X + a.Y * b.Y;

    /// <summary>The z component of the 3D cross product, <c>a.X*b.Y - a.Y*b.X</c>.</summary>
    public static double Cross(Vector2D a, Vector2D b) => a.X * b.Y - a.Y * b.X;

    /// <summary>Squared length, <c>X*X + Y*Y</c>.</summary>
    public double LengthSquared() => X * X + Y * Y;

    /// <summary>Length: the correctly rounded square root of <see cref="LengthSquared"/>.</summary>
    public double Length() => Scalar.Sqrt(LengthSquared());

    /// <summary>Squared distance between two points, <c>(a - b).LengthSquared()</c>.</summary>
    public static double DistanceSquared(Vector2D a, Vector2D b) => (a - b).LengthSquared();

    /// <summary>Distance between two points, <c>(a - b).Length()</c>.</summary>
    public static double Distance(Vector2D a, Vector2D b) => (a - b).Length();

    /// <summary>Each component divided by <see cref="Length"/>. Returns <see cref="Zero"/> when the length is zero; other non-finite cases follow IEEE arithmetic.</summary>
    public Vector2D Normalized()
    {
        double len = Length();
        return len == 0 ? default : this / len;
    }

    /// <summary>Linear interpolation, <c>a + (b - a) * t</c> per component (t is not clamped).</summary>
    public static Vector2D Lerp(Vector2D a, Vector2D b, double t) => a + (b - a) * t;

    /// <summary>Component-wise minimum; where a component is NaN in either operand the result is <paramref name="b"/>'s.</summary>
    public static Vector2D Min(Vector2D a, Vector2D b) => new(Scalar.Min(a.X, b.X), Scalar.Min(a.Y, b.Y));

    /// <summary>Component-wise maximum; where a component is NaN in either operand the result is <paramref name="b"/>'s.</summary>
    public static Vector2D Max(Vector2D a, Vector2D b) => new(Scalar.Max(a.X, b.X), Scalar.Max(a.Y, b.Y));

    /// <summary>Component-wise absolute value.</summary>
    public Vector2D Abs() => new(Math.Abs(X), Math.Abs(Y));

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public Vector2F ToSingle() => new((float)X, (float)Y);

    /// <summary>Value equality that treats NaN as equal to itself (and +0 as equal to -0).</summary>
    public bool Equals(Vector2D other) => X.Equals(other.X) && Y.Equals(other.Y);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Vector2D v && Equals(v);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Scalar.Bits(X), Scalar.Bits(Y));

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({X:R}, {Y:R})");


    /// <summary>Converts from a <see cref="System.Numerics.Vector2"/> (exact).</summary>
    public static Vector2D FromSystemNumerics(System.Numerics.Vector2 v) => new(v.X, v.Y);

    /// <summary>Converts to a <see cref="System.Numerics.Vector2"/>; each component is narrowed to float32 (round to nearest even).</summary>
    public System.Numerics.Vector2 ToSystemNumerics() => new((float)X, (float)Y);
}
