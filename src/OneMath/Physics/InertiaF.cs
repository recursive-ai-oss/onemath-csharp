using System;
using System.Diagnostics.CodeAnalysis;

namespace RecursiveAI.OneMath.Physics;

/// <summary>
/// The mass, centre of mass and inertia tensor of a body or of one part of a body. The tensor is taken about
/// <see cref="Center"/>, in the axes of the frame the centre is expressed in.
/// </summary>
public readonly struct MassPropertiesF : IEquatable<MassPropertiesF>
{
    /// <summary>Creates mass properties. No validation happens here; <see cref="InertiaF.Combine"/> validates what it is given.</summary>
    public MassPropertiesF(float mass, Vector3F center, Matrix3x3F inertia)
    {
        Mass = mass;
        Center = center;
        Inertia = inertia;
    }

    /// <summary>The mass; zero or more.</summary>
    public float Mass { get; }

    /// <summary>The centre of mass.</summary>
    public Vector3F Center { get; }

    /// <summary>The symmetric inertia tensor about <see cref="Center"/>.</summary>
    public Matrix3x3F Inertia { get; }

    /// <summary>Equality of all three parts.</summary>
    public bool Equals(MassPropertiesF other) => Mass.Equals(other.Mass) && Center.Equals(other.Center) && Inertia.Equals(other.Inertia);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is MassPropertiesF m && Equals(m);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Scalar.Bits(Mass), Center.GetHashCode(), Inertia.GetHashCode());

    /// <summary>Equality of all three parts.</summary>
    public static bool operator ==(MassPropertiesF a, MassPropertiesF b) => a.Equals(b);

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(MassPropertiesF a, MassPropertiesF b) => !a.Equals(b);
}

/// <summary>An inertia tensor in its principal frame: moments about the principal axes and the rotation to that frame.</summary>
public readonly struct PrincipalAxesF : IEquatable<PrincipalAxesF>
{
    /// <summary>Creates a result from its parts.</summary>
    public PrincipalAxesF(Vector3F moments, Matrix3x3F rotation, QuaternionF orientation)
    {
        Moments = moments;
        Rotation = rotation;
        Orientation = orientation;
    }

    /// <summary>The principal moments, ascending.</summary>
    public Vector3F Moments { get; }

    /// <summary>The principal axes as columns (right-handed): <c>I = Rotation * diag(Moments) * Rotation^T</c>.</summary>
    public Matrix3x3F Rotation { get; }

    /// <summary>The same rotation as a quaternion (<c>Quaternion.FromMatrix(Rotation)</c>).</summary>
    public QuaternionF Orientation { get; }

    /// <summary>Equality of all three parts.</summary>
    public bool Equals(PrincipalAxesF other) => Moments.Equals(other.Moments) && Rotation.Equals(other.Rotation) && Orientation.Equals(other.Orientation);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is PrincipalAxesF p && Equals(p);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Moments.GetHashCode(), Rotation.GetHashCode(), Orientation.GetHashCode());

    /// <summary>Equality of all three parts.</summary>
    public static bool operator ==(PrincipalAxesF a, PrincipalAxesF b) => a.Equals(b);

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(PrincipalAxesF a, PrincipalAxesF b) => !a.Equals(b);
}

/// <summary>
/// Inertia-tensor arithmetic for rigid bodies (Baraff, "An Introduction to Physically Based Modeling: Rigid Body Simulation I";
/// Mirtich, "Fast and Accurate Computation of Polyhedral Mass Properties"): rotating a tensor, moving the reference point
/// (parallel-axis theorem), combining parts and finding principal axes. Tensors are symmetric; every result is exactly
/// symmetric (the upper triangle is computed and mirrored). Evaluation order is fixed and documented in docs/physics.md.
/// </summary>
public static class InertiaF
{
    /// <summary>
    /// The tensor in a rotated frame: <c>R I R^T</c>, with <c>M = R * I</c> by the library's matrix product and entry (i, j) of the
    /// result the dot product of row i of <c>M</c> with row j of <c>R</c>. <paramref name="rotation"/> should be a rotation; it is
    /// checked for finiteness only. The upper triangle of <paramref name="tensor"/> is used.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An entry is NaN or infinite.</exception>
    /// <exception cref="ArgumentException"><paramref name="tensor"/> is not symmetric.</exception>
    public static Matrix3x3F Rotate(Matrix3x3F tensor, Matrix3x3F rotation)
    {
        GuardsF.Symmetric(tensor, nameof(tensor), nameof(tensor));
        GuardsF.Finite(rotation, nameof(rotation), nameof(rotation));
        return RotateCore(tensor, rotation);
    }

