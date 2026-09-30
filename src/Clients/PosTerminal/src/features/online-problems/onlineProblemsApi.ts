/** V12-OUI-006: the online food screen's Problems tab — open online order cases and their safe next action. */

export interface OnlineProblem {
  caseId: string;
  kind: string;
  provider: string | null;
  externalOrderId: string | null;
  amount: number;
  severity: string;
  status: string;
  openedAt: string;
  rowVersion: number;
  nextAction: string;
  canRetry: boolean;
}

export class OnlineProblemsApiError extends Error {}

// Every server value shown to a person goes through these dictionaries; an unknown one is never shown raw.
const kindLabels: Record<string, string> = {
  ProviderAcceptedLocallyRefused: "Platformun kabul ettiği sipariş burada oluşturulamadı",
  ProviderEventFailed: "Platformdan gelen olay işlenemedi",
  LocallyAcceptedProviderUnknown: "Sipariş durumu platforma bildirilemedi",
  CancelledAfterHandover: "Sipariş teslimden sonra platformda iptal edildi",
  AvailabilityNotDelivered: "Stok durumu platforma bildirilemedi",
  ProviderTotalMismatch: "Platform toplamı sipariş toplamından farklı",
  ProviderStatusUnknown: "Platformdan bilinmeyen bir durum geldi",
  ProviderPriceMismatch: "Platform fiyatı katalog fiyatından farklı",
  ProviderPollingFailing: "Siparişleri çekme sürekli hata veriyor",
  NotHandedOver: "Sipariş saatlerdir teslim edilmedi ya da iptal edilmedi",
};
const actionLabels: Record<string, string> = {
  ReprocessProviderEvent: "Ürün eşlemesini düzeltip olayı yeniden işleyin",
  ResendProviderCancellation: "İptali platforma yeniden gönderin",
  ResendProviderUpdate: "Durumu platforma yeniden gönderin",
  SettleWithProvider: "Platformla elle uzlaşın ve sonucu not edin",
  CheckChannelConnection: "Platform bağlantısını ve ayarlarını kontrol edin",
  HandOverOrCancelOrder: "Siparişi teslim edin ya da iptal edin",
};
const statusLabels: Record<string, string> = { Open: "Açık", Investigating: "İnceleniyor", Escalated: "Üst yönetimde" };

export const problemKindLabel = (kind: string) => kindLabels[kind] ?? "Online sipariş sorunu";
export const problemActionLabel = (action: string) => actionLabels[action] ?? "Önerilen eylemi uygulayın";
export const problemStatusLabel = (status: string) => statusLabels[status] ?? "Açık";

const base = (terminalId: string) => `/api/v1/terminals/${encodeURIComponent(terminalId)}/online-problems`;

async function call(url: string, init: RequestInit | undefined, fallback: string, fetcher: typeof fetch): Promise<Response> {
  let response: Response;
  try {
    response = await fetcher(url, { credentials: "same-origin", signal: AbortSignal.timeout(10_000), ...init });
  } catch {
    throw new OnlineProblemsApiError("Sunucuya ulaşılamadı. Tekrar deneyin.");
  }
  if (!response.ok) {
    const payload = await response.json().catch(() => undefined) as { error?: { message?: string } } | undefined;
    const message = response.status === 401 ? "Oturum sona erdi." : response.status === 403 ? "Bu işlem için yetkiniz yok." : payload?.error?.message ?? fallback;
    throw new OnlineProblemsApiError(message);
  }
  return response;
}

export async function loadProblems(terminalId: string, fetcher: typeof fetch = fetch): Promise<OnlineProblem[]> {
  const response = await call(`${base(terminalId)}/`, undefined, "Sorunlar okunamadı.", fetcher);
  return response.json() as Promise<OnlineProblem[]>;
}

export async function retryProblem(terminalId: string, caseId: string, fetcher: typeof fetch = fetch): Promise<void> {
  await call(`${base(terminalId)}/${encodeURIComponent(caseId)}/retry`, { method: "POST" }, "Yeniden denenemedi.", fetcher);
}

export async function resolveProblem(terminalId: string, problem: OnlineProblem, note: string, fetcher: typeof fetch = fetch): Promise<void> {
  await call(`${base(terminalId)}/${encodeURIComponent(problem.caseId)}/resolve`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ expectedVersion: problem.rowVersion, note }),
  }, "Sorun kapatılamadı.", fetcher);
}
