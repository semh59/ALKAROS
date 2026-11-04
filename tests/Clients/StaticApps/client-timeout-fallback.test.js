import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const paymentsDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";

// A browser without AbortSignal.timeout must still end a hung request after the 15 seconds the screens promise.
describe.each(["cash-session", "split-payment", "customer-accounts"])("%s without AbortSignal.timeout", (name) => {
  const original = AbortSignal.timeout;

  beforeEach(() => {
    delete AbortSignal.timeout;
    window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  });

  afterEach(() => {
    AbortSignal.timeout = original;
    vi.restoreAllMocks();
    document.body.innerHTML = "";
  });

  it("gives every request a signal of its own that aborts after fifteen seconds", async () => {
    const timers = vi.spyOn(globalThis, "setTimeout");
    const fetchMock = installFetchRouter([
      { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
      { test: () => true, status: 404 },
    ]);

    await loadApp(resolve(paymentsDir, name, "index.html"), resolve(paymentsDir, name, `${name}.js`));
    await vi.waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThan(0));

    const signal = fetchMock.mock.calls[0][1].signal;
    expect(signal).toBeInstanceOf(AbortSignal);
    expect(signal.aborted).toBe(false);
    const deadline = timers.mock.calls.find(([, delay]) => delay === 15_000);
    expect(deadline).toBeDefined();
    deadline[0]();
    expect(signal.aborted).toBe(true);
  });
});
