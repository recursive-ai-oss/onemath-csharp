# RecursiveAI.OneMath

Deterministic math for .NET: vectors, matrices, quaternions, elementary functions and exact geometric predicates that give the **same bits on every OS, CPU, runtime, WebAssembly mode and thread count**. No dependencies beyond the .NET base class library; NativeAOT and trimming compatible.

```csharp
using RecursiveAI.OneMath;

double s = Math64.Sin(1e300);                       // correctly rounded, same on every platform
int side = ExactPredicates.Orient2D(0, 0, 1, 0, 0.5, 1e-10);   // +1, -1 or 0, never wrong
long raw = FixedBridge.FromDouble(1.5);            // Q31.32 fixed point <-> double, exact within +-2^21
```

- **Types:** `Vector2/3/4`, `Matrix2x2/3x3/4x4`, `Quaternion`, `Affine3`, `RigidTransform`, `Bounds2/3`, each in float (`F`) and double (`D`) form, with a documented evaluation order.
- **Functions:** `Math32` and `Math64` — sin, cos, tan, asin, acos, atan, atan2, exp, exp2, expm1, log, log2, log10, log1p, pow, cbrt, hypot, sinh, cosh, tanh — correctly rounded (the nearest float or double to the exact result).
- **Exact predicates:** `ExactPredicates` (Orient2D, Orient3D, InCircle, InSphere) and `SegmentPredicates`, adaptive with exact fallback (after Shewchuk 1997). Coordinates must be finite and either zero or of magnitude in [2^-140, 2^200]; **out-of-range input may throw** `ArgumentOutOfRangeException`.
- **Fixed-point bridge:** `FixedBridge` converts Q31.32 integers to and from double, exact for magnitudes below 2^21 and rejecting beyond.
- **Physics math:** symmetric 3x3 eigen decomposition and inertia tensors in `RecursiveAI.OneMath.Physics`.

**Matrix convention.**

Matrices use **column vectors**: `m * v` transforms the column `v`, the translation of a 4x4 sits in column 4 (`M14`, `M24`, `M34`), and `a * b` applies `b` first. **System.Numerics is the opposite**: row vectors, translation in `M41..M43`, and `a * b` applies `a` first, although the field names `Mrc` are the same. Porting by copying fields, or keeping operand order, silently gives a different transform. Translate then rotate:

```csharp
// System.Numerics (row vectors): T first, then R
var sn = System.Numerics.Matrix4x4.CreateTranslation(10, 0, 0) * System.Numerics.Matrix4x4.CreateRotationY(angle);
// OneMath (column vectors): the same transform is R * T
var m = Matrix4x4F.CreateRotationY(angle) * Matrix4x4F.CreateTranslation(new Vector3F(10, 0, 0));
// Convert instead of copying fields: both directions transpose
Matrix4x4F same = Matrix4x4F.FromSystemNumerics(sn);   // same transform as m
System.Numerics.Matrix4x4 back = m.ToSystemNumerics();
```

Vectors and quaternions convert with `FromSystemNumerics` / `ToSystemNumerics` too (quaternions keep their components and their Hamilton product). Converting from double to float is only available as the explicit `ToSystemNumerics()` and rounds to nearest even.

**Stable.** The public API follows semantic versioning from 1.0.0. Results are checked against exact references and compared across x64, arm64 and WebAssembly in the repository's hash matrix.

Full documentation, the normative notes and the changelog: https://github.com/recursive-ai-oss/onemath-csharp/blob/main/README.md. Licensed under MIT or Apache-2.0, at your option.
