# RecursiveAI.OneMath

Deterministic math for .NET: vectors, matrices, quaternions, elementary functions and exact geometric predicates that give the **same bits on every OS, CPU, runtime, WebAssembly mode and thread count**. No dependencies beyond the .NET base class library; NativeAOT and trimming compatible.

```csharp
using RecursiveAI.OneMath;

double s = Math64.Sin(1e300);                       // correctly rounded, same on every platform
int side = ExactPredicates.Orient2D(0, 0, 1, 0, 0.5, 1e-300);  // +1, -1 or 0, never wrong
long raw = FixedBridge.FromDouble(1.5);            // Q31.32 fixed point <-> double, exact within +-2^21
```

- **Types:** `Vector2/3/4`, `Matrix2x2/3x3/4x4`, `Quaternion`, `Affine3`, `RigidTransform`, `Bounds2/3`, each in float (`F`) and double (`D`) form, with a documented evaluation order.
- **Functions:** `Math32` and `Math64` — sin, cos, tan, asin, acos, atan, atan2, exp, exp2, expm1, log, log2, log10, log1p, pow, cbrt, hypot, sinh, cosh, tanh — correctly rounded (the nearest float or double to the exact result).
- **Exact predicates:** `ExactPredicates` (Orient2D, Orient3D, InCircle, InSphere) and `SegmentPredicates`, adaptive with exact fallback (after Shewchuk 1997).
- **Fixed-point bridge:** `FixedBridge` converts Q31.32 integers to and from double, exact for magnitudes below 2^21 and rejecting beyond.
- **Physics math:** symmetric 3x3 eigen decomposition and inertia tensors in `RecursiveAI.OneMath.Physics`.

**Preview.** This is a 0.x preview: the API may still change. Results are checked against exact references and compared across x64, arm64 and WebAssembly in the repository's hash matrix.

Full documentation, the normative notes and the changelog: https://github.com/recursive-ai-oss/onemath-csharp/blob/main/README.md. Licensed under MIT or Apache-2.0, at your option.