    /// <summary>The tensor in a rotated frame, for a unit quaternion (converted with <c>ToMatrix</c>); see <see cref="Rotate(Matrix3x3F, Matrix3x3F)"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An entry is NaN or infinite.</exception>
    /// <exception cref="ArgumentException"><paramref name="tensor"/> is not symmetric.</exception>
    public static Matrix3x3F Rotate(Matrix3x3F tensor, QuaternionF rotation)
    {
        GuardsF.Symmetric(tensor, nameof(tensor), nameof(tensor));
        GuardsF.Finite(rotation.X, nameof(rotation), "rotation.X");
        GuardsF.Finite(rotation.Y, nameof(rotation), "rotation.Y");
        GuardsF.Finite(rotation.Z, nameof(rotation), "rotation.Z");
        GuardsF.Finite(rotation.W, nameof(rotation), "rotation.W");
        return RotateCore(tensor, rotation.ToMatrix());
    }

    /// <summary>
    /// Moves the reference point from the centre of mass to a point displaced by <paramref name="offset"/> (point minus centre):
    /// <c>I + m (|d|^2 E - d d^T)</c>. The diagonal terms are written as sums of two squares (no cancellation); the
    /// off-diagonal terms as <c>-(m*dx)*dy</c> and so on. Offset and tensor are in the same frame.
    /// </summary>
    /// <param name="tensorAtCenter">Symmetric tensor about the centre of mass.</param>
    /// <param name="mass">Zero or more.</param>
    /// <param name="offset">Point minus centre of mass.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is NaN or infinite, or the mass is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="tensorAtCenter"/> is not symmetric.</exception>
    public static Matrix3x3F ShiftToPoint(Matrix3x3F tensorAtCenter, float mass, Vector3F offset)
    {
        GuardsF.Symmetric(tensorAtCenter, nameof(tensorAtCenter), nameof(tensorAtCenter));
        GuardsF.Mass(mass, nameof(mass), nameof(mass));
        GuardsF.Finite(offset, nameof(offset), nameof(offset));
        return Shift(tensorAtCenter, mass, offset, 1);
    }

    /// <summary>
    /// The inverse of <see cref="ShiftToPoint"/>: from the tensor about a point to the tensor about the centre of mass, where
    /// <paramref name="offset"/> is again the point minus the centre. The same terms are subtracted instead of added, so a round trip
    /// returns the input to within rounding, not bit for bit.
    /// </summary>
    /// <param name="tensorAtPoint">Symmetric tensor about the point.</param>
    /// <param name="mass">Zero or more.</param>
    /// <param name="offset">Point minus centre of mass.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is NaN or infinite, or the mass is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="tensorAtPoint"/> is not symmetric.</exception>
    public static Matrix3x3F ShiftToCenter(Matrix3x3F tensorAtPoint, float mass, Vector3F offset)
    {
        GuardsF.Symmetric(tensorAtPoint, nameof(tensorAtPoint), nameof(tensorAtPoint));
        GuardsF.Mass(mass, nameof(mass), nameof(mass));
        GuardsF.Finite(offset, nameof(offset), nameof(offset));
        return Shift(tensorAtPoint, mass, offset, -1);
    }

