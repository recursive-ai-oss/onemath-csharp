using System;
using RecursiveAI.OneMath.Physics;
using Xunit;

namespace RecursiveAI.OneMath.Tests.PhysicsTests;

public class InertiaTests
{
    private static void Near(Matrix3x3D expected, Matrix3x3D actual, double tol)
    {
        Assert.InRange(actual.M11 - expected.M11, -tol, tol);
        Assert.InRange(actual.M12 - expected.M12, -tol, tol);
        Assert.InRange(actual.M13 - expected.M13, -tol, tol);
        Assert.InRange(actual.M21 - expected.M21, -tol, tol);
        Assert.InRange(actual.M22 - expected.M22, -tol, tol);
        Assert.InRange(actual.M23 - expected.M23, -tol, tol);
        Assert.InRange(actual.M31 - expected.M31, -tol, tol);
        Assert.InRange(actual.M32 - expected.M32, -tol, tol);
        Assert.InRange(actual.M33 - expected.M33, -tol, tol);
    }

    private static QuaternionD Axis(Vector3D axis, double angle)
    {
        // Half-angle sine and cosine from Math here: the library has no angle constructor, and the test only needs a rotation.
        return QuaternionD.FromAxisSinCos(axis.Normalized(), Math.Sin(angle / 2), Math.Cos(angle / 2));
    }

    private static double Trace(Matrix3x3D m) => m.M11 + m.M22 + m.M33;

    [Fact]
    public void AnalyticSolids()
    {
        Matrix3x3D box = InertiaD.SolidBox(12, new Vector3D(1, 2, 3));
        Assert.Equal(new Matrix3x3D(52, 0, 0, 0, 40, 0, 0, 0, 20), box);
        Matrix3x3D sphere = InertiaD.SolidSphere(5, 2);
        Assert.Equal(new Matrix3x3D(8, 0, 0, 0, 8, 0, 0, 0, 8), sphere);
        Matrix3x3D cyl = InertiaD.SolidCylinder(12, 1, 2);
        Assert.Equal(new Matrix3x3D(19, 0, 0, 0, 6, 0, 0, 0, 19), cyl);
        Assert.Equal(new Matrix3x3F(52, 0, 0, 0, 40, 0, 0, 0, 20), InertiaF.SolidBox(12, new Vector3F(1, 2, 3)));
    }

    [Fact]
    public void RotationPreservesTraceAndEigenvalues()
    {
        var rng = new TestRng(0x1E27);
        for (int n = 0; n < 2000; n++)
        {
            Matrix3x3D t = InertiaD.SolidBox(1 + rng.Unit() * 9, new Vector3D(0.1 + rng.Unit(), 0.1 + rng.Unit(), 0.1 + rng.Unit()));
            QuaternionD q = Axis(new Vector3D(rng.Signed() + 0.1, rng.Signed(), rng.Signed()), rng.Signed() * 3);
            Matrix3x3D viaQ = InertiaD.Rotate(t, q);
            Matrix3x3D viaM = InertiaD.Rotate(t, q.ToMatrix());
            Assert.Equal(viaM, viaQ);
            Assert.Equal(viaQ.M12, viaQ.M21);
            Assert.Equal(viaQ.M13, viaQ.M31);
            Assert.Equal(viaQ.M23, viaQ.M32);
            Assert.Equal(Trace(t), Trace(viaQ), 1e-12);
            EigenDecompositionD e0 = SymmetricEigenD.Decompose(t);
            EigenDecompositionD e1 = SymmetricEigenD.Decompose(viaQ);
            Assert.Equal(e0.Values.X, e1.Values.X, 1e-12);
            Assert.Equal(e0.Values.Y, e1.Values.Y, 1e-12);
            Assert.Equal(e0.Values.Z, e1.Values.Z, 1e-12);
            // Rotating back recovers the tensor.
            Near(t, InertiaD.Rotate(viaQ, q.ToMatrix().Transpose()), 1e-12);
        }
    }

    [Fact]
    public void QuarterTurnSwapsAxes()
    {
        Matrix3x3D t = InertiaD.SolidBox(12, new Vector3D(1, 2, 3));
        Matrix3x3D r = InertiaD.Rotate(t, Axis(new Vector3D(0, 0, 1), Math.PI / 2));
        Near(new Matrix3x3D(40, 0, 0, 0, 52, 0, 0, 0, 20), r, 1e-12);
        Assert.Equal(t, InertiaD.Rotate(t, Matrix3x3D.Identity));
    }

