// Installs Linux native packages that transitive dependencies require but which are
// missing from the Windows-authored package-lock.json. npm does not self-heal
// platform-specific optional dependencies when the lockfile was generated on a
// different platform (see the ERR_SWC_NATIVE_CACHE / lightningcss incidents).
// No-op on non-Linux platforms, so it is safe to run everywhere.
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";

if (process.platform !== "linux") {
  console.log(`[ci-install-platform-deps] skipped on ${process.platform}`);
  process.exit(0);
}

const pairs = [
  ["@parcel/watcher", "@parcel/watcher-linux-x64-glibc"],
  ["lightningcss", "lightningcss-linux-x64-gnu"],
  ["@tailwindcss/oxide", "@tailwindcss/oxide-linux-x64-gnu"],
  ["typescript", "@typescript/typescript-linux-x64"],
  ["@img/sharp", "@img/sharp-linux-x64"]
];

let lock;
try {
  lock = JSON.parse(readFileSync("package-lock.json", "utf8"));
} catch (error) {
  console.warn(`[ci-install-platform-deps] could not read package-lock.json: ${error.message}`);
  process.exit(0);
}

const pinned = [];
for (const [base, linuxPackage] of pairs) {
  const version = lock.packages?.[`node_modules/${base}`]?.version;
  if (version) {
    pinned.push(`${linuxPackage}@${version}`);
  }
}

if (pinned.length === 0) {
  console.log("[ci-install-platform-deps] nothing to install");
  process.exit(0);
}

const run = (specs) =>
  execFileSync("npm", ["install", "--no-save", "--no-audit", "--no-fund", ...specs], {
    stdio: "inherit"
  });

console.log(`[ci-install-platform-deps] installing: ${pinned.join(" ")}`);
try {
  run(pinned);
} catch {
  // Fall back to unpinned versions if a locked version is not published for Linux.
  const unpinned = pairs.map(([, linuxPackage]) => linuxPackage);
  console.log(`[ci-install-platform-deps] retrying unpinned: ${unpinned.join(" ")}`);
  run(unpinned);
}
