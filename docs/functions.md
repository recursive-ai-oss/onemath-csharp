# Elementary functions

`Math64` (double) and `Math32` (float32) in `RecursiveAI.OneMath`: `Sin Cos Tan SinCos Asin Acos Atan Atan2 Exp Exp2 Expm1 Log Log2 Log10 Log1p Pow Cbrt Hypot Sinh Cosh Tanh`.

## Contract

- **Every function is correctly rounded for every input**: the result is the IEEE 754 round-to-nearest, ties-to-even value of the exact mathematical result, with gradual underflow and overflow to infinity. Results are therefore bit-identical on every platform and equal to any other correctly rounded implementation. Nothing in the library calls `Math.*`/`MathF.*` for a function result: only `+ - * / sqrt` on float32/double (IEEE-exact), integer arithmetic, and `System.Numerics.BigInteger` (integer arithmetic) decide a bit.
- Special values follow IEEE 754-2019 clause 9.2 and C99 Annex F (`pow(x, ±0) = pow(1, y) = 1` even for NaN, `atan2(±0, -0) = ±π`, `hypot(±∞, NaN) = +∞`, `expm1(-0) = -0`, ...). Every NaN result is the canonical `double.NaN` / `float.NaN` regardless of input payload.
- Float32 inputs are converted to double (exact) and run through the same code as double; only the final rounding differs.
- Domain errors return NaN (no exceptions). Large arguments are fine: `Sin(1e300)` is exact.

## How a result is produced: three stages and a rounding test

Each call produces a value `v` with a known relative error bound `δ`, then asks Ziv's question (Ziv 1991; Muller, *Elementary Functions*, ch. 10): do all reals in `v(1 ± δ)` round to the same output? If yes, that output is correct. If not, the next, more accurate stage runs.

1. **Fast stage** (`Fast.cs`, tables in `FastTables.cs`): table-driven, plain double arithmetic plus error-free transformations; relative error ≤ 2^-62 .. 2^-65 depending on the function (bounds below). Decides 98–99.8% of double results (measured per function below) and all but a few in 10^6 of float32 results.
2. **Double-double stage** (`Cores.cs`, `DD.cs`): relative error ≈ 2^-100 (measured: `pow` 2^-96, `exp`/`exp2` 2^-106, the rest 2^-101 to 2^-105). Taylor-type series with exact-rational coefficients computed at start-up by double-double division. Decides all but about 1 double in 2^31 (and effectively no float32).
3. **Exact stage** (`Exact/`): about 500 bits using `BigInteger`, then one rounding. Used when the double-double test fails, when a double result is subnormal or at the overflow edge, and for `hypot`/`cbrt` ties (exact integer roots with a sticky bit). This stage shares no code with the other two, and it is what the tests treat as ground truth, alongside an independent mpmath (Python) run for the known-answer file.

Rounding test, double result from a double-double `(hi, lo)` with bound `δ`: `err = |hi| δ`; `a = hi + (lo + err)`, `b = hi + (lo - err)`; the result is decided iff `a == b` (monotonicity of rounding), and equals `a`. The result must be a normal double, otherwise stage 3 runs. Float32: `f = (float) hi`, `d = (hi - f) + lo`, compared with half the distance to the neighbouring float in the direction of `d`, with margin `err + 2.3e-16 |d|`; inconclusive or overflowing results go to the next stage.

The declared δ for stages 1 and 2 are the *inputs to the test*; they are validated, not proved, as follows: `FastStageTests` compares stage 1 with stage 2 on ≥ 4·10^6 inputs per function and requires every sample's error to be at most a quarter of its δ (measured margins in the table below); `Double_random` compares stage 2 with stage 3 on 2·10^7 inputs per function. A violated δ could only produce a wrong rounding when the exact result lies within the violation of a rounding boundary, so the exhaustive float32 run and the exact-checked double samples are the backstop.

## Argument handling

