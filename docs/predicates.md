# Exact geometric predicates

Normative description of `RecursiveAI.OneMath.Predicates`. A port that follows this text produces the same signs on every input, and the same expansion components in the internal engine.

Sources: Shewchuk, *Adaptive Precision Floating-Point Arithmetic and Fast Robust Geometric Predicates* (Discrete & Computational Geometry 18, 1997); Dekker (1971), Knuth (TAOCP vol. 2) and Priest (1991) for the error-free transformations.

## Arithmetic

Only IEEE 754 binary64 `+`, `-`, `*` with round-to-nearest-even and `abs`. No fused multiply-add anywhere; products are made exact by Dekker splitting. Float32 inputs convert to double exactly, so every predicate accepts them unchanged.

## Public API

| Call | Returns the sign of |
|---|---|
| `Orient2D(a, b, c)` | `(ax-cx)(by-cy) - (ay-cy)(bx-cx)`. +1 = counter-clockwise (c left of a→b, y up). |
| `Orient3D(a, b, c, d)` | `det[a-d; b-d; c-d]`. +1 = d below the plane abc, where a, b, c run counter-clockwise seen from above (Shewchuk's convention: a point above the plane gives -1). |
| `InCircle(a, b, c, d)` | `det[(a-d, \|a-d\|²); (b-d, \|b-d\|²); (c-d, \|c-d\|²)]`. +1 = d inside the circle when a, b, c are counter-clockwise. |
| `InSphere(a, b, c, d, e)` | `det[(a-e, \|a-e\|²); …; (d-e, \|d-e\|²)]` (4×4, rows a, b, c, d). +1 = e inside the sphere when `Orient3D(a, b, c, d) > 0`. |
| `SegmentPredicates.Classify(a, b, c, d)` | relation of closed segments ab and cd: `Disjoint`, `Proper`, `Touching`, `CollinearOverlap`. |

The result is `-1`, `0` or `+1`, and equals the sign of the determinant of the *exact real values* of the input doubles.

## Input requirements and range

- Every coordinate must be finite. NaN or ±∞ throws `ArgumentOutOfRangeException`.
- Exactness is guaranteed when each coordinate is 0 or has magnitude in [2^-140, 2^200]. The determinants have degree at most 5 (in-sphere), so every product then stays within [2^-960, 2^1005]: nothing overflows and the error term of no product underflows.
- **Out-of-range input may throw.** Outside that range a call that needs the exact path throws `ArgumentOutOfRangeException`; its `ParamName` is the coordinate parameter of the method that was called (`cx`, `dz`, ...). A call the filter decides by margin returns that sign, which is still correct, so whether an out-of-range call throws depends on the values; do not rely on either outcome and keep inputs inside the range. `SegmentPredicates.Classify` always checks the range first, so it throws for every out-of-range coordinate.
- A later release may extend the exact path (for example by power-of-two pre-scaling) so that out-of-range finite input stops throwing; removing an exception is compatible, so callers must not depend on it being thrown.
- The coordinate rule of the family (|x| < 2^21, resolution 2^-32) is far inside the range.

## Stage A: the floating-point filter

Each predicate first evaluates its determinant in double with a fixed operation order and also accumulates the *permanent*: the same expression with every subtraction replaced by addition of absolute values. With ε = 2^-53 the result is accepted when `|det| > c·permanent + 2^-1000`, with

| predicate | c |
|---|---|
| orient2d | (3 + 16ε)ε |
| orient3d | (7 + 56ε)ε |
| incircle | (10 + 96ε)ε |
| insphere | (16 + 224ε)ε |

These are Shewchuk's published stage-A bounds for exactly this evaluation order (the orders are written down in the code comments and must not be reassociated). `2^-1000` is an absolute term that has no effect inside the supported range. NaN and ∞ never pass the comparison, so they reach the exact path and are rejected there.

Evaluation orders (translated differences first):

- orient2d: `detleft = (ax-cx)*(by-cy)`, `detright = (ay-cy)*(bx-cx)`, `det = detleft - detright`; permanent `|detleft| + |detright|`.
- orient3d: `det = adz*(bdx*cdy - cdx*bdy) + bdz*(cdx*ady - adx*cdy) + cdz*(adx*bdy - bdx*ady)` with `adx = ax-dx` etc., left-to-right; permanent with absolute values.
- incircle: lifts `alift = adx*adx + ady*ady`, then `det = alift*(bdx*cdy - cdx*bdy) + blift*(cdx*ady - adx*cdy) + clift*(adx*bdy - bdx*ady)`; permanent likewise.
- insphere: 2×2 minors `xi*yj - xj*yi` of the translated (x, y) columns for the pairs ab, bc, cd, da, ac, bd; 3×3 minors `abc = aez*bc - bez*ac + cez*ab`, `bcd = bez*cd - cez*bd + dez*bc`, `cda = cez*da + dez*ac + aez*cd`, `dab = dez*ab + aez*bd + bez*da`; lifts `x²+y²+z²` added left to right; `det = (dlift*abc - clift*dab) + (blift*cda - alift*bcd)`; permanent mirrors it with absolute values.

## Stage D: exact evaluation

When the filter cannot decide, the determinant is evaluated exactly with expansion arithmetic and its sign read from the largest component. (The intermediate adaptive stages of the paper are not used: the exact stage is entered directly. Its sign is the same by construction; only the cost of near-degenerate inputs differs.)

- Translated coordinate differences are exact two-component expansions (`TwoDiff`), except orient2d which uses the translation-free form `ax(by-cy) + bx(cy-ay) + cx(ay-by)`, equal to the filter's determinant as a real number.
- 3×3 determinants expand along the first row of 2×2 minors; the in-sphere 4×4 uses the cofactor form of the filter's last line, with the four 3×3 minors computed on the translated rows in the order named above. Lifts are exact sums of exact squares.
- Products of expansions are sums, in increasing component order of the right factor, of `Scale` results.

## Expansion arithmetic (internal engine)

`ExpansionArithmetic` is internal: it is the engine behind `ExactPredicates` and `SegmentPredicates`, not public API (its preconditions are unchecked, and its output-size rules are not a promise). The text below is normative for the predicates' results.

An expansion is a list of doubles, sorted by increasing magnitude, nonoverlapping (the highest set bit of each component is below the lowest set bit of the next), with no zero component. The value is the exact sum; the sign is the sign of the last component; zero is the empty list.

- `TwoSum(a,b)`: `x = a+b; bv = x-a; av = x-bv; y = (a-av)+(b-bv)`. `TwoDiff` likewise with `bv = a-x; av = x+bv; y = (a-av)+(bv-b)`.
- `Split(a)`: `c = (2^27+1)·a; abig = c-a; hi = c-abig; lo = a-hi`. `TwoProduct(a,b)`: `x = a·b`, then with both operands split, `y = alo·blo - (((x - ahi·bhi) - alo·bhi) - ahi·blo)`. Requires |a|,|b| < 2^995 and the product's error term not to underflow.
- `Sum(e,f)`: copy e, then for each component of f run Grow-Expansion (Shewchuk Alg. 3): `Q = b; for each h_i: (Q, hh) = TwoSum(Q, h_i); keep hh if nonzero`; append Q if nonzero. `Difference` adds the negated components.
- `Scale(e,b)` (Shewchuk Alg. 6): `(Q, h) = TwoProduct(e_1, b)`; for i ≥ 2: `(T, t) = TwoProduct(e_i, b)`; `(S, h) = TwoSum(Q, t)`; `(Q, h') = FastTwoSum(T, S)`; keep nonzero h, h'; append Q.
- `Product(e,f)`: the sum over components f_j (increasing) of `Scale(e, f_j)`, accumulated with `Sum`.
- Output sizes: `Sum`/`Difference` ≤ |e|+|f|; `Scale` ≤ 2|e|; `Product` ≤ 2|e||f| (the caller's buffer needs that capacity).

Scratch space for intermediates is a per-thread bump allocator of 2^17 doubles allocated on first exact evaluation. Which thread ran a call never affects its result.

## Segment classification

With `o1 = Orient2D(a,b,c)`, `o2 = Orient2D(a,b,d)`, `o3 = Orient2D(c,d,a)`, `o4 = Orient2D(c,d,b)`:

1. A segment with equal endpoints is a point; point–point is `Touching` if equal else `Disjoint`; point–segment is `Touching` if collinear and inside the segment's bounding box, else `Disjoint`.
2. `Disjoint` if `o1·o2 > 0` or `o3·o4 > 0`.
3. If `o1 = o2 = 0` (collinear): order each segment's endpoints lexicographically (x, then y, the order along the line), take the larger lower end and smaller upper end: `CollinearOverlap` if lower < upper, `Touching` if equal, else `Disjoint`.
4. `Proper` if `o1·o2 < 0` and `o3·o4 < 0`; otherwise `Touching`.

## Checks

`tests/OneMath.Tests/PredicateTests.cs` compares every predicate with exact BigInteger evaluation (inputs scaled by 2^1074 so each double is an integer) on random, small-integer, wide-exponent, large-offset, and near-degenerate sets (collinear, coplanar, cocircular, cospherical points nudged by 0–2 ulps); the case count comes from `ONEMATH_PREDICATE_CASES` (default 100 000 per predicate). The input generator is `tools/HashMatrix/Scenarios/Predicates/PredicateInputs.cs`; the hash-matrix scenarios `predicates/*` hash the signs over fixed sets. Benchmarks: `bench/Micro/PredicateBench.cs`.
