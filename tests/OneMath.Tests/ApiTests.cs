using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using RecursiveAI.OneMath.HashMatrix;
using RecursiveAI.OneMath.Physics;
using RecursiveAI.OneMath.Predicates;
using Xunit;
using SN = System.Numerics;

namespace RecursiveAI.OneMath.Tests;

public class RotationFactoryTests
{
    private static void SameBits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));

    [Fact]
    public void AxisAngle_is_defined_as_axis_sin_cos_of_the_half_angle()
    {
        var rng = new Random(5);
        for (int i = 0; i < 2000; i++)
        {
            var axis = new Vector3D(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5).Normalized();
            double angle = (rng.NextDouble() - 0.5) * 40;
            var (s, c) = Math64.SinCos(angle / 2);
            Assert.True(QuaternionD.CreateFromAxisSinCos(axis, s, c).Equals(QuaternionD.CreateFromAxisAngle(axis, angle)));
            var (sf, cf) = Math32.SinCos((float)angle / 2);
            var axf = axis.ToSingle();
            Assert.True(QuaternionF.CreateFromAxisSinCos(axf, sf, cf).Equals(QuaternionF.CreateFromAxisAngle(axf, (float)angle)));
        }
    }

    [Fact]
    public void Rotation_matrices_known_answers()
    {
        Assert.Equal(Matrix3x3D.Identity, Matrix3x3D.CreateRotationX(0));
        Assert.Equal(Matrix3x3D.Identity, Matrix3x3D.CreateRotationY(0));
        Assert.Equal(Matrix4x4D.Identity, Matrix4x4D.CreateRotationZ(0));
        // The double nearest pi/2 has cosine 6.123233995736766e-17 (correctly rounded) and sine exactly 1.0 after rounding.
        double halfPi = Math.PI / 2;
        Matrix3x3D rz = Matrix3x3D.CreateRotationZ(halfPi);
        SameBits(6.123233995736766E-17, rz.M11);
        SameBits(-1.0, rz.M12);
        SameBits(1.0, rz.M21);
        SameBits(6.123233995736766E-17, rz.M22);
        // +90 degrees about Y takes +X to -Z, about Z takes +X to +Y, about X takes +Y to +Z (right-handed).
        Vector3D ry = Matrix3x3D.CreateRotationY(halfPi).Transform(Vector3D.UnitX);
        Assert.True(Math.Abs(ry.X) < 1e-15 && Math.Abs(ry.Z + 1) < 1e-15);
        Vector3D rx = Matrix3x3D.CreateRotationX(halfPi).Transform(Vector3D.UnitY);
        Assert.True(Math.Abs(rx.Y) < 1e-15 && Math.Abs(rx.Z - 1) < 1e-15);
    }

    [Fact]
    public void Matrix_factories_agree_with_the_quaternion()
    {
        var rng = new Random(6);
        for (int i = 0; i < 500; i++)
        {
            var axis = new Vector3D(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5).Normalized();
            double angle = (rng.NextDouble() - 0.5) * 10;
            QuaternionD q = QuaternionD.CreateFromAxisAngle(axis, angle);
            Matrix3x3D m = Matrix3x3D.CreateFromQuaternion(q);
            Assert.Equal(q.ToMatrix(), m);
            Assert.Equal(Matrix4x4D.CreateFromMatrix3x3(m), Matrix4x4D.CreateFromQuaternion(q));
            Assert.Equal(new Affine3D(m, default).ToMatrix(), Matrix4x4D.CreateFromQuaternion(q));
            Assert.Equal(Affine3D.CreateFromQuaternion(q).ToMatrix(), Matrix4x4D.CreateFromQuaternion(q));
            var v = new Vector3D(rng.NextDouble(), rng.NextDouble(), rng.NextDouble());
            Assert.True((q.Rotate(v) - m.Transform(v)).Length() < 1e-14);
            // Axis rotations agree with the general axis-angle form.
            Assert.True((Matrix3x3D.CreateRotationZ(angle).Transform(v) - QuaternionD.CreateFromAxisAngle(Vector3D.UnitZ, angle).Rotate(v)).Length() < 1e-14);
            Assert.True((Matrix3x3D.CreateRotationX(angle).Transform(v) - QuaternionD.CreateFromAxisAngle(Vector3D.UnitX, angle).Rotate(v)).Length() < 1e-14);
            Assert.True((Matrix3x3D.CreateRotationY(angle).Transform(v) - QuaternionD.CreateFromAxisAngle(Vector3D.UnitY, angle).Rotate(v)).Length() < 1e-14);
            // Round trip through the matrix.
            QuaternionD back = QuaternionD.CreateFromRotationMatrix(m);
            Assert.True(Math.Abs(Math.Abs(QuaternionD.Dot(q, back)) - 1) < 1e-14);
        }
    }

    [Fact]
    public void Affine_factories()
    {
        var s = new Vector3D(2, 3, 4);
        Assert.Equal(new Affine3D(Matrix3x3D.CreateScale(s), default), Affine3D.CreateScale(s));
        Assert.Equal(new Vector3D(2, 6, 12), Affine3D.CreateScale(s).TransformPoint(new Vector3D(1, 2, 3)));
        Assert.Equal(new Vector3D(1, 2, 3), Affine3D.CreateTranslation(new Vector3D(1, 2, 3)).TransformPoint(default));
        Matrix3x3D lin = Matrix3x3D.CreateRotationZ(0.3);
        Assert.Equal(new Affine3D(lin, default), Affine3D.CreateFromLinear(lin));
        Assert.Equal(new Affine3D(lin, default), Affine3D.CreateRotationZ(0.3));
        Assert.Equal(Matrix4x4D.CreateTranslation(new Vector3D(1, 2, 3)), Matrix4x4D.CreateFromAffine(Affine3D.CreateTranslation(new Vector3D(1, 2, 3))));
        Assert.Equal(Matrix4x4F.CreateScale(new Vector3F(2, 3, 4)), Matrix4x4F.CreateFromAffine(Affine3F.CreateScale(new Vector3F(2, 3, 4))));
    }

    [Fact]
    public void Slerp_endpoints_midpoint_and_shortest_arc()
    {
        QuaternionD a = QuaternionD.CreateFromAxisAngle(Vector3D.UnitZ, 0.2);
        QuaternionD b = QuaternionD.CreateFromAxisAngle(Vector3D.UnitZ, 1.8);
        Assert.True(QuaternionD.Slerp(a, b, 0).Equals(a));
        Assert.True(QuaternionD.Slerp(a, b, 1).Equals(b));
        QuaternionD mid = QuaternionD.Slerp(a, b, 0.5);
        QuaternionD want = QuaternionD.CreateFromAxisAngle(Vector3D.UnitZ, 1.0);
        Assert.True(Math.Abs(mid.X - want.X) + Math.Abs(mid.Y - want.Y) + Math.Abs(mid.Z - want.Z) + Math.Abs(mid.W - want.W) < 1e-15);
        // -b is the same rotation: the result takes the short way round, so it equals the result for b.
        QuaternionD nb = new(-b.X, -b.Y, -b.Z, -b.W);
        Assert.True(QuaternionD.Slerp(a, nb, 0.37).Equals(QuaternionD.Slerp(a, b, 0.37)));
        // Unit length along the arc, and the nearly parallel branch stays unit too.
        for (int i = 0; i <= 20; i++)
        {
            Assert.True(Math.Abs(QuaternionD.Slerp(a, b, i / 20.0).Length() - 1) < 1e-15);
        }
        QuaternionD close = QuaternionD.CreateFromAxisAngle(Vector3D.UnitZ, 0.2000001);
        Assert.True(Math.Abs(QuaternionD.Slerp(a, close, 0.5).Length() - 1) < 1e-15);
    }

    [Fact]
    public void Quaternion_matches_System_Numerics()
    {
        var rng = new Random(7);
        for (int i = 0; i < 500; i++)
        {
            var axis = new Vector3F((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).Normalized();
            float angle = (float)((rng.NextDouble() - 0.5) * 6);
            QuaternionF q = QuaternionF.CreateFromAxisAngle(axis, angle);
            SN.Quaternion sq = SN.Quaternion.CreateFromAxisAngle(axis.ToSystemNumerics(), angle);
            AssertNear(sq, q.ToSystemNumerics(), 1e-6f);
            var axis2 = new Vector3F((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).Normalized();
            QuaternionF q2 = QuaternionF.CreateFromAxisAngle(axis2, angle * 0.7f);
            SN.Quaternion sq2 = SN.Quaternion.CreateFromAxisAngle(axis2.ToSystemNumerics(), angle * 0.7f);
            AssertNear(sq * sq2, (q * q2).ToSystemNumerics(), 1e-5f);
            AssertNear(SN.Quaternion.Slerp(sq, sq2, 0.3f), QuaternionF.Slerp(q, q2, 0.3f).ToSystemNumerics(), 1e-4f);
            var v = new Vector3F(1, 2, 3);
            SN.Vector3 want = SN.Vector3.Transform(v.ToSystemNumerics(), sq);
            SN.Vector3 got = q.Rotate(v).ToSystemNumerics();
            Assert.True((want - got).Length() < 1e-4f);
            Assert.True(QuaternionF.FromSystemNumerics(sq).ToSystemNumerics() == sq);
        }
    }

    private static void AssertNear(SN.Quaternion want, SN.Quaternion got, float tol)
    {
        // q and -q are one rotation; Slerp and products here keep the sign, so compare directly.
        Assert.True(Math.Abs(want.X - got.X) < tol && Math.Abs(want.Y - got.Y) < tol && Math.Abs(want.Z - got.Z) < tol && Math.Abs(want.W - got.W) < tol,
            $"{want} vs {got}");
    }
}

public class SystemNumericsConversionTests
{
    [Fact]
    public void Vectors_round_trip_and_double_narrows_explicitly()
    {
        var v3 = new Vector3F(1.5f, -2.25f, 1e-30f);
        Assert.Equal(v3, Vector3F.FromSystemNumerics(v3.ToSystemNumerics()));
        Assert.Equal(new SN.Vector2(1, 2), new Vector2F(1, 2).ToSystemNumerics());
        Assert.Equal(new SN.Vector4(1, 2, 3, 4), new Vector4D(1, 2, 3, 4).ToSystemNumerics());
        Assert.Equal(new Vector3D(1.5, 2, 3), Vector3D.FromSystemNumerics(new SN.Vector3(1.5f, 2, 3)));
        // 0.1 is not a float: the narrowing is the documented round to nearest even.
        Assert.Equal(0.1f, new Vector2D(0.1, 0).ToSystemNumerics().X);
    }

    [Fact]
    public void Matrix_conversion_transposes()
    {
        var sn = SN.Matrix4x4.CreateTranslation(1, 2, 3);
        Matrix4x4F m = Matrix4x4F.FromSystemNumerics(sn);
        Assert.Equal(1f, m.M14);
        Assert.Equal(2f, m.M24);
        Assert.Equal(3f, m.M34);
        Assert.Equal(0f, m.M41);
        Assert.Equal(new Vector3F(1, 2, 3), m.TransformPoint(default));
        Assert.Equal(Matrix4x4F.CreateTranslation(new Vector3F(1, 2, 3)), m);
        Assert.True(Matrix4x4F.CreateTranslation(new Vector3F(1, 2, 3)).ToSystemNumerics() == sn);
        Assert.True(Matrix4x4D.CreateScale(new Vector3D(2, 3, 4)).ToSystemNumerics() == SN.Matrix4x4.CreateScale(2, 3, 4));
    }

    [Fact]
    public void Translate_then_rotate_is_R_times_T_here_and_T_times_R_in_System_Numerics()
    {
        // The worked example in the README: the same transform, written in each library's own order.
        float quarter = (float)(Math.PI / 2);
        SN.Matrix4x4 snTR = SN.Matrix4x4.CreateTranslation(10, 0, 0) * SN.Matrix4x4.CreateRotationY(quarter);
        Matrix4x4F ours = Matrix4x4F.CreateRotationY(quarter) * Matrix4x4F.CreateTranslation(new Vector3F(10, 0, 0));
        SN.Vector3 snP = SN.Vector3.Transform(SN.Vector3.Zero, snTR);
        Vector3F p = ours.TransformPoint(default);
        Assert.True(Math.Abs(snP.X - p.X) < 1e-5 && Math.Abs(snP.Y - p.Y) < 1e-5 && Math.Abs(snP.Z - p.Z) < 1e-5, $"{snP} vs {p}");
        Assert.True(Math.Abs(p.X) < 1e-5 && Math.Abs(p.Z + 10) < 1e-5);
        // The literal port (same operand order) is a different transform.
        Matrix4x4F wrong = Matrix4x4F.CreateTranslation(new Vector3F(10, 0, 0)) * Matrix4x4F.CreateRotationY(quarter);
        Assert.True(Math.Abs(wrong.TransformPoint(default).X - 10) < 1e-5);
        // Converting the System.Numerics product gives this library's product.
        Matrix4x4F conv = Matrix4x4F.FromSystemNumerics(snTR);
        Vector3F q = conv.TransformPoint(new Vector3F(1, 2, 3));
        SN.Vector3 snQ = SN.Vector3.Transform(new SN.Vector3(1, 2, 3), snTR);
        Assert.True(Math.Abs(snQ.X - q.X) < 1e-4 && Math.Abs(snQ.Y - q.Y) < 1e-4 && Math.Abs(snQ.Z - q.Z) < 1e-4);
    }

    [Fact]
    public void Random_matrices_and_rotations_agree_with_System_Numerics()
    {
        var rng = new Random(8);
        for (int i = 0; i < 300; i++)
        {
            float ang = (float)((rng.NextDouble() - 0.5) * 6);
            var t = new Vector3F((float)rng.NextDouble() * 4, (float)rng.NextDouble() * 4, (float)rng.NextDouble() * 4);
            AssertNear(SN.Matrix4x4.CreateRotationX(ang), Matrix4x4F.CreateRotationX(ang).ToSystemNumerics());
            AssertNear(SN.Matrix4x4.CreateRotationY(ang), Matrix4x4F.CreateRotationY(ang).ToSystemNumerics());
            AssertNear(SN.Matrix4x4.CreateRotationZ(ang), Matrix4x4F.CreateRotationZ(ang).ToSystemNumerics());
            AssertNear(SN.Matrix4x4.CreateTranslation(t.ToSystemNumerics()) * SN.Matrix4x4.CreateRotationZ(ang),
                (Matrix4x4F.CreateRotationZ(ang) * Matrix4x4F.CreateTranslation(t)).ToSystemNumerics());
            var q = QuaternionF.CreateFromAxisAngle(new Vector3F(0, 0.6f, 0.8f), ang);
            AssertNear(SN.Matrix4x4.CreateFromQuaternion(q.ToSystemNumerics()), Matrix4x4F.CreateFromQuaternion(q).ToSystemNumerics());
        }
    }

    private static void AssertNear(SN.Matrix4x4 want, SN.Matrix4x4 got)
    {
        float d = Math.Abs(want.M11 - got.M11) + Math.Abs(want.M12 - got.M12) + Math.Abs(want.M13 - got.M13) + Math.Abs(want.M14 - got.M14)
            + Math.Abs(want.M21 - got.M21) + Math.Abs(want.M22 - got.M22) + Math.Abs(want.M23 - got.M23) + Math.Abs(want.M24 - got.M24)
            + Math.Abs(want.M31 - got.M31) + Math.Abs(want.M32 - got.M32) + Math.Abs(want.M33 - got.M33) + Math.Abs(want.M34 - got.M34)
            + Math.Abs(want.M41 - got.M41) + Math.Abs(want.M42 - got.M42) + Math.Abs(want.M43 - got.M43) + Math.Abs(want.M44 - got.M44);
        Assert.True(d < 1e-4f, $"{want} vs {got}");
    }
}

public class JsonRoundTripTests
{
    private static void RoundTrip<T>(T value) where T : struct
    {
        string json = JsonSerializer.Serialize(value);
        T back = JsonSerializer.Deserialize<T>(json);
        ReadOnlySpan<byte> a = MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in value));
        ReadOnlySpan<byte> b = MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in back));
        Assert.True(a.SequenceEqual(b), json);
    }

    [Fact]
    public void Every_value_type_round_trips_bit_exactly_with_default_options()
    {
        double[] e = { 0.0, -0.0, 1.0, -1.0, double.Epsilon, 0.1, 1.0 / 3.0, double.MaxValue, -2.2250738585072009E-308, 2097151.999999999 };
        for (int i = 0; i < e.Length; i++)
        {
            double a = e[i], b = e[(i + 3) % e.Length], c = e[(i + 7) % e.Length], d = e[(i + 1) % e.Length];
            float[] ef = { 0.0f, -0.0f, 1.0f, -1.0f, float.Epsilon, 0.1f, 1.0f / 3.0f, float.MaxValue, -1.17549435E-38f, 2097151.9f };
            float fa = ef[i], fb = ef[(i + 3) % ef.Length], fc = ef[(i + 7) % ef.Length], fd = ef[(i + 1) % ef.Length];
            var v3 = new Vector3D(a, b, c);
            var m3 = new Matrix3x3D(a, b, c, d, a, b, c, d, a);
            var q = new QuaternionD(a, b, c, d);
            RoundTrip(new Vector2D(a, b));
            RoundTrip(v3);
            RoundTrip(new Vector4D(a, b, c, d));
            RoundTrip(new Matrix2x2D(a, b, c, d));
            RoundTrip(m3);
            RoundTrip(new Matrix4x4D(a, b, c, d, d, a, b, c, c, d, a, b, b, c, d, a));
            RoundTrip(q);
            RoundTrip(new Affine3D(m3, v3));
            RoundTrip(new RigidTransformD(q, v3));
            RoundTrip(new Bounds2D(new Vector2D(a, b), new Vector2D(c, d)));
            RoundTrip(new Bounds3D(v3, new Vector3D(d, c, b)));
            RoundTrip(new EigenDecompositionD(v3, m3));
            RoundTrip(new MassPropertiesD(Math.Abs(a), v3, m3));
            RoundTrip(new PrincipalAxesD(v3, m3));

            var v3f = new Vector3F(fa, fb, fc);
            var m3f = new Matrix3x3F(fa, fb, fc, fd, fa, fb, fc, fd, fa);
            var qf = new QuaternionF(fa, fb, fc, fd);
            RoundTrip(new Vector2F(fa, fb));
            RoundTrip(v3f);
            RoundTrip(new Vector4F(fa, fb, fc, fd));
            RoundTrip(new Matrix2x2F(fa, fb, fc, fd));
            RoundTrip(m3f);
            RoundTrip(new Matrix4x4F(fa, fb, fc, fd, fd, fa, fb, fc, fc, fd, fa, fb, fb, fc, fd, fa));
            RoundTrip(qf);
            RoundTrip(new Affine3F(m3f, v3f));
            RoundTrip(new RigidTransformF(qf, v3f));
            RoundTrip(new Bounds2F(new Vector2F(fa, fb), new Vector2F(fc, fd)));
            RoundTrip(new Bounds3F(v3f, new Vector3F(fd, fc, fb)));
            RoundTrip(new EigenDecompositionF(v3f, m3f));
            RoundTrip(new MassPropertiesF(Math.Abs(fa), v3f, m3f));
            RoundTrip(new PrincipalAxesF(v3f, m3f));
        }
    }

    [Fact]
    public void Derived_properties_are_not_written()
    {
        Assert.Equal("{\"X\":1,\"Y\":2,\"Z\":3,\"W\":4}", JsonSerializer.Serialize(new QuaternionD(1, 2, 3, 4)));
        Assert.Equal("{\"X\":1,\"Y\":2,\"Z\":3,\"W\":4}", JsonSerializer.Serialize(new Vector4D(1, 2, 3, 4)));
        Assert.Equal("{\"Min\":{\"X\":0,\"Y\":0},\"Max\":{\"X\":1,\"Y\":1}}", JsonSerializer.Serialize(new Bounds2D(default, new Vector2D(1, 1))));
        Assert.DoesNotContain("Orientation", JsonSerializer.Serialize(InertiaD.PrincipalAxes(Matrix3x3D.Identity)));
    }

    [Fact]
    public void Source_generated_round_trip_scenario_passes()
    {
        // The scenario throws when any value comes back different; it uses source-generated metadata only.
        var s = Scenarios.All().Single(x => x.Name == "types/json");
        s.Run(new ScenarioContext(1));
    }
}

