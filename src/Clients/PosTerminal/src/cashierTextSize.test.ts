import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const read = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");
const MINIMUM_PX = 14;
// The cashier's screens: header and menu, sale screen, forms and the split workspace.
const screens = ["./styles.css", "./shell/shell.css", "./design-system/primitives.css", "./features/billing/billing.css"];
// The customer display is read from a distance and is not a cashier screen (docs/CASHIER_UI_RULES.md 1).
const customerDisplay = new Set([".display-order-number span", ".display-table-head", ".privacy-note"]);

const blocks = (source: string) => [...source.replace(/\/\*[\s\S]*?\*\//g, "").matchAll(/([^{}]+)\{([^{}]*)\}/g)]
  .map((match) => ({ selector: match[1].replace(/\s+/g, " ").trim(), body: match[2] }));
const sizesOf = (body: string) => [...body.matchAll(/font(?:-size)?:\s*(?:[\d.]+\s+)?(?:clamp\()?(\.?\d+(?:\.\d+)?)(rem|px)/g)]
  .map((match) => Number(match[1]) * (match[2] === "rem" ? 16 : 1));

describe("cashier text size (docs/CASHIER_UI_RULES.md 2)", () => {
  it.each(screens)("%s sets no text below 14px outside the customer display", (file) => {
    const all = blocks(read(file));
    expect(all.length).toBeGreaterThan(5);
    const small = all
      .filter((block) => !customerDisplay.has(block.selector))
      .flatMap((block) => sizesOf(block.body).filter((size) => size < MINIMUM_PX).map((size) => `${block.selector} = ${size}px`));
    expect(small).toEqual([]);
  });

  it.each(screens)("%s writes every font size in rem or px, the units this test can read", (file) => {
    const declared = [...read(file).replace(/\/\*[\s\S]*?\*\//g, "").matchAll(/font-size:\s*([^;}]+)/g)].map((match) => match[1].trim());
    expect(declared.filter((value) => !/^(clamp\()?\.?\d+(\.\d+)?(rem|px)(,|$)/.test(value))).toEqual([]);
    const shorthand = [...read(file).matchAll(/(?<![-\w])font:\s*([^;}]+)/g)].map((match) => match[1].trim()).filter((value) => value !== "inherit");
    expect(shorthand.filter((value) => !/^(?:[\w.-]+\s+)*(clamp\()?\.?\d+(\.\d+)?(rem|px)(,|\/|\s|$)/.test(value))).toEqual([]);
  });

  it("lists only customer display rules that exist", () => {
    const selectors = new Set(blocks(read("./styles.css")).map((block) => block.selector));
    for (const selector of customerDisplay) expect(selectors.has(selector), selector).toBe(true);
  });

  it("gives <small> an explicit size, so the browser default (a fraction of its parent) cannot take a note below 14px", () => {
    const rule = blocks(read("./styles.css")).find((block) => block.selector === "small");
    expect(rule, "a plain small rule").toBeDefined();
    const sizes = sizesOf(rule!.body);
    expect(sizes).toHaveLength(1);
    expect(sizes[0]).toBeGreaterThanOrEqual(MINIMUM_PX);
  });
});
