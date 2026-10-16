import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { findButtonSites } from "./buttonSites";

/** Button sites without data-telemetry-id today; V1-RMD-567 brings it to zero. It may only go down. */
const UNMARKED_BASELINE = 304;
const ROOT = join(__dirname, "..", "..");

function sourceFiles(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return sourceFiles(path);
    return path.endsWith(".tsx") && !path.endsWith(".test.tsx") ? [path] : [];
  });
}

const sites = sourceFiles(ROOT).flatMap((file) => findButtonSites(file, readFileSync(file, "utf8")));

describe("button telemetry keys (static scan)", () => {
  it("finds the button sites and reports those without a data-telemetry-id", () => {
    expect(sites.length).toBeGreaterThan(250);
  });

  it("never lets the number of unmarked buttons grow", () => {
    expect(sites.filter((site) => !site.marked).length).toBeLessThanOrEqual(UNMARKED_BASELINE);
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
