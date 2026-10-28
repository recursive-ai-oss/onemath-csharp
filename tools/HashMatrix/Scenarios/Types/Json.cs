using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using RecursiveAI.OneMath.Physics;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>Source-generated System.Text.Json metadata for every public value type (no reflection, so it works under NativeAOT and trimming).</summary>
[JsonSerializable(typeof(Vector2D))]
[JsonSerializable(typeof(Vector3D))]
[JsonSerializable(typeof(Vector4D))]
[JsonSerializable(typeof(Matrix2x2D))]
[JsonSerializable(typeof(Matrix3x3D))]
[JsonSerializable(typeof(Matrix4x4D))]
[JsonSerializable(typeof(QuaternionD))]
[JsonSerializable(typeof(Affine3D))]
[JsonSerializable(typeof(RigidTransformD))]
[JsonSerializable(typeof(Bounds2D))]
[JsonSerializable(typeof(Bounds3D))]
[JsonSerializable(typeof(EigenDecompositionD))]
[JsonSerializable(typeof(MassPropertiesD))]
[JsonSerializable(typeof(PrincipalAxesD))]
[JsonSerializable(typeof(Vector2F))]
[JsonSerializable(typeof(Vector3F))]
[JsonSerializable(typeof(Vector4F))]
[JsonSerializable(typeof(Matrix2x2F))]
[JsonSerializable(typeof(Matrix3x3F))]
[JsonSerializable(typeof(Matrix4x4F))]
[JsonSerializable(typeof(QuaternionF))]
[JsonSerializable(typeof(Affine3F))]
[JsonSerializable(typeof(RigidTransformF))]
[JsonSerializable(typeof(Bounds2F))]
[JsonSerializable(typeof(Bounds3F))]
[JsonSerializable(typeof(EigenDecompositionF))]
[JsonSerializable(typeof(MassPropertiesF))]
[JsonSerializable(typeof(PrincipalAxesF))]
internal sealed partial class OneMathJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Hash-matrix scenario: every public value type round-trips bit for bit through System.Text.Json using source-generated metadata,
/// and the JSON text itself is part of the output. The scenario throws if any value comes back different, so the AOT and WebAssembly
/// runs check the round trip too.
/// </summary>
internal static class JsonScenarios
{
    [ModuleInitializer]
    internal static void Init() => Scenarios.Register("types/json", Run);

    private static readonly double[] s_edges =
    {
        0.0, -0.0, 1.0, -1.0, double.Epsilon, -double.Epsilon, 2.2250738585072009E-308, double.MaxValue, -double.MaxValue, 0.1, 1.0 / 3.0, 2097151.999999999,
    };

    private static readonly float[] s_edgesF =
    {
        0.0f, -0.0f, 1.0f, -1.0f, float.Epsilon, -float.Epsilon, 1.17549435E-38f, float.MaxValue, -float.MaxValue, 0.1f, 1.0f / 3.0f, 2097151.9f,
    };

    private static double D(TypesRng r, int i) =>
        i % 5 == 0 ? s_edges[(int)(r.NextUInt64() % (ulong)s_edges.Length)] : (r.NextDouble() * 2 - 1) * r.Scale();

    private static float F(TypesRng r, int i) => i % 5 == 0 ? s_edgesF[(int)(r.NextUInt64() % (ulong)s_edgesF.Length)] : (float)D(r, 1);

    private static void Check<T>(ScenarioContext c, T value, JsonTypeInfo<T> info) where T : struct
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, info);
        T back = JsonSerializer.Deserialize(json, info);
        ReadOnlySpan<byte> a = MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in value));
        ReadOnlySpan<byte> b = MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in back));
        if (!a.SequenceEqual(b))
        {
            throw new InvalidOperationException("JSON round trip changed a " + typeof(T).Name + ": " + System.Text.Encoding.UTF8.GetString(json));
        }
        c.Write(json.Length);
        c.Write(json);
    }

    private static void Run(ScenarioContext c)
    {
        var r = new TypesRng(0xA00A);
        OneMathJsonContext x = OneMathJsonContext.Default;
        for (int i = 0; i < 200; i++)
        {
            {
                Vector3D v3 = new Vector3D(D(r, i), D(r, i), D(r, i));
                Check(c, new Vector2D(D(r, i), D(r, i)), x.Vector2D);
                Check(c, v3, x.Vector3D);
                Check(c, new Vector4D(D(r, i), D(r, i), D(r, i), D(r, i)), x.Vector4D);
                Check(c, new Matrix2x2D(D(r, i), D(r, i), D(r, i), D(r, i)), x.Matrix2x2D);
                Matrix3x3D m3 = new(D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i));
                Check(c, m3, x.Matrix3x3D);
                Check(c, new Matrix4x4D(D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i), D(r, i)), x.Matrix4x4D);
                QuaternionD q = new(D(r, i), D(r, i), D(r, i), D(r, i));
                Check(c, q, x.QuaternionD);
                Check(c, new Affine3D(m3, v3), x.Affine3D);
                Check(c, new RigidTransformD(q, v3), x.RigidTransformD);
                Check(c, new Bounds2D(new Vector2D(D(r, i), D(r, i)), new Vector2D(D(r, i), D(r, i))), x.Bounds2D);
                Check(c, new Bounds3D(v3, new Vector3D(D(r, i), D(r, i), D(r, i))), x.Bounds3D);
                Check(c, new EigenDecompositionD(v3, m3), x.EigenDecompositionD);
                Check(c, new MassPropertiesD(Math.Abs(D(r, i)), v3, m3), x.MassPropertiesD);
                Check(c, new PrincipalAxesD(v3, m3), x.PrincipalAxesD);
            }
            {
                Vector3F v3 = new Vector3F(F(r, i), F(r, i), F(r, i));
                Check(c, new Vector2F(F(r, i), F(r, i)), x.Vector2F);
                Check(c, v3, x.Vector3F);
                Check(c, new Vector4F(F(r, i), F(r, i), F(r, i), F(r, i)), x.Vector4F);
                Check(c, new Matrix2x2F(F(r, i), F(r, i), F(r, i), F(r, i)), x.Matrix2x2F);
                Matrix3x3F m3 = new(F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i));
                Check(c, m3, x.Matrix3x3F);
                Check(c, new Matrix4x4F(F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i), F(r, i)), x.Matrix4x4F);
                QuaternionF q = new(F(r, i), F(r, i), F(r, i), F(r, i));
                Check(c, q, x.QuaternionF);
                Check(c, new Affine3F(m3, v3), x.Affine3F);
                Check(c, new RigidTransformF(q, v3), x.RigidTransformF);
                Check(c, new Bounds2F(new Vector2F(F(r, i), F(r, i)), new Vector2F(F(r, i), F(r, i))), x.Bounds2F);
                Check(c, new Bounds3F(v3, new Vector3F(F(r, i), F(r, i), F(r, i))), x.Bounds3F);
                Check(c, new EigenDecompositionF(v3, m3), x.EigenDecompositionF);
                Check(c, new MassPropertiesF(Math.Abs(F(r, i)), v3, m3), x.MassPropertiesF);
                Check(c, new PrincipalAxesF(v3, m3), x.PrincipalAxesF);
            }
        }
    }
}
