using System.Runtime.CompilerServices;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>Hash-matrix scenario for the rotation factories, spherical interpolation and the System.Numerics conversions (double).</summary>
internal static class RotationsDScenarios
{
    [ModuleInitializer]
    internal static void Init() => Scenarios.Register("types/f64/rotation", Run);

    private static double R(TypesRng r) => (double)((r.NextDouble() * 2 - 1) * r.Scale());

    private static Vector3D V3(TypesRng r) => new(R(r), R(r), R(r));

    private static void W(ScenarioContext c, Vector3D v) { c.Write(v.X); c.Write(v.Y); c.Write(v.Z); }

    private static void W(ScenarioContext c, QuaternionD q) { c.Write(q.X); c.Write(q.Y); c.Write(q.Z); c.Write(q.W); }

    private static void W(ScenarioContext c, Matrix3x3D m)
    {
        c.Write(m.M11); c.Write(m.M12); c.Write(m.M13); c.Write(m.M21); c.Write(m.M22); c.Write(m.M23); c.Write(m.M31); c.Write(m.M32); c.Write(m.M33);
    }

    private static void W(ScenarioContext c, Matrix4x4D m)
    {
        W(c, new Matrix3x3D(m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33));
        c.Write(m.M14); c.Write(m.M24); c.Write(m.M34); c.Write(m.M41); c.Write(m.M42); c.Write(m.M43); c.Write(m.M44);
    }

    private static void W(ScenarioContext c, System.Numerics.Matrix4x4 m)
    {
        c.Write(m.M11); c.Write(m.M12); c.Write(m.M13); c.Write(m.M14); c.Write(m.M21); c.Write(m.M22); c.Write(m.M23); c.Write(m.M24);
        c.Write(m.M31); c.Write(m.M32); c.Write(m.M33); c.Write(m.M34); c.Write(m.M41); c.Write(m.M42); c.Write(m.M43); c.Write(m.M44);
    }

    private static void Run(ScenarioContext c)
    {
        var r = new TypesRng(0x9009);
        for (int i = 0; i < 1500; i++)
        {
            double angle = R(r);
            Vector3D axis = V3(r).Normalized();
            QuaternionD q = QuaternionD.CreateFromAxisAngle(axis, angle);
            W(c, q);
            QuaternionD a = new QuaternionD(R(r), R(r), R(r), R(r)).Normalized();
            QuaternionD b = new QuaternionD(R(r), R(r), R(r), R(r)).Normalized();
            double t = (double)r.NextDouble();
            W(c, QuaternionD.Slerp(a, b, t));
            W(c, QuaternionD.Slerp(a, b, R(r)));
            W(c, QuaternionD.Slerp(a, a, t));

            Matrix3x3D rx = Matrix3x3D.CreateRotationX(angle), ry = Matrix3x3D.CreateRotationY(angle), rz = Matrix3x3D.CreateRotationZ(angle);
            W(c, rx); W(c, ry); W(c, rz);
            W(c, Matrix3x3D.CreateFromQuaternion(q));
            Matrix4x4D m4 = Matrix4x4D.CreateRotationX(angle);
            W(c, m4); W(c, Matrix4x4D.CreateRotationY(angle)); W(c, Matrix4x4D.CreateRotationZ(angle));
            W(c, Matrix4x4D.CreateFromQuaternion(q));
            W(c, Matrix4x4D.CreateFromMatrix3x3(rz));
            var aff = new Affine3D(rx, V3(r));
            W(c, Matrix4x4D.CreateFromAffine(aff));
            Affine3D ar = Affine3D.CreateFromQuaternion(q);
            W(c, ar.Linear); W(c, Affine3D.CreateScale(V3(r)).Linear); W(c, Affine3D.CreateRotationY(angle).Linear); W(c, Affine3D.CreateFromLinear(rz).Linear);
            W(c, Affine3D.CreateTranslation(V3(r)).Translation);

            // System.Numerics round trips (rows and columns swap; double narrows to float).
            System.Numerics.Matrix4x4 sn = (Matrix4x4D.CreateRotationY(angle) * Matrix4x4D.CreateTranslation(V3(r))).ToSystemNumerics();
            W(c, sn);
            W(c, Matrix4x4D.FromSystemNumerics(sn));
            System.Numerics.Quaternion sq = q.ToSystemNumerics();
            c.Write(sq.X); c.Write(sq.Y); c.Write(sq.Z); c.Write(sq.W);
            W(c, QuaternionD.FromSystemNumerics(sq));
            System.Numerics.Vector3 sv = V3(r).ToSystemNumerics();
            c.Write(sv.X); c.Write(sv.Y); c.Write(sv.Z);
            W(c, Vector3D.FromSystemNumerics(sv));
        }
    }
}