    /// <summary>
    /// Combines parts into one body: the total mass is the sum of the masses (in span order, left to right), the centre is
    /// <c>(sum of m_i c_i) / M</c> accumulated the same way, and the tensor about that centre is the sum, in span order, of each part's tensor
    /// shifted from its own centre to the combined one. All parts must be expressed in one frame.
    /// </summary>
    /// <exception cref="ArgumentException">The span is empty, the total mass is zero, or a tensor is not symmetric.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A value is NaN or infinite, or a mass is negative.</exception>
    public static MassPropertiesF Combine(ReadOnlySpan<MassPropertiesF> parts)
    {
        if (parts.Length == 0)
        {
            throw new ArgumentException("parts is empty; at least one part is expected.", nameof(parts));
        }
        float total = 0;
        float sx = 0, sy = 0, sz = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            MassPropertiesF p = parts[i];
            string at = "parts[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
            GuardsF.Mass(p.Mass, nameof(parts), at + ".Mass");
            GuardsF.Finite(p.Center, nameof(parts), at + ".Center");
            GuardsF.Symmetric(p.Inertia, nameof(parts), at + ".Inertia");
            total += p.Mass;
            sx += p.Mass * p.Center.X;
            sy += p.Mass * p.Center.Y;
            sz += p.Mass * p.Center.Z;
        }
        if (!(total > 0) || !float.IsFinite(total))
        {
            throw new ArgumentException("The total mass of parts is " + (total > 0 ? "not finite" : "zero") + "; a positive finite total is expected.", nameof(parts));
        }
        var center = new Vector3F(sx / total, sy / total, sz / total);

