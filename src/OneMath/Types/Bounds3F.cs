using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace RecursiveAI.OneMath;

/// <summary>
/// An axis-aligned 3D box of <see cref="float"/> with inclusive faces. A box is empty when any <c>Min</c> component
/// exceeds the matching <c>Max</c> component; <see cref="Empty"/> is the identity of <see cref="Union"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Bounds3F : IEquatable<Bounds3F>
{
    /// <summary>The corner with the smallest coordinates.</summary>
    [JsonInclude]
    public readonly Vector3F Min;

    /// <summary>The corner with the largest coordinates.</summary>
    [JsonInclude]
    public readonly Vector3F Max;

    /// <summary>Creates a box from two corners as given (no sorting; use <see cref="CreateFromPoints"/> for unordered input).</summary>
    [JsonConstructor]
    public Bounds3F(Vector3F min, Vector3F max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>The empty box (Min = +infinity, Max = -infinity): it contains and intersects nothing and is the identity of <see cref="Union"/>.</summary>
    public static Bounds3F Empty => new(
        new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
        new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity));

    /// <summary>A box from its center and half-sizes: <c>(center - extents, center + extents)</c>.</summary>
    public static Bounds3F CreateFromCenterExtents(Vector3F center, Vector3F extents) => new(center - extents, center + extents);

    /// <summary>The smallest box containing all points, folded in index order; <see cref="Empty"/> for an empty span.</summary>
    public static Bounds3F CreateFromPoints(ReadOnlySpan<Vector3F> points)
    {
        Bounds3F b = Empty;
        foreach (Vector3F p in points)
        {
            b = b.Encapsulate(p);
        }
        return b;
    }

    /// <summary>True when any component of <see cref="Min"/> exceeds the matching component of <see cref="Max"/>.</summary>
    [JsonIgnore]
    public bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;

    /// <summary>The size, <c>Max - Min</c>.</summary>
    [JsonIgnore]
    public Vector3F Size => Max - Min;

    /// <summary>The half-size, <c>(Max - Min) * 0.5</c> (exact scaling by a power of two).</summary>
    [JsonIgnore]
    public Vector3F Extents => (Max - Min) * (float)0.5;

    /// <summary>The center, <c>Min + (Max - Min) * 0.5</c>.</summary>
    [JsonIgnore]
    public Vector3F Center => Min + (Max - Min) * (float)0.5;

    /// <summary>Volume, <c>(Size.X * Size.Y) * Size.Z</c>; zero for an empty box.</summary>
    public float Volume()
    {
        if (IsEmpty)
        {
            return 0;
        }
        Vector3F s = Max - Min;
        return s.X * s.Y * s.Z;
    }

    /// <summary>Surface area, <c>2 * ((sx*sy + sy*sz) + sz*sx)</c> with s = Size; zero for an empty box.</summary>
    public float SurfaceArea()
    {
        if (IsEmpty)
        {
            return 0;
        }
        Vector3F s = Max - Min;
        return 2 * (s.X * s.Y + s.Y * s.Z + s.Z * s.X);
    }

    /// <summary>True when the point lies inside or on the boundary.</summary>
    public bool Contains(Vector3F p) =>
        p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;

    /// <summary>True when <paramref name="other"/> lies entirely inside this box (boundary contact counts); false if <paramref name="other"/> is empty.</summary>
    public bool Contains(Bounds3F other) => !other.IsEmpty &&
        other.Min.X >= Min.X && other.Max.X <= Max.X &&
        other.Min.Y >= Min.Y && other.Max.Y <= Max.Y &&
        other.Min.Z >= Min.Z && other.Max.Z <= Max.Z;

    /// <summary>True when the boxes share at least one point (touching faces count); never true for an empty box.</summary>
    public bool Intersects(Bounds3F other) =>
        Min.X <= other.Max.X && other.Min.X <= Max.X &&
        Min.Y <= other.Max.Y && other.Min.Y <= Max.Y &&
        Min.Z <= other.Max.Z && other.Min.Z <= Max.Z;

    /// <summary>The smallest box containing both (component-wise minimum of the minima and maximum of the maxima).</summary>
    public static Bounds3F Union(Bounds3F a, Bounds3F b) => new(Vector3F.Min(a.Min, b.Min), Vector3F.Max(a.Max, b.Max));

    /// <summary>The overlap of both boxes, which is empty (see <see cref="IsEmpty"/>) when they do not intersect.</summary>
    public static Bounds3F Intersection(Bounds3F a, Bounds3F b) => new(Vector3F.Max(a.Min, b.Min), Vector3F.Min(a.Max, b.Max));

    /// <summary>The smallest box containing this box and the point.</summary>
    public Bounds3F Encapsulate(Vector3F p) => new(Vector3F.Min(Min, p), Vector3F.Max(Max, p));

    /// <summary>The box grown by <paramref name="amount"/> on every side (shrunk when negative).</summary>
    public Bounds3F Inflate(float amount)
    {
        Vector3F a = new(amount, amount, amount);
        return new(Min - a, Max + a);
    }

    /// <summary>Component-wise IEEE equality.</summary>
    public static bool operator ==(Bounds3F a, Bounds3F b) => a.Min == b.Min && a.Max == b.Max;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(Bounds3F a, Bounds3F b) => !(a == b);

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public Bounds3D ToDouble() => new(Min.ToDouble(), Max.ToDouble());

    /// <summary>Value equality that treats NaN as equal to itself.</summary>
    public bool Equals(Bounds3F other) => Min.Equals(other.Min) && Max.Equals(other.Max);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Bounds3F b && Equals(b);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Min.GetHashCode(), Max.GetHashCode());

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"[{Min} .. {Max}]");
}
