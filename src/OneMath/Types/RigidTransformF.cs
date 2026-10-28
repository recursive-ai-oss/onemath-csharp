using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace RecursiveAI.OneMath;

/// <summary>
/// A rigid transform of 3D space of <see cref="float"/>: a unit-quaternion rotation followed by a translation,
/// <c>p -> Rotation.Rotate(p) + Translation</c>. The rotation is assumed to be unit length and is not renormalized.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct RigidTransformF : IEquatable<RigidTransformF>
{
    /// <summary>The rotation applied first.</summary>
    [JsonInclude]
    public readonly QuaternionF Rotation;

    /// <summary>The translation applied after the rotation.</summary>
    [JsonInclude]
    public readonly Vector3F Translation;

    /// <summary>Creates a transform from its parts.</summary>
    [JsonConstructor]
    public RigidTransformF(QuaternionF rotation, Vector3F translation)
    {
        Rotation = rotation;
        Translation = translation;
    }

    /// <summary>The identity transform.</summary>
    public static RigidTransformF Identity => new(QuaternionF.Identity, default);

    /// <summary>Transforms a point: <c>Rotation.Rotate(p) + Translation</c>.</summary>
    public Vector3F TransformPoint(Vector3F p) => Rotation.Rotate(p) + Translation;

    /// <summary>Transforms a direction (translation ignored).</summary>
    public Vector3F TransformVector(Vector3F v) => Rotation.Rotate(v);

    /// <summary>
    /// Composition <c>a * b</c> (apply <paramref name="b"/> first): the rotation is <c>a.Rotation * b.Rotation</c> and the
    /// translation is <c>a.Rotation.Rotate(b.Translation) + a.Translation</c>. The rotation is not renormalized.
    /// </summary>
    public static RigidTransformF operator *(RigidTransformF a, RigidTransformF b) =>
        new(a.Rotation * b.Rotation, a.Rotation.Rotate(b.Translation) + a.Translation);

    /// <summary>The inverse transform: the conjugate rotation and the translation <c>-(conjugate.Rotate(Translation))</c>.</summary>
    public RigidTransformF Inverted()
    {
        QuaternionF c = Rotation.Conjugated();
        return new(c, -c.Rotate(Translation));
    }

    /// <summary>The equivalent affine transform (the rotation converted with <see cref="QuaternionF.ToMatrix"/>).</summary>
    public Affine3F ToAffine() => new(Rotation.ToMatrix(), Translation);

    /// <summary>The equivalent 4x4 matrix.</summary>
    public Matrix4x4F ToMatrix() => ToAffine().ToMatrix();

    /// <summary>Component-wise IEEE equality.</summary>
    public static bool operator ==(RigidTransformF a, RigidTransformF b) => a.Rotation == b.Rotation && a.Translation == b.Translation;

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(RigidTransformF a, RigidTransformF b) => !(a == b);

    /// <summary>Converts to the other precision: exact when widening, round to nearest even when narrowing.</summary>
    public RigidTransformD ToDouble() => new(Rotation.ToDouble(), Translation.ToDouble());

    /// <summary>Value equality that treats NaN as equal to itself.</summary>
    public bool Equals(RigidTransformF other) => Rotation.Equals(other.Rotation) && Translation.Equals(other.Translation);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is RigidTransformF r && Equals(r);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Rotation.GetHashCode(), Translation.GetHashCode());

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{{{Rotation} + {Translation}}}");
}
