import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { describe, expect, it } from "vitest";

const here = dirname(fileURLToPath(import.meta.url));
const wwwroot = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot");
const MINIMUM_PX = 48;
const read = (relative) => readFileSync(resolve(wwwroot, relative), "utf8");

// Controls staff press with wet or greasy hands; each entry is the opening of a rule exactly as written in its file.
const targets = [
  ["payments/cash-session/cash-session.css", ".cs-chip {"],
  ["payments/cash-session/cash-session.css", ".cs-denom-count {"],
  ["payments/split-payment/split-payment.css", '.sp-confirm-row input[type="checkbox"] {'],
  ["payments/split-payment/split-payment.css", ".sp-fold > summary {"],
  ["payments/split-payment/split-payment.css", ".sp-btn {"],
  ["payments/split-payment/split-payment.css", ".sp-method-chip {"],
  ["cashier-app.css", ".modifier-option {"],
  ["cashier-app.css", "a.session-pill {"],
];

/** Heights and widths a rule declares, in px; a `var(--touch-target)` counts as the token's own value. */
function sizesOf(css, opening) {
  const token = Number(/--touch-target:\s*(\d+)px/.exec(css)?.[1] ?? 0);
  const sizes = [];
  // Every occurrence counts: a later rule or media query must not shrink the target again.
  for (let start = css.indexOf(opening); start >= 0; start = css.indexOf(opening, start + 1)) {
    const body = css.slice(start + opening.length, css.indexOf("}", start));
    for (const match of body.matchAll(/(?<![-\w])(?:min-)?(?:height|width):\s*(?:([\d.]+)px|var\(--touch-target\))/g)) {
      sizes.push(match[1] ? Number(match[1]) : token);
    }
  }
  return sizes;
}

describe("till pages touch targets", () => {
  it.each(targets)("%s %s is at least 48 px high and wide", (file, selector) => {
    const sizes = sizesOf(read(file), selector);
    expect(sizes.length).toBeGreaterThan(0);
    for (const size of sizes) expect(size).toBeGreaterThanOrEqual(MINIMUM_PX);
  });

  it("carries no style for the tab buttons the payment page no longer draws", () => {
    expect(read("payments/split-payment/split-payment.css")).not.toMatch(/\.sp-tabs?\b/);
    expect(read("payments/split-payment/split-payment.js")).not.toContain("sp-tab");
  });
});