        float i11 = 0, i12 = 0, i13 = 0, i22 = 0, i23 = 0, i33 = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            MassPropertiesF p = parts[i];
            Matrix3x3F t = Shift(p.Inertia, p.Mass, p.Center - center, 1);
            i11 += t.M11;
            i12 += t.M12;
            i13 += t.M13;
            i22 += t.M22;
            i23 += t.M23;
            i33 += t.M33;
        }
        return new MassPropertiesF(total, center, new Matrix3x3F(i11, i12, i13, i12, i22, i23, i13, i23, i33));
    }

    /// <summary>
    /// The principal moments and axes of a symmetric tensor: <see cref="SymmetricEigenF.Decompose"/> of the tensor, plus the
    /// rotation as a quaternion. Moments ascend; the axes are right-handed. A physical tensor has non-negative moments; this does not check that.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An entry is NaN or infinite.</exception>
    /// <exception cref="ArgumentException"><paramref name="tensor"/> is not symmetric.</exception>
    public static PrincipalAxesF PrincipalAxes(Matrix3x3F tensor)
    {
        EigenDecompositionF e = SymmetricEigenF.Decompose(tensor);
        return new PrincipalAxesF(e.Values, e.Vectors, QuaternionF.FromMatrix(e.Vectors));
    }

    /// <summary>
    /// The tensor of a solid box of uniform density about its centre, axes along the box: with half extents (hx, hy, hz),
    /// Ixx = <c>m*(hy*hy + hz*hz) / 3</c>, Iyy = <c>m*(hx*hx + hz*hz) / 3</c>, Izz = <c>m*(hx*hx + hy*hy) / 3</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is NaN or infinite, or negative.</exception>
    public static Matrix3x3F SolidBox(float mass, Vector3F halfExtents)
    {
        GuardsF.Mass(mass, nameof(mass), nameof(mass));
        GuardsF.Length(halfExtents.X, nameof(halfExtents), "halfExtents.X");
        GuardsF.Length(halfExtents.Y, nameof(halfExtents), "halfExtents.Y");
        GuardsF.Length(halfExtents.Z, nameof(halfExtents), "halfExtents.Z");
        float xx = halfExtents.X * halfExtents.X;
        float yy = halfExtents.Y * halfExtents.Y;
        float zz = halfExtents.Z * halfExtents.Z;
        return new Matrix3x3F(
            mass * (yy + zz) / 3, 0, 0,
            0, mass * (xx + zz) / 3, 0,
            0, 0, mass * (xx + yy) / 3);
    }

    /// <summary>The tensor of a solid sphere about its centre: <c>(2*m*(r*r)) / 5</c> on the diagonal.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is NaN or infinite, or negative.</exception>
    public static Matrix3x3F SolidSphere(float mass, float radius)
    {
        GuardsF.Mass(mass, nameof(mass), nameof(mass));
        GuardsF.Length(radius, nameof(radius), nameof(radius));
        float i = 2 * mass * (radius * radius) / 5;
        return new Matrix3x3F(i, 0, 0, 0, i, 0, 0, 0, i);
    }

    /// <summary>
    /// The tensor of a solid cylinder about its centre with its axis along Y: Iyy = <c>m*(r*r) / 2</c>, and
    /// Ixx = Izz = <c>m*(3*(r*r) + 4*(hh*hh)) / 12</c> for half height <c>hh</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is NaN or infinite, or negative.</exception>
    public static Matrix3x3F SolidCylinder(float mass, float radius, float halfHeight)
    {
        GuardsF.Mass(mass, nameof(mass), nameof(mass));
        GuardsF.Length(radius, nameof(radius), nameof(radius));
        GuardsF.Length(halfHeight, nameof(halfHeight), nameof(halfHeight));
        float rr = radius * radius;
        float hh = halfHeight * halfHeight;
        float side = mass * (3 * rr + 4 * hh) / 12;
        float axis = mass * rr / 2;
        return new Matrix3x3F(side, 0, 0, 0, axis, 0, 0, 0, side);
    }

    private static Matrix3x3F RotateCore(Matrix3x3F tensor, Matrix3x3F r)
    {
        var s = new Matrix3x3F(
            tensor.M11, tensor.M12, tensor.M13,
            tensor.M12, tensor.M22, tensor.M23,
            tensor.M13, tensor.M23, tensor.M33);
        Matrix3x3F m = r * s;
        Vector3F m1 = m.Row(0), m2 = m.Row(1), m3 = m.Row(2);
        Vector3F r1 = r.Row(0), r2 = r.Row(1), r3 = r.Row(2);
        float i11 = Vector3F.Dot(m1, r1);
        float i12 = Vector3F.Dot(m1, r2);
        float i13 = Vector3F.Dot(m1, r3);
        float i22 = Vector3F.Dot(m2, r2);
        float i23 = Vector3F.Dot(m2, r3);
        float i33 = Vector3F.Dot(m3, r3);
        return new Matrix3x3F(i11, i12, i13, i12, i22, i23, i13, i23, i33);
    }

    // sign = +1 adds the parallel-axis terms, -1 subtracts them.
    private static Matrix3x3F Shift(Matrix3x3F t, float m, Vector3F d, int sign)
    {
        float xx = d.Y * d.Y + d.Z * d.Z;
        float yy = d.X * d.X + d.Z * d.Z;
        float zz = d.X * d.X + d.Y * d.Y;
        float mx = m * d.X;
        float my = m * d.Y;
        float a11 = m * xx;
        float a22 = m * yy;
        float a33 = m * zz;
        float a12 = mx * d.Y;
        float a13 = mx * d.Z;
        float a23 = my * d.Z;
        float i11, i22, i33, i12, i13, i23;
        if (sign > 0)
        {
            i11 = t.M11 + a11;
            i22 = t.M22 + a22;
            i33 = t.M33 + a33;
            i12 = t.M12 - a12;
            i13 = t.M13 - a13;
            i23 = t.M23 - a23;
        }
        else
        {
            i11 = t.M11 - a11;
            i22 = t.M22 - a22;
            i33 = t.M33 - a33;
            i12 = t.M12 + a12;
            i13 = t.M13 + a13;
            i23 = t.M23 + a23;
        }
        return new Matrix3x3F(i11, i12, i13, i12, i22, i23, i13, i23, i33);
    }
}
