import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "split-payment.js");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const SESSION_ID = "66666666-6666-6666-6666-666666666666";

const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };
const settled = { ...unpaid, allocatedTotal: 100, remainingAmount: 0 };
const partial = { ...unpaid, allocatedTotal: 50, remainingAmount: 50 };

async function open({ cash = true, tenderResult = { approvedAmount: 100, changeAmount: 50 }, afterTender = settled } = {}) {
  let current = unpaid;
  const results = Array.isArray(tenderResult) ? [...tenderResult] : null;
  const routeSet = [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", get body() { return current; } },
    cash
      ? { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: SESSION_ID } }
      : { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    {
      test: (url, init) => url.endsWith("/cash-tender") && init?.method === "POST",
      get body() { current = afterTender; return results ? (results.length > 1 ? results.shift() : results[0]) : tenderResult; },
    },
  ];
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routeSet);
  await loadApp(htmlPath, scriptPath);
  return fetchMock;
}

const app = () => document.getElementById("app");
const tenderedField = () => document.getElementById("tendered-draft");

function typeTendered(value) {
  tenderedField().value = value;
  tenderedField().dispatchEvent(new Event("input", { bubbles: true }));
}

function tenderBody(fetchMock) {
  const call = fetchMock.mock.calls.find(([url, init]) => url.endsWith("/cash-tender") && init?.method === "POST");
  return call ? JSON.parse(call[1].body) : null;
}

describe("split-payment.js optional cash change", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("sends the tendered amount and shows the change the server returns", async () => {
    const fetchMock = await open();
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    typeTendered("150");
    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(tenderBody(fetchMock)).not.toBeNull());
    expect(tenderBody(fetchMock).TenderedAmount).toBe(150);
    expect(tenderBody(fetchMock).AmountDue).toBe(100);
    await vi.waitFor(() => expect(document.getElementById("change-due")).not.toBeNull());
    expect(document.getElementById("change-due").textContent).toContain("50,00");
  });

  it("leaves the flow unchanged when no tendered amount is typed", async () => {
    const fetchMock = await open({ tenderResult: { approvedAmount: 100, changeAmount: 0 } });
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(tenderBody(fetchMock)).not.toBeNull());
    expect(tenderBody(fetchMock).TenderedAmount).toBe(100);
    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));
    expect(document.getElementById("change-due")).toBeNull();
  });

  it("previews the change while typing", async () => {
    await open();
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());
    const preview = () => document.getElementById("change-preview").textContent;

    typeTendered("120");
    expect(preview()).toBe("Para üstü: ₺20,00");

    typeTendered("80");
    expect(preview()).toContain("tutardan az");

    typeTendered("");
    expect(preview()).toBe("");
  });

  it("refuses a tendered amount below the amount due without calling the server", async () => {
    const fetchMock = await open();
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    typeTendered("80");
    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(app().textContent).toContain("Alınan para tutardan az olamaz"));
    expect(tenderBody(fetchMock)).toBeNull();
  });

  it("accepts a tendered amount equal to the amount due", async () => {
    const fetchMock = await open({ tenderResult: { approvedAmount: 100, changeAmount: 0 } });
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    typeTendered("100");
    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(tenderBody(fetchMock)).not.toBeNull());
    expect(tenderBody(fetchMock).TenderedAmount).toBe(100);
  });

  it("does not show change the cashier never asked for", async () => {
    await open({ tenderResult: { approvedAmount: 100, changeAmount: 5 } });
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));
    expect(document.getElementById("change-due")).toBeNull();
  });

  it("ignores a leftover tendered amount once another method is chosen", async () => {
    const fetchMock = await open();
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());
    typeTendered("80");

    document.querySelector('.sp-method-chip[data-method="Eft"]').click();
    document.getElementById("eft-confirm").click();
    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(fetchMock.mock.calls.some(([url, init]) => url.endsWith("/tenders/") && init?.method === "POST")).toBe(true));
    expect(app().textContent).not.toContain("Alınan para tutardan az olamaz");
  });

  it("shows the change on the open bill, clears the tendered field, and drops the change on the next tender", async () => {
    await open({
      afterTender: partial,
      tenderResult: [{ approvedAmount: 50, changeAmount: 30 }, { approvedAmount: 50, changeAmount: 0 }],
    });
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());
    document.getElementById("amount-draft").value = "50";
    document.getElementById("amount-draft").dispatchEvent(new Event("input", { bubbles: true }));
    typeTendered("80");

    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(document.getElementById("change-due")).not.toBeNull());
    expect(document.getElementById("change-due").textContent).toContain("30,00");
    expect(tenderedField().value).toBe("");

    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(document.getElementById("change-due")).toBeNull());
  });

  it("drops the previous change as soon as the next tender is sent, even if it then fails", async () => {
    const fetchMock = await open({ afterTender: partial, tenderResult: { approvedAmount: 50, changeAmount: 30 } });
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());
    document.getElementById("amount-draft").value = "50";
    document.getElementById("amount-draft").dispatchEvent(new Event("input", { bubbles: true }));
    typeTendered("80");
    document.getElementById("submit-tender").click();
    await vi.waitFor(() => expect(document.getElementById("change-due")).not.toBeNull());
    const original = fetchMock.getMockImplementation();
    fetchMock.mockImplementation(async (url, init) => {
      if (String(url).endsWith("/cash-tender") && init?.method === "POST") {
        return { ok: false, status: 500, json: async () => ({}), headers: { get: () => null } };
      }
      return original(url, init);
    });

    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(document.getElementById("change-due")).toBeNull());
  });

  it("keeps the change on the paid screen when the payment closes the bill", async () => {
    await open();
    await vi.waitFor(() => expect(tenderedField()).not.toBeNull());

    typeTendered("150");
    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));
    expect(document.getElementById("change-due").textContent).toContain("50,00");
  });

  it("offers no tendered field for a non-cash method", async () => {
    await open({ cash: false });
    await vi.waitFor(() => expect(document.getElementById("submit-tender")).not.toBeNull());

    expect(tenderedField()).toBeNull();
  });
});
