import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const css = readFileSync(new URL("./kitchen-operations.css", import.meta.url), "utf8").replace(/\/\*[\s\S]*?\*\//g, "");
const MINIMUM_PX = 14;

const blocks = [...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)].map((match) => ({ selector: match[1].replace(/\s+/g, " ").trim(), body: match[2] }));
const sizesOf = (body: string) => [...body.matchAll(/font-size:\s*(?:clamp\()?(\.?\d+(?:\.\d+)?)(rem|px)/g)]
  .map((match) => Number(match[1]) * (match[2] === "rem" ? 16 : 1));

describe("kitchen board text size (docs/CASHIER_UI_RULES.md 2)", () => {
  it("sets no text below 14px", () => {
    expect(blocks.length).toBeGreaterThan(40);
    const small = blocks.flatMap((block) => sizesOf(block.body).filter((size) => size < MINIMUM_PX).map((size) => `${block.selector} = ${size}px`));
    expect(small).toEqual([]);
  });

  it("writes every font size in rem or px, the units this test can read", () => {
    const declared = [...css.matchAll(/font-size:\s*([^;}]+)/g)].map((match) => match[1].trim());
    expect(declared.filter((value) => !/^(clamp\()?\.?\d+(\.\d+)?(rem|px)(,|$)/.test(value))).toEqual([]);
    expect([...css.matchAll(/(?<![-\w])font:\s*([^;}]+)/g)].map((match) => match[1].trim()).filter((value) => value !== "inherit")).toEqual([]);
  });

  it("lets the four step buttons share a row by content width without wrapping their text", () => {
    const step = blocks.find((block) => block.selector === ".kitchen-step")!.body;
    expect(step).toMatch(/flex:\s*1 1 auto/);
    expect(step).toMatch(/white-space:\s*nowrap/);
    expect(step).toMatch(/min-height:\s*48px/);
    expect(blocks.find((block) => block.selector === ".kitchen-stepper")!.body).toContain("flex-wrap: wrap");
  });
});
