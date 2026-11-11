# Interval arithmetic

`IntervalD` is a closed interval of real numbers with double bounds; `Interval3D` is a box of three of them. Every operation returns an interval that **encloses** the exact result for every point of the operands, and for the elementary functions also the value of this library's own scalar function (`Math64.Sin`, ...) at every point. They exist to bound a function over a region, for example to prune an octree when meshing an implicit surface (Duff, "Interval arithmetic and recursive subdivision for implicit functions and constructive solid geometry", SIGGRAPH 1992; Keeter, "Massively parallel rendering of complex closed-form implicit surfaces", SIGGRAPH 2020). Semantics follow Moore, *Interval Analysis* (1966) and IEEE 1788-2015; the tables below say where they differ.

## Representation

- `Lo <= Hi`. `Lo` may be `-infinity` and `Hi` may be `+infinity` (unbounded on that side; the infinity is a limit, not a member). `Lo = +infinity` and `Hi = -infinity` are rejected, as is NaN (`ArgumentOutOfRangeException`, parameter `lo` or `hi`) and `lo > hi` (`ArgumentException`, parameter `lo`).
- `IntervalD.Entire` is `[-inf, +inf]`. `IntervalD.Empty` has both bounds NaN; it is the result of an operation whose domain misses the operand, and every operation maps it to itself. `Contains` is false for it, and it is contained in every interval.
- A zero lower bound is stored as `-0.0` and a zero upper bound as `+0.0`, so equal intervals have equal bits. An endpoint 0 is the real number 0.
- `default(IntervalD)` is the point `[0, 0]`.
- Scalar operator overloads (`x + 2.0`) build the point interval and throw for NaN or infinity.

## Rounding

.NET cannot set the FPU rounding mode, so every bound is computed round-to-nearest and then moved outward by exact bit stepping (`Math.BitIncrement` / `BitDecrement`) only when it has to be:

