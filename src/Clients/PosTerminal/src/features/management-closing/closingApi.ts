import type { BusinessDay, BusinessDayReport, CaseStatus, ManualConfirmation, ReconciliationCase, SettlementReport } from "./models";

export class ClosingApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

const fallbackByStatus = (status: number): string =>
  status === 401 ? "Oturum sona erdi; yeniden giriş yapın."
    : status === 403 ? "Bu işlem için yetkiniz yok."
    : status === 409 ? "Kayıt başka biri tarafından değiştirildi; yenileyip tekrar deneyin."
    : "İşlem tamamlanamadı.";

export interface ClosingClient {
  getDay: (date: string) => Promise<BusinessDay | null>;
  getFullReport: (date: string) => Promise<BusinessDayReport>;
  openDay: (date: string) => Promise<void>;
  closeDay: (date: string, cancelledItems: number, printFailures: number) => Promise<void>;
  getSettlement: (date: string) => Promise<SettlementReport>;
  listManualConfirmations: () => Promise<readonly ManualConfirmation[]>;
  scanPayments: () => Promise<void>;
  listCases: (status: CaseStatus) => Promise<readonly ReconciliationCase[]>;
  transitionCase: (caseId: string, newStatus: CaseStatus, expectedVersion: number, note: string) => Promise<void>;
}

export function createClosingClient(fetcher: typeof fetch = fetch): ClosingClient {
  const prefix = "/api/v1/management";

  async function call(path: string, init: RequestInit = {}): Promise<Response> {
    let response: Response;
    try {
      response = await fetcher(`${prefix}${path}`, {
        ...init,
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID(), ...(init.method === "POST" ? { "X-Idempotency-Key": crypto.randomUUID() } : {}) },
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new ClosingApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Bağlantıyı kontrol edip tekrar deneyin.");
    }
    if (response.ok) return response;
    const body = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
    // The server's own Turkish reason is shown as is; only a missing one falls back to a generic Turkish line.
    throw new ClosingApiError(response.status, body?.error?.code ?? "REQUEST_FAILED", body?.error?.message ?? fallbackByStatus(response.status));
  }

  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  const post = (path: string, body: unknown) => call(path, { method: "POST", body: JSON.stringify(body) }).then(() => undefined);

  return {
    async getDay(date) {
      try {
        return await json<BusinessDay>(`/reporting/business-day/${date}`);
      } catch (reason) {
        if (reason instanceof ClosingApiError && reason.status === 404) return null;
        throw reason;
      }
    },
    getFullReport: (date) => json(`/reporting/business-day/${date}/full-report`),
    openDay: (date) => post("/reporting/business-day/open", { businessDate: date }),
    closeDay: (date, cancelledItems, printFailures) => post(`/reporting/business-day/${date}/close`, { cancelledItems, printFailures }),
    getSettlement: (date) => json(`/payments/settlement-report?businessDate=${date}`),
    listManualConfirmations: () => json("/payments/manual-confirmations"),
    scanPayments: () => post("/payments/reconciliation-scan", {}),
    listCases: (status) => json(`/reconciliation/cases?status=${status}&limit=50`),
    transitionCase: (caseId, newStatus, expectedVersion, note) =>
      post(`/reconciliation/cases/${caseId}/transition`, { newStatus, expectedVersion, reasonOrNote: note.trim() || null }),
  };
}
