# The Q31.32 fixed-point bridge

A deterministic game host keeps world coordinates in Q31.32: a signed 64-bit integer (the raw value) holding the value times 2^32. `FixedBridge` converts the raw `long` to and from floating point. It has no dependency on any fixed-point type.

## Range

| | Bound |
|---|---|
| Accepted value | finite, \|v\| < 2^21 = 2,097,152 |
| Accepted raw | \|raw\| <= 2^53 - 1 |

Anything else (including NaN and infinities) throws `ArgumentOutOfRangeException` whose message states the offending value and the bound (`TryToDouble` and `TryFromDouble` return false instead).

The limit is the exactness limit: 32 fractional bits plus 21 integer bits is 53 bits, the binary64 significand. Inside the range, every raw value is a binary64 number.

## Conversions

| Function | Result | Rounding |
|---|---|---|
| raw to double | `raw * 2^-32` | none: exact |
| raw to float32 | the exact value rounded once to binary32 | nearest, ties to even |
| double to raw | `round(v * 2^32)` | nearest integer, ties to even; `-0` gives 0 |
| float32 to raw | widen to double (exact), then as double to raw | as above |

- raw to float32 goes through the exact double, so there is exactly one rounding. Converting a `long` straight to `float` can round twice on some platforms.
- A double of magnitude 2^20 or more is always an exact multiple of 2^-32, so double to raw rounds only below that; the largest accepted double, 2^21 - 2^-32, maps to raw 2^53 - 1.
- Scaling by 2^32 and 2^-32 is exact (powers of two, no overflow or underflow inside the range), so raw to double and back is the identity on every raw value in range.
- The span forms convert in index order, throw on the first out-of-range element (earlier elements are already written) and require the destination to be at least as long as the source.

## Why float32 is not exact

A value like 1 + 2^-32 needs 33 significand bits, so no float32 holds it. Positions are therefore carried as double at the public boundary and as tile-local float32 (relative to a double origin) inside; a tile of extent 2^t resolves 2^(t-24).
