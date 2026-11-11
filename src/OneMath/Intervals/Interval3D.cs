using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace RecursiveAI.OneMath;

/// <summary>
/// An axis-aligned box of <see cref="IntervalD"/> components: the set of vectors whose X, Y and Z lie in the three
/// intervals. It is the argument type of a distance-field evaluator that prunes space with interval arithmetic. Every
/// operation encloses the exact result for all vectors in the operands (see <see cref="IntervalD"/>). The box is empty when
/// any component is empty; <c>default(Interval3D)</c> is the point (0, 0, 0). Because components are treated independently,
/// results such as <c>Dot(v, v)</c> are enclosures and can be wider than the true range; use <see cref="LengthSquared"/>
/// (which squares each component tightly) for a vector's squared length.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Interval3D : IEquatable<Interval3D>
{
    /// <summary>The X component interval.</summary>
    [JsonInclude]
    public readonly IntervalD X;

    /// <summary>The Y component interval.</summary>
    [JsonInclude]
    public readonly IntervalD Y;

    /// <summary>The Z component interval.</summary>
    [JsonInclude]
    public readonly IntervalD Z;

    /// <summary>Creates a box from its component intervals.</summary>
    [JsonConstructor]
    public Interval3D(IntervalD x, IntervalD y, IntervalD z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>Creates the box containing exactly one point. Throws <see cref="ArgumentOutOfRangeException"/> if a component is NaN or infinite.</summary>
    public Interval3D(Vector3D point)
        : this(new IntervalD(point.X), new IntervalD(point.Y), new IntervalD(point.Z))
    {
    }

    /// <summary>Creates the box covering <paramref name="bounds"/>. Throws if the bounds are empty (Min above Max), NaN or infinite in the way <see cref="IntervalD(double, double)"/> does.</summary>
    public static Interval3D FromBounds(Bounds3D bounds) => new(
        new IntervalD(bounds.Min.X, bounds.Max.X),
        new IntervalD(bounds.Min.Y, bounds.Max.Y),
        new IntervalD(bounds.Min.Z, bounds.Max.Z));

    /// <summary>The empty box.</summary>
    public static Interval3D Empty => new(IntervalD.Empty, IntervalD.Empty, IntervalD.Empty);

    /// <summary>True when any component is empty.</summary>
    [JsonIgnore]
    public bool IsEmpty => X.IsEmpty || Y.IsEmpty || Z.IsEmpty;

    /// <summary>The component at <paramref name="index"/> (0 = X, 1 = Y, 2 = Z). Throws <see cref="ArgumentOutOfRangeException"/> for any other index.</summary>
    public IntervalD this[int index] => index switch
    {
        0 => X,
        1 => Y,
        2 => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0, 1 or 2."),
    };

    /// <summary>True when the vector lies in the box.</summary>
    public bool Contains(Vector3D v) => X.Contains(v.X) && Y.Contains(v.Y) && Z.Contains(v.Z);

    /// <summary>The smallest box containing both operands (an empty operand is ignored).</summary>
    public static Interval3D Hull(Interval3D a, Interval3D b)
    {
        if (a.IsEmpty)
        {
            return b;
        }
        if (b.IsEmpty)
        {
            return a;
        }
        return new(IntervalD.Hull(a.X, b.X), IntervalD.Hull(a.Y, b.Y), IntervalD.Hull(a.Z, b.Z));
    }

    /// <summary>Component-wise sum.</summary>
    public static Interval3D operator +(Interval3D a, Interval3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>Component-wise difference.</summary>
    public static Interval3D operator -(Interval3D a, Interval3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>Negation of each component (exact).</summary>
    public static Interval3D operator -(Interval3D a) => new(-a.X, -a.Y, -a.Z);

    /// <summary>Sum with a point vector. Throws if a component of <paramref name="b"/> is NaN or infinite.</summary>
    public static Interval3D operator +(Interval3D a, Vector3D b) => a + new Interval3D(b);

    /// <summary>Difference with a point vector. Throws if a component of <paramref name="b"/> is NaN or infinite.</summary>
    public static Interval3D operator -(Interval3D a, Vector3D b) => a - new Interval3D(b);

    /// <summary>Each component multiplied by the scalar <paramref name="s"/>. Throws if <paramref name="s"/> is NaN or infinite.</summary>
    public static Interval3D operator *(Interval3D a, double s)
    {
        var si = new IntervalD(s);
        return new(a.X * si, a.Y * si, a.Z * si);
    }

    /// <summary>Each component multiplied by the scalar <paramref name="s"/>. Throws if <paramref name="s"/> is NaN or infinite.</summary>
    public static Interval3D operator *(double s, Interval3D a) => a * s;

    /// <summary>Each component multiplied by the interval <paramref name="s"/>.</summary>
    public static Interval3D operator *(Interval3D a, IntervalD s) => new(a.X * s, a.Y * s, a.Z * s);

    /// <summary>Component-wise product.</summary>
    public static Interval3D operator *(Interval3D a, Interval3D b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    /// <summary>The dot product <c>a.X*b.X + a.Y*b.Y + a.Z*b.Z</c>, summed in that order (an enclosure; components are independent).</summary>
    public static IntervalD Dot(Interval3D a, Interval3D b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    /// <summary>The squared length: the sum of the tight squares of the components. Never below 0.</summary>
    public IntervalD LengthSquared() => IntervalD.Square(X) + IntervalD.Square(Y) + IntervalD.Square(Z);

    /// <summary>The length, an enclosure of sqrt(x²+y²+z²) over the box (the square root of <see cref="LengthSquared"/>).</summary>
    public IntervalD Length() => IntervalD.Sqrt(LengthSquared());

    /// <summary>Component-wise absolute value (exact).</summary>
    public static Interval3D Abs(Interval3D a) => new(IntervalD.Abs(a.X), IntervalD.Abs(a.Y), IntervalD.Abs(a.Z));

    /// <summary>Component-wise minimum (exact).</summary>
    public static Interval3D Min(Interval3D a, Interval3D b) => new(IntervalD.Min(a.X, b.X), IntervalD.Min(a.Y, b.Y), IntervalD.Min(a.Z, b.Z));

    /// <summary>Component-wise maximum (exact).</summary>
    public static Interval3D Max(Interval3D a, Interval3D b) => new(IntervalD.Max(a.X, b.X), IntervalD.Max(a.Y, b.Y), IntervalD.Max(a.Z, b.Z));

    /// <summary>True when all three components are equal as intervals.</summary>
    public static bool operator ==(Interval3D a, Interval3D b) => a.Equals(b);

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(Interval3D a, Interval3D b) => !a.Equals(b);

    /// <summary>Value equality of the three components.</summary>
    public bool Equals(Interval3D other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Interval3D v && Equals(v);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(X.GetHashCode(), Y.GetHashCode(), Z.GetHashCode());

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({X}, {Y}, {Z})");
}