- **Representation.** `DD` is an unevaluated sum `Hi + Lo`, |Lo| ≤ ulp(Hi)/2. `TwoSum` is Knuth's, `TwoProd` is Dekker/Veltkamp splitting with constant 2^27+1 (no fused multiply-add; inputs below 2^996 in magnitude — callers scale first). `+`: accurate variant (relative error < 2^-103); `*`: < 2^-103; `/`: three quotient digits (< 2^-102); `sqrt`: one Karp–Markstein step.
- **Results as `m · 2^k`.** Cores return a double-double mantissa and a separate integer exponent, so the rounding test runs on a value near 1 and the scaling by `2^k` is exact. A double result that would be subnormal or overflow goes to stage 3.
- **Tiny arguments.** `sin tan asin atan sinh tanh x → x` and `cos, cosh → 1` for |x| < 2^-27; `expm1, log1p x → x` for |x| < 2^-55 (the first dropped term is at most 2^-56 of x, below the smallest rounding half-gap 2^-54 of x; this is the argument, and the known-answer and exhaustive float32 runs cross-check it). `tanh(x) = ±1` for |x| ≥ 22.
- **Thresholds** that return `±∞`/`0` directly are far outside the range where the result could still be finite (`exp`: x > 710 or x < -746; `exp2`: |x| > 1100; `sinh/cosh`: |x| > 711); anything between a threshold and the true boundary is decided by the stages.

## Methods per function

