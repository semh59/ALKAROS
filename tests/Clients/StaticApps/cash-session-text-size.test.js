import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { describe, expect, it } from "vitest";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const read = (relative) => readFileSync(resolve(root, relative), "utf8");
const page = "src/Clients/Cashier/wwwroot/payments/cash-session";

describe("cash session page text size", () => {
  it("keeps the rule it follows: no text under 14px", () => {
    expect(read("docs/CASHIER_UI_RULES.md")).toContain("En küçük yazı 14px");
  });

  it("states every font size as a plain px or rem value of at least 14px", () => {
    const declarations = [...read(`${page}/cash-session.css`).matchAll(/\bfont(-size)?\s*:\s*([^;}]+)/gi)];
    const sizes = declarations.map(([whole, longhand, value]) => {
      const literal = /^(\d+(?:\.\d+)?)(px|rem)\b/i.exec(value.trim());
      return { whole: whole.trim(), px: longhand && literal ? Number(literal[1]) * (literal[2].toLowerCase() === "rem" ? 16 : 1) : NaN };
    });

    expect(sizes.length).toBeGreaterThan(10);
    expect(sizes.filter((size) => !(size.px >= 14)).map((size) => size.whole)).toEqual([]);
  });

  it("sets no text size outside the style sheet, where this test could not see it", () => {
    for (const file of ["cash-session.js", "index.html"])
      expect(read(`${page}/${file}`), file).not.toMatch(/font-size|font\s*:|<small|<sub|<sup|fontSize|cssText|zoom/i);
  });
});
