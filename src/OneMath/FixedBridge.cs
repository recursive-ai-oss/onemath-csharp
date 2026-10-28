using System;
using System.Globalization;

namespace RecursiveAI.OneMath;

/// <summary>
/// Conversions between a deterministic host's Q31.32 fixed-point number (a signed 64-bit integer holding
/// the value times 2^32, called the raw value here) and floating point. The bridge works on the raw
/// <see cref="long"/> and has no dependency on any fixed-point type.
/// <para>
/// The accepted range is |value| &lt; 2^21 (2,097,152), that is |raw| &lt; 2^53. Inside it a raw value converts to
/// <see cref="double"/> exactly (32 fractional bits plus 21 integer bits fit double's 53-bit significand) and
/// a <see cref="double"/> that is a multiple of 2^-32 converts back exactly. Anything outside the range, and any NaN or
/// infinity, is rejected with <see cref="ArgumentOutOfRangeException"/> naming the value and the bound.
/// </para>
/// <para>
/// Rounding: raw to double never rounds. Raw to float32 rounds the exact value to nearest, ties to even (one
/// rounding, taken from the exact double). Double to raw rounds to the nearest multiple of 2^-32, ties to even;
/// a value of magnitude 2^20 or more is always an exact multiple, so ties only occur below that.
/// </para>
/// </summary>
public static class FixedBridge
{
    /// <summary>Number of fractional bits in the raw format (32).</summary>
    public const int FractionalBits = 32;

    /// <summary>The largest raw magnitude accepted, 2^53 - 1.</summary>
    public const long MaxRaw = (1L << 53) - 1;

    /// <summary>
    /// The exclusive bound on the magnitude of a converted value, 2^21 = 2,097,152: a value is accepted when its magnitude is
    /// strictly below this number. A property rather than a constant so the bound is not compiled into callers.
    /// </summary>
    public static double ExclusiveMagnitudeBound => 2097152.0;

    // 2^32 and 2^-32: powers of two, so scaling by them is exact (no overflow or underflow in range).
    private const double Scale = 4294967296.0;
    private const double InverseScale = 1.0 / 4294967296.0;

    /// <summary>True when <paramref name="raw"/> is within ±<see cref="MaxRaw"/>.</summary>
    public static bool IsInRange(long raw) => raw >= -MaxRaw && raw <= MaxRaw;

    /// <summary>True when <paramref name="value"/> is finite and |value| &lt; 2^21.</summary>
    public static bool IsInRange(double value) => value > -ExclusiveMagnitudeBound && value < ExclusiveMagnitudeBound;

    /// <summary>Converts a raw value to double, exactly. Throws <see cref="ArgumentOutOfRangeException"/> if |raw| &gt; <see cref="MaxRaw"/>.</summary>
    public static double ToDouble(long raw)
    {
        if (!IsInRange(raw))
        {
            throw RawOutOfRange(raw);
        }
        return raw * InverseScale;
    }

    /// <summary>Converts a raw value to double, exactly; returns false (and 0) if |raw| &gt; <see cref="MaxRaw"/>.</summary>
    public static bool TryToDouble(long raw, out double value)
    {
        if (!IsInRange(raw))
        {
            value = 0;
            return false;
        }
        value = raw * InverseScale;
        return true;
    }

    /// <summary>
    /// Converts a raw value to float32, rounding to nearest with ties to even. Throws
    /// <see cref="ArgumentOutOfRangeException"/> if |raw| &gt; <see cref="MaxRaw"/>.
    /// </summary>
    public static float ToSingle(long raw)
    {
        // The double is exact, so narrowing it is the only rounding (converting the long straight to float
        // could round twice on some platforms).
        return (float)ToDouble(raw);
    }

    /// <summary>
    /// Converts a double to a raw value, rounding to the nearest multiple of 2^-32 with ties to even (negative zero gives 0).
    /// Throws <see cref="ArgumentOutOfRangeException"/> for NaN, infinities and |value| &gt;= 2^21.
    /// </summary>
    public static long FromDouble(double value)
    {
        if (!IsInRange(value))
        {
            throw ValueOutOfRange(value);
        }
        // value * 2^32 is exact; Math.Round rounds half to even; the result is below 2^53 so it fits a long.
        return (long)Math.Round(value * Scale);
    }

    /// <summary>Like <see cref="FromDouble(double)"/> but returns false (and 0) instead of throwing.</summary>
    public static bool TryFromDouble(double value, out long raw)
    {
        if (!IsInRange(value))
        {
            raw = 0;
            return false;
        }
        raw = (long)Math.Round(value * Scale);
        return true;
    }

    /// <summary>Converts a float32 to a raw value (the float widens exactly to double first, then as <see cref="FromDouble(double)"/>).</summary>
    public static long FromSingle(float value) => FromDouble(value);

    /// <summary>
    /// Converts a span of raw values to doubles. <paramref name="destination"/> must be at least as long as <paramref name="raw"/>
    /// (<see cref="ArgumentException"/> otherwise). Throws <see cref="ArgumentOutOfRangeException"/> on the first out-of-range element;
    /// elements before it have already been written.
    /// </summary>
    public static void ToDouble(ReadOnlySpan<long> raw, Span<double> destination)
    {
        if (destination.Length < raw.Length)
        {
            throw new ArgumentException("Destination is shorter than the source.", nameof(destination));
        }
        for (int i = 0; i < raw.Length; i++)
        {
            destination[i] = ToDouble(raw[i]);
        }
    }

    /// <summary>
    /// Converts a span of doubles to raw values. <paramref name="destination"/> must be at least as long as <paramref name="values"/>
    /// (<see cref="ArgumentException"/> otherwise). Throws <see cref="ArgumentOutOfRangeException"/> on the first out-of-range element;
    /// elements before it have already been written.
    /// </summary>
    public static void FromDouble(ReadOnlySpan<double> values, Span<long> destination)
    {
        if (destination.Length < values.Length)
        {
            throw new ArgumentException("Destination is shorter than the source.", nameof(destination));
        }
        for (int i = 0; i < values.Length; i++)
        {
            destination[i] = FromDouble(values[i]);
        }
    }

    private static ArgumentOutOfRangeException ValueOutOfRange(double value) =>
        new(nameof(value), value, string.Create(CultureInfo.InvariantCulture,
            $"The value {value:R} is outside the exact range: it must be finite with magnitude below 2^21 = 2097152."));

    private static ArgumentOutOfRangeException RawOutOfRange(long raw) =>
        new(nameof(raw), raw, string.Create(CultureInfo.InvariantCulture,
            $"The raw value {raw} is outside the exact range: its magnitude must be at most 2^53 - 1 = {MaxRaw} (a value below 2^21 = 2097152)."));
}
