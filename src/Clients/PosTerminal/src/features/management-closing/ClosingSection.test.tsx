// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createClosingClient, type ClosingClient } from "./closingApi";
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
    listCaseActions: vi.fn().mockResolvedValue([]),
    addCaseNote: vi.fn().mockResolvedValue(undefined),
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
  async function renderCases(client: ClosingClient, capabilities = new Set(["reports.view", "reconciliation.manage"])) {
    await render(<ClosingSection capabilities={capabilities} client={client} />);
  }
  const press = async (text: string) => {
    const button = [...document.querySelectorAll("button")].find((candidate) => candidate.textContent === text)!;
    await act(async () => button.click());
  };

  it("shows a case's history with Turkish action names and the stored note or reason", async () => {
    const client = fakeClient({
      listCaseActions: vi.fn().mockResolvedValue([
        { actionId: "a1", actionType: "Created", performedAt: "2026-09-29T09:00:00Z", detailsJson: null },
        { actionId: "a2", actionType: "NoteAdded", performedAt: "2026-09-29T10:00:00Z", detailsJson: "{\"note\":\"Banka ile görüşüldü\"}" },
        { actionId: "a3", actionType: "StatusChanged", performedAt: "2026-09-29T11:00:00Z", detailsJson: "{\"reason\":\"Fiş bulundu\"}" },
      ]),
    });
    await renderCases(client, new Set(["reports.view"]));
    await press("Geçmiş");
    expect(client.listCaseActions).toHaveBeenCalledWith("c1");
    const region = document.querySelector('[aria-label="Vaka geçmişi"]')!;
    expect(region.textContent).toContain("Vaka açıldı");
    expect(region.textContent).toContain("Banka ile görüşüldü");
    expect(region.textContent).toContain("Fiş bulundu");
    expect(region.textContent).not.toContain("NoteAdded");
    expect(region.textContent).not.toContain("{");
  });

  it("adds a note without changing the status, only for a manager and never an empty one", async () => {
    const client = fakeClient();
    await renderCases(client, new Set(["reports.view"]));
    expect([...document.querySelectorAll("button")].map((button) => button.textContent)).not.toContain("Not ekle");

    await renderCases(client);
    await press("Not ekle");
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Önce bir not yazın.");
    expect(client.addCaseNote).not.toHaveBeenCalled();
    const input = document.getElementById("closing-case-note") as HTMLInputElement;
    await act(async () => {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(input, "Müşteri arandı");
      input.dispatchEvent(new Event("input", { bubbles: true }));
    });
    await press("Not ekle");
    expect(client.addCaseNote).toHaveBeenCalledWith("c1", "Müşteri arandı");
    expect(client.transitionCase).not.toHaveBeenCalled();
  });

  it("shows the server's Turkish refusal when the history cannot be read", async () => {
    const client = fakeClient({ listCaseActions: vi.fn().mockRejectedValue(new ManagementApiError(404, "NOT_FOUND", "Vaka bulunamadı.")) });
    await renderCases(client);
    await press("Geçmiş");
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Vaka bulunamadı.");
  });

  it("the client reads actions and posts notes on the case routes", async () => {
    const fetcher = vi.fn((_url: string, _init?: RequestInit) => Promise.resolve(new Response("[]", { status: 200 })));
    const api = createClosingClient(fetcher as unknown as typeof fetch);
    await api.listCaseActions("c1");
    expect(fetcher.mock.calls.at(-1)![0]).toBe("/api/v1/management/reconciliation/cases/c1/actions");
    await api.addCaseNote("c1", "  not  ");
    const [url, init] = fetcher.mock.calls.at(-1)!;
    expect(url).toBe("/api/v1/management/reconciliation/cases/c1/notes");
    expect(init?.method).toBe("POST");
    expect(JSON.parse(init!.body as string)).toEqual({ note: "not" });
  });
});
