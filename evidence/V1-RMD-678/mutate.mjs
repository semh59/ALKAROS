import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const file = path.join(root, "src/Clients/WaiterPwa/wwwroot/js/offline-queue.js");
const mutants = [
  { id: "O1", from: "sortQueueByPriority(ownQueue())", to: "sortQueueByPriority(state.offlineQueue)" },
  { id: "O2", from: "payload.ownerUserId === userId;", to: "true;" },
  { id: "O3", from: "return !payload.ownerUserId || !userId ||", to: "return !userId ||" },
  { id: "O4", from: "|| !userId || payload.ownerUserId", to: "|| payload.ownerUserId" },
];
const log = [];
for (const m of mutants) {
  const orig = fs.readFileSync(file, "utf8");
  const crlf = orig.includes("\r\n");
  const normalized = crlf ? orig.replace(/\r\n/g, "\n") : orig;
  if (!normalized.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log.at(-1)); continue; }
  const mutated = normalized.replace(m.from, () => m.to);
  fs.writeFileSync(file, crlf ? mutated.replace(/\n/g, "\r\n") : mutated);
  let out;
  try {
    out = execSync("npx vitest run offline-queue.test.js", { cwd: path.join(root, "tests/Clients/StaticApps"), encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 300000 });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(file, orig);
  log.push(`${m.id} ${/failed|FAILED/.test(out) ? "KILLED" : "SURVIVED"} restored=${fs.readFileSync(file, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.writeFileSync(path.join(root, "evidence/V1-RMD-678/mutations.log"), log.join("\n") + "\n");
