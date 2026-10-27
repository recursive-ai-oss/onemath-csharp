# Hash matrix

A scenario is a deterministic function that writes bytes to a `ScenarioContext`; its SHA-256 goes in a manifest. Two platforms agree when their manifests are identical.

## Adding a scenario

Create a file under `tools/HashMatrix/Scenarios/<area>/` (the `HashMatrix` project and the smoke apps compile everything beneath it) with a module initializer:

```csharp
namespace RecursiveAI.OneMath.HashMatrix;

internal static class MyScenarios
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Init() => Scenarios.Register("area/name", ctx => { ctx.Write(1.0); });
}
```

No shared file changes. Names are lower-case, slash separated and unique; the manifest orders them by ordinal comparison. Writers (`Write(int|long|float|double|bytes)`) are little-endian by bit pattern; every NaN is written as one canonical NaN because arithmetic NaN sign and payload are chosen by the CPU. `ctx.Threads` is the worker count a parallel scenario should use; the output must not depend on it (split by index, combine in index order). Use a seeded generator, never `Random` or time.

## Running

`HashMatrix [--threads N] [--filter PREFIX] [--out FILE] [--list]` prints `name sha256` per scenario and a final `manifest sha256` over those lines. `tools/hash-matrix.sh` runs every column (see its header) and compares them.

Columns: x64 JIT; x64 JIT with `DOTNET_EnableHWIntrinsic=0`; with `DOTNET_EnableAVX2=0`; 1 thread and all cores; x64 NativeAOT (`tests/AotSmoke`); browser-wasm interpreted and AOT-compiled (`tests/WasmSmoke`, run under Node); osx-arm64.
