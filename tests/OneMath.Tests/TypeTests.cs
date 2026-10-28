using System;
using Xunit;

namespace RecursiveAI.OneMath.Tests;

public class VectorTests
{
    private static void SameBits(float expected, float actual) =>
        Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual));

    private static void SameBits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));

    [Fact]
    public void Known_answers_float()
    {
        var a = new Vector3F(1, 2, 3);
        var b = new Vector3F(4, 5, 6);
        Assert.Equal(32f, Vector3F.Dot(a, b));
        Assert.Equal(new Vector3F(-3, 6, -3), Vector3F.Cross(a, b));
        Assert.Equal(new Vector3F(5, 7, 9), a + b);
        Assert.Equal(3f, new Vector3F(1, 2, 2).Length());
        Assert.Equal(5f, new Vector2F(3, 4).Length());
        Assert.Equal(-2f, Vector2F.Cross(new Vector2F(1, 2), new Vector2F(3, 4)));
        // 3/5 and 4/5 are the correctly rounded quotients, i.e. the literals.
        Vector3F n = new Vector3F(0, 3, 4).Normalized();
        SameBits(0f, n.X);
        SameBits(0.6f, n.Y);
        SameBits(0.8f, n.Z);
        Assert.Equal(Vector3F.Zero, Vector3F.Zero.Normalized());
        Assert.Equal(30f, new Vector4F(1, 2, 3, 4).LengthSquared());
        Assert.Equal(new Vector3F(2.5f, 3.5f, 4.5f), Vector3F.Lerp(a, b, 0.5f));
    }

    [Fact]
    public void Known_answers_double()
    {
        var a = new Vector3D(1, 2, 3);
        var b = new Vector3D(4, 5, 6);
        Assert.Equal(32.0, Vector3D.Dot(a, b));
        Assert.Equal(new Vector3D(-3, 6, -3), Vector3D.Cross(a, b));
        SameBits(0.6, new Vector3D(0, 3, 4).Normalized().Y);
        SameBits(0.8, new Vector2D(3, 4).Normalized().Y);
        Assert.Equal(Math.Sqrt(30.0), new Vector4D(1, 2, 3, 4).Length());
    }

    [Fact]
    public void Sums_accumulate_left_to_right()
    {
        // (1e8 + 1) rounds to 1e8 in float32; a reassociated sum would give 1 instead of 0.
        float dot = Vector3F.Dot(new Vector3F(1e8f, 1f, -1e8f), Vector3F.One);
        SameBits(0f, dot);
        Assert.Equal(0.0, Vector3D.Dot(new Vector3D(1e16, 1, -1e16), Vector3D.One));
    }

    [Fact]
    public void Multiply_then_add_is_not_fused()
    {
        // a*a is 1 + 2^-11 + 2^-24 exactly; rounded it is 1 + 2^-11. Fused with -(1 + 2^-11) it would leave 2^-24.
        float a = 1f + MathF.Pow(2f, -12);
        float c = -(1f + MathF.Pow(2f, -11));
        SameBits(0f, Vector2F.Dot(new Vector2F(a, c), new Vector2F(a, 1f)));
        double ad = 1.0 + Math.Pow(2.0, -27);
        double cd = -(1.0 + Math.Pow(2.0, -26));
        SameBits(0.0, Vector2D.Dot(new Vector2D(ad, cd), new Vector2D(ad, 1.0)));
    }

    [Fact]
    public void Float_sqrt_is_correctly_rounded()
    {
        var rng = new Random(5);
        for (int i = 0; i < 200000; i++)
        {
            float x = BitConverter.Int32BitsToSingle(rng.Next(0x00800000, 0x7f000000));
            float got = Scalar.Sqrt(x);
            // Correct rounding: x lies between the squares of the midpoints on either side of the result.
            // Midpoints have 25 significant bits, so their squares are exact in double.
            double lo = ((double)got + MathF.BitDecrement(got)) / 2;
            double hi = ((double)got + MathF.BitIncrement(got)) / 2;
            Assert.True(lo * lo < x && x < hi * hi, $"sqrt({x}) = {got}");
        }
    }

    [Fact]
    public void Indexer_and_conversions()
    {
        var v = new Vector4D(1.1, 2.2, 3.3, 4.4);
        Assert.Equal(3.3, v[2]);
        Assert.Throws<ArgumentOutOfRangeException>(() => v[4]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vector2F(1, 2)[-1]);
        // Double to float rounds to nearest; float to double is exact.
        Assert.Equal(new Vector4F(1.1f, 2.2f, 3.3f, 4.4f), v.ToSingle());
        Assert.Equal(new Vector3D(0.1f, 0.2f, 0.3f), new Vector3F(0.1f, 0.2f, 0.3f).ToDouble());
    }

    [Fact]
    public void Equality_follows_ieee_but_equals_treats_nan_as_equal()
    {
        var nan = new Vector2F(float.NaN, 0);
        var nanCopy = nan;
        Assert.False(nan == nanCopy);
        Assert.True(nan.Equals(nan));
        Assert.True(new Vector2F(0f, 0f) == new Vector2F(-0f, 0f));
        Assert.Equal(new Vector2F(0f, 1f).GetHashCode(), new Vector2F(-0f, 1f).GetHashCode());
        Assert.Equal(new Vector2D(double.NaN, 1).GetHashCode(), new Vector2D(-double.NaN, 1).GetHashCode());
    }

    [Fact]
    public void Min_max_abs()
    {
        var a = new Vector3F(1, -5, 3);
        var b = new Vector3F(2, -6, 3);
        Assert.Equal(new Vector3F(1, -6, 3), Vector3F.Min(a, b));
        Assert.Equal(new Vector3F(2, -5, 3), Vector3F.Max(a, b));
        Assert.Equal(new Vector3F(1, 5, 3), a.Abs());
    }
}

