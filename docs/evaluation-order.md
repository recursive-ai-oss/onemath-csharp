# Evaluation order of the vector, matrix and quaternion types

Normative: a port must evaluate in this order to produce the same bits.

## Rules

- Only IEEE 754 binary32/binary64 `+ - * /` and square root. Each operation is rounded to nearest, ties to even, in the type's own precision (binary32 for the `F` types, binary64 for the `D` types). No fused multiply-add, no reciprocal or square-root estimate instructions, no extended precision.
- Every `a*b + c*d` rounds each product, then rounds the sum. Sums of three or more terms accumulate left to right: `((t1 + t2) + t3) + t4`. Subtractions in the formulas below are written in the order given.
- Division is a true division (`x / s`), never a multiplication by a precomputed reciprocal.
- Square root is the correctly rounded square root. For binary32 it is computed as the binary64 square root of the widened value, rounded once to binary32; this equals the correctly rounded binary32 square root (double rounding is innocuous when the wide format has at least 2p + 2 significand bits; Figueroa 1995).
- Scaling by 0.5 or 2 is exact and written as a multiplication.
- Min and Max return the second operand when the comparison `a < b` (or `a > b`) is false, so a NaN in either operand gives the second operand.
- **NaN:** whether an operation that produces a NaN yields a positive or negative NaN, and which payload, is decided by the CPU (x64 and arm64 differ) and is not part of the contract. Everything else, including infinities, signed zeros and subnormals, is bit-exact. The hash-matrix runner writes every NaN as one canonical NaN.

## Vectors

- `Dot(a, b)` = `((a.X*b.X + a.Y*b.Y) + a.Z*b.Z) + a.W*b.W` (as many terms as the dimension).
- `LengthSquared` = the dot product with itself; `Length` = sqrt of that. `Normalized` divides each component by `Length` and returns zero when `Length == 0`.
- `Cross` (3D) = `(a.Y*b.Z - a.Z*b.Y, a.Z*b.X - a.X*b.Z, a.X*b.Y - a.Y*b.X)`; 2D cross = `a.X*b.Y - a.Y*b.X`.
- `Lerp(a, b, t)` = `a + (b - a) * t`, component-wise.

## Matrices

Row-major fields `Mrc`; column-vector convention (`M * v`). Translation sits in column 4 (`M14`, `M24`, `M34`) and `a * b` applies `b` first. This is the transpose of System.Numerics' row-vector convention, which keeps the same field names; `FromSystemNumerics` / `ToSystemNumerics` on `Matrix4x4` transpose. The product of System.Numerics matrices `T * R` (translate, then rotate) corresponds to `R * T` here.

- `CreateRotationX/Y/Z(radians)` with `(s, c) = SinCos(radians)`: X is rows `(1,0,0) (0,c,-s) (0,s,c)`, Y is `(c,0,s) (0,1,0) (-s,0,c)`, Z is `(c,-s,0) (s,c,0) (0,0,1)`. The 4x4 forms embed these with zero translation (`CreateFromMatrix3x3`: entries copied, `0 0 0 1` bottom row). `CreateFromQuaternion(q)` is `q.ToMatrix()`.

- Product entry (r, c) = `((a.Mr1*b.M1c + a.Mr2*b.M2c) + a.Mr3*b.M3c) + a.Mr4*b.M4c`; matrix-vector likewise.
- 2x2 determinant `M11*M22 - M12*M21`; inverse entries `M22/det, -M12/det, -M21/det, M11/det`.
- 3x3 cofactors `c11 = M22*M33 - M23*M32`, `c12 = M23*M31 - M21*M33`, `c13 = M21*M32 - M22*M31`, `c21 = M13*M32 - M12*M33`, `c22 = M11*M33 - M13*M31`, `c23 = M12*M31 - M11*M32`, `c31 = M12*M23 - M13*M22`, `c32 = M13*M21 - M11*M23`, `c33 = M11*M22 - M12*M21`; determinant `(M11*c11 + M12*c12) + M13*c13`; inverse = `[c11 c21 c31; c12 c22 c32; c13 c23 c33] / det` (each entry one division).
- 4x4: minors of rows 1-2 `s0 = M11*M22 - M21*M12`, `s1 = M11*M23 - M21*M13`, `s2 = M11*M24 - M21*M14`, `s3 = M12*M23 - M22*M13`, `s4 = M12*M24 - M22*M14`, `s5 = M13*M24 - M23*M14`; of rows 3-4 `c5 = M33*M44 - M43*M34`, `c4 = M32*M44 - M42*M34`, `c3 = M32*M43 - M42*M33`, `c2 = M31*M44 - M41*M34`, `c1 = M31*M43 - M41*M33`, `c0 = M31*M42 - M41*M32`. Determinant `s0*c5 - s1*c4 + s2*c3 + s3*c2 - s4*c1 + s5*c0` summed left to right. Inverse entries are the three-term sums in `Matrix4x4.TryInvert` (each term a product of one matrix entry and one minor, summed left to right, then divided by the determinant).
- `TryInvert` fails (returns false, zero matrix) when the determinant is zero or not finite. There is no conditioning test.

