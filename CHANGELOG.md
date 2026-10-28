# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/) with pre-release suffixes for previews.

## [1.0.0]

The first stable release. The public API is frozen under semantic versioning. Results are bit-identical to 0.1.0-preview.5: there is no numeric change.

From this release on, results are also bit-identical across every release of a major version: no 1.x minor or patch release changes an existing result (see "The determinism promise" in the README).

### Changed (breaking since 0.1.0-preview.5)

One verb per concept: `Create...` builds a value from parts or another concept, `From...` is only a representation change, and instance methods that return a new value are participles. Numeric results are unchanged; only names move.

| Before | Now |
|---|---|
| `Matrix2x2/3x3/4x4 D/F .Transpose()` | `.Transposed()` |
| `QuaternionD/F .Conjugate()` | `.Conjugated()` |
| `RigidTransformD/F .Inverse()` | `.Inverted()` |
| `QuaternionD/F .FromAxisSinCos(axis, sin, cos)` | `.CreateFromAxisSinCos(axis, sin, cos)` |
| `QuaternionD/F .FromMatrix(m)` | `.CreateFromRotationMatrix(m)` |
| `Affine3D/F .FromTranslation(v)` | `.CreateTranslation(v)` |
| `Bounds2/3 D/F .FromPoints(points)` | `.CreateFromPoints(points)` |
| `Bounds2/3 D/F .FromCenterExtents(c, e)` | `.CreateFromCenterExtents(c, e)` |
| `FixedBridge.MaxMagnitude` (`const`) | `FixedBridge.ExclusiveMagnitudeBound` (static property; the bound is exclusive) |
| `PrincipalAxesD/F(moments, rotation, orientation)` | `PrincipalAxesD/F(moments, rotation)`; `Orientation` is derived from `Rotation`, so the two cannot disagree |
| `MassPropertiesD/F(mass, ...)` accepted a negative mass | throws `ArgumentOutOfRangeException` (parameter `mass`) |

Also: the parameters of `FromRows` / `FromColumns` are now `row0..` / `column0..` (0-based, like `Row(int)` and `Column(int)`); `ParamName` of every argument exception names a parameter of the public method that was called (predicates: `ax`..`ez`; `InertiaD/F.PrincipalAxes`: `tensor`); all public value types serialize with System.Text.Json (public fields and component constructors, derived members ignored; bit-exact round trip with default options and with source-generated metadata); the package project URL points at the repository; the matrix convention (column vectors, the opposite of System.Numerics) and the predicate input range (out-of-range input may throw) are documented in the README, the package readme and the type and method docs.

### Added

- `QuaternionD/F`: `CreateFromAxisAngle` (exactly `CreateFromAxisSinCos(axis, SinCos(radians / 2))`) and `Slerp` (shortest arc; evaluation order in `docs/evaluation-order.md`).
- `Matrix3x3D/F`: `CreateRotationX/Y/Z`, `CreateFromQuaternion`. `Matrix4x4D/F`: `CreateRotationX/Y/Z`, `CreateFromQuaternion`, `CreateFromMatrix3x3`, `CreateFromAffine`.
- `Affine3D/F`: `CreateScale`, `CreateFromLinear`, `CreateFromQuaternion`, `CreateRotationX/Y/Z`.
- `FromSystemNumerics` / `ToSystemNumerics` on `Vector2/3/4`, `Quaternion` and `Matrix4x4` (D and F). The matrix conversions transpose; double to float rounds to nearest even.
- Hash-matrix scenarios `types/f32/rotation`, `types/f64/rotation` and `types/json` (a source-generated JSON round trip that also runs in the NativeAOT and WebAssembly smoke apps). Every earlier manifest line is unchanged.

### Removed

- `ExpansionArithmetic` is no longer public. Its preconditions are not checked (invalid input gives silently wrong results), and publishing it would freeze Shewchuk's output-size rules. It remains the internal engine behind `ExactPredicates` and `SegmentPredicates`; exposing a checked form later would be an additive change.

## [0.1.0-preview.5]

The first version published to nuget.org. It is a preview: public API may change before 1.0.

### Added

- Vectors (2, 3, 4), matrices (2x2, 3x3, 4x4), quaternions, affine and rigid transforms and axis-aligned bounds, in float and double, with a documented evaluation order (`docs/evaluation-order.md`).
- Correctly rounded elementary functions for float and double (`Math32`, `Math64`): sin, cos, tan, asin, acos, atan, atan2, exp, exp2, expm1, log, log2, log10, log1p, pow, cbrt, hypot, sinh, cosh, tanh, sincos. Float results were checked exhaustively; double results against an exact big-number stage. They are slower than the platform's `Math` functions (about 3x to 20x single-threaded in an informal measurement; see `docs/functions.md`), in exchange for identical bits everywhere.
- Exact geometric predicates (`Orient2D`, `Orient3D`, `InCircle`, `InSphere`, segment classification) with a fast filter that always agrees with the exact sign.
- The Q31.32 fixed-point bridge (`FixedBridge`): exact conversion for magnitudes below 2^21, rejecting beyond.
- Symmetric 3x3 eigen decomposition and inertia-tensor math (`RecursiveAI.OneMath.Physics`).
- NativeAOT and trimming compatible; no dependencies beyond the .NET base class library.
- A hash-matrix runner that compares results across JIT, scalar, NativeAOT and WebAssembly builds, and across x64 and arm64.

### Known limits

- Preview status: no API stability promise yet.
- The correctly rounded functions favour exactness over speed; faster opt-in variants are not part of this release.

[1.0.0]: https://github.com/recursive-ai-oss/onemath-csharp/releases/tag/v1.0.0
[0.1.0-preview.5]: https://github.com/recursive-ai-oss/onemath-csharp/releases/tag/v0.1.0-preview.5
