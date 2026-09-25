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
