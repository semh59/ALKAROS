// @vitest-environment jsdom

import { afterEach, describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { URL as NodeUrl } from "node:url";

const css = readFileSync(new NodeUrl("./kitchen-operations.css", import.meta.url), "utf8").replace(/\/\*[\s\S]*?\*\//g, "");
const hidingSelectors = [...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)]
  .filter((match) => /display:\s*none|visibility:\s*hidden|opacity:\s*0(?![.\d])|font-size:\s*0(?![.\d])|max-height:\s*0(?![.\d])/.test(match[2]))
  .flatMap((match) => match[1].split(",").map((selector) => selector.trim()));

function hiddenIn(mode: "" | "is-dense") {
  document.body.innerHTML = `<section class="kitchen-workspace ${mode}"><div class="kitchen-item-row"><div class="kitchen-item-row__body">
    <span class="kitchen-item-row__detail" id="modifiers">Ekstra peynir</span>
    <span class="kitchen-item-row__detail kitchen-item-row__detail--note" id="note">Not: soğansız</span>
  </div></div></section>`;
  const hidden = (id: string) => hidingSelectors.some((selector) => document.getElementById(id)!.matches(selector));
  return { modifiers: hidden("modifiers"), note: hidden("note") };
}

afterEach(() => {
  document.body.innerHTML = "";
});

describe("kitchen line note in every density mode", () => {
  it("reads the hiding rules of the stylesheet", () => {
    expect(hidingSelectors.length).toBeGreaterThan(0);
  });

  it("shows the cook's note in the dense mode the board enters on its own", () => {
    expect(hiddenIn("is-dense").note).toBe(false);
  });

  it("shows the note in the calm mode too", () => {
    expect(hiddenIn("").note).toBe(false);
  });

  it("keeps hiding the option detail in the dense mode only", () => {
    expect(hiddenIn("is-dense").modifiers).toBe(true);
    expect(hiddenIn("").modifiers).toBe(false);
  });
});

describe("the board's line markup", () => {
  it("gives the note span both classes the stylesheet's rules are written for", () => {
    const source = readFileSync(new NodeUrl("./KitchenOperationsWorkspace.tsx", import.meta.url), "utf8");
    expect(source).toContain('className="kitchen-item-row__detail kitchen-item-row__detail--note"');
  });
});
