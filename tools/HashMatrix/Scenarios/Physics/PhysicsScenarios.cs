using System;
using System.Runtime.CompilerServices;
using RecursiveAI.OneMath.Physics;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>Hash-matrix scenarios for the symmetric eigen decomposition and the inertia functions, in both precisions.</summary>
internal static class PhysicsScenarios
{
    private const int Count = 6000;

    [ModuleInitializer]
    internal static void Init()
    {
        Scenarios.Register("physics/f64/eigen", EigenD);
        Scenarios.Register("physics/f32/eigen", EigenF);
        Scenarios.Register("physics/f64/inertia", InertiaScenarioD);
        Scenarios.Register("physics/f32/inertia", InertiaScenarioF);
    }

    // Exactly 2^e for e in [-1000, 1000].
    private static double Pow2(int e) => BitConverter.Int64BitsToDouble((long)(e + 1023) << 52);

    // Upper triangle (a11 a12 a13 a22 a23 a33) of a symmetric matrix of one of several shapes, as doubles; the
    // float scenario rounds them. Shapes cover random, wide dynamic range, rank-deficient, repeated eigenvalues,
    // near-degenerate pairs, diagonal, small integers and huge/tiny binary scales.
    private static double[] Sym(TypesRng r, int i, bool single)
    {
        var m = new double[6];
        switch (i % 8)
        {
            case 0:
                for (int k = 0; k < 6; k++)
                {
                    m[k] = r.NextDouble() * 2 - 1;
                }
                break;
            case 1:
                for (int k = 0; k < 6; k++)
                {
                    m[k] = (r.NextDouble() * 2 - 1) * r.Scale();
                }
                break;
            case 2:
            {
                double x = r.NextDouble() * 2 - 1, y = r.NextDouble() * 2 - 1, z = r.NextDouble() * 2 - 1;
                m[0] = x * x;
                m[1] = x * y;
                m[2] = x * z;
                m[3] = y * y;
                m[4] = y * z;
                m[5] = z * z;
                break;
            }
            case 3:
            {
                // Two nearly equal diagonal entries coupled by a tiny off-diagonal.
                double d = r.NextDouble() * 2 - 1;
                double tiny = Pow2(-(int)(r.NextUInt64() % 40) - 5);
                m[0] = d;
                m[3] = d + tiny * r.NextDouble();
                m[5] = r.NextDouble() * 2 - 1;
                m[1] = tiny * (r.NextDouble() * 2 - 1);
                m[2] = r.NextUInt64() % 3 == 0 ? tiny : 0;
                break;
            }
            case 4:
                m[0] = (int)(r.NextUInt64() % 3) - 1;
                m[3] = (int)(r.NextUInt64() % 3) - 1;
                m[5] = (int)(r.NextUInt64() % 3) - 1;
                break;
            case 5:
                for (int k = 0; k < 6; k++)
                {
                    m[k] = (int)(r.NextUInt64() % 5) - 2;
                }
                break;
            case 6:
            {
                int e = (int)(r.NextUInt64() % (single ? 200UL : 1800UL)) - (single ? 100 : 900);
                double s = Pow2(e);
                for (int k = 0; k < 6; k++)
                {
                    m[k] = (r.NextDouble() * 2 - 1) * s;
                }
                break;
            }
            default:
                // All entries equal: a rank-one matrix with a three-fold null-space-ish structure.
                m[0] = m[1] = m[2] = m[3] = m[4] = m[5] = r.NextDouble() * 2 - 1;
                break;
        }
        return m;
    }

    private static void EigenD(ScenarioContext c)
    {
        var r = new TypesRng(0xE1D0);
        for (int i = 0; i < Count; i++)
        {
            double[] m = Sym(r, i, false);
            var e = SymmetricEigenD.Decompose(new Matrix3x3D(m[0], m[1], m[2], m[1], m[3], m[4], m[2], m[4], m[5]));
            Write(c, e.Values);
            Write(c, e.Vectors);
        }
    }

    private static void EigenF(ScenarioContext c)
    {
        var r = new TypesRng(0xE1F0);
        for (int i = 0; i < Count; i++)
        {
            double[] d = Sym(r, i, true);
            float[] m = { (float)d[0], (float)d[1], (float)d[2], (float)d[3], (float)d[4], (float)d[5] };
            var e = SymmetricEigenF.Decompose(new Matrix3x3F(m[0], m[1], m[2], m[1], m[3], m[4], m[2], m[4], m[5]));
            Write(c, e.Values);
            Write(c, e.Vectors);
        }
    }

    private static double U(TypesRng r) => r.NextDouble() * 2 - 1;

