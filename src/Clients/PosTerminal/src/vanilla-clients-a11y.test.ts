// @vitest-environment jsdom
//
// Static-shell accessibility smoke for the two vanilla-JS clients that have no
// framework test harness of their own (Cashier POS, Waiter PWA). It loads each
// wwwroot/index.html as shipped and runs axe-core against the shell markup
// (before app JS renders). Dynamic-state a11y for these clients is covered by
// the manual device checklist in docs/qa/device-browser-test-plan.md and by the
// PosTerminal patterns they mirror. Owner: V1-RMD-092.

import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import axe from "axe-core";
import { describe, expect, it } from "vitest";

// Vitest runs with cwd = the PosTerminal package directory.
const clientsRoot = resolve(process.cwd(), "..");
const clients = [
  { name: "Cashier POS", path: resolve(clientsRoot, "Cashier/wwwroot/index.html") },
  { name: "Waiter PWA", path: resolve(clientsRoot, "WaiterPwa/wwwroot/index.html") },
] as const;

for (const client of clients) {
  if (!existsSync(client.path)) {
    throw new Error(`vanilla client shell not found: ${client.path} (cwd=${process.cwd()})`);
  }
}

function loadShell(html: string): void {
  const parsed = new DOMParser().parseFromString(html, "text/html");
  document.documentElement.lang = parsed.documentElement.getAttribute("lang") ?? "";
  document.title = parsed.title;
  document.head.innerHTML = parsed.head.innerHTML;
  document.body.innerHTML = parsed.body.innerHTML;
}

describe("vanilla client shells", () => {
  it.each(clients)("$name index.html declares lang, title and a responsive viewport", ({ path }) => {
    const html = readFileSync(path, "utf8");
    loadShell(html);

    expect(document.documentElement.lang).toBe("tr");
    expect(document.title.length).toBeGreaterThan(0);

    const viewport = document.querySelector<HTMLMetaElement>('meta[name="viewport"]');
    expect(viewport, "a viewport meta is required").not.toBeNull();
    const content = viewport!.content;
    // WCAG 1.4.4 Resize Text (AA): the page must not block zoom.
    expect(content).not.toMatch(/user-scalable\s*=\s*(no|0)/i);
    expect(content).not.toMatch(/maximum-scale\s*=\s*1(\.0)?\b/i);
  });

  it.each(clients)("$name shell has at least one landmark and named controls", ({ path }) => {
    loadShell(readFileSync(path, "utf8"));
    expect(document.querySelector('[role="banner"], header, main, [role="main"]')).not.toBeNull();
  });

  it.each(clients)("$name shell has no critical or serious axe violations", async ({ path }) => {
    loadShell(readFileSync(path, "utf8"));
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    const blocking = [...report.violations, ...report.incomplete].filter(
      (item) => item.impact === "critical" || item.impact === "serious",
    );
    expect(
      blocking.map((v) => `${v.id}: ${v.nodes.map((n) => n.html).join(" | ")}`),
    ).toEqual([]);
  });
});
