import { notifySessionExpired } from "../../api";
import { RefundApiError, describeRefundFailure } from "../billing";

/** A check a waiter sent to the till (V1-ORD-006). It is tracked by its own number, never by the table. */
export interface PendingCheck {
  orderId: string;
  orderNumber: string;
  tableNumber: string;
  itemCount: number;
  total: number;
  createdAt: string;
  /** Null until the cashier opens the bill. */
  billId: string | null;
  /** Already collected on the bill; the cashier resumes a part-paid check where it stopped. */
  paidAmount: number;
  itemPreview: string | null;
  /** How many of the lines the cashier added without a kitchen ticket. */
  addedWithoutKitchenCount: number;
  /** Where the check came from; lets the till send a mistakenly sent check back. */
  tableId: string | null;
}

export class PendingChecksApiError extends Error {
  constructor(public readonly status: number, message: string) { super(message); }
}

export async function loadPendingChecks(terminalId: string, fetcher: typeof fetch = fetch): Promise<PendingCheck[]> {
  if (!terminalId.trim()) throw new Error("terminalId is required");
  let response: Response;
  try {
    response = await fetcher(`/api/v1/terminals/${encodeURIComponent(terminalId)}/orders/awaiting-payment`, {
      credentials: "same-origin",
      signal: AbortSignal.timeout(8_000),
    });
  } catch {
    throw new PendingChecksApiError(0, "Sunucuya ulaşılamadı. Bekleyen hesaplar okunamadı.");
  }
  if (!response.ok) {
    if (response.status === 401) notifySessionExpired();
    throw new PendingChecksApiError(
      response.status,
      response.status === 401 ? "Oturum sona erdi." : response.status === 403 ? "Bu listeyi görmek için yetkiniz yok." : "Bekleyen hesaplar okunamadı.",
    );
  }
  return response.json() as Promise<PendingCheck[]>;
}

/** Where the cashier collects a bill: the payment page, opened with the bill id. */
export function collectionHref(billId: string): string {
  return `/cashier/payments/split-payment/index.html?billId=${encodeURIComponent(billId)}`;
}

const recallFailureText: Record<string, string> = {
  TABLE_HAS_OPEN_CHECK: "Bu masada yeni bir hesap açık; önce onu kasaya gönderin.",
  CHECK_HAS_PAYMENT: "Bu hesapta tahsilat başlamış; masaya geri alınamaz.",
  CHECK_NOT_RECALLABLE: "Bu hesap artık kasa kuyruğunda değil; liste yenilendi.",
  INVALID_TABLE: "Hesabın masası bulunamadı; geri gönderilemedi.",
};

/** Sends a mistakenly sent check back to its table. Refused with a fixed sentence once money moved. */
export async function recallPendingCheck(terminalId: string, orderId: string, tableId: string, fetcher: typeof fetch = fetch): Promise<void> {
  let response: Response;
  try {
    response = await fetcher(`/api/v1/terminals/${encodeURIComponent(terminalId)}/orders/${encodeURIComponent(orderId)}/recall-from-cashier`, {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ tableId }),
      signal: AbortSignal.timeout(8_000),
    });
  } catch {
    throw new PendingChecksApiError(0, "Sunucuya ulaşılamadı. Hesap geri gönderilemedi.");
  }
  if (!response.ok) {
    if (response.status === 401) notifySessionExpired();
    const payload = await response.json().catch(() => undefined) as { error?: { code?: string } } | undefined;
    throw new PendingChecksApiError(response.status, recallFailureText[payload?.error?.code ?? ""] ?? "Hesap masaya geri gönderilemedi.");
  }
}

async function orderCall<T>(url: string, method: "GET" | "POST", body: unknown, fetcher: typeof fetch): Promise<T> {
  let response: Response;
  try {
    response = await fetcher(url, {
      method,
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: AbortSignal.timeout(8_000),
    });
  } catch {
    throw new PendingChecksApiError(0, describeRefundFailure(new RefundApiError(0, "NETWORK_UNAVAILABLE", "network")));
  }
  if (!response.ok) {
    if (response.status === 401) notifySessionExpired();
    const payload = await response.json().catch(() => undefined) as { error?: { code?: string } } | undefined;
    throw new PendingChecksApiError(response.status, describeRefundFailure(new RefundApiError(response.status, payload?.error?.code ?? "REQUEST_FAILED", "request failed")));
  }
  return response.json() as Promise<T>;
}

/** Takes back a whole order that never got paid (stock, waste, bills and table are settled by the server). The current row version is read first so a changed order is refused, not overwritten. */
export async function cancelPendingCheckOrder(terminalId: string, orderId: string, reason: string, idempotencyKey: string, fetcher: typeof fetch = fetch): Promise<void> {
  const base = `/api/v1/terminals/${encodeURIComponent(terminalId)}/orders/${encodeURIComponent(orderId)}`;
  const order = await orderCall<{ rowVersion: number }>(base, "GET", undefined, fetcher);
  await orderCall(`${base}/cancel`, "POST", { idempotencyKey, expectedRowVersion: order.rowVersion, reason }, fetcher);
}
