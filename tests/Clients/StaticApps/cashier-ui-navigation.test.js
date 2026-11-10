import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const wwwroot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "src/Clients/Cashier/wwwroot");
const read = (path) => readFileSync(resolve(wwwroot, path), "utf8");

function ruleBody(css, selector) {
  const start = css.indexOf(`\n${selector} {`);
  expect(start, `${selector} not found`).toBeGreaterThanOrEqual(0);
  return css.slice(start, css.indexOf("}", start));
}

describe("payment pages lead back to the till", () => {
  it.each([
    ["payments/split-payment/index.html", "payments/split-payment/split-payment.css", ".sp-back"],
    ["payments/cash-session/index.html", "payments/cash-session/cash-session.css", ".cs-back"],
  ])("%s has a persistent back link of at least 48 px", (page, stylesheet, selector) => {
    const html = read(page);
    const link = html.match(/<a class="([^"]+)" href="([^"]+)">([^<]+)<\/a>/);
    expect(link, "back link").not.toBeNull();
    expect(link[1]).toBe(selector.slice(1));
    expect(link[3]).toBe("← Kasaya dön");
    expect(html.indexOf(link[0])).toBeLessThan(html.indexOf('<main id="app"'));
    expect(ruleBody(read(stylesheet), selector)).toMatch(/min-height:\s*var\(--touch-target\)|min-height:\s*(4[89]|[5-9]\d)px/);
  });

  it("returns from the payment, cash session and customer account pages to the main till", () => {
    expect(read("payments/split-payment/index.html")).toContain('<a class="sp-back" href="/">');
    expect(read("payments/cash-session/index.html")).toContain('<a class="cs-back" href="/">');
    expect(read("payments/customer-accounts/customer-accounts.js")).toContain('<a class="ca-back" href="/">');
  });
});

describe("quick till touch targets", () => {
  it.each([
    ["cashier-app.css", ".item-note-input"],
    ["cashier-app.css", ".dispatch-waiter-select"],
    ["payments/cash-session/cash-session.css", ".cs-btn-ghost"],
  ])("%s %s is at least 48 px high", (file, selector) => {
    const css = read(file);
    const sizes = [...ruleBody(css, selector).matchAll(/(?:min-)?height:\s*(var\(--touch-target\)|[\d.]+px)/g)]
      .map((match) => (match[1].startsWith("var") ? 48 : Number(match[1].slice(0, -2))));
    expect(sizes.length).toBeGreaterThan(0);
    for (const size of sizes) expect(size).toBeGreaterThanOrEqual(48);
  });
});
