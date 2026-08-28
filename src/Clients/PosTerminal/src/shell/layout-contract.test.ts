import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const shellCss = readFileSync(new URL("./shell.css", import.meta.url), "utf8");
const primitiveCss = readFileSync(new URL("../design-system/primitives.css", import.meta.url), "utf8");
const tokenCss = readFileSync(new URL("../design-system/tokens.css", import.meta.url), "utf8");

describe("responsive and interaction style contract", () => {
  it("defines the approved breakpoint boundaries and prevents page overflow", () => {
    expect(shellCss).toContain("min-width: 768px");
    expect(shellCss).toContain("max-width: 1279px");
    expect(shellCss).toContain("max-width: 767px");
    expect(shellCss).toContain("overflow-x: clip");
    expect(primitiveCss).toContain("width: 100%");
  });

  it("uses a shared 44px minimum target for interactive primitives", () => {
    expect(tokenCss).toContain("--ds-target-min: 44px");
    expect(shellCss).toContain("min-height: var(--ds-target-min)");
    expect(primitiveCss).toContain("min-height: var(--ds-target-min)");
  });

  it("keeps visible keyboard focus and disables non-essential motion", () => {
    expect(shellCss).toContain(":focus-visible");
    expect(primitiveCss).toContain(":focus-visible");
    expect(shellCss).toContain("prefers-reduced-motion: reduce");
    expect(primitiveCss).toContain("prefers-reduced-motion: reduce");
  });

  it("reserves the ultra-narrow header for critical actions", () => {
    expect(shellCss).toContain("@media (max-width: 360px)");
    expect(shellCss).toContain(".production-shell__identity { display: none; }");
    expect(shellCss).toContain("@media (max-width: 319px)");
    expect(shellCss).toContain(".production-shell__brand { display: none; }");
    expect(shellCss).toContain(".production-shell__header-actions { width: 100%; margin-left: 0; justify-content: space-between; }");
  });

  it("returns fixed shell regions to document flow in shallow zoom viewports", () => {
    expect(shellCss).toContain("@media (max-width: 767px) and (max-height: 360px)");
    expect(shellCss).toContain(".production-shell__header,\n  .production-shell__nav,\n  .production-shell__status { position: static; }");
    expect(shellCss).toContain(".production-shell__nav-link { min-height: var(--ds-target-min); }");
  });
});
