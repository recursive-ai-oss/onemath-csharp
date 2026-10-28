using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace RecursiveAI.OneMath;

/// <summary>
/// An affine transform of 3D space of <see cref="float"/>: a 3x3 linear part followed by a translation,
/// <c>p -> Linear * p + Translation</c>. (The bottom row of the equivalent 4x4 matrix is always 0, 0, 0, 1.)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Affine3F : IEquatable<Affine3F>
{
    /// <summary>The linear (rotation, scale, shear) part.</summary>
    [JsonInclude]
    public readonly Matrix3x3F Linear;

    /// <summary>The translation applied after the linear part.</summary>
    [JsonInclude]
    public readonly Vector3F Translation;

    /// <summary>Creates a transform from its parts.</summary>
    [JsonConstructor]
    public Affine3F(Matrix3x3F linear, Vector3F translation)
    {
        Linear = linear;
        Translation = translation;
    }

    /// <summary>The identity transform.</summary>
    public static Affine3F Identity => new(Matrix3x3F.Identity, default);

    /// <summary>A pure translation.</summary>
    public static Affine3F CreateTranslation(Vector3F offset) => new(Matrix3x3F.Identity, offset);

    /// <summary>Creates a scaling transform (the diagonal of <see cref="Matrix3x3F.CreateScale"/>, translation zero).</summary>
    public static Affine3F CreateScale(Vector3F scale) => new(Matrix3x3F.CreateScale(scale), default);

    /// <summary>Creates a transform with the given linear part and zero translation.</summary>
    public static Affine3F CreateFromLinear(Matrix3x3F linear) => new(linear, default);

    /// <summary>Creates the rotation of a unit quaternion (<see cref="QuaternionF.ToMatrix"/>), translation zero.</summary>
    public static Affine3F CreateFromQuaternion(QuaternionF rotation) => new(rotation.ToMatrix(), default);

    /// <summary>Rotation about the X axis, translation zero; see <see cref="Matrix3x3F.CreateRotationX"/>.</summary>
    public static Affine3F CreateRotationX(float radians) => new(Matrix3x3F.CreateRotationX(radians), default);

    /// <summary>Rotation about the Y axis, translation zero; see <see cref="Matrix3x3F.CreateRotationY"/>.</summary>
    public static Affine3F CreateRotationY(float radians) => new(Matrix3x3F.CreateRotationY(radians), default);

    /// <summary>Rotation about the Z axis, translation zero; see <see cref="Matrix3x3F.CreateRotationZ"/>.</summary>
    public static Affine3F CreateRotationZ(float radians) => new(Matrix3x3F.CreateRotationZ(radians), default);

    /// <summary>Transforms a point: <c>Linear.Transform(p) + Translation</c>.</summary>
    public Vector3F TransformPoint(Vector3F p) => Linear.Transform(p) + Translation;

    /// <summary>Transforms a direction (translation ignored): <c>Linear.Transform(v)</c>.</summary>
    public Vector3F TransformVector(Vector3F v) => Linear.Transform(v);

    /// <summary>
    /// Composition <c>a * b</c> (apply <paramref name="b"/> first): the linear part is <c>a.Linear * b.Linear</c> and the
    /// translation is <c>a.Linear.Transform(b.Translation) + a.Translation</c>.
    /// </summary>
    public static Affine3F operator *(Affine3F a, Affine3F b) =>
        new(a.Linear * b.Linear, a.Linear.Transform(b.Translation) + a.Translation);

    /// <summary>
    /// The inverse transform: the linear part inverted with <see cref="Matrix3x3F.TryInvert"/> and the translation
    /// <c>-(inverse.Transform(Translation))</c>. Returns false and sets <paramref name="inverse"/> to <see cref="Identity"/> when the linear part is singular.
    /// </summary>
    public bool TryInvert(out Affine3F inverse)
    {
        if (!Linear.TryInvert(out Matrix3x3F inv))
        {
            inverse = Identity;
            return false;
        }
        inverse = new(inv, -inv.Transform(Translation));
        return true;
    }

    /// <summary>The equivalent 4x4 matrix (translation in column 4, bottom row 0, 0, 0, 1).</summary>
    public Matrix4x4F ToMatrix() => new(
        Linear.M11, Linear.M12, Linear.M13, Translation.X,
        Linear.M21, Linear.M22, Linear.M23, Translation.Y,
        Linear.M31, Linear.M32, Linear.M33, Translation.Z,
        0, 0, 0, 1);

    /// <summary>Entry-wise IEEE equality.</summary>
    public static bool operator ==(Affine3F a, Affine3F b) => a.Linear == b.Linear && a.Translation == b.Translation;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(Affine3F a, Affine3F b) => !(a == b);

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public Affine3D ToDouble() => new(Linear.ToDouble(), Translation.ToDouble());

    /// <summary>Value equality that treats NaN as equal to itself.</summary>
    public bool Equals(Affine3F other) => Linear.Equals(other.Linear) && Translation.Equals(other.Translation);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Affine3F a && Equals(a);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Linear.GetHashCode(), Translation.GetHashCode());

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{{{Linear} + {Translation}}}");
}
