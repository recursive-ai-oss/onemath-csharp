using System;
using System.Diagnostics.CodeAnalysis;

namespace RecursiveAI.OneMath.Physics;

/// <summary>
/// The mass, centre of mass and inertia tensor of a body or of one part of a body. The tensor is taken about
/// <see cref="Center"/>, in the axes of the frame the centre is expressed in.
/// </summary>
public readonly struct MassPropertiesD : IEquatable<MassPropertiesD>
{
    /// <summary>Creates mass properties. No validation happens here; <see cref="InertiaD.Combine"/> validates what it is given.</summary>
    public MassPropertiesD(double mass, Vector3D center, Matrix3x3D inertia)
    {
        Mass = mass;
        Center = center;
        Inertia = inertia;
    }

    /// <summary>The mass; zero or more.</summary>
    public double Mass { get; }

    /// <summary>The centre of mass.</summary>
    public Vector3D Center { get; }

    /// <summary>The symmetric inertia tensor about <see cref="Center"/>.</summary>
    public Matrix3x3D Inertia { get; }

    /// <summary>Equality of all three parts.</summary>
    public bool Equals(MassPropertiesD other) => Mass.Equals(other.Mass) && Center.Equals(other.Center) && Inertia.Equals(other.Inertia);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is MassPropertiesD m && Equals(m);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Scalar.Bits(Mass), Center.GetHashCode(), Inertia.GetHashCode());

    /// <summary>Equality of all three parts.</summary>
    public static bool operator ==(MassPropertiesD a, MassPropertiesD b) => a.Equals(b);

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(MassPropertiesD a, MassPropertiesD b) => !a.Equals(b);
}

/// <summary>An inertia tensor in its principal frame: moments about the principal axes and the rotation to that frame.</summary>
public readonly struct PrincipalAxesD : IEquatable<PrincipalAxesD>
{
    /// <summary>Creates a result from its parts.</summary>
    public PrincipalAxesD(Vector3D moments, Matrix3x3D rotation, QuaternionD orientation)
    {
        Moments = moments;
        Rotation = rotation;
        Orientation = orientation;
    }

    /// <summary>The principal moments, ascending.</summary>
    public Vector3D Moments { get; }

    /// <summary>The principal axes as columns (right-handed): <c>I = Rotation * diag(Moments) * Rotation^T</c>.</summary>
    public Matrix3x3D Rotation { get; }

    /// <summary>The same rotation as a quaternion (<c>Quaternion.FromMatrix(Rotation)</c>).</summary>
    public QuaternionD Orientation { get; }

    /// <summary>Equality of all three parts.</summary>
    public bool Equals(PrincipalAxesD other) => Moments.Equals(other.Moments) && Rotation.Equals(other.Rotation) && Orientation.Equals(other.Orientation);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is PrincipalAxesD p && Equals(p);

    /// <inheritdoc />
    public override int GetHashCode() => Scalar.Mix(Moments.GetHashCode(), Rotation.GetHashCode(), Orientation.GetHashCode());

    /// <summary>Equality of all three parts.</summary>
    public static bool operator ==(PrincipalAxesD a, PrincipalAxesD b) => a.Equals(b);

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(PrincipalAxesD a, PrincipalAxesD b) => !a.Equals(b);
}

/// <summary>
/// Inertia-tensor arithmetic for rigid bodies (Baraff, "An Introduction to Physically Based Modeling: Rigid Body Simulation I";
/// Mirtich, "Fast and Accurate Computation of Polyhedral Mass Properties"): rotating a tensor, moving the reference point
/// (parallel-axis theorem), combining parts and finding principal axes. Tensors are symmetric; every result is exactly
/// symmetric (the upper triangle is computed and mirrored). Evaluation order is fixed and documented in docs/physics.md.
/// </summary>
public static class InertiaD
{
    /// <summary>
    /// The tensor in a rotated frame: <c>R I R^T</c>, with <c>M = R * I</c> by the library's matrix product and entry (i, j) of the
    /// result the dot product of row i of <c>M</c> with row j of <c>R</c>. <paramref name="rotation"/> should be a rotation; it is
    /// checked for finiteness only. The upper triangle of <paramref name="tensor"/> is used.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An entry is NaN or infinite.</exception>
    /// <exception cref="ArgumentException"><paramref name="tensor"/> is not symmetric.</exception>
    public static Matrix3x3D Rotate(Matrix3x3D tensor, Matrix3x3D rotation)
    {
        GuardsD.Symmetric(tensor, nameof(tensor), nameof(tensor));
        GuardsD.Finite(rotation, nameof(rotation), nameof(rotation));
        return RotateCore(tensor, rotation);
    }