    private static void InertiaScenarioD(ScenarioContext c)
    {
        var r = new TypesRng(0x1AE7D);
        for (int i = 0; i < Count / 3; i++)
        {
            double mass = 0.25 + r.NextDouble() * 8;
            Matrix3x3D box = InertiaD.SolidBox(mass, new Vector3D(0.1 + r.NextDouble(), 0.1 + r.NextDouble(), 0.1 + r.NextDouble()));
            Write(c, box);
            Write(c, InertiaD.SolidSphere(mass, 0.1 + r.NextDouble()));
            Write(c, InertiaD.SolidCylinder(mass, 0.1 + r.NextDouble(), 0.1 + r.NextDouble()));

            QuaternionD q = new QuaternionD(U(r), U(r), U(r), U(r) + 2).Normalized();
            Matrix3x3D rotated = InertiaD.Rotate(box, q);
            Write(c, rotated);
            Write(c, InertiaD.Rotate(rotated, q.ToMatrix().Transpose()));

            var d = new Vector3D(U(r) * 4, U(r) * 4, U(r) * 4);
            Matrix3x3D at = InertiaD.ShiftToPoint(rotated, mass, d);
            Write(c, at);
            Write(c, InertiaD.ShiftToCenter(at, mass, d));

            int n = 1 + (int)(r.NextUInt64() % 5);
            var parts = new MassPropertiesD[n];
            for (int k = 0; k < n; k++)
            {
                double pm = 0.1 + r.NextDouble() * 3;
                parts[k] = new MassPropertiesD(pm, new Vector3D(U(r) * 3, U(r) * 3, U(r) * 3),
                    InertiaD.Rotate(InertiaD.SolidBox(pm, new Vector3D(0.2 + r.NextDouble(), 0.2 + r.NextDouble(), 0.2 + r.NextDouble())),
                        new QuaternionD(U(r), U(r), U(r), U(r) + 2).Normalized()));
            }
            Write(c, InertiaD.Combine(parts));

            PrincipalAxesD p = InertiaD.PrincipalAxes(rotated);
            Write(c, p.Moments);
            Write(c, p.Rotation);
            c.Write(p.Orientation.X);
            c.Write(p.Orientation.Y);
            c.Write(p.Orientation.Z);
            c.Write(p.Orientation.W);
        }
    }

    private static void InertiaScenarioF(ScenarioContext c)
    {
        var r = new TypesRng(0x1AE7F);
        for (int i = 0; i < Count / 3; i++)
        {
            float mass = (float)(0.25 + r.NextDouble() * 8);
            Matrix3x3F box = InertiaF.SolidBox(mass, new Vector3F((float)(0.1 + r.NextDouble()), (float)(0.1 + r.NextDouble()), (float)(0.1 + r.NextDouble())));
            Write(c, box);
            Write(c, InertiaF.SolidSphere(mass, (float)(0.1 + r.NextDouble())));
            Write(c, InertiaF.SolidCylinder(mass, (float)(0.1 + r.NextDouble()), (float)(0.1 + r.NextDouble())));

            QuaternionF q = new QuaternionF((float)U(r), (float)U(r), (float)U(r), (float)(U(r) + 2)).Normalized();
            Matrix3x3F rotated = InertiaF.Rotate(box, q);
            Write(c, rotated);
            Write(c, InertiaF.Rotate(rotated, q.ToMatrix().Transpose()));

            var d = new Vector3F((float)(U(r) * 4), (float)(U(r) * 4), (float)(U(r) * 4));
            Matrix3x3F at = InertiaF.ShiftToPoint(rotated, mass, d);
            Write(c, at);
            Write(c, InertiaF.ShiftToCenter(at, mass, d));

            int n = 1 + (int)(r.NextUInt64() % 5);
            var parts = new MassPropertiesF[n];
            for (int k = 0; k < n; k++)
            {
                float pm = (float)(0.1 + r.NextDouble() * 3);
                parts[k] = new MassPropertiesF(pm, new Vector3F((float)(U(r) * 3), (float)(U(r) * 3), (float)(U(r) * 3)),
                    InertiaF.Rotate(InertiaF.SolidBox(pm, new Vector3F((float)(0.2 + r.NextDouble()), (float)(0.2 + r.NextDouble()), (float)(0.2 + r.NextDouble()))),
                        new QuaternionF((float)U(r), (float)U(r), (float)U(r), (float)(U(r) + 2)).Normalized()));
            }
            Write(c, InertiaF.Combine(parts));

            PrincipalAxesF p = InertiaF.PrincipalAxes(rotated);
            Write(c, p.Moments);
            Write(c, p.Rotation);
            c.Write(p.Orientation.X);
            c.Write(p.Orientation.Y);
            c.Write(p.Orientation.Z);
            c.Write(p.Orientation.W);
        }
    }

    private static void Write(ScenarioContext c, MassPropertiesD m)
    {
        c.Write(m.Mass);
        Write(c, m.Center);
        Write(c, m.Inertia);
    }

    private static void Write(ScenarioContext c, MassPropertiesF m)
    {
        c.Write(m.Mass);
        Write(c, m.Center);
        Write(c, m.Inertia);
    }

    private static void Write(ScenarioContext c, Vector3D v)
    {
        c.Write(v.X);
        c.Write(v.Y);
        c.Write(v.Z);
    }

    private static void Write(ScenarioContext c, Vector3F v)
    {
        c.Write(v.X);
        c.Write(v.Y);
        c.Write(v.Z);
    }

    private static void Write(ScenarioContext c, Matrix3x3D m)
    {
        c.Write(m.M11);
        c.Write(m.M12);
        c.Write(m.M13);
        c.Write(m.M21);
        c.Write(m.M22);
        c.Write(m.M23);
        c.Write(m.M31);
        c.Write(m.M32);
        c.Write(m.M33);
    }

    private static void Write(ScenarioContext c, Matrix3x3F m)
    {
        c.Write(m.M11);
        c.Write(m.M12);
        c.Write(m.M13);
        c.Write(m.M21);
        c.Write(m.M22);
        c.Write(m.M23);
        c.Write(m.M31);
        c.Write(m.M32);
        c.Write(m.M33);
    }
}
