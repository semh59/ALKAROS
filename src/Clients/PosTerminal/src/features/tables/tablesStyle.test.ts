// @vitest-environment jsdom
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const here = dirname(fileURLToPath(import.meta.url));
const css = (name: string) => readFileSync(resolve(here, name), "utf8");
const rulesDoc = readFileSync(resolve(here, "../../../../../../docs/CASHIER_UI_RULES.md"), "utf8");
const remPx = (value: string, unit: string) => Number(value) * (unit === "rem" ? 16 : 1);
const sizesIn = (source: string) => [...source.matchAll(/font(?:-size)?:\s*(?:[\w.-]+\s+)*?(?:clamp\()?(\.?\d+(?:\.\d+)?)(rem|px)/g)].map((match) => remPx(match[1], match[2]));
const blocks = (source: string) => [...source.replace(/\/\*[\s\S]*?\*\//g, "").matchAll(/([^{}]+)\{([^{}]*)\}/g)].map((match) => ({ selector: match[1].replace(/\s+/g, " ").trim(), body: match[2] }));

describe("table screens style rules", () => {
  // Text lines inside a table node on the canvas: the room is the node's own size on the drawn plan (about 58px high by default,
  // for a number, a state line and a badge), so 14px lines would overflow it. The seat number sits in a fixed rem circle and has room. Everything around the canvas has room and follows the 14px minimum of docs/CASHIER_UI_RULES.md 2.
  const insideTableNode = new Set([
    ".floor-plan-table__state, .floor-plan-table__context, .floor-plan-table__elapsed",
    ".floor-plan-table__badge",
  ]);

  it("has no floor plan text smaller than 14px outside the table nodes on the canvas, and none smaller than 11px inside them", () => {
    const all = blocks(css("floorPlan.css"));
    const exempt = all.filter((block) => insideTableNode.has(block.selector));
    expect(exempt).toHaveLength(insideTableNode.size);
    const below = (list: typeof all, limit: number) => list.flatMap((block) => sizesIn(block.body).filter((size) => size < limit).map((size) => `${block.selector} = ${size}px`));
    expect(below(all.filter((block) => !insideTableNode.has(block.selector)), 14)).toEqual([]);
    expect(below(exempt, 11)).toEqual([]);
    // An exception that grows to 14px is no exception: it must leave the list instead.
    expect(exempt.flatMap((block) => sizesIn(block.body)).filter((size) => size >= 14)).toEqual([]);
  });

  it("writes every floor plan font size in rem or px, the units this file's checks can read", () => {
    const source = css("floorPlan.css").replace(/\/\*[\s\S]*?\*\//g, "");
    const declared = [...source.matchAll(/font-size:\s*([^;}]+)/g)].map((match) => match[1].trim());
    expect(declared.filter((value) => !/^(clamp\()?\.?\d+(\.\d+)?(rem|px)(,|$)/.test(value))).toEqual([]);
    const shorthand = [...source.matchAll(/(?<![-\w])font:\s*([^;}]+)/g)].map((match) => match[1].trim()).filter((value) => value !== "inherit");
    expect(shorthand.filter((value) => !/^(?:[\w.-]+\s+)*(clamp\()?\.?\d+(\.\d+)?(rem|px)(,|\/|\s|$)/.test(value))).toEqual([]);
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

  it("lets no rule except the status rules set the text colour of the side panel status badge, with or without the just-vacated hint after it", () => {
    const sheets = [...blocks(css("tables.css")), ...blocks(css("../../styles.css"))]
      .filter((block) => /(?:^|[;\s])color:/.test(block.body) && !block.selector.startsWith("@") && !block.selector.includes(":root"));
    expect(sheets.length).toBeGreaterThan(20);
    for (const hint of ["", '<span class="table-card__recently-vacated">Az önce boşaldı</span>']) {
      document.body.innerHTML = `<section class="table-workspace"><div class="table-workspace__content"><div class="table-details"><div class="table-details__header"><div><h2>M-01</h2><span>Masa bilgisi</span></div><div class="table-details__status-group"><span class="table-status table-status--pay">Ödeme bekliyor</span>${hint}</div></div></div></div></section>`;
      const badge = document.querySelector(".table-status")!;
      const offenders = sheets.filter((block) => block.selector.split(",").some((part) => part.trim() !== ".table-status" && badge.matches(part.trim())));
      expect(offenders.map((block) => block.selector)).toEqual([]);
    }
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
