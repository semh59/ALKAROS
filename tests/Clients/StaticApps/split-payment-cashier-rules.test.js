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

const PAYMENT_ID = "33333333-3333-3333-3333-333333333333";
const TEA = "aaaaaaaa-0000-0000-0000-000000000001";

/** A paid bill with one refundable line: the screen then offers İade iste and Ürün bazlı iade. */
async function openPaid({ requests = [], capabilities = [] } = {}) {
  const base = `/api/v1/terminals/${TERMINAL_ID}/billing`;
  const paid = { billId: BILL_ID, payableAmount: 150, allocatedTotal: 150, remainingAmount: 0, unsettledPayment: null, allocations: [{ allocationId: "a1", paymentId: PAYMENT_ID, amount: 150, paymentStatus: "Approved" }] };
  const receipt = {
    billId: BILL_ID, billNumber: "B-1", status: "Open", currencyCode: "TRY", openedAt: "2026-10-14T10:00:00Z", closedAt: null,
    items: [{ billItemId: TEA, name: "Çay", quantity: 3, unitPrice: 50, amount: 150, refundedQuantity: 0 }], adjustments: [], subtotal: 150, payableAmount: 150, taxTotal: 0, payments: [],
  };
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1", capabilities } },
    { test: (url) => url.endsWith("/tenders/"), body: paid },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith(`${base}/refund-allowance`), body: { hasOpenSession: false, allowance: 0, used: 0, remaining: 0 } },
    { test: (url) => url.endsWith(`/bills/${BILL_ID}/receipt`), body: receipt },
    { test: (url) => url.endsWith(`${base}/refund-requests`), body: requests },
    { test: (url) => url.endsWith("/show-on-display"), status: 204 },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.querySelector("h1")).not.toBeNull());
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

  it("draws the destructive actions with the danger tokens on the surface, never filled and never like a neutral button", () => {
    expect(declared(".sp-btn-destructive", "background")).toBe("var(--color-surface)");
    expect(declared(".sp-btn-destructive", "color")).toBe("var(--color-danger)");
    expect(declared(".sp-btn-destructive", "border")).toContain("var(--color-danger)");
    expect(declared(".sp-btn-destructive:disabled", "background")).toBe("var(--color-disabled)");
    expect(declared(".sp-btn-destructive:disabled", "color")).toBe("var(--color-text-muted)");
  });

  it("draws Hesabı iptal et as a destructive button", async () => {
    await open();

    const button = document.querySelector('[data-refund-action="cancel"]');
    expect(button.classList.contains("sp-btn-destructive")).toBe(true);
    expect(button.classList.contains("sp-btn-secondary")).toBe(false);
  });

  it("draws Onaylı iadeyi iptal et as a destructive button", async () => {
    const waiting = { requestId: "44444444-4444-4444-4444-444444444444", billId: BILL_ID, paymentId: PAYMENT_ID, amount: 150, method: "Cash", reason: "İkram", status: "AwaitingManualRefund", requestedBy: "u2", requestedAt: "2026-10-14T10:00:00Z", decidedBy: "u3", decidedAt: null, slipNumber: null, billCancelled: false, approvedFromAllowance: false };
    await openPaid({ requests: [waiting], capabilities: ["payments.refund.approve"] });
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="cancel-approved"]')).not.toBeNull());

    expect(document.querySelector('[data-refund-action="cancel-approved"]').classList.contains("sp-btn-destructive")).toBe(true);
  });

  it.each(["request", "request-items"])("draws the refund button %s as a destructive button", async (action) => {
    await openPaid();
    await vi.waitFor(() => expect(document.querySelector(`[data-refund-action="${action}"]`)).not.toBeNull());

    const button = document.querySelector(`[data-refund-action="${action}"]`);
    expect(button.classList.contains("sp-btn-destructive")).toBe(true);
    expect(button.classList.contains("sp-btn-secondary")).toBe(false);
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

  it.each([".sp-method-chips", ".sp-row", ".sp-quick-cash", ".sp-customer-list"])("keeps at least 16px between the touch targets laid out by %s", (selector) => {
    const gaps = [...css.replace(/\/\*[\s\S]*?\*\//g, "").matchAll(/([^{}]+)\{([^{}]*)\}/g)]
      .filter(([, names]) => names.split(",").some((name) => name.trim() === selector))
      .flatMap(([, , body]) => [...body.matchAll(/(?:^|[;\s])(?:row-|column-)?gap:\s*([^;]+)/g)].flatMap((match) => match[1].trim().split(/\s+/)));
    expect(gaps.length).toBeGreaterThan(0);
    // Only px is read; any other unit fails here instead of passing as a number.
    expect(gaps.filter((gap) => !/^\d+px$/.test(gap) || Number.parseInt(gap, 10) < 16)).toEqual([]);
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
