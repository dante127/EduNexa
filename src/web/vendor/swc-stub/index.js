"use strict";

// Local stub for @swc/core (see package.json in this folder and the "overrides"
// entry in the web app's package.json).
//
// Why: @swc/core >= 1.16 materializes its native binding through a "secure cache"
// whose directory validation fails on some Windows hosts (drive roots granting
// replacement rights to other principals) with ERR_SWC_NATIVE_CACHE.
//
// What we actually need: next-intl uses @swc/core only for its optional message
// extraction feature (sources that call `useExtracted`/`getExtracted`). This project
// does not use that feature, so a lazy stub is sufficient and the native binary is
// never required.
//
// If you ever enable next-intl message extraction, remove the `overrides` entry in
// package.json so the real @swc/core is installed, and run on a host whose ACLs
// satisfy SWC's secure-cache validation (or provide SWC_NATIVE_BINDING_CACHE).

function unavailable() {
  throw new Error(
    "@swc/core is stubbed in this workspace (see vendor/swc-stub). " +
      "It is only required for next-intl message extraction (useExtracted/getExtracted), " +
      "which is not enabled in this project."
  );
}

module.exports = new Proxy(
  {},
  {
    get(_target, property) {
      if (property === "then" || property === "catch" || property === "finally") {
        return undefined;
      }
      if (property === "__esModule") {
        return false;
      }
      if (property === Symbol.toStringTag) {
        return "@swc/core (stub)";
      }
      // Return a function so `typeof x === "function"` checks pass; calling any
      // real operation surfaces a clear error instead of loading the native binary.
      return unavailable;
    }
  }
);
