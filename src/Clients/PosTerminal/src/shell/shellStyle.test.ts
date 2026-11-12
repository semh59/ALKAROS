import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const css = readFileSync(new URL("./shell.css", import.meta.url), "utf8");
const start = css.indexOf("@media (min-width: 768px) and (max-width: 1279px)");
const compact = css.slice(start, css.indexOf("@media (max-width: 767px)", start));
const rule = (selector: string) => compact.match(new RegExp(String.raw`${selector.replaceAll(".", String.raw`\.`)}\s*\{([^}]*)\}`))?.[1] ?? "";

describe("the compact side rail (768 to 1279 px)", () => {
  it("keeps the menu labels visible under the icons", () => {
    const label = rule(".production-shell__nav-label");

    expect(label).not.toMatch(/clip|width:\s*1px|height:\s*1px/);
    expect(label).toMatch(/font-size:\s*\.?\d+(\.\d+)?rem/);
  });

  it("stacks the icon over the label and keeps labels at least 11px", () => {
    expect(rule(".production-shell__nav-link")).toMatch(/flex-direction:\s*column/);
    const size = Number(rule(".production-shell__nav-label").match(/font-size:\s*(\.?\d+(?:\.\d+)?)rem/)?.[1]);
    expect(size * 16).toBeGreaterThanOrEqual(11);
  });
});
