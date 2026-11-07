import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";

const comp = { adjustmentId: "a1", adjustmentType: "DiscountAmount", isDeduction: true, amount: 100, reason: "CustomerSatisfaction" };
const discount = { adjustmentId: "a2", adjustmentType: "DiscountAmount", isDeduction: true, amount: 100, reason: "PromotionalOffer" };
const tip = { adjustmentId: "a3", adjustmentType: "Tip", isDeduction: false, amount: 5, reason: "Tip" };

async function open(allocatedTotal, adjustments) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    {
      test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET",
      body: { billId: BILL_ID, payableAmount: 100, allocatedTotal, remainingAmount: 0, allocations: [], unsettledPayment: null },
    },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments, summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());
}

const title = () => document.querySelector("h1.sp-title").textContent;
const subtitle = () => document.querySelector("p.sp-subtitle").textContent;

describe("split-payment.js heading of a bill with nothing left to pay", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("says a bill that was only comped closed by comp and that nothing was collected", async () => {
    await open(0, [comp]);

    expect(title()).toBe("İkramla Kapandı");
    expect(subtitle()).toBe("Bu hesabın tamamı ikram edildi; tahsilat yapılmadı.");
  });

  it("does not count a tip as a discount", async () => {
    await open(0, [comp, tip]);

    expect(title()).toBe("İkramla Kapandı");
  });

  it("says a bill closed by discount alone closed by discount", async () => {
    await open(0, [discount]);

    expect(title()).toBe("İndirimle Kapandı");
    expect(subtitle()).toBe("Bu hesap indirimle kapandı; tahsilat yapılmadı.");
  });

  it("names both when a discount and a comp closed the bill, ignoring a tip", async () => {
    await open(0, [comp, discount, tip]);

    expect(title()).toBe("İndirim ve İkramla Kapandı");
    expect(subtitle()).toBe("Bu hesap indirim ve ikramla kapandı; tahsilat yapılmadı.");
  });

  it("says a bill with nothing to collect and no deduction is closed", async () => {
    await open(0, []);

    expect(title()).toBe("Hesap Kapandı");
    expect(subtitle()).toBe("Bu hesapta tahsil edilecek tutar kalmadı.");
  });

  it.each([
    ["a comp", [comp], "Bu hesap tahsilat ve ikramla kapandı."],
    ["a discount", [discount], "Bu hesap tahsilat ve indirimle kapandı."],
    ["a discount and a comp", [discount, comp], "Bu hesap tahsilat, indirim ve ikramla kapandı."],
  ])("says what closed the rest of a bill that was partly collected: %s", async (_name, adjustments, expected) => {
    await open(60, adjustments);

    expect(title()).toBe("Hesap Ödendi");
    expect(subtitle()).toBe(expected);
  });

  it("keeps the plain paid wording when only a tip is on the collected bill", async () => {
    await open(100, [tip]);

    expect(title()).toBe("Hesap Ödendi");
    expect(subtitle()).toBe("Bu hesabın tamamı tahsil edildi.");
  });

  it("keeps paid wording for an ordinary paid bill", async () => {
    await open(100, []);

    expect(title()).toBe("Hesap Ödendi");
  });
});