public class ValidationTests
{
    [Fact]
    public void PrincipalAxes_orientation_is_derived_from_the_rotation()
    {
        Matrix3x3D rot = QuaternionD.CreateFromAxisAngle(new Vector3D(0, 0.6, 0.8), 1.1).ToMatrix();
        var p = new PrincipalAxesD(new Vector3D(1, 2, 3), rot);
        Assert.True(p.Orientation.Equals(QuaternionD.CreateFromRotationMatrix(rot)));
        PrincipalAxesD q = InertiaD.PrincipalAxes(InertiaD.SolidBox(3, new Vector3D(1, 2, 3)));
        Assert.True(q.Orientation.Equals(QuaternionD.CreateFromRotationMatrix(q.Rotation)));
        var pf = new PrincipalAxesF(new Vector3F(1, 2, 3), Matrix3x3F.Identity);
        Assert.True(pf.Orientation.Equals(QuaternionF.Identity));
    }

    [Fact]
    public void MassProperties_rejects_a_negative_mass()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new MassPropertiesD(-1, default, default));
        Assert.Equal("mass", ex.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MassPropertiesF(-0.5f, default, default));
        _ = new MassPropertiesD(0, default, default);
        _ = new MassPropertiesD(-0.0, default, default);
    }

    [Fact]
    public void Parameter_names_are_the_public_parameters()
    {
        Assert.Equal("cx", Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.Orient2D(0, 0, 1, 0, double.NaN, 1)).ParamName);
        Assert.Equal("by", Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.Orient2D(0, 0, 1, double.NaN, 0, 1)).ParamName);
        Assert.Equal("dz", Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.Orient3D(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, double.NaN)).ParamName);
        Assert.Equal("dy", Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.InCircle(0, 0, 1, 0, 0, 1, 0, double.NaN)).ParamName);
        Assert.Equal("ez", Assert.Throws<ArgumentOutOfRangeException>(() =>
            ExactPredicates.InSphere(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, double.NaN)).ParamName);
        Assert.Equal("dy", Assert.Throws<ArgumentOutOfRangeException>(() => SegmentPredicates.Classify(0, 0, 1, 1, 0, 1, 1, double.NaN)).ParamName);
        Assert.Equal("ax", Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.Orient2D(double.PositiveInfinity, 0, 1, 0, 0.5, 1)).ParamName);
        var asym = Assert.Throws<ArgumentException>(() => InertiaD.PrincipalAxes(new Matrix3x3D(1, 2, 0, 0, 1, 0, 0, 0, 1)));
        Assert.Equal("tensor", asym.ParamName);
        var asymF = Assert.Throws<ArgumentException>(() => InertiaF.PrincipalAxes(new Matrix3x3F(1, 2, 0, 0, 1, 0, 0, 0, 1)));
        Assert.Equal("tensor", asymF.ParamName);
    }

    [Fact]
    public void Predicate_range_example_from_the_docs()
    {
        // 1e-50 is outside [2^-140, 2^200]: a clear-cut call may still return its sign, a near-degenerate one throws.
        Assert.Equal(1, ExactPredicates.Orient2D(0, 0, 1, 0, 0.5, 1e-10));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExactPredicates.Orient2D(1e-50, 1e-50, 2e-50, 2e-50, 3e-50, 3e-50));
    }

    [Fact]
    public void FixedBridge_bound_is_exclusive()
    {
        Assert.Equal(2097152.0, FixedBridge.ExclusiveMagnitudeBound);
        Assert.False(FixedBridge.IsInRange(FixedBridge.ExclusiveMagnitudeBound));
        Assert.True(FixedBridge.IsInRange(Math.BitDecrement(FixedBridge.ExclusiveMagnitudeBound)));
    }
}
