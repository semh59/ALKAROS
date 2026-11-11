// @vitest-environment jsdom

import { act, type ReactElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createClosingClient, type ClosingClient } from "./closingApi";
import { ClosingSection } from "./index";
import type { BusinessDay, ClosePreflight } from "./models";
import { ManagementApiError } from "../management/http";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const openDay: BusinessDay = {
  businessDayId: "d1", businessDate: "2026-09-29", openedAt: "2026-09-29T06:00:00Z", closedAt: null, status: "Open",
  totalRevenue: 1250, totalOrdersCount: 14, totalCancelledItemsCount: 0, totalPrintFailuresCount: 0,
  totalTipsAmount: 0, openCashSessionsCount: 0, openBillsCount: 0, unsettledPaymentsCount: 0, openItemsAcknowledged: false,
  tableBreakdown: [], cashReconciliation: null,
};

const cleanPreflight: ClosePreflight = {
  businessDate: "2026-09-29", revenue: 1250, tips: 80, orders: 14, cancelledItems: 2, printFailures: 1,
  openCashSessions: 0, openBills: 0, unsettledPayments: 0, requiresAcknowledgement: false,
};
const openItemsPreflight: ClosePreflight = { ...cleanPreflight, openCashSessions: 1, openBills: 2, unsettledPayments: 3, requiresAcknowledgement: true };