public class MatrixTests
{
    [Fact]
    public void Known_answers_4x4()
    {
        // Scale (2, 4, 8) then translation (1, 2, 3): everything is exactly representable.
        Matrix4x4F m = Matrix4x4F.CreateTranslation(new Vector3F(1, 2, 3)) * Matrix4x4F.CreateScale(new Vector3F(2, 4, 8));
        Assert.Equal(new Matrix4x4F(2, 0, 0, 1, 0, 4, 0, 2, 0, 0, 8, 3, 0, 0, 0, 1), m);
        Assert.Equal(64f, m.Determinant());
        Assert.True(m.TryInvert(out Matrix4x4F inv));
        Assert.Equal(new Matrix4x4F(0.5f, 0, 0, -0.5f, 0, 0.25f, 0, -0.5f, 0, 0, 0.125f, -0.375f, 0, 0, 0, 1), inv);
        Assert.Equal(Matrix4x4F.Identity, m * inv);
        Assert.Equal(new Vector3F(3, 6, 11), m.TransformPoint(new Vector3F(1, 1, 1)));
        Assert.Equal(new Vector3F(2, 4, 8), m.TransformVector(new Vector3F(1, 1, 1)));
        Assert.Equal(new Vector4F(3, 6, 11, 1), m.Transform(new Vector4F(1, 1, 1, 1)));
    }

    [Fact]
    public void Known_answers_3x3_and_2x2()
    {
        var m = new Matrix3x3D(2, 0, 0, 0, 4, 0, 0, 0, 8);
        Assert.Equal(64.0, m.Determinant());
        Assert.True(m.TryInvert(out Matrix3x3D inv));
        Assert.Equal(new Matrix3x3D(0.5, 0, 0, 0, 0.25, 0, 0, 0, 0.125), inv);
        var m2 = new Matrix2x2F(1, 2, 3, 4);
        Assert.Equal(-2f, m2.Determinant());
        Assert.True(m2.TryInvert(out Matrix2x2F i2));
        Assert.Equal(new Matrix2x2F(-2, 1, 1.5f, -0.5f), i2);
        Assert.Equal(new Matrix2x2F(7, 10, 15, 22), m2 * m2);
        Assert.Equal(new Vector2F(5, 11), m2.Transform(new Vector2F(1, 2)));
        Assert.Equal(new Matrix2x2F(1, 3, 2, 4), m2.Transposed());
    }

    [Fact]
    public void Singular_and_non_finite_matrices_do_not_invert()
    {
        Assert.False(new Matrix2x2F(1, 2, 2, 4).TryInvert(out Matrix2x2F z2));
        Assert.Equal(default, z2);
        Assert.False(new Matrix3x3F(1, 2, 3, 2, 4, 6, 0, 1, 1).TryInvert(out _));
        Assert.False(Matrix4x4D.Identity.Transposed().Equals(default));
        Assert.False(new Matrix4x4D(1, 2, 3, 4, 2, 4, 6, 8, 0, 1, 0, 0, 0, 0, 1, 0).TryInvert(out _));
        Assert.False(new Matrix3x3D(double.NaN, 0, 0, 0, 1, 0, 0, 0, 1).TryInvert(out _));
    }

    private static double Next(Random r) => (r.NextDouble() * 2) - 1;

