import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const wwwroot = resolve(here, "..", "..", "..", "src/Clients/WaiterPwa/wwwroot");
const TABLE_ID = "44444444-4444-4444-4444-444444444444";
const PRODUCT_ID = "22222222-2222-2222-2222-222222222222";

const routes = (allowsHalfPortion) => [
  { test: (url) => url.includes("/api/v1/auth/session?"), body: { userId: "u1", name: "Garson Ahmet" } },
  { test: (url) => url.includes("/runtime-configuration"), body: { garsonFeatures: {} } },
  { test: (url) => url.includes("/table-management/zones"), body: [] },
  { test: (url) => url.includes("/catalog"), body: [{ productId: PRODUCT_ID, name: "Kola", unitPrice: 45, categoryCode: "c1", categoryName: "İçecek", allowsHalfPortion }] },
  { test: (url) => url.includes("/table-management/tables"), body: [{ tableId: TABLE_ID, tableNumber: "M-05", capacity: 4, zoneId: "z1", status: "Available" }] },
  { test: (url) => url.includes(`/orders/table/${TABLE_ID}`), status: 404, body: {} },
  { test: (url) => url.includes("/orders/pending"), body: [] },
];

// A draft saved on this device before the product lost its half portion setting: the line carries a half quantity and no flag.
const savedLine = (quantity, allowsHalfPortion) => ({
  id: "line-1", productId: PRODUCT_ID, name: "Kola", price: 45, quantity, modifiers: [], note: "", seatId: null, courseNumber: null,
  ...(allowsHalfPortion === undefined ? {} : { allowsHalfPortion }),
});

async function openBillWithSavedLine(allowsHalfPortion, line) {
  localStorage.setItem("alkaros_waiter_drafts_by_table", JSON.stringify([[TABLE_ID, { number: "M-05", lines: [line] }]]));
  installFetchRouter(routes(allowsHalfPortion));
  await loadApp(resolve(wwwroot, "index.html"), resolve(wwwroot, "waiter-app.js"));
  await vi.waitFor(() => {
    if (!document.querySelector(".table[data-table]")) throw new Error("tables not rendered yet");
  }, { timeout: 10_000 });
  document.querySelector(".table[data-table]").click();
  await vi.waitFor(() => {
    if (!document.querySelector(".product[data-product]")) throw new Error("menu not rendered yet");
  }, { timeout: 10_000 });
  document.getElementById("btnOpenBill").click();
  await vi.waitFor(() => {
    if (!document.querySelector('[data-step="+"]')) throw new Error("draft line not rendered yet");
  }, { timeout: 10_000 });
}

const step = (sign) => document.querySelector(`[data-step="${sign}"]`).click();
const quantity = () => document.querySelector('[data-step="+"]')?.parentElement.textContent;

describe("stepping a waiter draft line", { timeout: 30_000 }, () => {
  beforeEach(() => {
    vi.resetModules();
    localStorage.clear();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("moves a flagged product by half units", async () => {
    await openBillWithSavedLine(true, savedLine(1, true));

    step("+");
    expect(quantity()).toContain("1,5");
    step("-");
    expect(quantity()).toContain("1");
    expect(quantity()).not.toContain("1,5");
  });

  it("moves an unflagged product by whole units", async () => {
    await openBillWithSavedLine(false, savedLine(1, false));

    step("+");
    expect(quantity()).toContain("2");
    step("-");
    expect(quantity()).not.toContain("2");
    expect(quantity()).toContain("1");
  });

  it("rounds a leftover half quantity of an unflagged product to a whole number instead of carrying it on", async () => {
    await openBillWithSavedLine(false, savedLine(1.5, false));

    step("+");
    expect(quantity()).toContain("2");
    expect(quantity()).not.toContain("2,5");
  });

  it("removes a flagged half portion line when it is stepped down from 0,5", async () => {
    await openBillWithSavedLine(true, savedLine(0.5, true));

    step("-");
    expect(document.querySelector('[data-step="-"]')).toBeNull();
  });
});
