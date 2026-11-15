import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const css = readFileSync(new URL("./kitchen-operations.css", import.meta.url), "utf8").replace(/\/\*[\s\S]*?\*\//g, "");
const bodyOf = (selector: string) => [...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)]
  .find((match) => match[1].replace(/\s+/g, " ").trim() === selector)?.[2] ?? "";

describe("kitchen table card height", () => {
  it("caps the station area so a table with many tickets scrolls inside its card", () => {
    const stations = bodyOf(".kitchen-order__stations");
    expect(stations).toMatch(/max-height:\s*[^;]*(rem|vh)/);
    expect(stations).toMatch(/overflow-y:\s*auto/);
  });

  it("keeps the card header (table, timer, warning button) outside the scrolling area", () => {
    expect(bodyOf(".kitchen-order")).toContain("border-left");
    expect(bodyOf(".kitchen-order")).not.toMatch(/max-height/);
    expect(bodyOf(".kitchen-order__head")).toContain("display: flex");
    expect(bodyOf(".kitchen-order__head")).not.toMatch(/overflow/);
  });
});
