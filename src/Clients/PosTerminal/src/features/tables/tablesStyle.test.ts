import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const css = (name: string) => readFileSync(new URL(`./${name}`, import.meta.url), "utf8");

describe("table screens style rules", () => {
  it.each(["tables.css", "floorPlan.css"])("%s has no text smaller than 11px", (file) => {
    const sizes = [...css(file).matchAll(/font-size:\s*(?:clamp\()?(\.?\d+(?:\.\d+)?)rem/g)].map((match) => Number(match[1]) * 16);
    expect(sizes.filter((size) => size < 11)).toEqual([]);
  });

  it("never sets text in the light accent colour, which has about 1.85:1 contrast on white", () => {
    const lines = [...css("tables.css").split("\n"), ...css("floorPlan.css").split("\n")]
      .filter((line) => /(^|[\s;{])color:\s*var\(--ds-color-accent(-hover)?\)/.test(line));
    expect(lines).toEqual([]);
  });
});
