"use strict";

const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const { existsSync, readFileSync } = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const { changedClients } = require("./clientBuildGate");

const client = path.resolve(__dirname, "..", "..", "src", "Clients", "PosTerminal");
const lintScript = JSON.parse(readFileSync(path.join(client, "package.json"), "utf8")).scripts.lint;
const fixture = (name) => path.join(__dirname, "fixtures", name);
// The production lint command itself, pointed at one fixture instead of src.
const lint = (file) => {
  assert.ok(existsSync(path.join(client, "node_modules", ".bin")), "PosTerminal dependencies must be installed");
  return spawnSync(`npx ${lintScript.replace(/^oxlint /, "oxlint ").replace(/ src$/, ` "${file}"`)}`, { cwd: client, encoding: "utf8", shell: true });
};

test("a client folder with a package.json is picked up by any file under it", () => {
  const exists = (file) => file === "src/Clients/PosTerminal/package.json";
  assert.deepEqual(changedClients(["src/Clients/PosTerminal/src/routes/Cashier.tsx", "docs/a.md"], exists), ["src/Clients/PosTerminal"]);
});

test("a Windows style path is picked up too", () => {
  assert.deepEqual(changedClients(["src\\Clients\\PosTerminal\\src\\api.ts"], () => true), ["src/Clients/PosTerminal"]);
});

test("a folder without a package.json and a path outside src/Clients are not clients", () => {
  assert.deepEqual(changedClients(["src/Clients/Cashier/wwwroot/app.js"], () => false), []);
  assert.deepEqual(changedClients(["src/Host/Program.cs", "docs/a.md"], () => true), []);
});

test("the production lint script allows no warning at all", () => {
  assert.match(lintScript, /--max-warnings=0/);
});

test("the production lint step rejects a hook that reads state declared after it", () => {
  const broken = lint(fixture("useBeforeDeclare.tsx"));
  assert.equal(broken.status, 1, broken.stdout + broken.stderr);
  assert.match(broken.stdout + broken.stderr, /error react\(immutability\)/);
});

test("the same component with the state declared first passes the lint step", () => {
  const fixed = lint(fixture("declaredFirst.tsx"));
  assert.equal(fixed.status, 0, fixed.stdout + fixed.stderr);
});