    [Fact]
    public void Random_inverses_multiply_to_identity()
    {
        var rng = new Random(99);
        int checkedCount = 0;
        for (int i = 0; i < 2000; i++)
        {
            var m4 = new Matrix4x4D(
                Next(rng), Next(rng), Next(rng), Next(rng), Next(rng), Next(rng), Next(rng), Next(rng),
                Next(rng), Next(rng), Next(rng), Next(rng), Next(rng), Next(rng), Next(rng), Next(rng));
            if (Math.Abs(m4.Determinant()) > 0.05 && m4.TryInvert(out Matrix4x4D i4))
            {
                AssertNearIdentity4(m4 * i4);
                AssertNearIdentity4(i4 * m4);
                checkedCount++;
            }
            var m3 = new Matrix3x3D(Next(rng), Next(rng), Next(rng), Next(rng), Next(rng), Next(rng), Next(rng), Next(rng), Next(rng));
            if (Math.Abs(m3.Determinant()) > 0.05 && m3.TryInvert(out Matrix3x3D i3))
            {
                var p = m3 * i3;
                Assert.True(Math.Abs(p.M11 - 1) < 1e-9 && Math.Abs(p.M22 - 1) < 1e-9 && Math.Abs(p.M33 - 1) < 1e-9);
                Assert.True(Math.Abs(p.M12) + Math.Abs(p.M13) + Math.Abs(p.M21) + Math.Abs(p.M23) + Math.Abs(p.M31) + Math.Abs(p.M32) < 1e-9);
            }
            var m2 = new Matrix2x2D(Next(rng), Next(rng), Next(rng), Next(rng));
            if (Math.Abs(m2.Determinant()) > 0.05 && m2.TryInvert(out Matrix2x2D i2))
            {
                var p = m2 * i2;
                Assert.True(Math.Abs(p.M11 - 1) < 1e-9 && Math.Abs(p.M22 - 1) < 1e-9 && Math.Abs(p.M12) + Math.Abs(p.M21) < 1e-9);
            }
        }
        Assert.True(checkedCount > 500);
    }

    private static void AssertNearIdentity4(Matrix4x4D p)
    {
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                double v = p.Row(r)[c];
                Assert.True(Math.Abs(v - (r == c ? 1 : 0)) < 1e-9, $"entry ({r},{c}) = {v}");
            }
        }
    }

    [Fact]
    public void Determinant_4x4_matches_recursive_expansion()
    {
        var rng = new Random(3);
        for (int i = 0; i < 500; i++)
        {
            double[,] a = new double[4, 4];
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    a[r, c] = Next(rng);
                }
            }
            var m = new Matrix4x4D(
                a[0, 0], a[0, 1], a[0, 2], a[0, 3], a[1, 0], a[1, 1], a[1, 2], a[1, 3],
                a[2, 0], a[2, 1], a[2, 2], a[2, 3], a[3, 0], a[3, 1], a[3, 2], a[3, 3]);
            Assert.True(Math.Abs(m.Determinant() - Det(a, 4)) < 1e-12);
            Assert.True(Math.Abs(m.Determinant() - m.Transposed().Determinant()) < 1e-12);
        }
    }

    private static double Det(double[,] a, int n)
    {
        if (n == 1)
        {
            return a[0, 0];
        }
        double sum = 0;
        for (int skip = 0; skip < n; skip++)
        {
            double[,] sub = new double[n - 1, n - 1];
            for (int r = 1; r < n; r++)
            {
                for (int c = 0, cc = 0; c < n; c++)
                {
                    if (c != skip)
                    {
                        sub[r - 1, cc++] = a[r, c];
                    }
                }
            }
            sum += (skip % 2 == 0 ? 1 : -1) * a[0, skip] * Det(sub, n - 1);
        }
        return sum;
    }

    [Fact]
    public void Rows_columns_and_factories()
    {
        var m = Matrix3x3F.FromRows(new Vector3F(1, 2, 3), new Vector3F(4, 5, 6), new Vector3F(7, 8, 9));
        Assert.Equal(new Vector3F(2, 5, 8), m.Column(1));
        Assert.Equal(m, Matrix3x3F.FromColumns(new Vector3F(1, 4, 7), new Vector3F(2, 5, 8), new Vector3F(3, 6, 9)));
        Assert.Equal(m.Transposed().Transposed(), m);
        Assert.Throws<ArgumentOutOfRangeException>(() => m.Row(3));
        Assert.Equal(Matrix4x4F.Identity, Matrix4x4F.Identity.Transposed());
        Assert.Equal(m.ToDouble().ToSingle(), m);
    }
}

public class QuaternionTests
{
    private static Random Rng() => new(2024);

