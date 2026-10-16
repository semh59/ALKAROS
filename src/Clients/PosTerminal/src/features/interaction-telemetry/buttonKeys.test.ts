import { readdirSync, readFileSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";
import { findButtonSites } from "./buttonSites";
import { isCatalogKey } from "./interactionKeys";

const ROOT = join(__dirname, "..", "..");

function sourceFiles(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return entry.name === "design-system" ? [] : sourceFiles(path);
    return path.endsWith(".tsx") && !path.endsWith(".test.tsx") ? [path] : [];
  });
}

const files = sourceFiles(ROOT).map((file) => ({ file, source: readFileSync(file, "utf8") }));
const sites = files.flatMap(({ file, source }) => findButtonSites(relative(ROOT, file), source));

describe("button telemetry keys (static scan)", () => {
  it("finds the button sites of the application screens", () => {
    expect(sites.length).toBeGreaterThan(250);
  });

  it("gives every button site a data-telemetry-id", () => {
    const missing = sites.filter((site) => !site.marked).map((site) => `${site.file}:${site.line}`);

    expect(missing).toEqual([]);
  });

  it("uses only catalog-shaped keys and never the same key twice", () => {
    const keys = files.flatMap(({ source }) => [...source.matchAll(/data-telemetry-id="([^"]+)"/g)].map((match) => match[1]!));

    expect(keys.filter((key) => !isCatalogKey(key))).toEqual([]);
    expect(keys.length - new Set(keys).size).toBe(0);
  });

  it.each([
    ['<Button data-telemetry-id="a.b" onClick={() => go()}>x</Button>', true],
    ["<Button onClick={() => go('data-telemetry-id=')}>x</Button>", false],
    ['<button\n  type="button"\n  data-telemetry-id="a.b"\n>x</button>', true],
    ["<Button disabled={a > b}>x</Button>", false],
  ])("reads the opening tag of %s", (source, marked) => {
    const found = findButtonSites("f.tsx", source);

    expect(found).toHaveLength(1);
    expect(found[0]!.marked).toBe(marked);
  });
});