Constants (π/2 and ln 2 parts, 1/ln 2, 1/ln 10, π, 2/π bits for reduction, the stage-1 tables) are generated by `tools/GenConstants/gen_constants.py` and `gen_tables.py` with mpmath at 600–1700 bits and written as exact bit patterns (`Constants.cs`) or shortest round-trip decimal literals (`FastTables.cs`; the C# compiler parses them exactly). No table is computed at run time from a platform function. Series coefficients `1/n!`, `1/(2k+1)` are computed at start-up as `DD` quotients (`+ - * /` only, so identical everywhere).

| Function | Stage 1 (fast) | Stage 2 (double-double) |
|---|---|---|
| **exp, exp2** | `n = round(t·64/ln2)`; `r = t − n·ln2/64` with ln2/64 in three parts (two 34-bit parts, so `n·part` is exact; `r` as a double-double renormalised); `2^(n/64) = 2^(n>>6)·T[n&63]`, `T` = 64 double-double table entries; `exp(r) = 1 + r + r²/2 + … + r⁶/720` (r ≤ ln2/128; the dropped `r⁷/5040 < 2^-64`); product by `T` with the high×high product formed exactly (`TwoProd`). exp2: `r = (x − n/64)·ln2` (exact subtraction). | ln2 in three parts (40, 40, 53 bits) reduces to |r| ≤ 0.35; scale by 2^-8; Taylor series for `expm1` (5 terms in double-double, 5 in double); then 8 times `e ← 2e + e²` (relative error preserved in `expm1` form). exp2: `r = (x − round x)·ln2` in double-double. |
| **expm1** | |x| ≥ 0.5: stage-1 `exp` minus 1 (≤ 1 bit of cancellation); smaller: stage 2. | As exp but not adding 1 when k = 0, so tiny results keep full relative accuracy; exact `2^k(1+e) − 1` for |k| ≤ 600. |
| **log, log2, log10** | Not within 1/16 of 1 (those go to stage 2). `x = 2^e·m`, `m ∈ [1,2)`; `j` = top 7 mantissa bits; `inv[j] ≈ 1/(1 + (j+½)/128)`; `r = m·inv[j] − 1` exactly (TwoProd), |r| ≤ 2^-8; `log m = r − r²/2 + … + r⁷/7 + L[j]`, `L[j] = −log(inv[j])` as a double-double (table of 128); plus `e·ln2` (three parts). log2/log10 multiply by `1/ln2`, `1/ln10` as double-doubles. | `m ∈ [√½, √2)`, `z = (m−1)/(m+1)`, `log m = 2 atanh z` as a series in `z²` (11 double-double terms, 11 double terms), plus `e·ln2`. Subnormal inputs scaled by 2^54 first. |
| **log1p** | stage 2 only | `|x| < 0.2`: `2 atanh(x/(2+x))` (relative accuracy for any small x); otherwise `1+x` as an exact double-double and the log path above. |
| **sin, cos, tan, sincos** | |x| ≤ 2^19 only. Cody–Waite reduction with π/2 in parts of 33, 33, 33 and 53 bits (`k·part` exact for k < 2^20), `r` as a double-double; `a = |r|`, `j = round(a·128)` (≤ 101), `b = a − j/128` (exact), `sin a = S_j cos b + C_j sin b`, `cos a = C_j cos b − S_j sin b` with tables of `sin(j/128)`, `cos(j/128)` as double-doubles; `sin b`, `cos b − 1` from short polynomials (`b ≤ 2^-8`), the cross term of `r`'s low part included; the large product `C_j·b` is formed exactly. tan = sin/cos with a corrected double division, `−cos/sin` for odd quadrants. | Payne–Hanek reduction (below) for any |x| ≥ π/4, then Taylor series for sin and cos of `r` (8 + 7 double-double terms, 7 + 7 double terms); tan = quotient. |
| **asin, acos, atan, atan2** | `atan` of a non-negative double-double `t`: `t > 1 → 1/t` and `π/2 −`; `j = round(u·64)`; `b = (u − j/64)/(1 + u·j/64)` as a double-double; `atan u = A_j + b − b³/3 + b⁵/5 − b⁷/7 + b⁹/9` (|b| ≤ 2^-7), `A_j = atan(j/64)` double-doubles (table of 65). asin: `atan(x/√(1−x²))` with `1−x²` from an exact `x²`; acos: `2·atan(√((1−x)/(1+x)))` (no cancellation near ±1); atan2: ratio of the scaled magnitudes (smaller over larger), quadrant by `π − a`, `π/2 − a`. | Same identities with `u ← u/(1+√(1+u²))` applied twice, then the Taylor series (12 double-double + 11 double terms), result times 4. |
| **sinh, cosh, tanh** | `u = e^|x|` from stage 1, then `(u − 1/u)/2` (|x| ≥ 0.5), `(u + 1/u)/2`, `(u² − 1)/(u² + 1)` (|x| ≥ 0.5); |x| < 35 (tanh: 22); beyond 35, `e^|x|/2` is the answer to 2^-101. Smaller |x| go to stage 2. | `E = expm1(x)`: `sinh = (E + E/(1+E))/2`, `cosh = (u + 1/u)/2`, `tanh = E₂/(E₂ + 2)`, `E₂ = expm1(2x)`; no cancellation anywhere. |
| **pow** | `|x|^y = exp(y·log|x|)`: stage-1 log as a double-double (x not within 1/16 of 1), times `y` (double-double × double), stage-1 exp (needs |y log x| < 64); error bound `δ = 2^-61 + |y log x|·2^-62`. | Same with the stage-2 log and exp (|y log x| up to 746; δ = 2^-80). Beyond: ±∞/±0. `y = 1, 2, -1, ½` use `x`, `x·x`, `1/x`, `sqrt x`, which IEEE arithmetic already rounds correctly. Sign for negative bases: negative iff `y` is an odd integer. |
| **cbrt** | stage 2 only | `x = m·2^(3q)`, `m ∈ [1,8)`; initial `1 + (m−1)/3.5`; six Newton steps in double; one Newton step in double-double. |
| **hypot** | stage 2 only | Scale so the larger magnitude is in [1,2), `a² + b²` from two exact `TwoProd`s added in double-double, double-double `sqrt`. |

**Payne–Hanek reduction** (`Cores.Reduce`, Payne & Hanek 1983): `|x| = m·2^e`, `m` an integer below 2^53; the binary expansion of 2/π (24 words of 64 bits, generated, `Consts.TwoOverPi`) starting at word `j0 = max(0, (e−2)/64)` for five words is multiplied by `m` into a 6-limb integer; the 2 integer bits give the quadrant, the next 192 bits the fraction; a fraction ≥ ½ is negated (two's complement) and the quadrant incremented; the fraction is converted to a double-double (six 32-bit pieces added smallest first) and multiplied by π/2 (double-double). Truncation error is below 2^-192 of a quadrant, so `r` keeps more than 2^-100 relative accuracy even when `r ≈ 2^-62` (the closest a double gets to a multiple of π/2). The x = `6381956970095103·2^797` case (Muller's worst case for reduction) is in the known-answer file.

**Exact stage algorithms** (`Exact.cs`; value = `M·2^E`, `BigInteger` mantissa truncated to 512 bits after each multiplication, division and square root): π by Machin (`16 atan(1/5) − 4 atan(1/239)`) at 1900 bits, ln 2 = `2 atanh(1/3)`, ln 10 = log 10. `exp`: `k = round(x/ln2)`, reduce, `expm1` series on `r/2^24` and square 24 times. `log`: `x = 2^e m`, eight square roots, `atanh` series. `sin/cos`: `k` by exact integer division by π/2 (1900 bits), series on `|r| ≤ π/4`. `atan`: `1/x` for x > 1, six halvings `u ← u/(1+√(1+u²))`, series. `asin(x) = atan(x/√(1−x²))`, `acos(x) = 2 atan √((1−x)/(1+x))`, `sinh/cosh` from `e^x` (|x| ≥ 1) or `expm1`, `tanh = E₂/(E₂+2)`, `pow = exp(y log x)` with exact power-of-two bases (`2^(jy)` is exact when `jy` is an integer, which matters on subnormal ties), `exp2` of an integer is an exact power of two. `cbrt` and `hypot`: integer cube/square root of the exact integer with ≥ 160 bits and a sticky bit, so exact cases and ties round correctly. Final rounding (`ToDouble`, `ToSingle`) is integer shifting with round-half-even, producing subnormals and overflow correctly. The ≈ 2^-500 truncation error is far below the worst cases published for double (≈ 2^-120 for exp, log, trig; Lefèvre & Muller 2001, Lefèvre 2005 for pow); the only inputs where a value lands exactly on a rounding boundary are those with exact results, which the integer algorithms and the exact power-of-two rules handle.

## Correct rounding: how it is established

- **float32 (2^32 inputs, all 17 unary functions): exhaustive.** Every bit pattern is run; the platform's `Math.*` (double, accurate to ≲ 2^-50) screens each result, and every input whose float rounding the screen cannot settle (within 2^-40 of a rounding boundary — about 5·10^4 inputs per function) or where the result differs from the screened one is checked against the exact stage. Counts of non-correctly-rounded results: see the table. `atan2`, `pow`, `hypot` (two arguments): 4·10^7 random pairs each through the same procedure.
- **double: argument for correctness plus sampling.** Stage 2 decides a result only when its interval `v(1 ± 2^-85)` rounds uniquely, and the measured stage-2 error is ≤ 2^-96, so a double is wrong only if stage 2's error exceeded its declared bound by 2^11 — a claim the tests probe on 2·10^7 inputs per function (including ≈ 2·10^5 compared with the exact stage per function) and 1.6·10^4 per-function error measurements against the exact stage. Everything the tests could not settle goes to stage 3, which uses 500 bits. That is the standard argument of CRlibm/CORE-MATH-style two-step schemes; the unproved part is only that the polynomial/table error bound declared for stages 1 and 2 holds on every input, which is measured, not proved. Known hard cases cited: the argument-reduction worst case above; exact results (`exp2` integers, `log2` of powers of two, `cbrt`, `hypot`, `pow` with exact results) are in the known-answer file and special-case tests, including exact ties on subnormal boundaries (`exp2(-1075)`, `pow(2, -1075)`, `pow(4, -537)`). We did not reproduce the published worst-case tables for double (their inputs are not in the sources we may use); the exact stage is available as the arbiter on any input (`Exact.Eval`, internal; the tests call it).

## Hash matrix

Scenarios `functions/<fn>/f64|f32` (20 functions + `sincos`): 2000 pseudo-random inputs per function and precision plus 19 special values, generated by integer arithmetic only (SplitMix64, exponent/mantissa assembled from bits), every result's bit pattern hashed. The manifest of the fast + double-double + exact design is identical to the manifest of the double-double + exact design (the fast stage adds speed, never a different result).

## Test and tooling map

| What | Where |
|---|---|
| Known answers (3200, both precisions, mpmath at 700 bits) | `testdata/functions/kat.txt`, generator `tools/GenConstants/gen_kat.py`, test `FunctionTests.Known_answers_are_bit_exact` |
| Fast/double-double stages agree with the exact stage | `FunctionTests.Fast_stage_agrees_with_exact_stage` |
| Special values against IEEE | `SpecialValueTests` |
| Stage-1 error bound | `FastStageTests` |
| float32 exhaustive, binary float32 random, double random | `Sweeps` (off unless `ONEMATH_SWEEP=float|double`) |
| Speed against `Math`/`MathF` | `bench/Micro/FunctionsBench.cs` |

## Results (this build; every row is reproducible with the commands in the last section)

**float32, all 2^32 inputs, one run per function (nothing differed from the exact stage):**

| Function | inputs checked against the exact stage after screening | not correctly rounded |
|---|---|---|
| sin / cos / tan | 62,974 / 64,990 / 56,584 | 0 / 0 / 0 |
| asin / acos / atan | 5,332 / 11,939 / 21,954 | 0 / 0 / 0 |
| exp / exp2 / expm1 | 13,614 / 14,038 / 13,079 | 0 / 0 / 0 |
| log / log2 / log10 / log1p | 50,630 / 55,097 / 51,331 / 36,070 | 0 / 0 / 0 / 0 |
| cbrt / sinh / cosh / tanh | 103,318 / 7,916 / 6,324 / 6,720 | 0 / 0 / 0 / 0 |
| atan2 / pow / hypot, 4·10^7 random pairs each (hypot: 1.6·10^7 finite pairs) | 876 / 432 / 4 | 0 / 0 / 0 |

NaN, ±0, ±∞ and out-of-domain inputs (up to half of all bit patterns for the inverse trigonometric and logarithmic functions) are compared with the platform's result, including sign of zero (`expm1(-0)` keeps its sign here; the runtime's series screen does not, which is a screen artifact).