    [Fact]
    public void ParallelAxisAnalyticAndRoundTrip()
    {
        // A sphere's tensor moved a distance d along x gains m d^2 about y and z only.
        Matrix3x3D s = InertiaD.SolidSphere(5, 2);
        Matrix3x3D moved = InertiaD.ShiftToPoint(s, 5, new Vector3D(3, 0, 0));
        Assert.Equal(new Matrix3x3D(8, 0, 0, 0, 8 + 45, 0, 0, 0, 8 + 45), moved);
        // An oblique offset: I + m(|d|^2 E - d d^T) with d = (1, 2, 3), m = 2.
        Matrix3x3D o = InertiaD.ShiftToPoint(default, 2, new Vector3D(1, 2, 3));
        Assert.Equal(new Matrix3x3D(26, -4, -6, -4, 20, -12, -6, -12, 10), o);
        var rng = new TestRng(0xA215);
        for (int n = 0; n < 2000; n++)
        {
            Matrix3x3D t = InertiaD.Rotate(InertiaD.SolidBox(1 + rng.Unit(), new Vector3D(0.5 + rng.Unit(), 0.5 + rng.Unit(), 0.5 + rng.Unit())),
                Axis(new Vector3D(rng.Signed() + 0.1, rng.Signed(), rng.Signed()), rng.Signed() * 3));
            double m = rng.Unit() * 4;
            var d = new Vector3D(rng.Signed() * 5, rng.Signed() * 5, rng.Signed() * 5);
            Near(t, InertiaD.ShiftToCenter(InertiaD.ShiftToPoint(t, m, d), m, d), 1e-12);
        }
        Near(default, InertiaD.ShiftToCenter(InertiaD.ShiftToPoint(default, 3, new Vector3D(1, 1, 1)), 3, new Vector3D(1, 1, 1)), 0);
    }

    [Fact]
    public void CombiningTwoBoxesGivesTheWholeBox()
    {
        // A box with half extents (1, 1, 2) and mass 8 is two (1, 1, 1) boxes of mass 4 centred at z = -1 and z = +1.
        Matrix3x3D half = InertiaD.SolidBox(4, new Vector3D(1, 1, 1));
        var parts = new[]
        {
            new MassPropertiesD(4, new Vector3D(0, 0, -1), half),
            new MassPropertiesD(4, new Vector3D(0, 0, 1), half),
        };
        MassPropertiesD whole = InertiaD.Combine(parts);
        Assert.Equal(8, whole.Mass);
        Assert.Equal(default, whole.Center);
        Near(InertiaD.SolidBox(8, new Vector3D(1, 1, 2)), whole.Inertia, 1e-14);

        // Offset the whole assembly: the centre moves, the tensor about the centre does not.
        var shifted = new[]
        {
            new MassPropertiesD(4, new Vector3D(10, 20, 29), half),
            new MassPropertiesD(4, new Vector3D(10, 20, 31), half),
        };
        MassPropertiesD w2 = InertiaD.Combine(shifted);
        Assert.Equal(new Vector3D(10, 20, 30), w2.Center);
        Near(InertiaD.SolidBox(8, new Vector3D(1, 1, 2)), w2.Inertia, 1e-12);
    }

