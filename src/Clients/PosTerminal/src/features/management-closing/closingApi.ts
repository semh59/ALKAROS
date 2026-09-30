import type { BusinessDay, BusinessDayReport, CaseStatus, ManualConfirmation, ReconciliationCase, SettlementReport } from "./models";
import { ManagementApiError, createRequester } from "../management/http";

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
  const call = createRequester(fetcher, { idempotentPosts: true });

  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  const post = (path: string, body: unknown) => call(path, { method: "POST", body: JSON.stringify(body) }).then(() => undefined);

  return {
    async getDay(date) {
      try {
        return await json<BusinessDay>(`/reporting/business-day/${date}`);
      } catch (reason) {
        if (reason instanceof ManagementApiError && reason.status === 404) return null;
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
