"use strict";

const { execFileSync, spawnSync } = require("node:child_process");
const { existsSync } = require("node:fs");
const path = require("node:path");

const repoRoot = path.resolve(__dirname, "..", "..");
const clientsRoot = "src/Clients/";
const steps = ["npm run lint", "npm run typecheck", "npx vite build"];

/** Client roots (a folder under src/Clients with a package.json) touched by the given repository paths. */
function changedClients(paths, exists = (file) => existsSync(path.join(repoRoot, file))) {
  const roots = new Set();
  for (const file of paths) {
    const normalized = file.replaceAll("\\", "/");
    if (!normalized.startsWith(clientsRoot)) continue;
    const root = normalized.split("/").slice(0, 3).join("/");
    if (exists(`${root}/package.json`)) roots.add(root);
  }
  return [...roots].sort();
}

function changedPaths(base) {
  const git = (args) => execFileSync("git", args, { cwd: repoRoot, encoding: "utf8" }).split("\n").filter(Boolean);
  return [...git(["diff", "--name-only", base]), ...git(["ls-files", "--others", "--exclude-standard"])];
}

function main(argv) {
  const baseAt = argv.indexOf("--base");
  const clients = changedClients(changedPaths(baseAt >= 0 ? argv[baseAt + 1] : "HEAD"));
  if (clients.length === 0) {
    console.log("No client changed; nothing to build.");
    return 0;
  }
  for (const client of clients) {
    for (const step of steps) {
      console.log(`[${client}] ${step}`);
      const result = spawnSync(step, { cwd: path.join(repoRoot, client), stdio: "inherit", shell: true });
      if (result.status !== 0) {
        console.error(`FAILED: ${client}: ${step} (exit ${result.status})`);
        return 1;
      }
    }
  }
  return 0;
}

if (require.main === module) process.exit(main(process.argv.slice(2)));

module.exports = { changedClients };
