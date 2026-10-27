using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>Hash-matrix scenarios for the float vector, matrix, quaternion, transform and bounds types.</summary>
internal static class TypesFScenarios
{
    private const int Count = 1500;

    [ModuleInitializer]
    internal static void Init()
    {
        Scenarios.Register("types/f32/vector", Vectors);
        Scenarios.Register("types/f32/matrix", Matrices);
        Scenarios.Register("types/f32/quaternion", Quaternions);
        Scenarios.Register("types/f32/transform", Transforms);
        Scenarios.Register("types/f32/bounds", Bounds);
        Scenarios.Register("types/f32/matrix-parallel", MatrixParallel);
    }

    // Random values of mixed magnitude (a power-of-two scale between 2^-20 and 2^20), so intermediate roundings vary.
    private static float R(TypesRng r)
    {
        double scale = r.Scale();
        return (float)((r.NextDouble() * 2 - 1) * scale);
    }

    private static Vector2F V2(TypesRng r) => new(R(r), R(r));

    private static Vector3F V3(TypesRng r) => new(R(r), R(r), R(r));

    private static Vector4F V4(TypesRng r) => new(R(r), R(r), R(r), R(r));

    private static Matrix2x2F M2(TypesRng r) => new(R(r), R(r), R(r), R(r));

