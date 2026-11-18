import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const MINIMUM_PX = 48;
const read = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");

// Controls staff press with wet or greasy hands; each entry is the opening of a rule exactly as written in its file.
const targets: ReadonlyArray<readonly [file: string, selector: string]> = [
  ["./styles.css", ".header-action, .icon-action {"],
  ["./styles.css", ".experience-header-link, .experience-header-button {"],
  ["./styles.css", ".toast-message button {"],
  ["./styles.css", ".help-alert button {"],
  ["./styles.css", ".quantity-controls button {"],
  ["./styles.css", ".quantity-controls .remove {"],
  ["./styles.css", ".pairing-card summary {"],
  ["./styles.css", ".dialog-close {"],
  ["./styles.css", ".search-field input {"],
  ["./styles.css", ".item-note__row input {"],
  ["./styles.css", ".item-note button {"],
  ["./features/pending-checks/pending-checks.css", ".pending-checks__recall {"],
  ["./features/recent-sales/recent-sales.css", ".recent-sales__row {"],
  ["./features/recent-sales/recent-sales.css", ".recent-sales__refresh {"],
  ["./features/kitchen-operations/kitchen-operations.css", ".kitchen-workspace__feedback button {"],
  ["./features/kitchen-operations/kitchen-operations.css", ".kitchen-density button, .kitchen-view-mode button {"],
  ["./features/kitchen-operations/kitchen-operations.css", ".kitchen-flag-btn {"],
  ["./features/kitchen-operations/kitchen-operations.css", ".kitchen-undo-btn {"],
  ["./features/kitchen-operations/kitchen-operations.css", ".kitchen-unknown__actions .ds-button {"],
  ["./features/kitchen-operations/kitchen-operations.css", ".kitchen-decision-form__actions .ds-button {"],
];

function sizesOf(css: string, opening: string): number[] {
  const start = css.indexOf(opening);
  expect(start, `${opening} not found`).toBeGreaterThanOrEqual(0);
  const body = css.slice(start + opening.length, css.indexOf("}", start));
  return [...body.matchAll(/(?<![-\w])(?:min-)?(?:height|width):\s*([\d.]+)(px|rem)/g)]
    .map((match) => (match[2] === "rem" ? Number(match[1]) * 16 : Number(match[1])));
}

describe("touch targets", () => {
  it.each(targets)("%s %s is at least 48 px high and wide", (file, selector) => {
    const sizes = sizesOf(read(file), selector);
    expect(sizes.length).toBeGreaterThan(0);
    for (const size of sizes) expect(size).toBeGreaterThanOrEqual(MINIMUM_PX);
  });
});

describe("the notice box stays off the header", () => {
  const css = read("./styles.css");
  const narrowStart = css.indexOf("@media (max-width: 900px)");
  const wide = css.slice(0, narrowStart);
  const narrow = css.slice(narrowStart);
  const rule = (text: string, opening: string) => {
    const start = text.indexOf(opening);
    expect(start, `${opening} not found`).toBeGreaterThanOrEqual(0);
    return text.slice(start, text.indexOf("}", start));
  };
  const placement = (body: string) => ({
    bottom: body.match(/(?<![-\w])bottom:\s*(?:calc\()?([\d.]+)(px|rem)/),
    top: /(?<![-\w])(?:top|inset[-\w]*):/.test(body),
  });

  it("is anchored to the bottom on a wide screen", () => {
    const { bottom, top } = placement(rule(wide, ".toast-message {"));
    expect(bottom).not.toBeNull();
    expect(top).toBe(false);
  });

  it("sits above the 72 px ticket bar on a narrow screen", () => {
    expect(narrowStart).toBeGreaterThanOrEqual(0);
    const { bottom, top } = placement(rule(narrow, ".toast-message {"));
    expect(top).toBe(false);
    expect(Number(bottom![1]) * (bottom![2] === "rem" ? 16 : 1)).toBeGreaterThanOrEqual(80);
  });
});

describe("the served-recall note follows the cashier UI rules", () => {
  const css = read("./features/pending-checks/pending-checks.css");
  const opening = ".pending-checks__served-note {";
  const start = css.indexOf(opening);
  const body = start < 0 ? "" : css.slice(start + opening.length, css.indexOf("}", start));
  const pixels = (value: string) => Number.parseFloat(value) * (value.endsWith("rem") ? 16 : 1);

  it("has its own rule after the shared meta colour so it wins", () => {
    expect(start, `${opening} not found`).toBeGreaterThan(css.indexOf(".pending-checks__meta, .pending-checks__preview {"));
  });

  it("is at least 14 px", () => {
    const size = body.match(/(?<![-\w])font-size:\s*([\d.]+(?:px|rem))/);
    expect(size).not.toBeNull();
    expect(pixels(size![1])).toBeGreaterThanOrEqual(14);
  });

  it("keeps 16 px (--ds-space-4) from the card sides and bottom", () => {
    const margin = body.match(/(?<![-\w])margin:\s*([^;]+);/);
    expect(margin).not.toBeNull();
    const [, side, bottom] = margin![1].trim().split(/\s+/);
    expect(side).toBe("var(--ds-space-4)");
    expect(bottom).toBe("var(--ds-space-4)");
  });

  it("uses the muted colour token, not a hard-coded colour", () => {
    expect(body).toMatch(/(?<![-\w])color:\s*var\(--ds-color-muted\)/);
  });
});
