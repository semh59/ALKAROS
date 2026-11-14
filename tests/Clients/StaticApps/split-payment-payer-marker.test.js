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

const summary = (lines) => ({
  billId: BILL_ID,
  payableAmount: 100,
  allocatedTotal: lines.reduce((sum, line) => sum + line.amount, 0),
  remainingAmount: 100 - lines.reduce((sum, line) => sum + line.amount, 0),
  allocations: lines.map((line, index) => ({ paymentId: `p${index + 1}`, paymentStatus: "Approved", ...line })),
  unsettledPayment: null,
});

async function open({ shares = false } = {}) {
  let lines = [];
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", get body() { return summary(lines); } },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/show-on-display"), body: {} },
    shares
      ? { test: (url) => url.endsWith("/split-design"), body: { allocations: [{ ownerId: "o1", amount: 40 }, { ownerId: "o2", amount: 60 }] } }
      : { test: (url) => url.endsWith("/split-design"), status: 404 },
    { test: (url) => url.endsWith("/split-design/owners"), body: [{ ownerId: "o1", label: "Ayşe" }, { ownerId: "o2", label: "Can" }] },
    {
      test: (url, init) => url.endsWith("/cash-tender") && init?.method === "POST",
      get body() {
        const amount = Number(JSON.parse(globalThis.fetch.mock.calls.at(-1)[1].body).AmountDue);
        lines = [...lines, { method: "Cash", amount }];
        return { outcome: "Approved", approvedAmount: amount, changeAmount: 0 };
      },
    },
  ]);
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(document.getElementById("submit-tender")).not.toBeNull());
}

const text = () => document.getElementById("app").textContent;
const lineTexts = () => {
  const field = [...document.querySelectorAll(".sp-field")].find((candidate) => candidate.querySelector(".sp-field-label")?.textContent === "Uygulanan tahsilatlar");
  return field ? [...field.querySelectorAll(".sp-line-method")].map((line) => line.textContent) : [];
};
const payNow = async (expectedLines) => {
  document.getElementById("submit-tender").click();
  await vi.waitFor(() => expect(lineTexts()).toHaveLength(expectedLines));
  await vi.waitFor(() => expect(document.getElementById("submit-tender").disabled).toBe(false));
};

describe("split-payment.js payer marker", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("names the payer of each equal-split payment, on the form and on the line it created", async () => {
    await open();
    const count = document.getElementById("split-count");
    count.value = "3";
    count.dispatchEvent(new Event("input", { bubbles: true }));
    document.getElementById("apply-split").click();
    expect(text()).toContain("Ödeyen: Kişi 1");

    await payNow(1);

    expect(lineTexts()[0]).toMatch(/ · Kişi 1$/);
    expect(text()).toContain("Ödeyen: Kişi 2");
    await payNow(2);
    expect(lineTexts().map((line) => line.split(" · ").pop())).toEqual(["Kişi 1", "Kişi 2"]);
    expect(text()).toContain("Ödeyen: Kişi 3");
  });

  it("keeps counting people off the count that was calculated, whatever the count box says afterwards", async () => {
    await open();
    const count = document.getElementById("split-count");
    count.value = "3";
    count.dispatchEvent(new Event("input", { bubbles: true }));
    document.getElementById("apply-split").click();
    await payNow(1);

    count.value = "5";
    count.dispatchEvent(new Event("input", { bubbles: true }));
    await payNow(2);

    expect(lineTexts().map((line) => line.split(" · ").pop())).toEqual(["Kişi 1", "Kişi 2"]);
  });

  it("drops a tapped person's name once the amount is typed by hand", async () => {
    await open({ shares: true });
    await vi.waitFor(() => expect(document.querySelectorAll("[data-share]")).toHaveLength(2));
    document.querySelector('[data-share="0"]').click();
    const amount = document.getElementById("amount-draft");
    amount.value = "10";
    amount.dispatchEvent(new Event("input", { bubbles: true }));

    document.getElementById("apply-split").click();

    expect(text()).not.toContain("Ödeyen: Ayşe");
  });

  it("uses the saved person's own name when that person's share is tapped", async () => {
    await open({ shares: true });
    await vi.waitFor(() => expect(document.querySelectorAll("[data-share]")).toHaveLength(2));
    document.querySelector('[data-share="0"]').click();
    expect(text()).toContain("Ödeyen: Ayşe");

    await payNow(1);

    expect(lineTexts()[0]).toMatch(/ · Ayşe$/);
    expect(text()).not.toContain("Ödeyen:");
  });

  it("marks no payer on a plain payment", async () => {
    await open();
    expect(text()).not.toContain("Ödeyen:");
    const amount = document.getElementById("amount-draft");
    amount.value = "30";
    amount.dispatchEvent(new Event("input", { bubbles: true }));

    await payNow(1);

    expect(lineTexts()[0]).not.toContain("Kişi");
  });
});
