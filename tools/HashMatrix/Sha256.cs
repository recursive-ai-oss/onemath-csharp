using System;
using System.Buffers.Binary;

namespace RecursiveAI.OneMath.HashMatrix;

/// <summary>
/// SHA-256 (FIPS 180-4), written out so the runner behaves the same on every runtime, including
/// browser WebAssembly where the platform hash providers are not guaranteed.
/// </summary>
internal sealed class Sha256
{
    private static readonly uint[] K =
    {
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
    };

    private readonly uint[] _h =
    {
        0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19,
    };

    private readonly byte[] _block = new byte[64];
    private readonly uint[] _w = new uint[64];
    private int _fill;
    private ulong _length;

    public void Append(ReadOnlySpan<byte> data)
    {
        _length += (ulong)data.Length;
        while (!data.IsEmpty)
        {
            int n = Math.Min(64 - _fill, data.Length);
            data.Slice(0, n).CopyTo(_block.AsSpan(_fill));
            _fill += n;
            data = data.Slice(n);
            if (_fill == 64)
            {
                Compress();
                _fill = 0;
            }
        }
    }

    public byte[] Finish()
    {
        ulong bits = _length * 8;
        Span<byte> pad = stackalloc byte[72];
        pad.Clear();
        pad[0] = 0x80;
        int padLen = (_fill < 56 ? 56 : 120) - _fill;
        BinaryPrimitives.WriteUInt64BigEndian(pad.Slice(padLen), bits);
        Append(pad.Slice(0, padLen + 8));
        var digest = new byte[32];
        for (int i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(digest.AsSpan(i * 4), _h[i]);
        }
        return digest;
    }

    public static byte[] Hash(ReadOnlySpan<byte> data)
    {
        var s = new Sha256();
        s.Append(data);
        return s.Finish();
    }

    private static uint Rotr(uint x, int n) => (x >> n) | (x << (32 - n));

    private void Compress()
    {
        var w = _w;
        for (int i = 0; i < 16; i++)
        {
            w[i] = BinaryPrimitives.ReadUInt32BigEndian(_block.AsSpan(i * 4));
        }
        for (int i = 16; i < 64; i++)
        {
            uint s0 = Rotr(w[i - 15], 7) ^ Rotr(w[i - 15], 18) ^ (w[i - 15] >> 3);
            uint s1 = Rotr(w[i - 2], 17) ^ Rotr(w[i - 2], 19) ^ (w[i - 2] >> 10);
            w[i] = w[i - 16] + s0 + w[i - 7] + s1;
        }
        uint a = _h[0], b = _h[1], c = _h[2], d = _h[3], e = _h[4], f = _h[5], g = _h[6], h = _h[7];
        for (int i = 0; i < 64; i++)
        {
            uint t1 = h + (Rotr(e, 6) ^ Rotr(e, 11) ^ Rotr(e, 25)) + ((e & f) ^ (~e & g)) + K[i] + w[i];
            uint t2 = (Rotr(a, 2) ^ Rotr(a, 13) ^ Rotr(a, 22)) + ((a & b) ^ (a & c) ^ (b & c));
            h = g; g = f; f = e; e = d + t1; d = c; c = b; b = a; a = t1 + t2;
        }
        _h[0] += a; _h[1] += b; _h[2] += c; _h[3] += d; _h[4] += e; _h[5] += f; _h[6] += g; _h[7] += h;
    }
}