**double, 2·10^7 inputs per function** (generators in `Table.cs`: uniform and log-uniform over the function's range, near ±1 for asin/acos, integer and fractional exponents for pow): 2·10^5 per function compared with the exact stage: **0 differences for all 20 functions**. The rate at which the fast stage could not decide and the slow stage ran (per 5·10^4 probed inputs): sin 108, cos 122, tan 120, asin 115, acos 160, atan 124, atan2 161, exp 909, exp2 902, expm1 281, log 512, log2 205, log10 504, pow 1200, sinh 331, cosh 389, tanh 442 — 0.2% to 2.4%. Stage-2 error was measured against the exact stage on 1.6·10^4 inputs per function (before the fast stage existed, so the double-double stage was the one measured) and never exceeded 2^-96 against a declared 2^-85 (pow: 2^-80).

**Fast-stage error bound (stage 1 against stage 2, ≥ 4·10^6 inputs per function, `FastStageTests`)** — worst relative error seen and the bound handed to the rounding test:

| Function | worst seen | bound used |
|---|---|---|
| sin, cos, tan | 2^-67.5 | 2^-62 |
| asin, acos, atan, atan2 | 2^-66.4 | 2^-62 |
| exp, exp2 | 2^-64.7 | 2^-61 |
| expm1 | 2^-63.6 | 2^-60 |
| log, log2, log10 | 2^-63.1 | 2^-60 |
| sinh, cosh, tanh | 2^-63.8 | 2^-60 |
| pow | 2^-62.1 | 2^-61 + \|y log x\|·2^-62 (at least 3 bits below its bound on every sample) |

**Speed, indicative only** (one shared development machine under load, single thread, 10^5 inputs, best of 5; not a gate measurement): ours / `Math`: sin 2.8×, cos 4.3×, tan 4.9×, asin 9×, acos 11×, atan 12×, atan2 5.5×, exp 12×, exp2 7.7×, log 15×, log2 14×, log10 12×, pow 16×, cbrt 10×, hypot 4.8×, sinh 13×, cosh 21×, tanh 12×. Before the fast stage (double-double stage only) the ratios were 11× to 73×. Where the time goes is the double-double glue (a few dozen flops per call beyond the table-and-polynomial core) and the by-value `R` record.

**Hash matrix** (`functions/*`, 42 scenarios, manifest `12f01fe38319903cec79bea6cdb88a6a04447cec418d13b7aec71d6dd5b660d6`): equal on x64 default, `DOTNET_EnableHWIntrinsic=0`, `DOTNET_EnableAVX2=0` with 4 threads, `TieredCompilation=0`, quick-jit-for-loops off, NativeAOT x64, osx-arm64 and browser-wasm (Node). The same manifest was produced before and after the fast stage was added.

## Reproducing

```
# known answers (needs mpmath):   python3 tools/GenConstants/gen_kat.py > testdata/functions/kat.txt
# tables and constants:           python3 tools/GenConstants/gen_tables.py > src/OneMath/Functions/FastTables.cs
#                                 python3 tools/GenConstants/gen_constants.py > src/OneMath/Functions/Constants.cs
dotnet test tests/OneMath.Tests -c Release                       # unit tests, error bounds, known answers
ONEMATH_SWEEP=float  ONEMATH_FN=exp dotnet test --no-build -c Release tests/OneMath.Tests --filter-method '*Float32_exhaustive_unary'   # one function, ~2-6 min on 8 threads
ONEMATH_SWEEP=float  ONEMATH_N=40000000 dotnet test ... --filter-method '*Float32_random_binary'
ONEMATH_SWEEP=double ONEMATH_N=20000000 ONEMATH_EXACT_EVERY=100 dotnet test ... --filter-method '*Double_random'
dotnet run -c Release --project bench/Micro -- --filter '*FunctionsBench*'   # speed against Math/MathF, ~20-25 min (add --job short for ~8 min)
dotnet run -c Release --project tools/HashMatrix -- --filter functions/       # hash-matrix manifest
```