    private static QuaternionD RandomUnit(Random r)
    {
        while (true)
        {
            var q = new QuaternionD((r.NextDouble() * 2) - 1, (r.NextDouble() * 2) - 1, (r.NextDouble() * 2) - 1, (r.NextDouble() * 2) - 1);
            if (q.Length() > 0.1)
            {
                return q.Normalized();
            }
        }
    }

    [Fact]
    public void Hamilton_known_answers()
    {
        var i = new QuaternionF(1, 0, 0, 0);
        var j = new QuaternionF(0, 1, 0, 0);
        var k = new QuaternionF(0, 0, 1, 0);
        Assert.Equal(k, i * j);
        Assert.Equal(new QuaternionF(0, 0, -1, 0), j * i);
        Assert.Equal(new QuaternionF(0, 0, 0, -1), i * i);
        Assert.Equal(i, i * QuaternionF.Identity);
        Assert.Equal(new QuaternionF(0, 0, 0, 1), new QuaternionF(0, 0, 0, 5).Normalized());
        Assert.Equal(QuaternionF.Identity, default(QuaternionF).Normalized());
    }

    [Fact]
    public void Rotate_known_answers()
    {
        // 180 degrees about Z is (0, 0, 1, 0).
        var half = new QuaternionF(0, 0, 1, 0);
        Assert.Equal(new Vector3F(-1, -2, 3), half.Rotate(new Vector3F(1, 2, 3)));
        // 90 degrees about Z from precomputed sin/cos of 45 degrees.
        double s = Math.Sqrt(0.5);
        var q = QuaternionD.CreateFromAxisSinCos(Vector3D.UnitZ, s, s);
        Vector3D r = q.Rotate(Vector3D.UnitX);
        Assert.True(Math.Abs(r.X) < 1e-15 && Math.Abs(r.Y - 1) < 1e-15 && r.Z == 0);
        Assert.Equal(new Vector3F(0, 0, 0), QuaternionF.Identity.Rotate(Vector3F.Zero));
    }

    [Fact]
    public void Matrix_conversion_agrees_with_rotation_and_round_trips()
    {
        var rng = Rng();
        for (int n = 0; n < 5000; n++)
        {
            QuaternionD q = RandomUnit(rng);
            Matrix3x3D m = q.ToMatrix();
            var v = new Vector3D((rng.NextDouble() * 2) - 1, (rng.NextDouble() * 2) - 1, (rng.NextDouble() * 2) - 1);
            Vector3D a = q.Rotate(v), b = m.Transform(v);
            Assert.True((a - b).Length() < 1e-14);
            Assert.True(Math.Abs(m.Determinant() - 1) < 1e-14);
            QuaternionD back = QuaternionD.CreateFromRotationMatrix(m);
            double d = Math.Abs(QuaternionD.Dot(q, back));
            Assert.True(Math.Abs(d - 1) < 1e-13, $"dot {d}");
        }
    }

    [Fact]
    public void Composition_matches_matrix_product_and_inverse_undoes()
    {
        var rng = Rng();
        for (int n = 0; n < 2000; n++)
        {
            QuaternionD a = RandomUnit(rng), b = RandomUnit(rng);
            Matrix3x3D pm = a.ToMatrix() * b.ToMatrix();
            Matrix3x3D qm = (a * b).ToMatrix();
            Assert.True(Math.Abs(pm.M11 - qm.M11) + Math.Abs(pm.M23 - qm.M23) + Math.Abs(pm.M32 - qm.M32) < 1e-13);
            Assert.True(a.TryInvert(out QuaternionD inv));
            QuaternionD id = a * inv;
            Assert.True(Math.Abs(id.W - 1) < 1e-14 && Math.Abs(id.X) + Math.Abs(id.Y) + Math.Abs(id.Z) < 1e-14);
        }
        Assert.False(default(QuaternionD).TryInvert(out QuaternionD z));
        Assert.Equal(QuaternionD.Identity, z);
    }

    [Fact]
    public void NLerp_takes_the_shortest_arc_and_stays_unit_length()
    {
        var a = QuaternionD.Identity;
        var b = new QuaternionD(0, 0, -Math.Sqrt(0.5), -Math.Sqrt(0.5)); // same rotation as (0,0,s,s), opposite hemisphere
        QuaternionD m = QuaternionD.NLerp(a, b, 0.5);
        Assert.True(m.W > 0);
        Assert.True(Math.Abs(m.Length() - 1) < 1e-15);
        Assert.Equal(a, QuaternionD.NLerp(a, b, 0.0));
    }
}

