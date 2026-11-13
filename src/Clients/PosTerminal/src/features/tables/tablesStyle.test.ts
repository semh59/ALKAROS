import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const css = (name: string) => readFileSync(new URL(`./${name}`, import.meta.url), "utf8");
const rulesDoc = readFileSync(new URL("../../../../../../docs/CASHIER_UI_RULES.md", import.meta.url), "utf8");
const remPx = (value: string, unit: string) => Number(value) * (unit === "rem" ? 16 : 1);
const sizesIn = (source: string) => [...source.matchAll(/font(?:-size)?:\s*(?:[\d.]+\s+)?(?:clamp\()?(\.?\d+(?:\.\d+)?)(rem|px)/g)].map((match) => remPx(match[1], match[2]));
const blocks = (source: string) => [...source.replace(/\/\*[\s\S]*?\*\//g, "").matchAll(/([^{}]+)\{([^{}]*)\}/g)].map((match) => ({ selector: match[1].replace(/\s+/g, " ").trim(), body: match[2] }));

describe("table screens style rules", () => {
  it("has no live floor plan text smaller than 11px", () => {
    expect(sizesIn(css("floorPlan.css")).filter((size) => size < 11)).toEqual([]);
  });

  it("has no table plan text smaller than 14px, the smallest size docs/CASHIER_UI_RULES.md allows", () => {
    const sizes = sizesIn(css("tables.css"));
    expect(sizes.length).toBeGreaterThan(20);
    expect(sizes.filter((size) => size < 14)).toEqual([]);
  });

  it("keeps the table list and the side panel in two columns down to 768px wide, and stacks them only below that", () => {
    const rules = css("tables.css");
    expect(rules).toMatch(/@media \(max-width: 820px\) \{\s*\.table-workspace__content \{ grid-template-columns: minmax\(0, 1fr\) minmax\(15rem, 17rem\); \}/);
    expect(rules).toMatch(/@media \(max-width: 767px\) \{[^@]*\.table-workspace__content \{ grid-template-columns: 1fr; \}/);
  });

  it("never sets text in the light accent colour, which has about 1.85:1 contrast on white", () => {
    const lines = [...css("tables.css").split("\n"), ...css("floorPlan.css").split("\n")]
      .filter((line) => /(^|[\s;{])color:\s*var\(--ds-color-accent(-hover)?\)/.test(line));
    expect(lines).toEqual([]);
  });

  it("draws each status once, with the text and background tokens of its row in docs/CASHIER_UI_RULES.md", () => {
    const rows = rulesDoc.split("\n").filter((line) => /^\| `\w+` \|/.test(line)).map((line) => line.split("|").slice(1, -1).map((cell) => cell.trim().replaceAll("`", "")));
    expect(rows).toHaveLength(7);
    const all = blocks(css("tables.css"));
    for (const [key, , , text, background] of rows) {
      const rule = all.filter((block) => block.selector === `.table-status--${key}, .table-card--plan-${key}`);
      expect(rule, key).toHaveLength(1);
      expect(rule[0].body).toContain(`--plan-text: var(${text});`);
      expect(rule[0].body).toContain(`--plan-bg: var(${background});`);
    }
    expect(all.filter((block) => /--plan-(text|bg):/.test(block.body))).toHaveLength(rows.length);
    expect(all.filter((block) => /\.table-status\b/.test(block.selector) && /color:/.test(block.body) && !/--plan/.test(block.body)).map((block) => block.selector)).toEqual([]);
    expect(css("tables.css")).not.toMatch(/\.table-details__status(?!-)|#5b3a9e|#fff0eb|--table-pay/);
  });

  it("draws a pattern only on a table that needs attention: asking for the bill or open too long", () => {
    const patterned = blocks(css("tables.css")).filter((block) => /gradient|url\(|background-image/.test(block.body)).map((block) => block.selector);
    expect(patterned).toEqual([".table-card--plan-ask", ".table-card--stale"]);
  });
});