- **`+`, `-`:** Knuth's TwoSum gives the exact sign of the rounding error `e` (`a + b = s + e`). The lower bound is `s`, or `BitDecrement(s)` when `e < 0`; the upper bound is `s`, or `BitIncrement(s)` when `e > 0`. So `[1, 2] + [3, 4]` is exactly `[4, 6]` and any inexact sum is 1 ulp wide. Overflow of finite operands rounds to `double.MaxValue` below and `+infinity` above.
- **`*`:** Dekker's exact product error (Veltkamp split by 2^27 + 1, no FMA) decides the direction the same way. It is used when both factors are at most 2^500 in magnitude and the product at least about 2^-900 (below that the error terms could underflow); outside that range the bound is widened one ulp each way, and a product that underflows to 0 gets the bound `[0, 2^-1074]` or `[-2^-1074, 0]` by sign. A factor 0 times an infinite bound is 0.
- **`/`, `sqrt`:** the exact residual `a - q*b` (or `a - q*q`) is computed with the same product error; its sign decides the direction. Same safe range and fallback.
- **Elementary functions** (`Exp`, `Log`, `Sin`, `Cos`, `Atan2`, `Pow`): `Math64` results are correctly rounded, so the true value lies within one ulp below and above the scalar result; each endpoint value is widened by exactly one ulp, except where the scalar result is exact by definition (`exp(0)`, `exp(-inf)`, `log(1)`, `sin(0)`, `cos(0)`, `atan2(0, x > 0)`, powers of 0, 1, -1, infinities, `x^0`, `x^1`, and integer powers 2..4 which are chains of directed multiplications). Results are clamped to the function's range (`[-1, 1]`, `[0, inf)`, `[-pi', pi']` with `pi'` the double above the nearest double to pi).

Measured: point inputs to `+ - * / sqrt` give an exact or 1-ulp wide result (about 94% exact in a mixed random and adversarial sample); `Exp`, `Log`, `Sin`, `Cos`, `Atan2` and non-integer `Pow` of a point are 2 ulp wide, i.e. 1 ulp per endpoint.

## Operations

| Operation | Result |
|---|---|
| `a + b`, `a - b`, `-a`, `a * b` | Outward-rounded; negation is exact. |
| `a / b`, `b` does not contain 0 | Four-case table by signs, outward-rounded; `a / infinity` is 0. |
| `a / b`, `b` contains 0 | `[0,0]` divisor: `Empty`. Else numerator `[0,0]`: `[0,0]`. Else numerator contains 0, or divisor has 0 strictly inside: `Entire`. Else (one-signed numerator, zero at a divisor end): a half-unbounded interval, e.g. `[1,2] / [0,4] = [0.25, +inf]`. (IEEE 1788 hull form.) A divisor of exactly zero is a pole outside the domain; the infinite limits are covered. |
| `Square(x)` | Tight: `[0, max(lo², hi²)]` when `x` contains 0 (`x * x` would give a negative lower bound). |
| `Sqrt(x)` | Domain-clipped: negative part removed; `Empty` if `hi < 0`. |
| `Abs`, `Min`, `Max`, `Hull`, `Intersect` | Exact. `Intersect` of disjoint intervals is `Empty`. |
| `Width` | `Hi - Lo` rounded up (infinite when unbounded, NaN when empty). |
| `Midpoint` | `(Lo + Hi) / 2` without overflow; 0 for `Entire`; `±double.MaxValue` for a half-unbounded interval; NaN when empty. Always inside the interval. |
| `Exp` | Monotone: endpoint values widened. |
| `Log` | Domain `x > 0`; `Empty` if `hi <= 0`; `lo <= 0` gives `-inf` as lower bound. |
| `Sin`, `Cos` | See below. |
| `Atan2(y, x)` | See below. |
| `Pow(x, int n)` | Exact odd/even/negative logic (below). |
| `Pow(x, y)` | See below. |
| `Interval3D` | `+ - *` (scalar, interval, component-wise), `Dot`, `LengthSquared` (sum of tight squares), `Length`, `Abs`, `Min`, `Max`, `Hull`, `Contains(Vector3D)`, `FromBounds(Bounds3D)`. Components are independent, so `Dot(v, v)` is wider than `LengthSquared`. |

### Sin and Cos: how the period is handled

The interval is `[-1, 1]` if it is unbounded or wider than 6.28 (less than 2 pi, so the quadrant count below is unambiguous). Otherwise the quadrant of each endpoint is read from the signs of the correctly rounded `sin` and `cos` of that endpoint: no double is a multiple of pi/2, so those signs are never wrong, and no reduction of the argument (and so no constructed value that would itself need rounding) is needed. With quadrants `qa`, `qb` and width `w`, the number of quadrant boundaries crossed is `(qb - qa) mod 4`, or 4 when that is 0 and `w > 3`. The boundaries are pi/2 (sin maximum 1), pi (cos minimum -1), 3 pi/2 (sin minimum -1) and 2 pi (cos maximum 1). A crossed extremum gives exactly ±1; otherwise the function is monotone and the bounds are the endpoint values widened one ulp, clamped to `[-1, 1]`.

### Atan2

`Atan2(y, x)` has the range of `Math64.Atan2`, `[-pi, pi]`, with the branch cut on the negative x axis, and **signed zeros are honoured** (`atan2(-0, -1) = -pi`). Therefore, if `y` contains 0 and `x.Lo <= 0`, the result is the full `[-pi', pi']`. Otherwise `atan2` is continuous and monotone in each argument separately, so the extremes are at the four corners (each widened one ulp). If any bound is infinite the result is the bounding sector (half plane) instead of corner values.

### Powers

- `Pow(x, n)` for an `int` `n` (and `Pow(x, y)` with a point integer `y`): `n = 0` gives `[1, 1]`; positive odd is monotone; positive even is monotone in `|x|`; negative `n` divides: `x > 0` or `x < 0` monotone pieces, `x` containing 0 gives half-unbounded or `Entire` results as for division, and `[0, 0]` gives `Empty` (a pole).
- `Pow(x, y)` general: for `x >= 0`, `x^y = exp(y ln x)` is bilinear in `(y, ln x)`, so the extremes over the box are at the four corners `(x.Lo|x.Hi, y.Lo|y.Hi)`. For `x < 0` the scalar is real only for integer `y`: if `y` contains an integer, the magnitude bound `|x|^y` is applied with both signs; otherwise the negative part contributes nothing (the scalar is NaN there). The result may be `Empty`. `pow(0, y < 0)` is a pole outside the domain, but the corner at `x = +0` contributes `+inf`.

## Containment guarantee and its exclusions

For any `x` in the operand intervals, the scalar result (`Math64.*`, or the correctly rounded IEEE operation) lies in the returned interval, **except at points where the scalar result is NaN, or is a pole decided by the sign of a zero** (`a / 0`, `log(0)`, `pow(±0, y < 0)`). The tests sample endpoints, neighbours of endpoints, midpoints, ±0, ±1, subnormals, huge values and neighbours of multiples of pi/2, and compare `+ - * / sqrt` with exact big-integer arithmetic.

## Hash matrix

`intervals/arith`, `intervals/functions` and `intervals/vector3` write every bound of a few ten thousand operations by bit pattern.