public class TransformAndBoundsTests
{
    [Fact]
    public void Rigid_transform_composition_inverse_and_matrix_agree()
    {
        double s = Math.Sqrt(0.5);
        var a = new RigidTransformD(QuaternionD.CreateFromAxisSinCos(Vector3D.UnitZ, s, s), new Vector3D(1, 2, 3));
        var b = new RigidTransformD(QuaternionD.CreateFromAxisSinCos(Vector3D.UnitX, s, s), new Vector3D(-4, 0, 5));
        var p = new Vector3D(0.5, -1.5, 2.5);
        Vector3D viaCompose = (a * b).TransformPoint(p);
        Vector3D viaSteps = a.TransformPoint(b.TransformPoint(p));
        Assert.True((viaCompose - viaSteps).Length() < 1e-14);
        Assert.True((a.Inverted().TransformPoint(a.TransformPoint(p)) - p).Length() < 1e-14);
        Assert.True((a.ToMatrix().TransformPoint(p) - a.TransformPoint(p)).Length() < 1e-14);
        Assert.Equal(new Vector3F(1, 2, 3), RigidTransformF.Identity.TransformPoint(new Vector3F(1, 2, 3)));
    }

    [Fact]
    public void Affine_composition_and_inverse()
    {
        var a = new Affine3F(new Matrix3x3F(2, 0, 0, 0, 4, 0, 0, 0, 8), new Vector3F(1, 2, 3));
        var b = Affine3F.CreateTranslation(new Vector3F(10, 20, 30));
        Assert.Equal(new Vector3F(23, 86, 251), (a * b).TransformPoint(Vector3F.One));
        Assert.Equal((a * b).TransformPoint(Vector3F.One), a.TransformPoint(b.TransformPoint(Vector3F.One)));
        Assert.True(a.TryInvert(out Affine3F inv));
        Assert.Equal(Vector3F.One, inv.TransformPoint(a.TransformPoint(Vector3F.One)));
        Assert.Equal(new Vector3F(2, 4, 8), a.TransformVector(Vector3F.One));
        Assert.Equal(a.ToMatrix().TransformPoint(Vector3F.One), a.TransformPoint(Vector3F.One));
        Assert.False(new Affine3F(default, Vector3F.Zero).TryInvert(out _));
    }

    [Fact]
    public void Bounds_behave()
    {
        var a = Bounds3F.CreateFromPoints(new[] { new Vector3F(0, 0, 0), new Vector3F(2, 4, 6) });
        var b = Bounds3F.CreateFromCenterExtents(new Vector3F(2, 4, 6), Vector3F.One);
        Assert.Equal(new Vector3F(1, 2, 3), a.Center);
        Assert.Equal(48f, a.Volume());
        Assert.Equal(2f * (8 + 24 + 12), a.SurfaceArea());
        Assert.True(a.Intersects(b));
        Assert.False(a.Contains(b));
        Assert.True(a.Contains(new Vector3F(2, 4, 6)));
        Assert.Equal(new Bounds3F(Vector3F.Zero, new Vector3F(3, 5, 7)), Bounds3F.Union(a, b));
        Assert.Equal(new Bounds3F(new Vector3F(1, 3, 5), new Vector3F(2, 4, 6)), Bounds3F.Intersection(a, b));
        Assert.True(Bounds3F.Intersection(a, Bounds3F.CreateFromCenterExtents(new Vector3F(10, 10, 10), Vector3F.One)).IsEmpty);
        Assert.True(Bounds3F.Empty.IsEmpty);
        Assert.False(Bounds3F.Empty.Intersects(a));
        Assert.False(a.Intersects(Bounds3F.Empty));
        Assert.Equal(a, Bounds3F.Union(Bounds3F.Empty, a));
        Assert.Equal(Bounds3F.Empty, Bounds3F.CreateFromPoints(ReadOnlySpan<Vector3F>.Empty));
        Assert.Equal(0f, Bounds3F.Empty.Volume());
        Assert.Equal(new Bounds3F(new Vector3F(-1, -1, -1), new Vector3F(3, 5, 7)), a.Inflate(1));

        var r = Bounds2D.CreateFromPoints(new[] { new Vector2D(-1, -1), new Vector2D(3, 1) });
        Assert.Equal(8.0, r.Area());
        Assert.Equal(new Vector2D(1, 0), r.Center);
        Assert.True(r.Contains(new Vector2D(3, 1)));
        Assert.False(r.Contains(new Vector2D(3.0000001, 1)));
        Assert.Equal(r, r.ToSingle().ToDouble());
        Assert.True(r.Contains(Bounds2D.CreateFromCenterExtents(Vector2D.Zero, new Vector2D(1, 1))));
    }
}
