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

/** Sends a mistakenly sent check back to its table. Refused (with the server's Turkish reason) once money moved. */
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
    const payload = await response.json().catch(() => undefined) as { error?: { message?: string } } | undefined;
    throw new PendingChecksApiError(response.status, payload?.error?.message ?? "Hesap masaya geri gönderilemedi.");
  }
}
