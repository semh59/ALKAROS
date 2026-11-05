import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const css = readFileSync(new URL("./styles.css", import.meta.url), "utf8");

function luminance(hex: string): number {
  const channels = [1, 3, 5].map((index) => parseInt(hex.slice(index, index + 2), 16) / 255)
    .map((value) => (value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4));
  return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
}
const contrast = (a: string, b: string) => {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (light + 0.05) / (dark + 0.05);
};
const colourOf = (opening: string) => {
  const start = css.indexOf(opening);
  expect(start, `${opening} not found`).toBeGreaterThanOrEqual(0);
  return /color:\s*(#[0-9a-fA-F]{6})/.exec(css.slice(start, css.indexOf("}", start)))?.[1] ?? "";
};

describe("small text in the till stylesheet", () => {
  it("is never smaller than 12 px", () => {
    const small = [...css.matchAll(/font-size:\s*(\d*\.?\d+)(rem|px)/g)]
      .filter((match) => Number(match[1]) * (match[2] === "rem" ? 16 : 1) < 12)
      .map((match) => match[0]);
    expect(small).toEqual([]);
  });

  // Each note is drawn on the background listed beside it.
  it.each([
    [".product-card-footer small {", "#ffffff"],
    [".product-price small {", "#ffffff"],
    [".product-category {", "#ffffff"],
    [".category-rail small {", "#163f65"],
    [".category-rail small {", "#1b4d7b"],
    [".operator-card small {", "#0b2135"],
  ])("%s reads at least 4.5:1 on %s", (opening, background) => {
    expect(contrast(colourOf(opening), background)).toBeGreaterThanOrEqual(4.5);
  });
});
