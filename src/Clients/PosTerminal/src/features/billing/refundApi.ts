export interface BillTenderLine {
  allocationId: string;
  paymentId: string;
  amount: number;
  paymentStatus: string;
}

export interface BillTenderSummary {
  allocatedTotal: number;
  remainingAmount: number;
  allocations: readonly BillTenderLine[];
  unsettledPayment?: unknown;
}

export interface RefundRequestRow {
  requestId: string;
  billId: string;
  paymentId: string;
  amount: number;
  method: string;
  reason: string;
  status: string;
  billCancelled?: boolean;
  requestedBy?: string;
  approvedFromAllowance?: boolean;
  items?: readonly RefundItemInput[];
}

export interface BillReceiptItem {
  billItemId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  amount: number;
  refundedQuantity: number;
}

export interface RefundItemInput {
  billItemId: string;
  quantity: number;
  restoreStock: boolean;
}

export interface RefundAllowance {
  hasOpenSession: boolean;
  allowance: number;
  used: number;
  remaining: number;
}

export class RefundApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

export interface BillingRefundClient {
  getTenders: () => Promise<BillTenderSummary>;
  listRequests: () => Promise<readonly RefundRequestRow[]>;
  cancelBill: (reason: string) => Promise<void>;
  requestRefund: (paymentId: string, reason: string, items?: readonly RefundItemInput[]) => Promise<RefundRequestRow>;
  getItems: () => Promise<readonly BillReceiptItem[]>;
  approve: (requestId: string) => Promise<RefundRequestRow>;
  reject: (requestId: string, note: string) => Promise<RefundRequestRow>;
  complete: (requestId: string, slipNumber: string) => Promise<RefundRequestRow>;
  getAllowance: () => Promise<RefundAllowance>;
  setAllowance: (cashierUserId: string, amount: number) => Promise<RefundAllowance>;
}

/** Fixed Turkish sentence per outcome; the server body and HTTP status never reach the screen. */
export function describeRefundFailure(error: unknown): string {
  const status = error instanceof RefundApiError ? error.status : -1;
  const code = error instanceof RefundApiError ? error.code : "";
  if (status === 0) return "Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.";
  if (status === 401) return "Oturum süresi doldu. Yeniden giriş yapın.";
  if (status === 403) return code === "SELF_DECISION"
    ? "Kendi açtığınız iade isteğini siz onaylayamazsınız."
    : "Bu işlem için yetkiniz yok.";
  if (status === 400) return "Gerekçe veya fiş numarası eksik ya da geçersiz.";
  if (status === 409) {
    if (code === "NO_OPEN_CASH_SESSION") return "Açık kasa oturumu yok. Önce kasayı açın.";
    if (code === "REFUND_ITEM_QUANTITY") return "Seçilen adet iade edilebilir adedi aşıyor. Güncel veriyi alıp yeniden deneyin.";
    if (code === "REFUND_AMOUNT_EXCEEDS_PAYMENT") return "İade tutarı seçilen ödemenin kalan tutarını aşıyor. Başka ödeme seçin.";
    if (code === "ACCOUNT_REFUND_UNSUPPORTED") return "Cari hesaba yazılan ödeme buradan iade edilemez.";
    return "Hesap veya iade isteği değişmiş. Güncel veriyi alıp yeniden deneyin.";
  }
  return "İşlem tamamlanamadı. Yeniden deneyin.";
}

/** Same rule as above for the manager granting a shift allowance. */
export function describeAllowanceFailure(error: unknown): string {
  const status = error instanceof RefundApiError ? error.status : -1;
  const code = error instanceof RefundApiError ? error.code : "";
  if (status === 403 && code === "SELF_DECISION") return "Kendinize iade bütçesi veremezsiniz.";
  if (status === 400) return "Tutar geçersiz ya da kasiyerin bu vardiyada harcadığından düşük.";
  if (status === 409) return "Kasiyerin açık kasa oturumu yok.";
  return describeRefundFailure(error);
}

export function createBillingRefundClient(terminalId: string, billId: string, fetcher: typeof fetch = fetch): BillingRefundClient {
  if (!terminalId.trim() || !billId.trim()) throw new Error("terminalId and billId are required");
  const base = `/api/v1/terminals/${encodeURIComponent(terminalId)}/billing`;
  const billBase = `${base}/bills/${encodeURIComponent(billId)}`;
  const refundsBase = `${base}/refund-requests`;

  // A retry of the same request after a lost response reuses its key; the key is dropped once the server answered.
  let pendingRefund: { signature: string; key: string } | undefined;

  async function call<T>(url: string, method = "GET", body?: unknown, headers?: Record<string, string>): Promise<T> {
    let response: Response;
    try {
      response = await fetcher(url, {
        method,
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID(), ...headers },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new RefundApiError(0, "NETWORK_UNAVAILABLE", "network");
    }
    if (!response.ok) {
      const payload = await response.json().catch(() => undefined) as { error?: { code?: string } } | undefined;
      throw new RefundApiError(response.status, payload?.error?.code ?? "REQUEST_FAILED", "request failed");
    }
    return response.json().catch(() => undefined) as Promise<T>;
  }

  return {
    getTenders: () => call<BillTenderSummary>(`${billBase}/tenders/`),
    listRequests: async () => (await call<readonly RefundRequestRow[]>(`${refundsBase}?billId=${encodeURIComponent(billId)}`))
      .filter((row) => row.billId === billId),
    cancelBill: async (reason) => { await call(`${billBase}/cancel`, "POST", { reason }); },
    requestRefund: async (paymentId, reason, items) => {
      const body = { paymentId, reason, items };
      const signature = JSON.stringify(body);
      if (pendingRefund?.signature !== signature) pendingRefund = { signature, key: crypto.randomUUID() };
      const result = await call<RefundRequestRow>(`${billBase}/refund-requests`, "POST", body, { "X-Idempotency-Key": pendingRefund.key });
      pendingRefund = undefined;
      return result;
    },
    getItems: async () => (await call<{ items: readonly BillReceiptItem[] }>(`${billBase}/receipt`)).items,
    approve: (requestId) => call(`${refundsBase}/${encodeURIComponent(requestId)}/approve`, "POST"),
    reject: (requestId, note) => call(`${refundsBase}/${encodeURIComponent(requestId)}/reject`, "POST", { note }),
    complete: (requestId, slipNumber) => call(`${refundsBase}/${encodeURIComponent(requestId)}/complete`, "POST", { slipNumber, note: null }),
    getAllowance: () => call<RefundAllowance>(`${base}/refund-allowance`),
    setAllowance: (cashierUserId, amount) => call<RefundAllowance>(`${base}/refund-allowance`, "PUT", { cashierUserId, amount }),
  };
}
