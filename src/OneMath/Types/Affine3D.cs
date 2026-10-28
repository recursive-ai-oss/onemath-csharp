using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace RecursiveAI.OneMath;

/// <summary>
/// An affine transform of 3D space of <see cref="double"/>: a 3x3 linear part followed by a translation,
/// <c>p -> Linear * p + Translation</c>. (The bottom row of the equivalent 4x4 matrix is always 0, 0, 0, 1.)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Affine3D : IEquatable<Affine3D>
{
    /// <summary>The linear (rotation, scale, shear) part.</summary>
    [JsonInclude]
    public readonly Matrix3x3D Linear;

    /// <summary>The translation applied after the linear part.</summary>
    [JsonInclude]
    public readonly Vector3D Translation;

    /// <summary>Creates a transform from its parts.</summary>
    [JsonConstructor]
    public Affine3D(Matrix3x3D linear, Vector3D translation)
    {
        Linear = linear;
        Translation = translation;
    }

    /// <summary>The identity transform.</summary>
    public static Affine3D Identity => new(Matrix3x3D.Identity, default);

    /// <summary>A pure translation.</summary>
    public static Affine3D CreateTranslation(Vector3D offset) => new(Matrix3x3D.Identity, offset);

    /// <summary>Creates a scaling transform (the diagonal of <see cref="Matrix3x3D.CreateScale"/>, translation zero).</summary>
    public static Affine3D CreateScale(Vector3D scale) => new(Matrix3x3D.CreateScale(scale), default);

    /// <summary>Creates a transform with the given linear part and zero translation.</summary>
    public static Affine3D CreateFromLinear(Matrix3x3D linear) => new(linear, default);

    /// <summary>Creates the rotation of a unit quaternion (<see cref="QuaternionD.ToMatrix"/>), translation zero.</summary>
    public static Affine3D CreateFromQuaternion(QuaternionD rotation) => new(rotation.ToMatrix(), default);

    /// <summary>Rotation about the X axis, translation zero; see <see cref="Matrix3x3D.CreateRotationX"/>.</summary>
    public static Affine3D CreateRotationX(double radians) => new(Matrix3x3D.CreateRotationX(radians), default);

    /// <summary>Rotation about the Y axis, translation zero; see <see cref="Matrix3x3D.CreateRotationY"/>.</summary>
    public static Affine3D CreateRotationY(double radians) => new(Matrix3x3D.CreateRotationY(radians), default);

    /// <summary>Rotation about the Z axis, translation zero; see <see cref="Matrix3x3D.CreateRotationZ"/>.</summary>
    public static Affine3D CreateRotationZ(double radians) => new(Matrix3x3D.CreateRotationZ(radians), default);

    /// <summary>Transforms a point: <c>Linear.Transform(p) + Translation</c>.</summary>
    public Vector3D TransformPoint(Vector3D p) => Linear.Transform(p) + Translation;

    /// <summary>Transforms a direction (translation ignored): <c>Linear.Transform(v)</c>.</summary>
    public Vector3D TransformVector(Vector3D v) => Linear.Transform(v);

    /// <summary>
    /// Composition <c>a * b</c> (apply <paramref name="b"/> first): the linear part is <c>a.Linear * b.Linear</c> and the
    /// translation is <c>a.Linear.Transform(b.Translation) + a.Translation</c>.
    /// </summary>
    public static Affine3D operator *(Affine3D a, Affine3D b) =>
        new(a.Linear * b.Linear, a.Linear.Transform(b.Translation) + a.Translation);

    /// <summary>
    /// The inverse transform: the linear part inverted with <see cref="Matrix3x3D.TryInvert"/> and the translation
    /// <c>-(inverse.Transform(Translation))</c>. Returns false and sets <paramref name="inverse"/> to <see cref="Identity"/> when the linear part is singular.
    /// </summary>
    public bool TryInvert(out Affine3D inverse)
    {
        if (!Linear.TryInvert(out Matrix3x3D inv))
        {
            inverse = Identity;
            return false;
        }
        inverse = new(inv, -inv.Transform(Translation));
        return true;
    }

    /// <summary>The equivalent 4x4 matrix (translation in column 4, bottom row 0, 0, 0, 1).</summary>
    public Matrix4x4D ToMatrix() => new(
        Linear.M11, Linear.M12, Linear.M13, Translation.X,
        Linear.M21, Linear.M22, Linear.M23, Translation.Y,
        Linear.M31, Linear.M32, Linear.M33, Translation.Z,
        0, 0, 0, 1);

    /// <summary>Entry-wise IEEE equality.</summary>
    public static bool operator ==(Affine3D a, Affine3D b) => a.Linear == b.Linear && a.Translation == b.Translation;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(Affine3D a, Affine3D b) => !(a == b);

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public Affine3F ToSingle() => new(Linear.ToSingle(), Translation.ToSingle());

    /// <summary>Value equality that treats NaN as equal to itself.</summary>
    public bool Equals(Affine3D other) => Linear.Equals(other.Linear) && Translation.Equals(other.Translation);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Affine3D a && Equals(a);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Linear.GetHashCode(), Translation.GetHashCode());

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{{{Linear} + {Translation}}}");
}
