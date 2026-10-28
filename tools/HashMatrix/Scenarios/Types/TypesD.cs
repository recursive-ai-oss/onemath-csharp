using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>Hash-matrix scenarios for the double vector, matrix, quaternion, transform and bounds types.</summary>
internal static class TypesDScenarios
{
    private const int Count = 1500;

    [ModuleInitializer]
    internal static void Init()
    {
        Scenarios.Register("types/f64/vector", Vectors);
        Scenarios.Register("types/f64/matrix", Matrices);
        Scenarios.Register("types/f64/quaternion", Quaternions);
        Scenarios.Register("types/f64/transform", Transforms);
        Scenarios.Register("types/f64/bounds", Bounds);
        Scenarios.Register("types/f64/matrix-parallel", MatrixParallel);
    }

    // Random values of mixed magnitude (a power-of-two scale between 2^-20 and 2^20), so intermediate roundings vary.
    private static double R(TypesRng r)
    {
        double scale = r.Scale();
        return (double)((r.NextDouble() * 2 - 1) * scale);
    }

    private static Vector2D V2(TypesRng r) => new(R(r), R(r));

    private static Vector3D V3(TypesRng r) => new(R(r), R(r), R(r));

    private static Vector4D V4(TypesRng r) => new(R(r), R(r), R(r), R(r));

    private static Matrix2x2D M2(TypesRng r) => new(R(r), R(r), R(r), R(r));

