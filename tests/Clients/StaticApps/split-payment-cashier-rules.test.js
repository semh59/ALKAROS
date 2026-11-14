import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "..", "..", "..");
const pageDir = resolve(root, "src/Clients/Cashier/wwwroot/payments/split-payment");
const css = readFileSync(resolve(pageDir, "split-payment.css"), "utf8");
const rules = readFileSync(resolve(root, "docs/CASHIER_UI_RULES.md"), "utf8");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 30, remainingAmount: 70, allocations: [], unsettledPayment: null };

async function open() {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: unpaid },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "66666666-6666-6666-6666-666666666666" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/show-on-display"), status: 204 },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.querySelector(".sp-summary-row")).not.toBeNull());
}

const rule = (selector) => css.split("}").find((block) => block.split("{")[0].split(",").some((name) => name.trim() === selector)) ?? "";
const declared = (selector, property) => new RegExp(String.raw`(?:^|[;{\s])${property}:\s*([^;]+)`).exec(rule(selector))?.[1].trim();
const ruleDoc = (name) => new RegExp(String.raw`\| ${name} \|[^\n]*`).exec(rules)?.[0] ?? "";

describe("Hesap Ödeme page follows docs/CASHIER_UI_RULES.md", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("opens with one heading, Tahsilat, and no label stacked above it", async () => {
    await open();

    expect(document.querySelectorAll("h1").length).toBe(1);
    expect(document.querySelector("h1").textContent).toBe("Tahsilat");
    expect(document.querySelector(".sp-eyebrow")).toBeNull();
    expect(document.body.textContent).not.toContain("Hesap Ödeme");
  });

  it("draws the table name as a quiet context line, not as a second heading", () => {
    expect(Number.parseInt(declared(".sp-place", "font-size"), 10)).toBeLessThan(Number.parseInt(declared(".sp-title", "font-size"), 10));
    expect(declared(".sp-place", "color")).toBe("var(--color-text-muted)");
  });

  it("keeps the remaining amount neutral, and green only once nothing is left", () => {
    expect(declared(".sp-summary-row.is-remaining .value", "color")).toBe("var(--color-ink)");
    expect(declared(".sp-summary-row.is-remaining.is-zero .value", "color")).toBe("var(--color-success)");
    expect(css).not.toMatch(/\.is-remaining[^{]*\{[^}]*--color-danger/);
  });

  it("stacks no label above a title on the session-ended screen either", () => {
    expect(readFileSync(resolve(pageDir, "split-payment.js"), "utf8")).not.toContain("sp-eyebrow");
  });

  it("marks the Ödenen row as the one drawn like the bill total", async () => {
    await open();

    const rows = [...document.querySelectorAll(".sp-summary-row")].map((row) => [row.firstElementChild.textContent, row.classList.contains("is-total"), row.classList.contains("is-paid")]);
    expect(rows).toEqual([["Hesap toplamı", true, false], ["Ödenen", false, true], ["Kalan", false, false]]);
  });

  it("writes every banknote button in the same money format as the amounts", async () => {
    await open();
    await vi.waitFor(() => expect(document.querySelector("[data-quick-cash]")).not.toBeNull());

    expect([...document.querySelectorAll("[data-quick-cash]")].map((button) => button.textContent)).toEqual(["₺5,00", "₺10,00", "₺20,00", "₺50,00", "₺100,00", "₺200,00", "Tam tutar"]);
  });

  it("draws the small explanations under a choice at 14px as well", () => {
    expect(declared(".sp-check small", "font-size")).toBe("14px");
  });

  it("draws the paid amount as large as the bill total, and the remaining amount larger than both", () => {
    const paid = ".sp-summary-row.is-paid .value";
    expect(declared(".sp-summary-row.is-total .value", "font-size")).toBe("20px");
    expect(declared(paid, "font-size")).toBe("20px");
    expect(rule(paid)).toBe(rule(".sp-summary-row.is-total .value"));
    expect(Number.parseInt(declared(".sp-summary-row.is-remaining .value", "font-size"), 10)).toBeGreaterThan(20);
  });

  it("paints the primary button in the accent with ink text, as the rules table says", () => {
    expect(ruleDoc("Birincil")).toContain("`--ds-color-accent`");
    expect(declared(".sp-btn-primary", "background")).toBe("var(--color-accent)");
    expect(declared(".sp-btn-primary", "color")).toBe("var(--color-ink)");
  });

  it("sets no text below 14px outside the receipt print sheet", () => {
    const screen = css.replace(/@media print \{[\s\S]*?\n\}\n/, "");
    const sizes = [...screen.matchAll(/font-size:\s*([\d.]+)(px|rem)/g)].map((match) => Number(match[1]) * (match[2] === "rem" ? 16 : 1));
    expect(sizes.length).toBeGreaterThan(20);
    expect(sizes.filter((size) => size < 14)).toEqual([]);
  });

  it("styles no sp- class that neither this page nor the customer accounts page that shares the sheet ever uses", () => {
    const sharing = resolve(pageDir, "..", "customer-accounts", "customer-accounts.js");
    const used = [resolve(pageDir, "split-payment.js"), resolve(pageDir, "index.html"), sharing].map((file) => readFileSync(file, "utf8")).join(" ");
    const screen = css.replace(/@media print \{[\s\S]*?\n\}\n/, "");
    const classes = [...new Set([...screen.matchAll(/\.(sp-[a-z0-9-]+)/g)].map((match) => match[1]))];
    expect(classes.length).toBeGreaterThan(30);
    expect(classes.filter((name) => !new RegExp(`${name}(?![\\w-])`).test(used))).toEqual([]);
  });
});
