# onemath

Deterministic math for .NET (`RecursiveAI.OneMath`): vectors, matrices, quaternions, elementary functions and exact geometric predicates that give the same bits on every OS, CPU, runtime, WebAssembly mode and thread count. Only IEEE-exact `+ - * /` and `sqrt` decide a result; every other function is implemented here. No dependencies beyond the .NET base class library; NativeAOT and trimming compatible.

**Status: 1.0.0.** The public API is frozen under semantic versioning. Every result that exists in `0.1.0-preview.5` is bit-identical in 1.0.0; the additions are listed in the changelog. See [CHANGELOG.md](CHANGELOG.md).

```
dotnet add package RecursiveAI.OneMath
```

```csharp
using RecursiveAI.OneMath;
using RecursiveAI.OneMath.Predicates;

double s = Math64.Sin(1e300);                                  // correctly rounded, same on every platform
int side = ExactPredicates.Orient2D(0, 0, 1, 0, 0.5, 1e-10);   // sign of the exact determinant: +1, -1 or 0
long raw = FixedBridge.FromDouble(1.5);                        // Q31.32 fixed point, exact within +-2^21
```

## What is in it

- **Types:** `Vector2/3/4`, `Matrix2x2/3x3/4x4`, `Quaternion`, `Affine3`, `RigidTransform`, `Bounds2/3`, each in float (`F`) and double (`D`) form. The evaluation order of every operation is written down in [docs/evaluation-order.md](docs/evaluation-order.md), so another implementation can reproduce the bits.
- **Elementary functions:** `Math32` and `Math64` provide sin, cos, tan, sincos, asin, acos, atan, atan2, exp, exp2, expm1, log, log2, log10, log1p, pow, cbrt, hypot, sinh, cosh and tanh. They are **correctly rounded**: the result is the float or double nearest to the exact value. How this is checked, and the error bounds of each internal stage, are in [docs/functions.md](docs/functions.md).
- **Exact predicates:** `Orient2D`, `Orient3D`, `InCircle`, `InSphere` and segment classification, adaptive arithmetic whose fast filter always returns the sign of the exact value ([docs/predicates.md](docs/predicates.md)). Coordinates must be finite and either zero or of magnitude in [2^-140, 2^200]; **out-of-range input may throw** `ArgumentOutOfRangeException`.
- **Fixed-point bridge:** conversion between a deterministic host's Q31.32 integers and double, exact for magnitudes below 2^21 and rejecting anything beyond ([docs/fixed-bridge.md](docs/fixed-bridge.md)).
- **Physics math:** symmetric 3x3 eigen decomposition and inertia tensors ([docs/physics.md](docs/physics.md)).

## Matrix convention

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

## The determinism promise

A result depends only on its inputs: not on the OS, the CPU or its features (SIMD or scalar, with or without FMA hardware), the runtime version, the JIT tier, NativeAOT or WebAssembly, or the thread count. The library never calls `Math.Sin` and friends and never uses fused multiply-add; a banned-symbol analyzer enforces this at build time.

**Across versions.** Within a major version, every release gives the same bits for the same inputs: no 1.x minor or patch release changes the result of any existing function, type or predicate. A change that would alter an existing result, even a more accurate one, either ships as a new member that leaves the old one as it was, or waits for the next major version and is listed in the changelog. Members added in a minor release then keep their results for the rest of that major version. So a deterministic host can update within 1.x without breaking replays, saved games or lockstep peers that run a different 1.x release.

The promise is tested by a hash matrix: [tools/HashMatrix](tools/HashMatrix) runs fixed scenarios and writes a SHA-256 manifest, and the manifests must be equal across x64 JIT, scalar (`DOTNET_EnableHWIntrinsic=0`), `DOTNET_EnableAVX2=0`, one thread and all cores, NativeAOT, browser-wasm (interpreted and AOT-compiled) and osx-arm64. See [docs/hash-matrix.md](docs/hash-matrix.md). Across versions, every line of the previous release's manifest must appear unchanged in the next release's; a minor release may only add lines.

## Speed

Correct rounding costs time. Informal single-thread measurements on a shared development machine (ours divided by the platform's `Math`; not a gate measurement): sin 2.8x, cos 4.3x, tan 4.9x, atan2 5.5x, exp 12x, log 15x, pow 16x, cosh 21x. The full table and how to reproduce it are in [docs/functions.md](docs/functions.md).

## Platforms

`net10.0` on Linux (x64, arm64), macOS (arm64), Windows (x64) and browser-wasm. CI builds and tests on the four native targets, publishes the NativeAOT smoke app with warnings as errors, and compares the hash-matrix manifest on WebAssembly.

## Layout

- `src/OneMath/` the library
- `tests/OneMath.Tests/` xUnit v3 tests; `tests/AotSmoke/` and `tests/WasmSmoke/` publish-and-run checks
- `tools/HashMatrix/` scenario runner; `tools/hash-matrix.sh` runs it across platforms
- `bench/Micro/` BenchmarkDotNet; `docs/` normative notes

## Build

```
dotnet build -c Release
dotnet test -c Release
dotnet build -c Checked     # Release optimisations plus the CHECKED define
```

Build output goes to `$ONEMATH_ARTIFACTS` (or `~/.cache/onemath-artifacts`), never into the tree.

## Licence

Licensed under either of [MIT](LICENSE-MIT) or [Apache-2.0](LICENSE-APACHE), at your option. Copyright Recursive AI, LLC.
