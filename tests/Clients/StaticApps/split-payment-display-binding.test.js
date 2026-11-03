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
const summary = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };

function routes(showOnDisplay) {
  return [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u2" } },
    ...(showOnDisplay ? [{ test: (url) => url.endsWith("/show-on-display"), ...showOnDisplay }] : []),
    { test: (url) => url.endsWith("/tenders/"), body: summary },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
  ];
}

async function open(showOnDisplay) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routes(showOnDisplay));
  await loadApp(htmlPath, scriptPath);
  return fetchMock;
}

describe("split-payment.js customer display binding", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("shows the bill being paid on the customer display as soon as the page opens it", async () => {
    const fetchMock = await open({ body: { bound: true } });

    await vi.waitFor(() => {
      const call = fetchMock.mock.calls.find(([url]) => url.endsWith("/show-on-display"));
      expect(call?.[0]).toBe(`/api/v1/terminals/${TERMINAL_ID}/billing/bills/${BILL_ID}/show-on-display`);
      expect(call?.[1].method).toBe("POST");
    });
  });

  it("logs a refusal from the server and keeps the payment page usable", async () => {
    const error = vi.spyOn(console, "error").mockImplementation(() => undefined);
    await open({ status: 403 });

    await vi.waitFor(() => expect(error).toHaveBeenCalledWith("Müşteri ekranına bağlanamadı:", 403));
    expect(document.body.textContent).toContain("Kalan");
    error.mockRestore();
  });

  it("keeps the payment page usable when the customer display cannot be reached", async () => {
    const error = vi.spyOn(console, "error").mockImplementation(() => undefined);
    await open(null);

    await vi.waitFor(() => expect(error).toHaveBeenCalledWith("Müşteri ekranına bağlanamadı:", expect.any(Error)));
    expect(document.body.textContent).toContain("Kalan");
    error.mockRestore();
  });
});
