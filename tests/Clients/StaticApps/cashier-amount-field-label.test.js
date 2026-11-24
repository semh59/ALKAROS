import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { describe, expect, it } from "vitest";

const here = dirname(fileURLToPath(import.meta.url));
const payments = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments");
const read = (relative) => readFileSync(resolve(payments, relative), "utf8");

describe("till amount boxes", () => {
  it.each([
    ["split-payment/split-payment.js", "sp-amount-field", 2],
    ["cash-session/cash-session.js", "cs-amount-field", 3],
  ])("%s draws every %s as a label around its input, so a tap anywhere in the box focuses the input", (file, box, count) => {
    const source = read(file);
    expect(source.split(`<label class="${box}"><input `).length - 1).toBe(count);
    expect(source).not.toContain(`<div class="${box}"`);
  });

  it.each([
    ["split-payment/split-payment.js", "sp-amount-suffix", 2],
    ["cash-session/cash-session.js", "cs-amount-suffix", 3],
  ])("%s hides the currency sign %s from the accessible name of the box", (file, suffix, count) => {
    expect(read(file).split(`<span class="${suffix}" aria-hidden="true">₺</span>`).length - 1).toBe(count);
  });

  it.each([
    ["split-payment/split-payment.js", "amount-draft", "Ödeme tutarı"],
    ["split-payment/split-payment.js", "tendered-draft", "Alınan para (isteğe bağlı)"],
  ])("keeps the accessible name of %s", (file, id, name) => {
    expect(read(file)).toContain(`id="${id}" aria-label="${name}"`);
  });

  it("draws the payment amount box's edge inside the box, so the input fills all 48 px and measures the same as the box", () => {
    const css = read("split-payment/split-payment.css");
    expect(css).toMatch(/\.sp-amount-field \{[^}]*border:\s*0[;\s][^}]*box-shadow:\s*inset 0 0 0 1\.5px var\(--color-brand\)/);
  });

  it.each([
    ["split-payment/split-payment.css", ".sp-amount-field"],
    ["cash-session/cash-session.css", ".cs-amount-field"],
  ])("%s lets the input fill the box height, and its padding moves from the box to the input", (file, box) => {
    const css = read(file);
    const rule = box.replace(".", String.raw`\.`);
    expect(css).toMatch(new RegExp(String.raw`${rule} \{[^}]*padding:\s*0 16px`));
    expect(css).toMatch(new RegExp(String.raw`${rule} input \{[^}]*align-self:\s*stretch`));
  });
});
