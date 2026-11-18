import { readdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { categoryLabel } from "./models";

const serverRoots = ["../../../../../Host/Observability", "../../../../../Modules/Observability"];
const prefixes = ["api", "db", "printer", "security", "browser", "background", "backup", "integration", "kitchen", "payment", "resource", "system", "outbox", "monitoring", "perf"];
const categoryLiteral = new RegExp(`"((?:${prefixes.join("|")})\\.[a-z0-9_]+)"`, "g");

// Not error-event categories: system.unreachable goes to the out-of-process outage channel (Telegram) and never lands in
// the error list; monitoring.exception_reported is only the HttpContext.Items key that stops one failure being counted twice.
const notShownOnTheErrorScreen = new Set(["system.unreachable", "monitoring.exception_reported"]);

const serverCategories = (): string[] => {
  const found = new Set<string>();
  for (const root of serverRoots) {
    const directory = fileURLToPath(new URL(root, import.meta.url));
    for (const entry of readdirSync(directory, { recursive: true, withFileTypes: true })) {
      if (!entry.isFile() || !entry.name.endsWith(".cs") || /[\\/](obj|bin)([\\/]|$)/.test(entry.parentPath)) continue;
      const source = readFileSync(`${entry.parentPath}/${entry.name}`, "utf8").replace(/^\s*\/\/.*$/gm, "");
      for (const match of source.matchAll(categoryLiteral)) found.add(match[1]!);
    }
  }
  return [...found].sort();
};

describe("error screen category names", () => {
  const found = serverCategories();
  const categories = found.filter((category) => !notShownOnTheErrorScreen.has(category));

  it("finds the server categories at all", () => {
    expect(categories.length).toBeGreaterThan(40);
    expect(new Set(found.map((category) => category.split(".")[0]))).toEqual(new Set(prefixes));
  });

  it("every category the server can raise has a Turkish name", () => {
    expect(categories.filter((category) => categoryLabel(category) === "Bilinmiyor")).toEqual([]);
  });
});