    /// <summary>The tensor in a rotated frame, for a unit quaternion (converted with <c>ToMatrix</c>); see <see cref="Rotate(Matrix3x3D, Matrix3x3D)"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An entry is NaN or infinite.</exception>
    /// <exception cref="ArgumentException"><paramref name="tensor"/> is not symmetric.</exception>
    public static Matrix3x3D Rotate(Matrix3x3D tensor, QuaternionD rotation)
    {
        GuardsD.Symmetric(tensor, nameof(tensor), nameof(tensor));
        GuardsD.Finite(rotation.X, nameof(rotation), "rotation.X");
        GuardsD.Finite(rotation.Y, nameof(rotation), "rotation.Y");
        GuardsD.Finite(rotation.Z, nameof(rotation), "rotation.Z");
        GuardsD.Finite(rotation.W, nameof(rotation), "rotation.W");
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
    public static Matrix3x3D ShiftToPoint(Matrix3x3D tensorAtCenter, double mass, Vector3D offset)
    {
        GuardsD.Symmetric(tensorAtCenter, nameof(tensorAtCenter), nameof(tensorAtCenter));
        GuardsD.Mass(mass, nameof(mass), nameof(mass));
        GuardsD.Finite(offset, nameof(offset), nameof(offset));
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
    public static Matrix3x3D ShiftToCenter(Matrix3x3D tensorAtPoint, double mass, Vector3D offset)
    {
        GuardsD.Symmetric(tensorAtPoint, nameof(tensorAtPoint), nameof(tensorAtPoint));
        GuardsD.Mass(mass, nameof(mass), nameof(mass));
        GuardsD.Finite(offset, nameof(offset), nameof(offset));
        return Shift(tensorAtPoint, mass, offset, -1);
    }

    /// <summary>
    /// Combines parts into one body: the total mass is the sum of the masses (in span order, left to right), the centre is
    /// <c>(sum of m_i c_i) / M</c> accumulated the same way, and the tensor about that centre is the sum, in span order, of each part's tensor
    /// shifted from its own centre to the combined one. All parts must be expressed in one frame.
    /// </summary>
    /// <exception cref="ArgumentException">The span is empty, the total mass is zero, or a tensor is not symmetric.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A value is NaN or infinite, or a mass is negative.</exception>
    public static MassPropertiesD Combine(ReadOnlySpan<MassPropertiesD> parts)
    {
        if (parts.Length == 0)
        {
            throw new ArgumentException("parts is empty; at least one part is expected.", nameof(parts));
        }
        double total = 0;
        double sx = 0, sy = 0, sz = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            MassPropertiesD p = parts[i];
            string at = "parts[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
            GuardsD.Mass(p.Mass, nameof(parts), at + ".Mass");
            GuardsD.Finite(p.Center, nameof(parts), at + ".Center");
            GuardsD.Symmetric(p.Inertia, nameof(parts), at + ".Inertia");
            total += p.Mass;
            sx += p.Mass * p.Center.X;
            sy += p.Mass * p.Center.Y;
            sz += p.Mass * p.Center.Z;
        }
        if (!(total > 0) || !double.IsFinite(total))
        {
            throw new ArgumentException("The total mass of parts is " + (total > 0 ? "not finite" : "zero") + "; a positive finite total is expected.", nameof(parts));
        }
        var center = new Vector3D(sx / total, sy / total, sz / total);

        double i11 = 0, i12 = 0, i13 = 0, i22 = 0, i23 = 0, i33 = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            MassPropertiesD p = parts[i];
            Matrix3x3D t = Shift(p.Inertia, p.Mass, p.Center - center, 1);
            i11 += t.M11;
            i12 += t.M12;
            i13 += t.M13;
            i22 += t.M22;
            i23 += t.M23;
            i33 += t.M33;
        }
        return new MassPropertiesD(total, center, new Matrix3x3D(i11, i12, i13, i12, i22, i23, i13, i23, i33));
    }

    /// <summary>
    /// The principal moments and axes of a symmetric tensor: <see cref="SymmetricEigenD.Decompose"/> of the tensor, plus the
    /// rotation as a quaternion. Moments ascend; the axes are right-handed. A physical tensor has non-negative moments; this does not check that.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An entry is NaN or infinite.</exception>
    /// <exception cref="ArgumentException"><paramref name="tensor"/> is not symmetric.</exception>
    public static PrincipalAxesD PrincipalAxes(Matrix3x3D tensor)
    {
        EigenDecompositionD e = SymmetricEigenD.Decompose(tensor);
        return new PrincipalAxesD(e.Values, e.Vectors, QuaternionD.FromMatrix(e.Vectors));
    }

