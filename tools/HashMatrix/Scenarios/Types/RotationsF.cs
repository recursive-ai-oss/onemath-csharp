using System.Runtime.CompilerServices;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>Hash-matrix scenario for the rotation factories, spherical interpolation and the System.Numerics conversions (float).</summary>
internal static class RotationsFScenarios
{
    [ModuleInitializer]
    internal static void Init() => Scenarios.Register("types/f32/rotation", Run);

    private static float R(TypesRng r) => (float)((r.NextDouble() * 2 - 1) * r.Scale());

    private static Vector3F V3(TypesRng r) => new(R(r), R(r), R(r));

    private static void W(ScenarioContext c, Vector3F v) { c.Write(v.X); c.Write(v.Y); c.Write(v.Z); }

    private static void W(ScenarioContext c, QuaternionF q) { c.Write(q.X); c.Write(q.Y); c.Write(q.Z); c.Write(q.W); }

    private static void W(ScenarioContext c, Matrix3x3F m)
    {
        c.Write(m.M11); c.Write(m.M12); c.Write(m.M13); c.Write(m.M21); c.Write(m.M22); c.Write(m.M23); c.Write(m.M31); c.Write(m.M32); c.Write(m.M33);
    }

    private static void W(ScenarioContext c, Matrix4x4F m)
    {
        W(c, new Matrix3x3F(m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33));
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
            float angle = R(r);
            Vector3F axis = V3(r).Normalized();
            QuaternionF q = QuaternionF.CreateFromAxisAngle(axis, angle);
            W(c, q);
            QuaternionF a = new QuaternionF(R(r), R(r), R(r), R(r)).Normalized();
            QuaternionF b = new QuaternionF(R(r), R(r), R(r), R(r)).Normalized();
            float t = (float)r.NextDouble();
            W(c, QuaternionF.Slerp(a, b, t));
            W(c, QuaternionF.Slerp(a, b, R(r)));
            W(c, QuaternionF.Slerp(a, a, t));

            Matrix3x3F rx = Matrix3x3F.CreateRotationX(angle), ry = Matrix3x3F.CreateRotationY(angle), rz = Matrix3x3F.CreateRotationZ(angle);
            W(c, rx); W(c, ry); W(c, rz);
            W(c, Matrix3x3F.CreateFromQuaternion(q));
            Matrix4x4F m4 = Matrix4x4F.CreateRotationX(angle);
            W(c, m4); W(c, Matrix4x4F.CreateRotationY(angle)); W(c, Matrix4x4F.CreateRotationZ(angle));
            W(c, Matrix4x4F.CreateFromQuaternion(q));
            W(c, Matrix4x4F.CreateFromMatrix3x3(rz));
            var aff = new Affine3F(rx, V3(r));
            W(c, Matrix4x4F.CreateFromAffine(aff));
            Affine3F ar = Affine3F.CreateFromQuaternion(q);
            W(c, ar.Linear); W(c, Affine3F.CreateScale(V3(r)).Linear); W(c, Affine3F.CreateRotationY(angle).Linear); W(c, Affine3F.CreateFromLinear(rz).Linear);
            W(c, Affine3F.CreateTranslation(V3(r)).Translation);

            // System.Numerics round trips (rows and columns swap; double narrows to float).
            System.Numerics.Matrix4x4 sn = (Matrix4x4F.CreateRotationY(angle) * Matrix4x4F.CreateTranslation(V3(r))).ToSystemNumerics();
            W(c, sn);
            W(c, Matrix4x4F.FromSystemNumerics(sn));
            System.Numerics.Quaternion sq = q.ToSystemNumerics();
            c.Write(sq.X); c.Write(sq.Y); c.Write(sq.Z); c.Write(sq.W);
            W(c, QuaternionF.FromSystemNumerics(sq));
            System.Numerics.Vector3 sv = V3(r).ToSystemNumerics();
            c.Write(sv.X); c.Write(sv.Y); c.Write(sv.Z);
            W(c, Vector3F.FromSystemNumerics(sv));
        }
    }
}
