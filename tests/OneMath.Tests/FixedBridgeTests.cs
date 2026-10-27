using System;
using System.Numerics;
using Xunit;

namespace RecursiveAI.OneMath.Tests;

public class FixedBridgeTests
{
    private const long Max = FixedBridge.MaxRaw;
    private static readonly BigInteger Two32 = BigInteger.One << 32;

    [Fact]
    public void Constants_match_the_format()
    {
        Assert.Equal((1L << 53) - 1, FixedBridge.MaxRaw);
        Assert.Equal(2097152.0, FixedBridge.MaxMagnitude);
        Assert.Equal(1L << 53, (long)(FixedBridge.MaxMagnitude * 4294967296.0));
    }

    [Theory]
    [InlineData(0L, 0.0)]
    [InlineData(1L << 32, 1.0)]
    [InlineData(-(1L << 32), -1.0)]
    [InlineData(1L, 2.3283064365386963e-10)]
    [InlineData(3L << 31, 1.5)]
    public void ToDouble_known_answers(long raw, double expected) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(FixedBridge.ToDouble(raw)));

    [Fact]
    public void Largest_value_is_exact()
    {
        double top = FixedBridge.ToDouble(Max);
        Assert.Equal(2097152.0 - 1.0 / 4294967296.0, top);
        Assert.Equal(Max, FixedBridge.FromDouble(top));
        Assert.Equal(-top, FixedBridge.ToDouble(-Max));
        Assert.Equal(-Max, FixedBridge.FromDouble(-top));
    }

    [Fact]
    public void Boundary_neighbourhood_round_trips_exactly_and_rejects_beyond()
    {
        for (long k = 0; k < 2000; k++)
        {
            foreach (long raw in new[] { Max - k, -(Max - k), k, -k })
            {
                double d = FixedBridge.ToDouble(raw);
                Assert.Equal(new BigInteger(raw), Exact(d));
                Assert.Equal(raw, FixedBridge.FromDouble(d));
            }
        }
        foreach (long raw in new[] { Max + 1, Max + 2, -Max - 1, long.MaxValue, long.MinValue })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FixedBridge.ToDouble(raw));
            Assert.Throws<ArgumentOutOfRangeException>(() => FixedBridge.ToSingle(raw));
            Assert.False(FixedBridge.TryToDouble(raw, out double zero));
            Assert.Equal(0.0, zero);
        }
        // Powers of two and their neighbours up to the bound.
        for (int e = 0; e <= 52; e++)
        {
            foreach (long raw in new[] { (1L << e) - 1, 1L << e, (1L << e) + 1 })
            {
                Assert.Equal(new BigInteger(raw), Exact(FixedBridge.ToDouble(raw)));
            }
        }
    }

    [Fact]
    public void Random_raw_values_convert_exactly_to_double_and_back()
    {
        var rng = new Random(12345);
        for (int i = 0; i < 200000; i++)
        {
            int bits = 1 + rng.Next(53);
            long raw = rng.NextInt64() >>> (64 - bits);
            if ((i & 1) == 1)
            {
                raw = -raw;
            }
            double d = FixedBridge.ToDouble(raw);
            Assert.Equal(new BigInteger(raw), Exact(d));
            Assert.Equal(raw, FixedBridge.FromDouble(d));
        }
    }

    [Fact]
    public void ToSingle_rounds_to_nearest_even_against_an_exact_reference()
    {
        var rng = new Random(777);
        for (int i = 0; i < 300000; i++)
        {
            long raw;
            if (i % 3 == 0)
            {
                // A 25-bit odd mantissa shifted into place: an exact float32 tie.
                long m = (rng.NextInt64(1L << 24) << 1) | 1 | (1L << 24);
                raw = m << rng.Next(0, 28);
            }
            else
            {
                raw = rng.NextInt64() >>> (11 + rng.Next(50));
            }
            if (rng.Next(2) == 1)
            {
                raw = -raw;
            }
            float f = FixedBridge.ToSingle(raw);
            // Both operands are doubles within one float ulp of each other, so each subtraction is exact (Sterbenz).
            double value = FixedBridge.ToDouble(raw);
            double err = Math.Abs((double)f - value);
            double errUp = Math.Abs((double)MathF.BitIncrement(f) - value);
            double errDown = Math.Abs((double)MathF.BitDecrement(f) - value);
            Assert.True(err <= errUp && err <= errDown, $"raw {raw}: {f} is not nearest");
            if (err == errUp || err == errDown)
            {
                Assert.Equal(0, BitConverter.SingleToInt32Bits(f) & 1);
            }
        }
    }

    [Fact]
    public void FromDouble_rounds_half_to_even_and_handles_signed_zero()
    {
        double half = 0.5 / 4294967296.0;
        Assert.Equal(0L, FixedBridge.FromDouble(half));
        Assert.Equal(2L, FixedBridge.FromDouble(3 * half));
        Assert.Equal(2L, FixedBridge.FromDouble(5 * half));
        Assert.Equal(4L, FixedBridge.FromDouble(7 * half));
        Assert.Equal(0L, FixedBridge.FromDouble(-half));
        Assert.Equal(-2L, FixedBridge.FromDouble(-3 * half));
        Assert.Equal(1L, FixedBridge.FromDouble(1.4 / 4294967296.0));
        Assert.Equal(0L, FixedBridge.FromDouble(-0.0));
        Assert.Equal(1L << 32, FixedBridge.FromDouble(1.0));
        Assert.Equal(FixedBridge.FromDouble(0.1), (long)Math.Round(0.1 * 4294967296.0));
    }

    [Fact]
    public void FromDouble_rejects_nan_infinity_and_the_bound_with_a_clear_message()
    {
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 2097152.0, -2097152.0, 1e300, 2097152.0000000005 })
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => FixedBridge.FromDouble(bad));
            Assert.Contains("2097152", ex.Message, StringComparison.Ordinal);
            Assert.Contains(bad.ToString("R", System.Globalization.CultureInfo.InvariantCulture), ex.Message, StringComparison.Ordinal);
            Assert.False(FixedBridge.TryFromDouble(bad, out long zero));
            Assert.Equal(0L, zero);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => FixedBridge.FromSingle(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => FixedBridge.FromSingle(2097152f));
        Assert.Equal(2097151.875, FixedBridge.ToDouble(FixedBridge.FromSingle(2097151.875f)));
    }

    [Fact]
    public void RawOutOfRange_message_names_value_and_bound()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => FixedBridge.ToDouble(long.MaxValue));
        Assert.Contains(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), ex.Message, StringComparison.Ordinal);
        Assert.Contains("9007199254740991", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Span_forms_convert_and_validate()
    {
        long[] raw = { 0, 1L << 32, -(3L << 31), Max };
        double[] d = new double[4];
        FixedBridge.ToDouble(raw, d);
        long[] back = new long[4];
        FixedBridge.FromDouble(d, back);
        Assert.Equal(raw, back);
        Assert.Throws<ArgumentException>(() => FixedBridge.ToDouble(raw, new double[3]));
        Assert.Throws<ArgumentException>(() => FixedBridge.FromDouble(d, new long[3]));
        Assert.Throws<ArgumentOutOfRangeException>(() => FixedBridge.FromDouble(new[] { 1.0, double.NaN }, new long[2]));
    }

    // The exact value times 2^32 as an integer (the argument must be a multiple of 2^-32).
    private static BigInteger Exact(double d)
    {
        double scaled = d * 4294967296.0; // exact: power-of-two scaling
        Assert.Equal(Math.Floor(scaled), scaled);
        return new BigInteger(scaled);
    }
}