    private static Matrix3x3F M3(TypesRng r) => new(R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r));

    private static Matrix4x4F M4(TypesRng r) => new(
        R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r), R(r));

    private static QuaternionF Q(TypesRng r) => new(R(r), R(r), R(r), R(r));

    private static void W(ScenarioContext c, Vector2F v) { c.Write(v.X); c.Write(v.Y); }

    private static void W(ScenarioContext c, Vector3F v) { c.Write(v.X); c.Write(v.Y); c.Write(v.Z); }

    private static void W(ScenarioContext c, Vector4F v) { c.Write(v.X); c.Write(v.Y); c.Write(v.Z); c.Write(v.W); }

    private static void W(ScenarioContext c, QuaternionF q) { c.Write(q.X); c.Write(q.Y); c.Write(q.Z); c.Write(q.W); }

    private static void W(ScenarioContext c, Matrix2x2F m) { W(c, m.Row(0)); W(c, m.Row(1)); }

    private static void W(ScenarioContext c, Matrix3x3F m) { W(c, m.Row(0)); W(c, m.Row(1)); W(c, m.Row(2)); }

    private static void W(ScenarioContext c, Matrix4x4F m) { W(c, m.Row(0)); W(c, m.Row(1)); W(c, m.Row(2)); W(c, m.Row(3)); }

    private static void W(ScenarioContext c, Bounds3F b) { W(c, b.Min); W(c, b.Max); }

    private static void Vectors(ScenarioContext c)
    {
        var r = new TypesRng(0x1001);
        for (int i = 0; i < Count; i++)
        {
            Vector2F a2 = V2(r), b2 = V2(r);
            W(c, a2 + b2); W(c, a2 - b2); W(c, a2 * b2); W(c, a2 / b2.X); W(c, a2.Normalized());
            c.Write(Vector2F.Dot(a2, b2)); c.Write(Vector2F.Cross(a2, b2)); c.Write(a2.Length()); c.Write(Vector2F.Distance(a2, b2));
            W(c, Vector2F.Lerp(a2, b2, R(r)));

            Vector3F a3 = V3(r), b3 = V3(r);
            W(c, a3 + b3); W(c, a3 - b3); W(c, a3 * b3); W(c, a3 * R(r)); W(c, a3 / b3.Y); W(c, a3.Normalized());
            c.Write(Vector3F.Dot(a3, b3)); W(c, Vector3F.Cross(a3, b3)); c.Write(a3.Length()); c.Write(Vector3F.Distance(a3, b3));
            W(c, Vector3F.Lerp(a3, b3, R(r))); W(c, Vector3F.Min(a3, b3)); W(c, Vector3F.Max(a3, b3)); W(c, a3.Abs());

            Vector4F a4 = V4(r), b4 = V4(r);
            W(c, a4 + b4); W(c, a4 - b4); W(c, a4 * b4); W(c, a4.Normalized()); c.Write(Vector4F.Dot(a4, b4)); c.Write(a4.Length());
            W(c, Vector4F.Lerp(a4, b4, R(r)));
            W(c, a3.ToDouble().ToSingle());
        }
    }

    private static void Matrices(ScenarioContext c)
    {
        var r = new TypesRng(0x2002);
        for (int i = 0; i < Count; i++)
        {
            Matrix2x2F a2 = M2(r), b2 = M2(r);
            W(c, a2 * b2); W(c, a2.Transpose()); c.Write(a2.Determinant()); W(c, a2.Transform(V2(r)));
            c.Write(a2.TryInvert(out Matrix2x2F i2) ? 1 : 0); W(c, i2);

            Matrix3x3F a3 = M3(r), b3 = M3(r);
            W(c, a3 * b3); W(c, a3.Transpose()); c.Write(a3.Determinant()); W(c, a3.Transform(V3(r)));
            c.Write(a3.TryInvert(out Matrix3x3F i3) ? 1 : 0); W(c, i3);

            Matrix4x4F a4 = M4(r), b4 = M4(r);
            W(c, a4 * b4); W(c, a4.Transpose()); c.Write(a4.Determinant()); W(c, a4.Transform(V4(r)));
            W(c, a4.TransformPoint(V3(r))); W(c, a4.TransformVector(V3(r)));
            c.Write(a4.TryInvert(out Matrix4x4F i4) ? 1 : 0); W(c, i4);
        }
    }

    private static void Quaternions(ScenarioContext c)
    {
        var r = new TypesRng(0x3003);
        for (int i = 0; i < Count; i++)
        {
            QuaternionF a = Q(r).Normalized(), b = Q(r).Normalized();
            W(c, a * b); W(c, a.Conjugate()); W(c, a.Normalized()); c.Write(QuaternionF.Dot(a, b)); c.Write(a.Length());
            W(c, QuaternionF.NLerp(a, b, R(r))); W(c, QuaternionF.Lerp(a, b, R(r)));
            c.Write(Q(r).TryInvert(out QuaternionF inv) ? 1 : 0); W(c, inv);
            W(c, a.Rotate(V3(r)));
            Matrix3x3F m = a.ToMatrix();
            W(c, m); W(c, QuaternionF.FromMatrix(m));
        }
    }

    private static void Transforms(ScenarioContext c)
    {
        var r = new TypesRng(0x4004);
        for (int i = 0; i < Count; i++)
        {
            var ra = new RigidTransformF(Q(r).Normalized(), V3(r));
            var rb = new RigidTransformF(Q(r).Normalized(), V3(r));
            RigidTransformF rc = ra * rb;
            W(c, rc.Rotation); W(c, rc.Translation); W(c, ra.Inverse().Rotation); W(c, ra.Inverse().Translation);
            W(c, ra.TransformPoint(V3(r))); W(c, ra.TransformVector(V3(r))); W(c, ra.ToMatrix());

            var aa = new Affine3F(M3(r), V3(r));
            var ab = new Affine3F(M3(r), V3(r));
            Affine3F ac = aa * ab;
            W(c, ac.Linear); W(c, ac.Translation); W(c, aa.TransformPoint(V3(r)));
            c.Write(aa.TryInvert(out Affine3F ai) ? 1 : 0); W(c, ai.Linear); W(c, ai.Translation);
        }
    }

    private static void Bounds(ScenarioContext c)
    {
        var r = new TypesRng(0x5005);
        var pts = new Vector3F[16];
        for (int i = 0; i < Count / 4; i++)
        {
            for (int k = 0; k < pts.Length; k++)
            {
                pts[k] = V3(r);
            }
            Bounds3F a = Bounds3F.FromPoints(pts.AsSpan(0, 8)), b = Bounds3F.FromPoints(pts.AsSpan(8, 8));
            W(c, a); W(c, Bounds3F.Union(a, b)); W(c, Bounds3F.Intersection(a, b)); W(c, a.Center); W(c, a.Extents);
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
        var results = new float[chunks][];
        Parallel.For(0, chunks, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, c.Threads) }, chunk =>
        {
            var r = new TypesRng(0x6000 + (ulong)chunk);
            var acc = Matrix4x4F.Identity;
            var det = new float[perChunk];
            for (int i = 0; i < perChunk; i++)
            {
                acc = acc * M4(r);
                det[i] = acc.Determinant();
                if (acc.TryInvert(out Matrix4x4F inv))
                {
                    acc = inv;
                }
            }
            results[chunk] = det;
        });
        for (int i = 0; i < chunks; i++)
        {
            foreach (float d in results[i])
            {
                c.Write(d);
            }
        }
    }
}
