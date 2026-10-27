using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RecursiveAI.OneMath;

/// <summary>
/// A rigid transform of 3D space of <see cref="double"/>: a unit-quaternion rotation followed by a translation,
/// <c>p -> Rotation.Rotate(p) + Translation</c>. The rotation is assumed to be unit length and is not renormalized.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct RigidTransformD : IEquatable<RigidTransformD>
{
    /// <summary>The rotation applied first.</summary>
    public readonly QuaternionD Rotation;

    /// <summary>The translation applied after the rotation.</summary>
    public readonly Vector3D Translation;

    /// <summary>Creates a transform from its parts.</summary>
    public RigidTransformD(QuaternionD rotation, Vector3D translation)
    {
        Rotation = rotation;
        Translation = translation;
    }

    /// <summary>The identity transform.</summary>
    public static RigidTransformD Identity => new(QuaternionD.Identity, default);

    /// <summary>Transforms a point: <c>Rotation.Rotate(p) + Translation</c>.</summary>
    public Vector3D TransformPoint(Vector3D p) => Rotation.Rotate(p) + Translation;

    /// <summary>Transforms a direction (translation ignored).</summary>
    public Vector3D TransformVector(Vector3D v) => Rotation.Rotate(v);

    /// <summary>
    /// Composition <c>a * b</c> (apply <paramref name="b"/> first): the rotation is <c>a.Rotation * b.Rotation</c> and the
    /// translation is <c>a.Rotation.Rotate(b.Translation) + a.Translation</c>. The rotation is not renormalized.
    /// </summary>
    public static RigidTransformD operator *(RigidTransformD a, RigidTransformD b) =>
        new(a.Rotation * b.Rotation, a.Rotation.Rotate(b.Translation) + a.Translation);

    /// <summary>The inverse transform: the conjugate rotation and the translation <c>-(conjugate.Rotate(Translation))</c>.</summary>
    public RigidTransformD Inverse()
    {
        QuaternionD c = Rotation.Conjugate();
        return new(c, -c.Rotate(Translation));
    }

    /// <summary>The equivalent affine transform (the rotation converted with <see cref="QuaternionD.ToMatrix"/>).</summary>
    public Affine3D ToAffine() => new(Rotation.ToMatrix(), Translation);

    /// <summary>The equivalent 4x4 matrix.</summary>
    public Matrix4x4D ToMatrix() => ToAffine().ToMatrix();

    /// <summary>Component-wise IEEE equality.</summary>
    public static bool operator ==(RigidTransformD a, RigidTransformD b) => a.Rotation == b.Rotation && a.Translation == b.Translation;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(RigidTransformD a, RigidTransformD b) => !(a == b);

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public RigidTransformF ToSingle() => new(Rotation.ToSingle(), Translation.ToSingle());

    /// <summary>Value equality that treats NaN as equal to itself.</summary>
    public bool Equals(RigidTransformD other) => Rotation.Equals(other.Rotation) && Translation.Equals(other.Translation);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is RigidTransformD r && Equals(r);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Rotation.GetHashCode(), Translation.GetHashCode());

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{{{Rotation} + {Translation}}}");
}
