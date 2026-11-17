import { notifySessionExpired } from "../../api";
import { recentSalesText } from "../../strings";
import { collectionHref } from "../pending-checks/pendingChecksApi";

export interface RecentSale {
  billId: string;
  /** Null for a sale without a table (Hızlı satış). */
  tableNumber: string | null;
  closedAt: string;
  amount: number;
  methods: readonly string[];
}

export interface RecentSales {
  /** False when the cashier has no open drawer session: the list is empty because of that, not because nothing was sold. */
  hasOpenSession: boolean;
  sales: readonly RecentSale[];
}

export class RecentSalesApiError extends Error {}

export async function loadRecentSales(terminalId: string, fetcher: typeof fetch = fetch): Promise<RecentSales> {
  let response: Response;
  try {
    response = await fetcher(`/api/v1/terminals/${encodeURIComponent(terminalId)}/billing/recent-sales`, {
      credentials: "same-origin",
      signal: AbortSignal.timeout(8_000),
    });
  } catch {
    throw new RecentSalesApiError(recentSalesText.unreachable);
  }
  if (!response.ok) {
    if (response.status === 401) notifySessionExpired();
    throw new RecentSalesApiError(
      response.status === 403 ? recentSalesText.forbidden : recentSalesText.failed,
    );
  }
  return response.json() as Promise<RecentSales>;
}

/** The payment page with its refund section already open; `from` makes its way back lead here. */
export const refundHref = (billId: string) => `${collectionHref(billId)}&open=refund&from=recent-sales`;

export const methodLabel = (method: string) => recentSalesText.methods[method] ?? recentSalesText.unknownMethod;
