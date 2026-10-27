#!/usr/bin/env bash
# Runs the hash-matrix scenarios in every local column and compares the manifests.
#
#   tools/hash-matrix.sh [-o OUTDIR] [column ...]
#
# Columns: jit jit-threads-all scalar noavx2 tc0 aot wasm wasm-aot   (default: all)
# Each column writes OUTDIR/<column>.txt (the runner's manifest). The first column is the reference; the
# script exits non-zero if any manifest differs. Further manifests (for example from an arm64 Mac) can be
# dropped into OUTDIR as <name>.txt and are compared by `tools/hash-matrix.sh -o OUTDIR compare`.
#
# Needs the .NET 10 SDK on PATH (DOTNET_ROOT set) and, for the wasm columns, the wasm-tools workload and Node.
# Build output goes where ONEPATH_ARTIFACTS points (default ~/.cache/onemath-artifacts).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ART="${ONEPATH_ARTIFACTS:-$HOME/.cache/onemath-artifacts}"
OUT="${TMPDIR:-/tmp}/hash-matrix"
if [[ "${1:-}" == "-o" ]]; then OUT="$2"; shift 2; fi
mkdir -p "$OUT"
RID="${HASHMATRIX_RID:-$(dotnet --info | sed -n 's/^ *RID: *//p' | head -1)}"

ALL=(jit jit-threads-all scalar noavx2 tc0 aot wasm wasm-aot)
if [[ $# -eq 0 ]]; then set -- "${ALL[@]}"; fi

build_jit() {
  [[ -n "${JIT_BUILT:-}" ]] && return
  dotnet build "$ROOT/tools/HashMatrix" -c Release -v quiet -clp:NoSummary >/dev/null
  JIT_BUILT=1
}
jit_dll() { echo "$ART/bin/HashMatrix/release/HashMatrix.dll"; }

publish_aot() {
  dotnet publish "$ROOT/tests/AotSmoke" -c Release -r "$RID" -o "$ART/aot" -v quiet -clp:NoSummary
}

publish_wasm() { # $1 = out dir, rest = extra msbuild args
  local dest="$1"; shift
  dotnet publish "$ROOT/tests/WasmSmoke" -c Release -o "$ART/$dest-publish" "$@" -v quiet -clp:NoSummary >/dev/null
  local bundle
  bundle="$(find "$ART/bin/WasmSmoke" -type d -name AppBundle | head -1)"
  rm -rf "$ART/$dest"; cp -r "$bundle" "$ART/$dest"
  cp "$ROOT/tests/WasmSmoke/main.mjs" "$ART/$dest/main.mjs"
}

run_column() {
  local col="$1" out="$OUT/$1.txt"
  case "$col" in
    jit)              build_jit; dotnet "$(jit_dll)" --threads 1 --out "$out" >/dev/null ;;
    jit-threads-all)  build_jit; dotnet "$(jit_dll)" --threads 0 --out "$out" >/dev/null ;;
    scalar)           build_jit; DOTNET_EnableHWIntrinsic=0 dotnet "$(jit_dll)" --threads 1 --out "$out" >/dev/null ;;
    noavx2)           build_jit; DOTNET_EnableAVX2=0 dotnet "$(jit_dll)" --threads 1 --out "$out" >/dev/null ;;
    tc0)              build_jit; DOTNET_TieredCompilation=0 DOTNET_ReadyToRun=0 dotnet "$(jit_dll)" --threads 1 --out "$out" >/dev/null ;;
    aot)              publish_aot; "$ART/aot/AotSmoke" --out "$out" >/dev/null ;;
    wasm)             publish_wasm wasm;                              (cd "$ART/wasm" && node main.mjs | grep -E "^(manifest |[a-z0-9/_.-]+ [0-9a-f]{64}\$)" > "$out") ;;
    wasm-aot)         publish_wasm wasm-aot -p:RunAOTCompilation=true; (cd "$ART/wasm-aot" && node main.mjs | grep -E "^(manifest |[a-z0-9/_.-]+ [0-9a-f]{64}\$)" > "$out") ;;
    *) echo "unknown column: $col" >&2; return 2 ;;
  esac
}

compare() {
  local ref="" status=0
  for f in "$OUT"/*.txt; do
    [[ -z "$ref" ]] && ref="$f"
    local name; name="$(basename "$f" .txt)"
    local m; m="$(grep '^manifest ' "$f" | cut -d' ' -f2)"
    if diff -q "$ref" "$f" >/dev/null; then echo "equal   $name  $m"; else echo "DIFFERS $name  $m"; status=1; fi
  done
  return $status
}

if [[ "$1" == "compare" ]]; then compare; exit $?; fi
for col in "$@"; do
  echo "== $col"
  run_column "$col"
done
compare
