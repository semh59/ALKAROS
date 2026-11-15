import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const settled = { billId: BILL_ID, payableAmount: 110, allocatedTotal: 110, remainingAmount: 0, allocations: [], unsettledPayment: null };

async function openPaid(receipt) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: settled },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "66666666-6666-6666-6666-666666666666" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/receipt"), body: { items: [], status: "Paid", ...receipt } },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());
}

const links = () => [...document.querySelectorAll("#app a.sp-btn")].map((a) => ({ text: a.textContent, href: a.getAttribute("href"), primary: a.classList.contains("sp-btn-primary") }));

describe("the paid-bill screen names the next job", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ""; });

  it("offers a primary new sale after a counter sale and keeps the pending list secondary", async () => {
    await openPaid({ tableNumber: null });
    await vi.waitFor(() => expect(links().some((l) => l.text === "Yeni satış")).toBe(true));
    expect(links().find((l) => l.text === "Yeni satış")).toMatchObject({ href: "/", primary: true });
    expect(links().find((l) => l.text === "Bekleyen hesaplara dön")).toMatchObject({ href: "/pending-checks", primary: false });
    expect(links().some((l) => l.text === "Masalara dön")).toBe(false);
  });

  it("offers a primary return to the tables after a table bill", async () => {
    await openPaid({ tableNumber: "5" });
    await vi.waitFor(() => expect(links().some((l) => l.text === "Masalara dön")).toBe(true));
    expect(links().find((l) => l.text === "Masalara dön")).toMatchObject({ href: "/tables", primary: true });
    expect(links().find((l) => l.text === "Bekleyen hesaplara dön").primary).toBe(false);
    expect(links().some((l) => l.text === "Yeni satış")).toBe(false);
  });

  it("makes the receipt print secondary so the next-job button is the primary one", async () => {
    await openPaid({ tableNumber: null });
    await vi.waitFor(() => expect(links().some((l) => l.text === "Yeni satış")).toBe(true));
    expect(document.getElementById("print-receipt").classList.contains("sp-btn-primary")).toBe(false);
  });

  it("keeps the receipt confirmation primary while the original/KOPYA warning is open", async () => {
    await openPaid({ tableNumber: "5" });
    await vi.waitFor(() => expect(links().some((l) => l.text === "Masalara dön")).toBe(true));
    document.getElementById("print-receipt").click();
    expect(document.getElementById("print-receipt").textContent).toBe("Evet, yazdır");
    expect(document.getElementById("print-receipt").classList.contains("sp-btn-primary")).toBe(true);
    expect(links().some((l) => l.text === "Masalara dön")).toBe(false);
  });

  it("leaves printing primary and offers no guess when the bill place could not be read", async () => {
    window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
    installFetchRouter([
      { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
      { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: settled },
      { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "66666666-6666-6666-6666-666666666666" } },
      { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
      { test: (url) => url.endsWith("/refund-requests"), body: [] },
      { test: (url) => url.endsWith("/receipt"), status: 500, body: {} },
    ]);
    await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
    await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());
    await new Promise((done) => setTimeout(done, 50));
    expect(links().map((l) => l.text)).toEqual(["Bekleyen hesaplara dön"]);
    expect(document.getElementById("print-receipt").classList.contains("sp-btn-primary")).toBe(true);
  });
});