    private static Matrix3x3D M3(TypesRng r) => new(R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r));

    private static Matrix4x4D M4(TypesRng r) => new(
        R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r));

    private static QuaternionD Q(TypesRng r) => new(R(r), R(r), R(r), R(r));

    private static void W(ScenarioContext c, Vector2D v) { c.Write(v.X); c.Write(v.Y); }

    private static void W(ScenarioContext c, Vector3D v) { c.Write(v.X); c.Write(v.Y); c.Write(v.Z); }

    private static void W(ScenarioContext c, Vector4D v) { c.Write(v.X); c.Write(v.Y); c.Write(v.Z); c.Write(v.W); }

    private static void W(ScenarioContext c, QuaternionD q) { c.Write(q.X); c.Write(q.Y); c.Write(q.Z); c.Write(q.W); }

    private static void W(ScenarioContext c, Matrix2x2D m) { W(c, m.Row(0)); W(c, m.Row(1)); }

    private static void W(ScenarioContext c, Matrix3x3D m) { W(c, m.Row(0)); W(c, m.Row(1)); W(c, m.Row(2)); }

    private static void W(ScenarioContext c, Matrix4x4D m) { W(c, m.Row(0)); W(c, m.Row(1)); W(c, m.Row(2)); W(c, m.Row(3)); }

    private static void W(ScenarioContext c, Bounds3D b) { W(c, b.Min); W(c, b.Max); }

    private static void Vectors(ScenarioContext c)
    {
        var r = new TypesRng(0x1001);
        for (int i = 0; i < Count; i++)
        {
            Vector2D a2 = V2(r), b2 = V2(r);
            W(c, a2 + b2); W(c, a2 - b2); W(c, a2 * b2); W(c, a2 / b2.X); W(c, a2.Normalized());
            c.Write(Vector2D.Dot(a2, b2)); c.Write(Vector2D.Cross(a2, b2)); c.Write(a2.Length()); c.Write(Vector2D.Distance(a2, b2));
            W(c, Vector2D.Lerp(a2, b2, R(r)));

            Vector3D a3 = V3(r), b3 = V3(r);
            W(c, a3 + b3); W(c, a3 - b3); W(c, a3 * b3); W(c, a3 * R(r)); W(c, a3 / b3.Y); W(c, a3.Normalized());
            c.Write(Vector3D.Dot(a3, b3)); W(c, Vector3D.Cross(a3, b3)); c.Write(a3.Length()); c.Write(Vector3D.Distance(a3, b3));
            W(c, Vector3D.Lerp(a3, b3, R(r))); W(c, Vector3D.Min(a3, b3)); W(c, Vector3D.Max(a3, b3)); W(c, a3.Abs());

            Vector4D a4 = V4(r), b4 = V4(r);
            W(c, a4 + b4); W(c, a4 - b4); W(c, a4 * b4); W(c, a4.Normalized()); c.Write(Vector4D.Dot(a4, b4)); c.Write(a4.Length());
            W(c, Vector4D.Lerp(a4, b4, R(r)));
            W(c, a3.ToSingle().ToDouble());
        }
    }

    private static void Matrices(ScenarioContext c)
    {
        var r = new TypesRng(0x2002);
        for (int i = 0; i < Count; i++)
        {
            Matrix2x2D a2 = M2(r), b2 = M2(r);
            W(c, a2 * b2); W(c, a2.Transposed()); c.Write(a2.Determinant()); W(c, a2.Transform(V2(r)));
            c.Write(a2.TryInvert(out Matrix2x2D i2) ? 1 : 0); W(c, i2);

            Matrix3x3D a3 = M3(r), b3 = M3(r);
            W(c, a3 * b3); W(c, a3.Transposed()); c.Write(a3.Determinant()); W(c, a3.Transform(V3(r)));
            c.Write(a3.TryInvert(out Matrix3x3D i3) ? 1 : 0); W(c, i3);

            Matrix4x4D a4 = M4(r), b4 = M4(r);
            W(c, a4 * b4); W(c, a4.Transposed()); c.Write(a4.Determinant()); W(c, a4.Transform(V4(r)));
            W(c, a4.TransformPoint(V3(r))); W(c, a4.TransformVector(V3(r)));
            c.Write(a4.TryInvert(out Matrix4x4D i4) ? 1 : 0); W(c, i4);
        }
    }

    private static void Quaternions(ScenarioContext c)
    {
        var r = new TypesRng(0x3003);
        for (int i = 0; i < Count; i++)
        {
            QuaternionD a = Q(r).Normalized(), b = Q(r).Normalized();
            W(c, a * b); W(c, a.Conjugated()); W(c, a.Normalized()); c.Write(QuaternionD.Dot(a, b)); c.Write(a.Length());
            W(c, QuaternionD.NLerp(a, b, R(r))); W(c, QuaternionD.Lerp(a, b, R(r)));
            c.Write(Q(r).TryInvert(out QuaternionD inv) ? 1 : 0); W(c, inv);
            W(c, a.Rotate(V3(r)));
            Matrix3x3D m = a.ToMatrix();
            W(c, m); W(c, QuaternionD.CreateFromRotationMatrix(m));
        }
    }

    private static void Transforms(ScenarioContext c)
    {
        var r = new TypesRng(0x4004);
        for (int i = 0; i < Count; i++)
        {
            var ra = new RigidTransformD(Q(r).Normalized(), V3(r));
            var rb = new RigidTransformD(Q(r).Normalized(), V3(r));
            RigidTransformD rc = ra * rb;
            W(c, rc.Rotation); W(c, rc.Translation); W(c, ra.Inverted().Rotation); W(c, ra.Inverted().Translation);
            W(c, ra.TransformPoint(V3(r))); W(c, ra.TransformVector(V3(r))); W(c, ra.ToMatrix());

            var aa = new Affine3D(M3(r), V3(r));
            var ab = new Affine3D(M3(r), V3(r));
            Affine3D ac = aa * ab;
            W(c, ac.Linear); W(c, ac.Translation); W(c, aa.TransformPoint(V3(r)));
            c.Write(aa.TryInvert(out Affine3D ai) ? 1 : 0); W(c, ai.Linear); W(c, ai.Translation);
        }
    }

    private static void Bounds(ScenarioContext c)
    {
        var r = new TypesRng(0x5005);
        var pts = new Vector3D[16];
        for (int i = 0; i < Count / 4; i++)
        {
            for (int k = 0; k < pts.Length; k++)
            {
                pts[k] = V3(r);
            }
            Bounds3D a = Bounds3D.CreateFromPoints(pts.AsSpan(0, 8)), b = Bounds3D.CreateFromPoints(pts.AsSpan(8, 8));
            W(c, a); W(c, Bounds3D.Union(a, b)); W(c, Bounds3D.Intersection(a, b)); W(c, a.Center); W(c, a.Extents);
            c.Write(a.Volume()); c.Write(a.SurfaceArea());
            c.Write(a.Intersects(b) ? 1 : 0); c.Write(a.Contains(b) ? 1 : 0); c.Write(a.Contains(pts[3]) ? 1 : 0);
            W(c, a.Inflate(R(r)));
        }
    }

    // Each chunk is computed independently into its own slot and the slots are hashed in index order,
    // so the manifest must not depend on c.Threads.
    private static void MatrixParallel(ScenarioContext c)
    {
        const int chunks = 64;
        const int perChunk = 40;
        var results = new double[chunks][];
        Parallel.For(0, chunks, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, c.Threads) }, chunk =>
        {
            var r = new TypesRng(0x6000 + (ulong)chunk);
            var acc = Matrix4x4D.Identity;
            var det = new double[perChunk];
            for (int i = 0; i < perChunk; i++)
            {
                acc = acc * M4(r);
                det[i] = acc.Determinant();
                if (acc.TryInvert(out Matrix4x4D inv))
                {
                    acc = inv;
                }
            }
            results[chunk] = det;
        });
        for (int i = 0; i < chunks; i++)
        {
            foreach (double d in results[i])
            {
                c.Write(d);
            }
        }
    }
}