    /// <summary>
    /// The tensor of a solid box of uniform density about its centre, axes along the box: with half extents (hx, hy, hz),
    /// Ixx = <c>m*(hy*hy + hz*hz) / 3</c>, Iyy = <c>m*(hx*hx + hz*hz) / 3</c>, Izz = <c>m*(hx*hx + hy*hy) / 3</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is NaN or infinite, or negative.</exception>
    public static Matrix3x3D SolidBox(double mass, Vector3D halfExtents)
    {
        GuardsD.Mass(mass, nameof(mass), nameof(mass));
        GuardsD.Length(halfExtents.X, nameof(halfExtents), "halfExtents.X");
        GuardsD.Length(halfExtents.Y, nameof(halfExtents), "halfExtents.Y");
        GuardsD.Length(halfExtents.Z, nameof(halfExtents), "halfExtents.Z");
        double xx = halfExtents.X * halfExtents.X;
        double yy = halfExtents.Y * halfExtents.Y;
        double zz = halfExtents.Z * halfExtents.Z;
        return new Matrix3x3D(
            mass * (yy + zz) / 3, 0, 0,
            0, mass * (xx + zz) / 3, 0,
            0, 0, mass * (xx + yy) / 3);
    }

    /// <summary>The tensor of a solid sphere about its centre: <c>(2*m*(r*r)) / 5</c> on the diagonal.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is NaN or infinite, or negative.</exception>
    public static Matrix3x3D SolidSphere(double mass, double radius)
    {
        GuardsD.Mass(mass, nameof(mass), nameof(mass));
        GuardsD.Length(radius, nameof(radius), nameof(radius));
        double i = 2 * mass * (radius * radius) / 5;
        return new Matrix3x3D(i, 0, 0, 0, i, 0, 0, 0, i);
    }

    /// <summary>
    /// The tensor of a solid cylinder about its centre with its axis along Y: Iyy = <c>m*(r*r) / 2</c>, and
    /// Ixx = Izz = <c>m*(3*(r*r) + 4*(hh*hh)) / 12</c> for half height <c>hh</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is NaN or infinite, or negative.</exception>
    public static Matrix3x3D SolidCylinder(double mass, double radius, double halfHeight)
    {
        GuardsD.Mass(mass, nameof(mass), nameof(mass));
        GuardsD.Length(radius, nameof(radius), nameof(radius));
        GuardsD.Length(halfHeight, nameof(halfHeight), nameof(halfHeight));
        double rr = radius * radius;
        double hh = halfHeight * halfHeight;
        double side = mass * (3 * rr + 4 * hh) / 12;
        double axis = mass * rr / 2;
        return new Matrix3x3D(side, 0, 0, 0, axis, 0, 0, 0, side);
    }

    private static Matrix3x3D RotateCore(Matrix3x3D tensor, Matrix3x3D r)
    {
        var s = new Matrix3x3D(
            tensor.M11, tensor.M12, tensor.M13,
            tensor.M12, tensor.M22, tensor.M23,
            tensor.M13, tensor.M23, tensor.M33);
        Matrix3x3D m = r * s;
        Vector3D m1 = m.Row(0), m2 = m.Row(1), m3 = m.Row(2);
        Vector3D r1 = r.Row(0), r2 = r.Row(1), r3 = r.Row(2);
        double i11 = Vector3D.Dot(m1, r1);
        double i12 = Vector3D.Dot(m1, r2);
        double i13 = Vector3D.Dot(m1, r3);
        double i22 = Vector3D.Dot(m2, r2);
        double i23 = Vector3D.Dot(m2, r3);
        double i33 = Vector3D.Dot(m3, r3);
        return new Matrix3x3D(i11, i12, i13, i12, i22, i23, i13, i23, i33);
    }

    // sign = +1 adds the parallel-axis terms, -1 subtracts them.
    private static Matrix3x3D Shift(Matrix3x3D t, double m, Vector3D d, int sign)
    {
        double xx = d.Y * d.Y + d.Z * d.Z;
        double yy = d.X * d.X + d.Z * d.Z;
        double zz = d.X * d.X + d.Y * d.Y;
        double mx = m * d.X;
        double my = m * d.Y;
        double a11 = m * xx;
        double a22 = m * yy;
        double a33 = m * zz;
        double a12 = mx * d.Y;
        double a13 = mx * d.Z;
        double a23 = my * d.Z;
        double i11, i22, i33, i12, i13, i23;
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
        return new Matrix3x3D(i11, i12, i13, i12, i22, i23, i13, i23, i33);
    }
}
