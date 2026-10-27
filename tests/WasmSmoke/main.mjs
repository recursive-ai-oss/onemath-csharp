// Runs the WasmSmoke app under Node: copy this file next to _framework/ in the published bundle and run
// `node main.mjs [hash-matrix options]`. Standard output is the manifest.
import { dotnet } from './_framework/dotnet.js';

const { runMain } = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArguments(...process.argv.slice(2))
    .create();

const exitCode = await runMain();
process.exit(exitCode);
