import type {
  AllocationVersionRequest,
  AmountSplitTarget,
  BillSplitDesign,
  ItemSplitTarget,
  SaveSplitRequest,
  SplitOwnerOption,
  SplitOwnerRequest,
} from "./models";
import { mergeChecksText } from "../../strings";

interface ServerOwnerOption {
  kind: string;
  id: string;
  label: string;
  secondaryLabel: string | null;
}

export class BillingSplitApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

const splitFailureTexts: Record<string, string> = {
  UNAUTHORIZED: "Oturum süresi doldu. Yeniden giriş yapın.",
  FORBIDDEN: "Bu işlem için yetkiniz yok.",
  NOT_FOUND: "İstenen kayıt bulunamadı. Hesabı yenileyin.",
  GRANT_DENIED: "İndirim isteği reddedildi.",
  UNSUPPORTED_ORDER_STATE: "Bu sipariş henüz mutfağa gönderilmedi ya da kapanmış; önce siparişi gönderin.",
  CONCURRENT_MODIFICATION: "Hesap bölme tasarımı başka bir işlem tarafından değiştirildi. Hesabı yenileyin.",
  UNSUPPORTED_BILL_STATE: "Bu hesap durumunda değişiklik yapılamaz.",
  DISCOUNT_BELOW_COLLECTED: "Bu indirim, hesaptan şimdiye kadar alınan tutarın altına iniyor; uygulanamaz.",
  ITEM_SPLIT_ON_ADJUSTED_BILL: "Ek ücret veya bahşiş eklenmiş hesap ürün bazında bölünemez; tutar, kişi ya da serbest bölme kullanın.",
  BILL_HAS_PAYMENT: "Bu hesapta ödeme var; iptal etmeden önce ödeme iade edilmeli.",
  DISCOUNT_REASON_INVALID: "İndirim gerekçesi eksik ya da geçersiz.",
  DISCOUNT_TYPE_INVALID: "İndirim türü geçersiz.",
  DISCOUNT_VALUE_INVALID: "İndirim değeri geçersiz.",
  IDEMPOTENCY_KEY_INVALID: "İşlem kimliği geçersiz. Sayfayı yenileyip yeniden deneyin.",
  NOTES_TOO_LONG: "Not çok uzun.",
  TIP_AMOUNT_INVALID: "Bahşiş tutarı geçersiz.",
  MERGE_NEEDS_TWO_ORDERS: "Birleştirmek için en az iki sipariş seçin.",
  MERGE_TOO_MANY_ORDERS: "Tek seferde en çok 20 sipariş birleştirilebilir.",
  MERGE_DIFFERENT_TABLES: "Yalnız aynı masadaki siparişler birleştirilebilir.",
  MERGE_ORDER_SOURCE: "QR ve çevrimiçi siparişler birleştirilemez.",
  MERGE_ORDER_ALREADY_BILLED: "Seçilen siparişlerden birinin zaten açık bir hesabı var; önce o hesabı kapatın ya da iptal edin.",
  MERGE_MIXED_SENT: "Seçilen siparişlerin bir kısmı kasaya gönderilmiş, bir kısmı masada; hepsi aynı yerde olmalı.",
  MERGE_ORDER_EMPTY: "Seçilen siparişlerden birinde hesaplanacak ürün yok.",
  IDEMPOTENCY_KEY_REQUIRED: "İşlem kimliği eksik. Sayfayı yenileyip yeniden deneyin.",
  IDEMPOTENCY_KEY_REUSED: "Bu işlem kimliği başka bir seçim için kullanılmış. Seçimi yenileyip yeniden deneyin.",
};

/** Fixed Turkish sentence per server error code; the server body never reaches the screen. */
export function describeSplitFailure(status: number, code: string): string {
  const known = splitFailureTexts[code];
  if (known) return known;
  if (status === 0) return "Sunucuya ulaşılamadı.";
  if (status === 401) return splitFailureTexts.UNAUTHORIZED;
  if (status === 403) return splitFailureTexts.FORBIDDEN;
  if (status === 404) return splitFailureTexts.NOT_FOUND;
  if (status === 409) return "Hesap başka bir yerde değişti. Hesabı yenileyip yeniden deneyin.";
  if (status === 400) return "Girilen değerler geçersiz. Kontrol edip yeniden deneyin.";
  return "Hesap dağıtımı kaydedilemedi.";
}

export interface BillingSplitClient {
  get: () => Promise<BillSplitDesign>;
  getOwners: () => Promise<SplitOwnerOption[]>;
  save: (request: SaveSplitRequest, design: BillSplitDesign) => Promise<BillSplitDesign>;
  clear: (design: BillSplitDesign) => Promise<BillSplitDesign>;
  createFromOrder: (orderId: string) => Promise<BillSplitDesign>;
}

