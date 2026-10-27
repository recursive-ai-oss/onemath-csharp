# onemath

Deterministic math for .NET (`RecursiveAI.OneMath`): vectors, matrices, quaternions, transcendental functions and exact geometric predicates that give the same bits on every OS, CPU, runtime, WebAssembly mode and thread count. Only IEEE-exact `+ - * /` and `sqrt` are used; every other function is implemented here.

Licensed under either of MIT or Apache-2.0, at your option. Copyright Recursive AI, LLC.

## Layout

- `src/OneMath/` the library (no dependencies beyond the .NET base class library)
- `tests/OneMath.Tests/` xUnit v3 tests; `tests/AotSmoke/` and `tests/WasmSmoke/` publish-and-run checks
- `tools/HashMatrix/` scenario runner that writes a SHA-256 manifest; `tools/hash-matrix.sh` runs it across platforms
- `bench/Micro/` BenchmarkDotNet; `docs/` normative notes

## Build

```
dotnet build -c Release
dotnet build -c Checked     # Release optimisations plus the CHECKED define
dotnet test -c Checked
```

Build output goes to `$ONEPATH_ARTIFACTS` (or `~/.cache/onemath-artifacts`), never into the tree.