    [Fact]
    public void CombiningPointMassesMatchesBruteForce()
    {
        var rng = new TestRng(0xC0B1);
        for (int n = 0; n < 500; n++)
        {
            int count = 1 + rng.Int(8);
            var parts = new MassPropertiesD[count];
            double total = 0;
            double cx = 0, cy = 0, cz = 0;
            var pos = new Vector3D[count];
            var mass = new double[count];
            for (int i = 0; i < count; i++)
            {
                mass[i] = 0.1 + rng.Unit() * 3;
                pos[i] = new Vector3D(rng.Signed() * 4, rng.Signed() * 4, rng.Signed() * 4);
                parts[i] = new MassPropertiesD(mass[i], pos[i], default);
                total += mass[i];
                cx += mass[i] * pos[i].X;
                cy += mass[i] * pos[i].Y;
                cz += mass[i] * pos[i].Z;
            }
            var c = new Vector3D(cx / total, cy / total, cz / total);
            double xx = 0, yy = 0, zz = 0, xy = 0, xz = 0, yz = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3D d = pos[i] - c;
                xx += mass[i] * (d.Y * d.Y + d.Z * d.Z);
                yy += mass[i] * (d.X * d.X + d.Z * d.Z);
                zz += mass[i] * (d.X * d.X + d.Y * d.Y);
                xy -= mass[i] * d.X * d.Y;
                xz -= mass[i] * d.X * d.Z;
                yz -= mass[i] * d.Y * d.Z;
            }
            MassPropertiesD got = InertiaD.Combine(parts);
            Assert.Equal(total, got.Mass, 1e-12);
            Near(new Matrix3x3D(xx, xy, xz, xy, yy, yz, xz, yz, zz), got.Inertia, 1e-11);
        }
    }

    [Fact]
    public void PrincipalAxesRecoverTheBox()
    {
        var rng = new TestRng(0xB0C5);
        for (int n = 0; n < 1000; n++)
        {
            var half = new Vector3D(0.2 + rng.Unit() * 2, 0.2 + rng.Unit() * 2, 0.2 + rng.Unit() * 2);
            Matrix3x3D body = InertiaD.SolidBox(3, half);
            QuaternionD q = Axis(new Vector3D(rng.Signed() + 0.1, rng.Signed(), rng.Signed()), rng.Signed() * 3);
            Matrix3x3D world = InertiaD.Rotate(body, q);
            PrincipalAxesD p = InertiaD.PrincipalAxes(world);
            EigenDecompositionD sorted = SymmetricEigenD.Decompose(body);
            Assert.Equal(sorted.Values.X, p.Moments.X, 1e-12);
            Assert.Equal(sorted.Values.Y, p.Moments.Y, 1e-12);
            Assert.Equal(sorted.Values.Z, p.Moments.Z, 1e-12);
            Near(world, p.Rotation * Matrix3x3D.CreateScale(p.Moments) * p.Rotation.Transpose(), 1e-12);
            Near(p.Rotation, p.Orientation.ToMatrix(), 1e-12);
            Assert.Equal(1, p.Orientation.Length(), 1e-12);
        }
        PrincipalAxesF pf = InertiaF.PrincipalAxes(InertiaF.SolidBox(3, new Vector3F(1, 2, 3)));
        Assert.Equal(3 * (1 + 4) / 3f, pf.Moments.X, 1e-5);
    }

    [Fact]
    public void SinglePrecisionCombineAndRotate()
    {
        Matrix3x3F half = InertiaF.SolidBox(4, new Vector3F(1, 1, 1));
        MassPropertiesF whole = InertiaF.Combine(new[]
        {
            new MassPropertiesF(4, new Vector3F(0, 0, -1), half),
            new MassPropertiesF(4, new Vector3F(0, 0, 1), half),
        });
        Matrix3x3F expected = InertiaF.SolidBox(8, new Vector3F(1, 1, 2));
        Assert.Equal(expected.M11, whole.Inertia.M11, 1e-5);
        Assert.Equal(expected.M33, whole.Inertia.M33, 1e-5);
        Matrix3x3F r = InertiaF.Rotate(expected, QuaternionF.Identity);
        Assert.Equal(expected, r);
    }

    [Fact]
    public void HostileInputIsRejectedWithTypedErrors()
    {
        Matrix3x3D t = InertiaD.SolidSphere(1, 1);
        var neg = Assert.Throws<ArgumentOutOfRangeException>(() => InertiaD.ShiftToPoint(t, -1, new Vector3D(1, 0, 0)));
        Assert.Contains("mass is negative", neg.Message);
        Assert.Equal("mass", neg.ParamName);
        var nan = Assert.Throws<ArgumentOutOfRangeException>(() => InertiaD.ShiftToPoint(t, 1, new Vector3D(1, double.NaN, 0)));
        Assert.Contains("offset.Y", nan.Message);
        var asym = Assert.Throws<ArgumentException>(() => InertiaD.Rotate(new Matrix3x3D(1, 5, 0, 0, 1, 0, 0, 0, 1), Matrix3x3D.Identity));
        Assert.Contains("not symmetric", asym.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => InertiaD.Rotate(t, new QuaternionD(0, 0, double.PositiveInfinity, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => InertiaD.SolidBox(1, new Vector3D(1, -1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => InertiaD.SolidSphere(double.NaN, 1));

        Assert.Throws<ArgumentException>(() => InertiaD.Combine(ReadOnlySpan<MassPropertiesD>.Empty));
        var zero = Assert.Throws<ArgumentException>(() => InertiaD.Combine(new[] { new MassPropertiesD(0, default, default) }));
        Assert.Contains("total mass", zero.Message);
        var badPart = Assert.Throws<ArgumentOutOfRangeException>(() => InertiaD.Combine(new[]
        {
            new MassPropertiesD(1, default, t),
            new MassPropertiesD(-2, default, t),
        }));
        Assert.Contains("parts[1].Mass", badPart.Message);
    }
}