function versions(design: BillSplitDesign): AllocationVersionRequest[] {
  return design.allocations.map((allocation) => ({ allocationId: allocation.allocationId, rowVersion: allocation.rowVersion }));
}

export function createBillingSplitClient(terminalId: string, billId: string, fetcher: typeof fetch = fetch): BillingSplitClient {
  if (!terminalId.trim() || !billId.trim()) throw new Error("terminalId and billId are required");
  const prefix = `/api/v1/terminals/${encodeURIComponent(terminalId)}/billing/bills/${encodeURIComponent(billId)}/split-design`;

  async function call(path = "", method = "GET", body?: unknown) {
    let response: Response;
    try {
      response = await fetcher(`${prefix}${path}`, {
        method,
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID() },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new BillingSplitApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Dağıtım taslağı korundu.");
    }
    if (!response.ok) {
      const payload = await response.json().catch(() => undefined) as { error?: { code?: string } } | undefined;
      const code = payload?.error?.code ?? "REQUEST_FAILED";
      throw new BillingSplitApiError(response.status, code, describeSplitFailure(response.status, code));
    }
    return response.json() as Promise<BillSplitDesign>;
  }

  return {
    get: () => call(),
    getOwners: async () => {
      const raw = (await call("/owners")) as unknown as readonly ServerOwnerOption[];
      return raw.map((owner) => ({
        kind: owner.kind === "Seat" ? "Seat" : "Person",
        ownerId: owner.id,
        label: owner.label,
        secondaryLabel: owner.secondaryLabel ?? undefined,
      }));
    },
    save: (request, design) => {
      const common = { expectedBillRowVersion: design.billRowVersion, expectedAllocations: versions(design) };
      if (request.mode === "EqualByPerson") return call("/equal", "PUT", { ...common, owners: request.owners satisfies readonly SplitOwnerRequest[] });
      if (request.mode === "ByItem") return call("/items", "PUT", { ...common, targets: request.targets satisfies readonly ItemSplitTarget[] });
      return call("/amounts", "PUT", { ...common, targets: request.targets satisfies readonly AmountSplitTarget[] });
    },
    clear: (design) => call("/clear", "POST", { expectedBillRowVersion: design.billRowVersion, expectedAllocations: versions(design) }),
    createFromOrder: (orderId: string) => createBillFromOrder(terminalId, orderId, fetcher),
  };
}

/** Opens one bill from several open orders of one table. The same key replays the same bill, so a retry after a lost answer never opens a second one. */
export async function mergeOrdersIntoBill(
  terminalId: string,
  orderIds: readonly string[],
  idempotencyKey: string,
  fetcher: typeof fetch = fetch
): Promise<BillSplitDesign> {
  let response: Response;
  try {
    response = await fetcher(`/api/v1/terminals/${encodeURIComponent(terminalId)}/billing/bills/merge`, {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID() },
      body: JSON.stringify({ orderIds, idempotencyKey }),
      signal: AbortSignal.timeout(8_000),
    });
  } catch {
    throw new BillingSplitApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Hesaplar birleştirilemedi.");
  }
  if (!response.ok) {
    const payload = await response.json().catch(() => undefined) as { error?: { code?: string } } | undefined;
    const code = payload?.error?.code ?? "REQUEST_FAILED";
    throw new BillingSplitApiError(response.status, code, response.status >= 500 && !(code in splitFailureTexts) ? mergeChecksText.failed : describeSplitFailure(response.status, code));
  }
  return response.json() as Promise<BillSplitDesign>;
}

export async function createBillFromOrder(
  terminalId: string,
  orderId: string,
  fetcher: typeof fetch = fetch
): Promise<BillSplitDesign> {
  if (!terminalId.trim() || !orderId.trim()) throw new Error("terminalId and orderId are required");
  const url = `/api/v1/terminals/${encodeURIComponent(terminalId)}/billing/bills/from-order/${encodeURIComponent(orderId)}`;

  let response: Response;
  try {
    response = await fetcher(url, {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID() },
      signal: AbortSignal.timeout(8_000),
    });
  } catch {
    throw new BillingSplitApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Adisyon oluşturulamadı.");
  }
  if (!response.ok) {
    const payload = await response.json().catch(() => undefined) as { error?: { code?: string } } | undefined;
    const code = payload?.error?.code ?? "REQUEST_FAILED";
    throw new BillingSplitApiError(response.status, code, describeSplitFailure(response.status, code));
  }
  return response.json() as Promise<BillSplitDesign>;
}
