using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RecursiveAI.OneMath;

/// <summary>
/// An axis-aligned 2D box of <see cref="float"/> with inclusive edges. A box is empty when any <c>Min</c> component
/// exceeds the matching <c>Max</c> component; <see cref="Empty"/> is the identity of <see cref="Union"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Bounds2F : IEquatable<Bounds2F>
{
    /// <summary>The corner with the smallest coordinates.</summary>
    public readonly Vector2F Min;

    /// <summary>The corner with the largest coordinates.</summary>
    public readonly Vector2F Max;

    /// <summary>Creates a box from two corners as given (no sorting; use <see cref="FromPoints"/> for unordered input).</summary>
    public Bounds2F(Vector2F min, Vector2F max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>The empty box (Min = +infinity, Max = -infinity): it contains and intersects nothing and is the identity of <see cref="Union"/>.</summary>
    public static Bounds2F Empty => new(
        new(float.PositiveInfinity, float.PositiveInfinity),
        new(float.NegativeInfinity, float.NegativeInfinity));

    /// <summary>A box from its center and half-sizes: <c>(center - extents, center + extents)</c>.</summary>
    public static Bounds2F FromCenterExtents(Vector2F center, Vector2F extents) => new(center - extents, center + extents);

    /// <summary>The smallest box containing all points, folded in index order; <see cref="Empty"/> for an empty span.</summary>
    public static Bounds2F FromPoints(ReadOnlySpan<Vector2F> points)
    {
        Bounds2F b = Empty;
        foreach (Vector2F p in points)
        {
            b = b.Encapsulate(p);
        }
        return b;
    }

    /// <summary>True when any component of <see cref="Min"/> exceeds the matching component of <see cref="Max"/>.</summary>
    public bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y;

    /// <summary>The size, <c>Max - Min</c>.</summary>
    public Vector2F Size => Max - Min;

    /// <summary>The half-size, <c>(Max - Min) * 0.5</c> (exact scaling by a power of two).</summary>
    public Vector2F Extents => (Max - Min) * (float)0.5;

    /// <summary>The center, <c>Min + (Max - Min) * 0.5</c>.</summary>
    public Vector2F Center => Min + (Max - Min) * (float)0.5;

    /// <summary>Area, <c>Size.X * Size.Y</c>; zero for an empty box.</summary>
    public float Area()
    {
        if (IsEmpty)
        {
            return 0;
        }
        Vector2F s = Max - Min;
        return s.X * s.Y;
    }

    /// <summary>True when the point lies inside or on the boundary.</summary>
    public bool Contains(Vector2F p) => p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y;

    /// <summary>True when <paramref name="other"/> lies entirely inside this box (boundary contact counts); false if <paramref name="other"/> is empty.</summary>
    public bool Contains(Bounds2F other) => !other.IsEmpty &&
        other.Min.X >= Min.X && other.Max.X <= Max.X &&
        other.Min.Y >= Min.Y && other.Max.Y <= Max.Y;

    /// <summary>True when the boxes share at least one point (touching edges count); never true for an empty box.</summary>
    public bool Intersects(Bounds2F other) =>
        Min.X <= other.Max.X && other.Min.X <= Max.X &&
        Min.Y <= other.Max.Y && other.Min.Y <= Max.Y;

    /// <summary>The smallest box containing both (component-wise minimum of the minima and maximum of the maxima).</summary>
    public static Bounds2F Union(Bounds2F a, Bounds2F b) => new(Vector2F.Min(a.Min, b.Min), Vector2F.Max(a.Max, b.Max));

    /// <summary>The overlap of both boxes, which is empty (see <see cref="IsEmpty"/>) when they do not intersect.</summary>
    public static Bounds2F Intersection(Bounds2F a, Bounds2F b) => new(Vector2F.Max(a.Min, b.Min), Vector2F.Min(a.Max, b.Max));

    /// <summary>The smallest box containing this box and the point.</summary>
    public Bounds2F Encapsulate(Vector2F p) => new(Vector2F.Min(Min, p), Vector2F.Max(Max, p));

    /// <summary>The box grown by <paramref name="amount"/> on every side (shrunk when negative).</summary>
    public Bounds2F Inflate(float amount)
    {
        Vector2F a = new(amount, amount);
        return new(Min - a, Max + a);
    }

    /// <summary>Component-wise IEEE equality.</summary>
    public static bool operator ==(Bounds2F a, Bounds2F b) => a.Min == b.Min && a.Max == b.Max;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(Bounds2F a, Bounds2F b) => !(a == b);

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public Bounds2D ToDouble() => new(Min.ToDouble(), Max.ToDouble());

    /// <summary>Value equality that treats NaN as equal to itself.</summary>
    public bool Equals(Bounds2F other) => Min.Equals(other.Min) && Max.Equals(other.Max);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Bounds2F b && Equals(b);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Min.GetHashCode(), Max.GetHashCode());

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"[{Min} .. {Max}]");
}