function fakeClient(overrides: Partial<ClosingClient> = {}): ClosingClient {
  return {
    getDay: vi.fn().mockResolvedValue(openDay),
    getFullReport: vi.fn().mockResolvedValue({ businessDay: openDay, waiterSummaries: [], printSummaries: [] }),
    openDay: vi.fn().mockResolvedValue(undefined),
    getClosePreflight: vi.fn().mockResolvedValue(cleanPreflight),
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

  it("the note is cleared after a successful transition and kept when the transition fails", async () => {
    const transitionCase = vi.fn().mockRejectedValueOnce(new ManagementApiError(409, "CONCURRENT_UPDATE", "Vaka başka bir kullanıcı tarafından değiştirildi.")).mockResolvedValue(undefined);
    await render(<ClosingSection client={fakeClient({ transitionCase })} capabilities={new Set(["reports.view", "reconciliation.manage"])} />);
    const note = document.getElementById("closing-case-note") as HTMLInputElement;
    await act(async () => {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(note, "Slip kontrol edildi");
      note.dispatchEvent(new Event("input", { bubbles: true }));
    });
    const transition = () => [...document.querySelectorAll("button")].find((candidate) => candidate.textContent === "İncelemeye al")!;
    await act(async () => transition().click());
    expect(transitionCase).toHaveBeenCalledTimes(1);
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Vaka başka bir kullanıcı tarafından değiştirildi.");
    expect((document.getElementById("closing-case-note") as HTMLInputElement).value).toBe("Slip kontrol edildi");
    await act(async () => transition().click());
    expect(transitionCase).toHaveBeenCalledTimes(2);
    expect((document.getElementById("closing-case-note") as HTMLInputElement).value).toBe("");
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
        { actionId: "a1", actionType: "Created", performedBy: "00000000-0000-0000-0000-000000000001", performedByName: null, performedAt: "2026-09-29T09:00:00Z", detailsJson: null },
        { actionId: "a2", actionType: "NoteAdded", performedBy: "u1", performedByName: "Ayşe Yılmaz", performedAt: "2026-09-29T10:00:00Z", detailsJson: "{\"note\":\"Banka ile görüşüldü\"}" },
        { actionId: "a4", actionType: "Resolved", performedBy: "00000000-0000-0000-0000-000000000001", performedByName: "Ali Demir", performedAt: "2026-09-29T12:00:00Z", detailsJson: null },
        { actionId: "a3", actionType: "StatusChanged", performedBy: "u2", performedByName: null, performedAt: "2026-09-29T11:00:00Z", detailsJson: "{\"reason\":\"Fiş bulundu\"}" },
      ]),
    });
    await renderCases(client, new Set(["reports.view"]));
    await press("Geçmiş");
    expect(client.listCaseActions).toHaveBeenCalledWith("c1");
    const region = document.querySelector('[aria-label="Vaka geçmişi"]')!;
    expect(region.textContent).toContain("Vaka açıldı");
    expect(region.textContent).toContain("Banka ile görüşüldü");
    expect(region.textContent).toContain("Fiş bulundu");
    expect(region.textContent).toContain("İşlemi yapan");
    const rows = [...region.querySelectorAll("tbody tr")];
    const cells = (index: number) => [...rows[index]!.querySelectorAll("td")].map((cell) => cell.textContent);
    const headers = [...region.querySelectorAll("thead th")].map((cell) => cell.textContent);
    expect(headers[2]).toBe("İşlemi yapan");
    expect(cells(0)[0]).toBe("Vaka açıldı");
    expect(cells(0)[2]).toBe("Sistem");
    expect(cells(1)[2]).toBe("Ayşe Yılmaz");
    expect(cells(2)[2]).toBe("Ali Demir");
    expect(cells(3)[2]).toBe("Bilinmiyor");
    expect(region.textContent).not.toContain("u2");
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

  const closeButton = () => [...document.querySelectorAll("button")].find((candidate) => candidate.textContent === "İş gününü kapat")!;
  const closeCaps = new Set(["reports.view", "reports.close-day"]);

  it("has no hand-typed count fields and shows the server's own preflight figures with tips on their own line", async () => {
    await render(<ClosingSection client={fakeClient()} capabilities={closeCaps} />);
    expect(document.querySelectorAll('input[type="number"]')).toHaveLength(0);
    const summary = document.querySelector('[aria-label="Kapanış özeti"]')!.textContent!;
    expect(summary).toContain("Ciro (bahşiş hariç)₺1.250,00");
    expect(summary).toContain("Bahşiş₺80,00");
    expect(summary).toContain("İptal edilen kalem2");
  });

  it("closes a clean day without a warning and sends the acknowledgement off", async () => {
    const client = fakeClient();
    await render(<ClosingSection client={client} capabilities={closeCaps} />);
    expect(document.querySelector('input[type="checkbox"]')).toBeNull();
    await act(async () => closeButton().click());
    expect(client.closeDay).toHaveBeenCalledWith(expect.any(String), false);
  });

  it("keeps the close off until open items are knowingly accepted, then sends the acknowledgement", async () => {
    const client = fakeClient({ getClosePreflight: vi.fn().mockResolvedValue(openItemsPreflight) });
    await render(<ClosingSection client={client} capabilities={closeCaps} />);
    const warning = document.querySelector('[role="alert"]')!.textContent!;
    expect(warning).toContain("İşletmede açık işlem var");
    expect(document.body.textContent).toContain("Açık hesap veya sipariş2");
    expect(document.body.textContent).toContain("Sonucu belirsiz ödeme3");
    expect(closeButton().disabled).toBe(true);
    await act(async () => closeButton().closest("form")!.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })));
    expect(client.closeDay).not.toHaveBeenCalled();

    await act(async () => (document.querySelector('input[type="checkbox"]') as HTMLInputElement).click());
    expect(closeButton().disabled).toBe(false);
    await act(async () => closeButton().click());
    expect(client.closeDay).toHaveBeenCalledWith(expect.any(String), true);
  });

  it("asks for the acknowledgement again after a close: the tick belongs to one preflight only", async () => {
    const client = fakeClient({ getClosePreflight: vi.fn().mockResolvedValue(openItemsPreflight) });
    await render(<ClosingSection client={client} capabilities={closeCaps} />);
    const box = () => document.querySelector('input[type="checkbox"]') as HTMLInputElement;
    await act(async () => box().click());
    expect(box().checked).toBe(true);

    await act(async () => closeButton().click());

    expect(client.closeDay).toHaveBeenCalledTimes(1);
    expect(box().checked).toBe(false);
    expect(closeButton().disabled).toBe(true);
  });

  it("cannot close while the preflight is missing: a failed read keeps the button off and offers a retry", async () => {
    const getClosePreflight = vi.fn().mockRejectedValueOnce(new ManagementApiError(500, "X", "Özet şu an hazırlanamıyor.")).mockResolvedValue(cleanPreflight);
    const client = fakeClient({ getClosePreflight });
    await render(<ClosingSection client={client} capabilities={closeCaps} />);
    expect(document.body.textContent).toContain("Özet şu an hazırlanamıyor.");
    expect(closeButton().disabled).toBe(true);
    await press("Tekrar dene");
    expect(closeButton().disabled).toBe(false);
  });

  it("reads the preflight again when the server refuses the close, so new open items show the warning", async () => {
    const getClosePreflight = vi.fn().mockResolvedValueOnce(cleanPreflight).mockResolvedValue(openItemsPreflight);
    const client = fakeClient({ getClosePreflight, closeDay: vi.fn().mockRejectedValue(new ManagementApiError(409, "BUSINESS_DAY_OPEN_ITEMS_NOT_ACKNOWLEDGED", "Açık işlem var.")) });
    await render(<ClosingSection client={client} capabilities={closeCaps} />);

    await act(async () => closeButton().click());

    expect(getClosePreflight).toHaveBeenCalledTimes(2);
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("Açık işlem var.");
    expect(document.querySelector('input[type="checkbox"]')).not.toBeNull();
  });

  it("shows a failed report once, on the waiter panel, not on every closed-day panel", async () => {
    const closed: BusinessDay = { ...openDay, status: "Closed", closedAt: "2026-09-30T05:00:00Z" };
    const client = fakeClient({ getDay: vi.fn().mockResolvedValue(closed), getFullReport: vi.fn().mockRejectedValue(new ManagementApiError(500, "X", "Döküm hazırlanamadı.")) });
    await render(<ClosingSection client={client} capabilities={closeCaps} />);

    expect(document.body.textContent!.split("Döküm hazırlanamadı.").length - 1).toBe(1);
  });

  it("does not read the preflight for a view-only session", async () => {
    const client = fakeClient();
    await render(<ClosingSection client={client} capabilities={new Set(["reports.view"])} />);
    expect(client.getClosePreflight).not.toHaveBeenCalled();
  });

  it("a closed day shows its stored tips, open-item counts, table breakdown, cash reconciliation and print summary", async () => {
    const closed: BusinessDay = {
      ...openDay, status: "Closed", closedAt: "2026-09-30T05:00:00Z", totalTipsAmount: 80, openBillsCount: 2, openItemsAcknowledged: true,
      tableBreakdown: [{ tableId: "aaaaaaaa-1111", tableNumber: "A1", orders: 3, sales: 450 }, { tableId: "bbbbbbbb-2222", tableNumber: null, orders: 1, sales: 70 }],
      cashReconciliation: { sessionsClosed: 2, expected: 1000, counted: 990, difference: -10, managerApprovedCloses: 1, selfReconciled: 0 },
    };
    const client = fakeClient({
      getDay: vi.fn().mockResolvedValue(closed),
      getFullReport: vi.fn().mockResolvedValue({
        businessDay: closed, waiterSummaries: [],
        printSummaries: [{ stationName: "Mutfak", totalPrintJobs: 10, failedPrintJobs: 2, recoveredPrintJobs: 1 }],
      }),
    });
    await render(<ClosingSection client={client} capabilities={closeCaps} />);
    const text = document.body.textContent!;
    expect(text).toContain("Bahşiş₺80,00");
    expect(text).toContain("Açık hesap veya sipariş2");
    expect(text).toContain("Açık işlemler bilerek kapatıldıEvet");
    expect(text).toContain("A1");
    expect(text).toContain("Numara kayıtlı değil");
    expect(text).not.toContain("aaaaaaaa");
    expect(text).toContain("Kapanan oturum2");
    expect(text).toContain("Yönetici onaylı kapanış1");
    expect(text).toContain("Mutfak");
    expect(buttons()).not.toContain("İş gününü kapat");
    expect(client.getClosePreflight).not.toHaveBeenCalled();
  });

  it("the client reads the preflight and posts only the acknowledgement", async () => {
    const fetcher = vi.fn((_url: string, _init?: RequestInit) => Promise.resolve(new Response("{}", { status: 200 })));
    const api = createClosingClient(fetcher as unknown as typeof fetch);
    await api.getClosePreflight("2026-09-29");
    expect(fetcher.mock.calls.at(-1)![0]).toBe("/api/v1/management/reporting/business-day/2026-09-29/close-preflight");
    await api.closeDay("2026-09-29", true);
    const [url, init] = fetcher.mock.calls.at(-1)!;
    expect(url).toBe("/api/v1/management/reporting/business-day/2026-09-29/close");
    expect(JSON.parse(init!.body as string)).toEqual({ acknowledgeOpenItems: true });
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
