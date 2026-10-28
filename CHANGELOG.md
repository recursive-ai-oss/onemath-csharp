# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/) with pre-release suffixes while it is in 0.x.

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

[0.1.0-preview.5]: https://github.com/recursive-ai-oss/onemath-csharp/releases/tag/v0.1.0-preview.5
