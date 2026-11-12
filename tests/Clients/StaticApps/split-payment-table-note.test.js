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
const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };
const settled = { ...unpaid, allocatedTotal: 100, remainingAmount: 0 };
const receipt = (extra = {}) => ({
  billId: BILL_ID, billNumber: "H-1", status: "Paid", currencyCode: "TRY", openedAt: "2026-10-13T10:05:00Z", closedAt: null,
  items: [
    { name: "Köfte", quantity: 1, unitPrice: 80, amount: 80 },
    { name: "Ayran <b>", quantity: 1, unitPrice: 20, amount: 20, addedWithoutKitchenReason: "waiter-forgot" },
  ],
  adjustments: [], subtotal: 100, payableAmount: 100, taxTotal: 0, payments: [], ...extra,
});

async function open(summary, receiptBody) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: summary },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "66666666-6666-6666-6666-666666666666" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/show-on-display"), status: 204 },
    { test: (url) => url.endsWith("/receipt"), body: receiptBody },
    { test: (url) => url.endsWith("/receipt/print"), body: receiptBody },
  ]);
  await loadApp(htmlPath, scriptPath);
}

describe("split-payment.js table name and kitchen-less note", () => {
  beforeEach(() => {
    vi.resetModules();
    window.print = vi.fn();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
    delete window.print;
  });

  it("names the table and the lines added at the till on the payment screen", async () => {
    await open(unpaid, receipt({ tableNumber: "T-4" }));

    await vi.waitFor(() => expect(document.querySelector(".sp-place")?.textContent).toBe("Masa T-4"));
    expect(document.querySelector(".sp-kitchenless-note")?.textContent).toBe("Kasada eklendi (mutfağa gitmedi): Ayran <b>");
    expect(document.querySelector(".sp-kitchenless-note b")).toBeNull();
  });

  it("names at most two added lines and counts the rest", async () => {
    const added = (name) => ({ name, quantity: 1, unitPrice: 1, amount: 1, addedWithoutKitchenReason: "other" });
    await open(unpaid, receipt({ items: [added("A"), added("B"), added("C"), added("D")] }));

    await vi.waitFor(() => expect(document.querySelector(".sp-kitchenless-note")?.textContent).toBe("Kasada eklendi (mutfağa gitmedi): A, B ve 2 kalem daha"));
  });

  it("shows neither line for a quick sale with every line sent to the kitchen", async () => {
    await open(unpaid, receipt({ items: [{ name: "Köfte", quantity: 1, unitPrice: 80, amount: 80 }] }));

    await vi.waitFor(() => expect(document.querySelector(".sp-summary-row")).not.toBeNull());
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(document.querySelector(".sp-place")).toBeNull();
    expect(document.querySelector(".sp-kitchenless-note")).toBeNull();
  });

  it("names the table and the note on the paid screen too", async () => {
    await open(settled, receipt({ tableNumber: "T-4" }));

    await vi.waitFor(() => expect(document.querySelector(".sp-place")?.textContent).toBe("Masa T-4"));
    expect(document.querySelector(".sp-kitchenless-note")).not.toBeNull();
  });

  it("prints the table and a Kasada eklendi row under the line on the receipt", async () => {
    await open(settled, receipt({ tableNumber: "T-4" }));
    await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());

    document.getElementById("print-receipt").click();
    document.getElementById("print-receipt").click();

    await vi.waitFor(() => expect(window.print).toHaveBeenCalledTimes(1));
    const sheet = document.getElementById("receipt-print");
    expect(sheet.textContent).toContain("Masa: T-4");
    const rows = [...sheet.querySelectorAll("tr")].map((row) => row.textContent);
    expect(rows[1]).toContain("Ayran <b>");
    expect(rows[2]).toContain("Kasada eklendi");
    expect(rows.filter((row) => row.includes("Kasada eklendi"))).toHaveLength(1);
  });

  it("puts the split controls after Ödemeyi Ekle in page order, so focus order matches what is drawn", async () => {
    await open(unpaid, receipt());
    await vi.waitFor(() => expect(document.getElementById("submit-tender")).not.toBeNull());

    const follows = (id) => Boolean(document.getElementById("submit-tender").compareDocumentPosition(document.getElementById(id)) & Node.DOCUMENT_POSITION_FOLLOWING);
    expect(follows("fold-split")).toBe(true);
    expect(follows("split-by-item")).toBe(true);
    expect(follows("amount-draft")).toBe(false);
  });

  it("keeps Ödemeyi Ekle on the first screen of a short display by tightening the form", () => {
    const shortScreen = /@media\s*\(max-height:\s*(\d+)px\)\s*\{([\s\S]*?)\n\}/.exec(css);

    expect(shortScreen).not.toBeNull();
    expect(Number(shortScreen[1])).toBeGreaterThanOrEqual(800);
    expect(shortScreen[2]).toMatch(/\.sp-card\s*\{[^}]*gap:\s*10px/);
    expect(shortScreen[2]).toMatch(/\.sp-shell\s*\{[^}]*padding:\s*8px/);
    expect(shortScreen[2]).not.toMatch(/order:/);
  });
});
