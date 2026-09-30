// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { type ClosingClient } from "./closingApi";
import { ClosingSection } from "./index";
import type { BusinessDay } from "./models";
import { ManagementApiError } from "../management/http";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const openDay: BusinessDay = {
  businessDayId: "d1", businessDate: "2026-09-29", openedAt: "2026-09-29T06:00:00Z", closedAt: null, status: "Open",
  totalRevenue: 1250, totalOrdersCount: 14, totalCancelledItemsCount: 0, totalPrintFailuresCount: 0,
};

function fakeClient(overrides: Partial<ClosingClient> = {}): ClosingClient {
  return {
    getDay: vi.fn().mockResolvedValue(openDay),
    getFullReport: vi.fn().mockResolvedValue({ businessDay: openDay, waiterSummaries: [] }),
    openDay: vi.fn().mockResolvedValue(undefined),
    closeDay: vi.fn().mockResolvedValue(undefined),
    getSettlement: vi.fn().mockResolvedValue({
      paymentMix: [{ method: "BankCard", approvedCount: 3, approvedAmount: 900 }],
      unsettledPayments: { unknownCount: 1, unknownAmount: 50, reconciliationRequiredCount: 0, reconciliationRequiredAmount: 0 },
      cashSessions: [], reconciliationTotals: [{ caseType: "CashVariance", openCount: 1, resolvedCount: 2, openDiscrepancyAmount: 10 }],
    }),
    listManualConfirmations: vi.fn().mockResolvedValue([
      { confirmationId: "m1", slipNumber: "SLP-9", amount: 120, status: "Pending", requestedAt: "2026-09-29T10:00:00Z", decidedAt: null },
    ]),
    scanPayments: vi.fn().mockResolvedValue(undefined),
    listCases: vi.fn().mockResolvedValue([
      { caseId: "c1", caseType: "PaymentMismatch", severity: "High", status: "Open", discrepancyAmount: 40, openedAt: "2026-09-29T09:00:00Z", rowVersion: 3 },
      { caseId: "c2", caseType: "OnlineOrderMismatch", severity: "Low", status: "Investigating", discrepancyAmount: 5, openedAt: "2026-09-29T09:30:00Z", rowVersion: 1 },
    ]),
    transitionCase: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

describe("closing and reconciliation section", () => {
  let root: Root | null = null;
  async function render(element: ReactElement) {
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(element));
  }
  const buttons = () => [...document.querySelectorAll("button")].map((button) => button.textContent);
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; vi.restoreAllMocks(); });

  it("a permitted session sees the action buttons", async () => {
    await render(<ClosingSection client={fakeClient()} capabilities={new Set(["reports.view", "reports.close-day", "reconciliation.manage"])} />);
    expect(buttons()).toEqual(expect.arrayContaining(["İş gününü kapat", "Ödeme taraması yap", "İncelemeye al", "Çözüldü olarak kapat"]));
  });

  it("a view-only session sees data but no action buttons", async () => {
    await render(<ClosingSection client={fakeClient()} capabilities={new Set(["reports.view"])} />);
    expect(document.body.textContent).toContain("₺1.250,00");
    expect(document.body.textContent).toContain("SLP-9");
    for (const forbidden of ["İş gününü kapat", "İş gününü aç", "Ödeme taraması yap", "İncelemeye al"]) expect(buttons()).not.toContain(forbidden);
  });

  it("shows Turkish labels and never a raw enum value", async () => {
    await render(<ClosingSection client={fakeClient()} capabilities={new Set(["reports.view"])} />);
    const text = document.body.textContent ?? "";
    for (const raw of ["BankCard", "PaymentMismatch", "OnlineOrderMismatch", "Investigating", "CashVariance"]) expect(text).not.toContain(raw);
    for (const turkish of ["Banka kartı", "Ödeme uyuşmazlığı", "Kasa farkı", "Bekliyor"]) expect(text).toContain(turkish);
  });

  it("an online order case cannot be resolved from this screen", async () => {
    await render(<ClosingSection client={fakeClient()} capabilities={new Set(["reports.view", "reconciliation.manage"])} />);
    const rows = [...document.querySelectorAll("tbody tr")].filter((row) => row.textContent?.includes("Online sipariş uyuşmazlığı"));
    expect(rows).toHaveLength(1);
    expect(rows[0]!.textContent).not.toContain("Çözüldü olarak kapat");
  });

  it("a case transition carries the expected version and the note", async () => {
    const client = fakeClient();
    await render(<ClosingSection client={client} capabilities={new Set(["reports.view", "reconciliation.manage"])} />);
    const note = document.getElementById("closing-case-note") as HTMLInputElement;
    await act(async () => {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(note, "Slip kontrol edildi");
      note.dispatchEvent(new Event("input", { bubbles: true }));
    });
    const button = [...document.querySelectorAll("button")].find((candidate) => candidate.textContent === "İncelemeye al")!;
    await act(async () => button.click());
    expect(client.transitionCase).toHaveBeenCalledWith("c1", "Investigating", 3, "Slip kontrol edildi");
  });

  it("the server's Turkish reason is shown as is", async () => {
    const client = fakeClient({ closeDay: vi.fn().mockRejectedValue(new ManagementApiError(409, "OPEN_BILLS", "Açık hesaplar varken gün kapatılamaz.")) });
    await render(<ClosingSection client={client} capabilities={new Set(["reports.view", "reports.close-day"])} />);
    const close = [...document.querySelectorAll("button")].find((candidate) => candidate.textContent === "İş gününü kapat")!;
    await act(async () => close.click());
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Açık hesaplar varken gün kapatılamaz.");
  });

  it("with no day record a permitted session can open the day", async () => {
    const client = fakeClient({ getDay: vi.fn().mockResolvedValue(null) });
    await render(<ClosingSection client={client} capabilities={new Set(["reports.view", "reports.close-day"])} />);
    expect(document.body.textContent).toContain("Bu tarih için iş günü kaydı yok.");
    const open = [...document.querySelectorAll("button")].find((candidate) => candidate.textContent === "İş gününü aç")!;
    await act(async () => open.click());
    expect(client.openDay).toHaveBeenCalledTimes(1);
  });

  it("a failed panel shows its error while the others keep working", async () => {
    const client = fakeClient({ getSettlement: vi.fn().mockRejectedValue(new ManagementApiError(500, "X", "Rapor şu an hazırlanamıyor.")) });
    await render(<ClosingSection client={client} capabilities={new Set(["reports.view"])} />);
    expect(document.body.textContent).toContain("Rapor şu an hazırlanamıyor.");
    expect(document.body.textContent).toContain("SLP-9");
  });
});
