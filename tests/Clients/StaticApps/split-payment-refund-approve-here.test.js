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
const PAYMENT_ID = "33333333-3333-3333-3333-333333333333";
const REQUEST_ID = "44444444-4444-4444-4444-444444444444";
const BASE = `/api/v1/terminals/${TERMINAL_ID}/billing`;

const paidSummary = {
  billId: BILL_ID, payableAmount: 100, allocatedTotal: 100, remainingAmount: 0,
  allocations: [{ allocationId: "a1", paymentId: PAYMENT_ID, amount: 100, paymentStatus: "Approved" }],
  unsettledPayment: null,
};
const waiting = (extra = {}) => ({
  requestId: REQUEST_ID, billId: BILL_ID, paymentId: PAYMENT_ID, amount: 100, method: "Cash", reason: "Yanlış tutar",
  status: "Requested", requestedBy: "u1", requestedAt: "2026-10-13T10:00:00Z", mayDecide: false, ...extra,
});

function routes({ requests, approve }) {
  return [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1", capabilities: [] } },
    { test: (url) => url.endsWith("/tenders/"), body: paidSummary },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url, init) => url.endsWith("/approve") && init?.method === "POST", ...approve },
    { test: (url) => url.endsWith(`${BASE}/refund-requests`), body: requests },
  ];
}

async function open(world) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routes(world));
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(document.getElementById("app").textContent).toContain("Başka bir yetkili onaylamalı."));
  return fetchMock;
}

const nameBox = () => document.getElementById(`refund-approver-name-${REQUEST_ID}`);
const secretBox = () => document.getElementById(`refund-approver-secret-${REQUEST_ID}`);
const press = () => document.querySelector('[data-refund-action="approve-here"]').click();
const approvePosts = (fetchMock) => fetchMock.mock.calls
  .filter(([url, init]) => url.endsWith(`/${REQUEST_ID}/approve`) && init?.method === "POST")
  .map(([, init]) => JSON.parse(init.body));

describe("split-payment.js a manager approving a refund at the till", () => {
  beforeEach(() => {
    vi.resetModules();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("offers the sign-in fields on a request the caller may not decide, with a password-type box", async () => {
    await open({ requests: [waiting()], approve: { body: {} } });

    expect(document.getElementById("app").textContent).toContain("Yönetici burada onaylasın");
    expect(secretBox().type).toBe("password");
    expect(document.querySelector('[data-refund-action="approve-here"]').textContent).toBe("Yönetici onayı ver");
  });

  it("does not offer them on a request the caller may decide", async () => {
    window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
    installFetchRouter(routes({ requests: [waiting({ mayDecide: true })], approve: { body: {} } }));
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="approve"]')).not.toBeNull());

    expect(nameBox()).toBeNull();
  });

  it("sends the typed sign-in and keeps the password nowhere on the page", async () => {
    const fetchMock = await open({ requests: [waiting()], approve: { body: { status: "Completed", billCancelled: false } } });
    nameBox().value = "mudur";
    secretBox().value = "Gizli-Sifre-9";

    press();
    await vi.waitFor(() => expect(approvePosts(fetchMock)).toHaveLength(1));

    expect(approvePosts(fetchMock)[0]).toEqual({ ManagerUsername: "mudur", ManagerPassword: "Gizli-Sifre-9" });
    await vi.waitFor(() => expect(document.getElementById("app").textContent).toContain("İade isteği onaylandı."));
    expect(document.body.innerHTML).not.toContain("Gizli-Sifre-9");
  });

  it("says the refund is done, not that the bill was cancelled, when the manager's approval closes the bill", async () => {
    await open({ requests: [waiting()], approve: { body: { status: "Completed", billCancelled: true, method: "Cash", amount: 100 } } });
    nameBox().value = "mudur";
    secretBox().value = "Gizli-Sifre-9";

    press();

    await vi.waitFor(() => expect(document.getElementById("app").textContent).toContain("İade Tamamlandı"));
    expect(document.getElementById("app").textContent).toContain("₺100,00 iade edildi. Parayı müşteriye geri verin.");
    expect(document.getElementById("app").textContent).not.toContain("Hesap İptal Edildi");
  });

  it("says in Turkish when the sign-in is wrong, keeps the name, and empties the password", async () => {
    const fetchMock = await open({
      requests: [waiting()],
      approve: { status: 403, body: { error: { code: "MANAGER_CREDENTIAL_INVALID", message: "RAW" } } },
    });
    nameBox().value = "mudur";
    secretBox().value = "yanlis";

    press();
    await vi.waitFor(() => expect(document.getElementById("app").textContent).toContain("Yönetici adı veya şifre hatalı."));

    expect(document.getElementById("app").textContent).not.toContain("RAW");
    expect(nameBox().value).toBe("mudur");
    expect(secretBox().value).toBe("");
    expect(approvePosts(fetchMock)).toHaveLength(1);
  });

  it("sends nothing and asks for both fields when one is empty", async () => {
    const fetchMock = await open({ requests: [waiting()], approve: { body: {} } });
    nameBox().value = "mudur";

    press();

    await vi.waitFor(() => expect(document.getElementById("app").textContent).toContain("Yönetici adı ve şifresini girin."));
    expect(approvePosts(fetchMock)).toHaveLength(0);
  });
});
