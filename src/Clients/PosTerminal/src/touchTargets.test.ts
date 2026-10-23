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
  ["./features/pending-checks/pending-checks.css", ".pending-checks__recall {"],
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
  return [...body.matchAll(/(?:min-)?(?:height|width):\s*([\d.]+)(px|rem)/g)]
    .map((match) => (match[2] === "rem" ? Number(match[1]) * 16 : Number(match[1])));
}

describe("touch targets", () => {
  it.each(targets)("%s %s is at least 48 px high and wide", (file, selector) => {
    const sizes = sizesOf(read(file), selector);
    expect(sizes.length).toBeGreaterThan(0);
    for (const size of sizes) expect(size).toBeGreaterThanOrEqual(MINIMUM_PX);
  });
});
