/** V12-OUI-001: the operations queue for QR orders waiting for staff and online-channel orders. */

export type OnlineOrderSource = "Qr" | "Online";

export interface OnlineOperationsOrder {
  orderId: string;
  source: OnlineOrderSource;
  status: string;
  orderNumber: string;
  tableNumber: string | null;
  displayCode: string | null;
  total: number;
  itemCount: number;
  createdAt: string;
  rowVersion: number;
}

export interface OnlineOperationsProblem {
  inboxId: string;
  externalOrderId: string;
  providerStatus: string;
  outcome: string;
  reason: string | null;
  attempts: number;
  receivedAt: string;
}

export interface OnlineOperationsRetries {
  pendingProviderEvents: number;
  catalogPublicationsRetrying: number;
  availabilityDivergences: number;
}

export interface OnlineOperationsQueue {
  orders: OnlineOperationsOrder[];
  problems: OnlineOperationsProblem[];
  retries: OnlineOperationsRetries;
}

export type SourceFilter = "all" | "qr" | "online";

export type CancellationReason = "Closed" | "ItemUnavailable" | "TooBusy";

export class OnlineOperationsApiError extends Error {
  constructor(public readonly status: number, message: string) { super(message); }
}

const base = (terminalId: string) => `/api/v1/terminals/${encodeURIComponent(terminalId)}`;

async function call(url: string, init: RequestInit | undefined, fallback: string, fetcher: typeof fetch): Promise<Response> {
  let response: Response;
  try {
    response = await fetcher(url, { credentials: "same-origin", signal: AbortSignal.timeout(8_000), ...init });
  } catch {
    throw new OnlineOperationsApiError(0, "Sunucuya ulaşılamadı. Tekrar deneyin.");
  }
  if (!response.ok) {
    // The server's own Turkish message when it sends one; never a raw status code or English text.
    const payload = await response.json().catch(() => undefined) as { error?: { message?: string } } | undefined;
    const message = response.status === 401 ? "Oturum sona erdi." : response.status === 403 ? "Bu işlem için yetkiniz yok." : payload?.error?.message ?? fallback;
    throw new OnlineOperationsApiError(response.status, message);
  }
  return response;
}

const post = (body: unknown): RequestInit => ({
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
});

export async function loadOnlineOperations(terminalId: string, source: SourceFilter, fetcher: typeof fetch = fetch): Promise<OnlineOperationsQueue> {
  if (!terminalId.trim()) throw new Error("terminalId is required");
  const response = await call(`${base(terminalId)}/online-operations?source=${source}`, undefined, "Online siparişler okunamadı.", fetcher);
  return response.json() as Promise<OnlineOperationsQueue>;
}

/** A QR order is confirmed through the same endpoint the rest of the till uses (PendingOrderConfirmationStore). */
export async function acceptQrOrder(terminalId: string, order: OnlineOperationsOrder, fetcher: typeof fetch = fetch): Promise<void> {
  await call(`${base(terminalId)}/orders/${encodeURIComponent(order.orderId)}/accept`,
    post({ expectedRowVersion: order.rowVersion, notes: null }), "QR siparişi onaylanamadı.", fetcher);
}

export async function rejectQrOrder(terminalId: string, order: OnlineOperationsOrder, reason: string, fetcher: typeof fetch = fetch): Promise<void> {
  await call(`${base(terminalId)}/orders/${encodeURIComponent(order.orderId)}/reject`,
    post({ expectedRowVersion: order.rowVersion, reason }), "QR siparişi reddedilemedi.", fetcher);
}

export async function handOverOnlineOrder(terminalId: string, order: OnlineOperationsOrder, fetcher: typeof fetch = fetch): Promise<void> {
  await call(`${base(terminalId)}/online-operations/orders/${encodeURIComponent(order.orderId)}/hand-over`,
    post({ expectedRowVersion: order.rowVersion }), "Sipariş kuryeye teslim edilemedi.", fetcher);
}

export async function cancelOnlineOrder(terminalId: string, order: OnlineOperationsOrder, reason: CancellationReason, fetcher: typeof fetch = fetch): Promise<void> {
  await call(`${base(terminalId)}/online-operations/orders/${encodeURIComponent(order.orderId)}/cancel`,
    post({ expectedRowVersion: order.rowVersion, reason }), "Sipariş iptal edilemedi.", fetcher);
}

// Every server value shown to a person goes through these dictionaries (docs/UI_STYLE_GUIDE.md);
// an unknown value is shown as the Turkish "other" label, never as the raw English code.
const statusLabels: Record<string, string> = {
  PendingConfirmation: "Onay bekliyor",
  Accepted: "Kabul edildi",
  Preparing: "Hazırlanıyor",
  Ready: "Hazır",
};
const sourceLabels: Record<OnlineOrderSource, string> = { Qr: "QR", Online: "Yemeksepeti" };
const outcomeLabels: Record<string, string> = {
  Rejected: "Sipariş alınamadı",
  Diverged: "Sağlayıcı ile uyuşmazlık",
  Failed: "İşlenemedi, durduruldu",
  Retrying: "Yeniden deneniyor",
};
const reasonLabels: Record<string, string> = {
  UnmappedSku: "Eşlenmemiş ürün",
  AmbiguousSku: "Belirsiz ürün eşlemesi",
  ProductInactive: "Ürün satışta değil",
  ProductRequiresModifierChoice: "Ürün seçenek gerektiriyor",
  ProductHasNoTaxProfile: "Ürünün vergi tanımı yok",
  UnsupportedPricingType: "Desteklenmeyen fiyat türü",
  InvalidQuantity: "Geçersiz adet",
  InvalidPrice: "Geçersiz fiyat",
  MalformedPayload: "Okunamayan sipariş verisi",
  EmptyOrder: "Boş sipariş",
  OutOfStock: "Stok yetersiz",
  NotConfigured: "Stok tanımı eksik",
  CancelledAfterHandover: "Teslimden sonra iptal edildi",
  UnsupportedTransportType: "Desteklenmeyen teslim türü",
  UnsupportedItemStatus: "Değiştirilmiş veya desteklenmeyen kalem",
};
export const cancellationReasonLabels: Record<CancellationReason, string> = {
  Closed: "Restoran kapalı",
  ItemUnavailable: "Ürün kalmadı",
  TooBusy: "Çok yoğun",
};

export const statusLabel = (status: string) => statusLabels[status] ?? "Diğer";
export const sourceLabel = (source: OnlineOrderSource) => sourceLabels[source] ?? "Diğer";
export const outcomeLabel = (outcome: string) => outcomeLabels[outcome] ?? "Diğer";
export const reasonLabel = (reason: string | null) => (reason === null ? "Ayrıntı yok" : reasonLabels[reason] ?? "Diğer");