## Quaternions

- Hamilton product `a * b`: `X = ((a.W*b.X + a.X*b.W) + a.Y*b.Z) - a.Z*b.Y`, `Y = ((a.W*b.Y - a.X*b.Z) + a.Y*b.W) + a.Z*b.X`, `Z = ((a.W*b.Z + a.X*b.Y) - a.Y*b.X) + a.Z*b.W`, `W = ((a.W*b.W - a.X*b.X) - a.Y*b.Y) - a.Z*b.Z`.
- `Rotate(v)`: `t = 2 * cross(q.xyz, v)`, result `(v + t*W) + cross(q.xyz, t)`.
- `ToMatrix`: `xx = X*X` etc., diagonal `1 - 2*(yy + zz)`, `1 - 2*(xx + zz)`, `1 - 2*(xx + yy)`, off-diagonals `2*(xy - wz)`, `2*(xz + wy)`, `2*(xy + wz)`, `2*(yz - wx)`, `2*(xz - wy)`, `2*(yz + wx)`.
- `CreateFromRotationMatrix`: trace method; branch order trace > 0, then `M11 > M22 && M11 > M33`, then `M22 > M33`, else M33.
- `NLerp`: negate `b` when `Dot(a, b) < 0`; then component-wise `Lerp`; then `Normalized` (identity for a zero result).
- `CreateFromAxisSinCos(axis, sinHalf, cosHalf)` only multiplies: `(axis.X*sinHalf, axis.Y*sinHalf, axis.Z*sinHalf, cosHalf)`. `CreateFromAxisAngle(axis, radians)` is exactly `CreateFromAxisSinCos(axis, SinCos(radians / 2))` with the library's own `SinCos` (`Math64` for double, `Math32` for float).
- `Slerp(a, b, t)`: `d = Dot(a, b)`; if `d < 0` negate `b` and `d`; if `d > 0.9995` return `Lerp(a, b, t).Normalized()`; otherwise `theta = Acos(d)`, `s = Sin(theta)`, `wa = Sin((1 - t) * theta) / s`, `wb = Sin(t * theta) / s`, and each component is `a.c * wa + b.c * wb` (two products, one sum; not renormalized).

## Transforms and bounds

- `Affine3`: `TransformPoint(p) = Linear*p + Translation`; composition `a*b` has linear part `a.Linear*b.Linear` and translation `a.Linear*b.Translation + a.Translation`; the inverse is `(L^-1, -(L^-1 * t))`.
- `RigidTransform`: same shape with the quaternion in place of the matrix; `Inverse` uses the conjugate.
- Bounds: `Center = Min + (Max - Min) * 0.5`; `Volume = (sx*sy)*sz`; `SurfaceArea = 2 * ((sx*sy + sy*sz) + sz*sx)`; empty boxes have `Min = +inf, Max = -inf`.

## Naming and conversions

- Factories that build a value from parts or another concept are `Create...` (`CreateTranslation`, `CreateScale`, `CreateRotationX`, `CreateFromQuaternion`, `CreateFromRotationMatrix`, `CreateFromPoints`). `From...` is only for pure representation changes (`FromRows`, `FromColumns`, `FromSystemNumerics`). Instance methods that return a new value use participles (`Normalized`, `Transposed`, `Inverted`, `Conjugated`); `TryInvert(out)` is the fallible form. Row and column parameters of `FromRows` / `FromColumns` are 0-based (`row0`, `column0`, ...) like `Row(int)` and `Column(int)`.
- `FromSystemNumerics` / `ToSystemNumerics` exist for vectors, quaternions (same components, same Hamilton product) and 4x4 matrices (transposed). Float to float is a plain copy; double to float rounds to nearest even and is only available as the explicit method `ToSystemNumerics()`.
- JSON (`System.Text.Json`): every public value type serializes its public fields (or properties for the physics result types) by name and reads them back through its component constructor, bit for bit with default options and with source-generated metadata. Derived members (`QuaternionD.Vector`, `Vector4D.XYZ`, `Bounds.IsEmpty`/`Size`/`Extents`/`Center`, `PrincipalAxes.Orientation`) are not written. JSON has no NaN or infinity: such a value (for example `Bounds.Empty`) cannot be written with default options.
