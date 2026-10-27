using System;
using System.Buffers.Binary;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>
/// Collects the bytes a scenario produces. The runner hashes them with SHA-256; two platforms agree
/// on a scenario exactly when they feed it identical bytes. All writers are little-endian and
/// write floating-point values by their bit patterns, so signed zeros count. Every NaN is written as the
/// same canonical quiet NaN, because the sign and payload of a NaN produced by arithmetic is chosen by the
/// CPU (x64 and arm64 differ), not by this library.
/// </summary>
public sealed class ScenarioContext
{
    private readonly Sha256 _hash = new();
    private readonly byte[] _scratch = new byte[8];

    public ScenarioContext(int threads) => Threads = threads;

    /// <summary>Worker count a parallel scenario should use (at least 1). Results must not depend on it.</summary>
    public int Threads { get; }

    /// <summary>Appends raw bytes.</summary>
    public void Write(ReadOnlySpan<byte> bytes) => _hash.Append(bytes);

    /// <summary>Appends a 32-bit integer.</summary>
    public void Write(int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(_scratch, value);
        _hash.Append(_scratch.AsSpan(0, 4));
    }

    /// <summary>Appends a 64-bit integer.</summary>
    public void Write(long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(_scratch, value);
        _hash.Append(_scratch.AsSpan(0, 8));
    }

    /// <summary>Appends the bit pattern of a float32.</summary>
    public void Write(float value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(_scratch, float.IsNaN(value) ? 0x7fc00000 : BitConverter.SingleToInt32Bits(value));
        _hash.Append(_scratch.AsSpan(0, 4));
    }

    /// <summary>Appends the bit pattern of a double.</summary>
    public void Write(double value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(_scratch, double.IsNaN(value) ? 0x7ff8000000000000L : BitConverter.DoubleToInt64Bits(value));
        _hash.Append(_scratch.AsSpan(0, 8));
    }

    /// <summary>Finishes the hash and returns the 32-byte SHA-256 digest.</summary>
    public byte[] Finish() => _hash.Finish();
}
