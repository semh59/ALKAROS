import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "split-payment.js");
const css = readFileSync(resolve(pageDir, "split-payment.css"), "utf8");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const SESSION_ID = "66666666-6666-6666-6666-666666666666";
const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 30, remainingAmount: 70, allocations: [], unsettledPayment: null };
const locked = { ...unpaid, unsettledPayment: { paymentId: "p1", status: "RequiresReconciliation", reason: "x", pendingConfirmation: null } };

const $ = (id) => document.getElementById(id);
const fold = (name) => $(`fold-${name}`);

async function open(summary = unpaid) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: summary },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: SESSION_ID } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/show-on-display"), status: 204 },
  ]);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(document.querySelector(".sp-summary-row")).not.toBeNull());
}

const declaredFontSize = (selector) => {
  const rule = css.split("}").find((block) => block.split("{")[0].trim() === selector);
  return Number(/font-size:\s*(\d+)px/.exec(rule ?? "")?.[1] ?? 0);
};

describe("split-payment.js page hierarchy", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("lists the summary as Hesap toplamı, Ödenen, Kalan", async () => {
    await open();

    const labels = [...document.querySelectorAll(".sp-summary-row > span:first-child")].map((span) => span.textContent);
    expect(labels).toEqual(["Hesap toplamı", "Ödenen", "Kalan"]);
  });

  it("draws Kalan larger than the bill total", () => {
    expect(declaredFontSize(".sp-summary-row.is-remaining .value")).toBeGreaterThan(declaredFontSize(".sp-summary-row.is-total .value"));
    expect(declaredFontSize(".sp-summary-row.is-remaining .value")).toBeGreaterThanOrEqual(28);
  });

  it("puts the primary Ödemeyi Ekle button before the discount, tip and refund sections in the page order", async () => {
    await open();

    const follows = (node) => Boolean($("submit-tender").compareDocumentPosition(node) & Node.DOCUMENT_POSITION_FOLLOWING);
    expect(follows(fold("discount-tip"))).toBe(true);
    expect(follows(fold("refund"))).toBe(true);
    expect(follows($("discount-value"))).toBe(true);
  });

  it("keeps the submit button in reach while the form is scrolled, without covering the fields above it", () => {
    expect(css).toMatch(/#submit-tender\s*\{[^}]*position:\s*sticky;[^}]*bottom:\s*\d+px/);
    expect(css).toMatch(/scroll-padding-bottom:\s*\d+px/);
  });

  it("starts with the discount, tip and refund sections folded", async () => {
    await open();

    expect(fold("discount-tip").tagName).toBe("DETAILS");
    expect(fold("discount-tip").open).toBe(false);
    expect(fold("refund").open).toBe(false);
    expect(fold("discount-tip").querySelector("summary").textContent).toBe("İndirim ve bahşiş");
    expect(fold("refund").querySelector("summary").textContent).toBe("İade ve iptal");
  });

  it("keeps a section the cashier opened open through the redraws of the polling and of choosing a method", async () => {
    await open();
    $("fold-discount-tip-summary").click();

    document.querySelector('.sp-method-chip[data-method="Eft"]').click();

    expect(fold("discount-tip").open).toBe(true);
    expect(fold("refund").open).toBe(false);
  });

  it("opens the section by itself when it has an error to show, so the cashier is never left looking at a closed fold", async () => {
    await open();

    $("submit-discount").click();

    expect(fold("discount-tip").open).toBe(true);
    expect(document.body.textContent).toContain("Tutar sıfırdan büyük olmalı.");
  });

  it("lets the cashier close a section that opened by itself, and keeps it closed through the next redraw", async () => {
    await open();
    $("submit-discount").click();
    expect(fold("discount-tip").open).toBe(true);

    $("fold-discount-tip-summary").click();
    document.querySelector('.sp-method-chip[data-method="Eft"]').click();

    expect(fold("discount-tip").open).toBe(false);
  });

  it("draws the applied tenders and corrections below the payment form, so the form stays under the summary", async () => {
    await open({ ...unpaid, allocations: [{ amount: 30 }] });

    const lines = [...document.querySelectorAll(".sp-field-label")].find((label) => label.textContent === "Uygulanan tahsilatlar");
    expect(Boolean($("submit-tender").compareDocumentPosition(lines) & Node.DOCUMENT_POSITION_FOLLOWING)).toBe(true);
  });

  it("folds the equal-split helper above the method chips, so the form under the summary stays short", async () => {
    await open();

    expect(fold("split").open).toBe(false);
    expect(fold("split").querySelector("summary").textContent).toBe("Eşit bölüştür");
    const chips = document.querySelector(".sp-method-chip");
    expect(Boolean(fold("split").compareDocumentPosition(chips) & Node.DOCUMENT_POSITION_FOLLOWING)).toBe(true);
  });

  it("keeps the manual reconciliation warning first, above the summary, when the bill is locked", async () => {
    await open(locked);

    const warning = document.querySelector(".sp-alert-warning");
    expect(warning.textContent).toContain("Manuel mutabakat gerekiyor");
    expect(Boolean(warning.compareDocumentPosition(document.querySelector(".sp-summary-row")) & Node.DOCUMENT_POSITION_FOLLOWING)).toBe(true);
    expect($("submit-tender")).toBeNull();
  });
});
