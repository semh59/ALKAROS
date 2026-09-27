/** V12-ONL-011: open the restaurant, close it for today, or pause it as busy — per online platform. */

export type StoreState = "Open" | "ClosedToday" | "Busy" | "ClosedUntil";
export type StoreDelivery = "Delivered" | "Pending" | "Retrying";
export type PlatformState = "Open" | "Closed" | "Unknown";

export interface OnlineStoreStatus {
  provider: string;
  configured: boolean;
  state: StoreState;
  closedUntil: string | null;
  delivery: StoreDelivery;
  platformState: PlatformState;
  platformClosedUntil: string | null;
}

export type StoreRequest = { state: "Open" } | { state: "ClosedToday" } | { state: "Busy"; minutes: number };

export class OnlineStoreStatusApiError extends Error {}

export const busyChoices: readonly number[] = [30, 60];

const time = (iso: string) => new Date(iso).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });

// Every server value shown to a person goes through these; an unknown one is never shown raw.
export function requestedText(status: OnlineStoreStatus): string {
  switch (status.state) {
    case "Open": return "Açık";
    case "ClosedToday": return "Bugün kapalı";
    case "Busy": return status.closedUntil ? `Yoğun, saat ${time(status.closedUntil)} itibarıyla açılacak` : "Yoğun";
    case "ClosedUntil": return status.closedUntil ? `Kapalı, saat ${time(status.closedUntil)} itibarıyla açılacak` : "Kapalı";
    default: return "Bilinmiyor";
  }
}

export function deliveryText(status: OnlineStoreStatus): string {
  switch (status.delivery) {
    case "Delivered": return "";
    case "Retrying": return "Platforma iletilemedi, yeniden deneniyor";
    default: return "Platforma iletiliyor…";
  }
}

export function platformText(status: OnlineStoreStatus): string {
  switch (status.platformState) {
    case "Open": return "Platformda: Açık";
    case "Closed": return status.platformClosedUntil ? `Platformda: Kapalı (saat ${time(status.platformClosedUntil)} itibarıyla açılacak)` : "Platformda: Kapalı";
    default: return "Platformda: Okunamadı";
  }
}

const base = (terminalId: string) => `/api/v1/terminals/${encodeURIComponent(terminalId)}/online-store-status`;

async function call(url: string, init: RequestInit | undefined, fallback: string, fetcher: typeof fetch): Promise<Response> {
  let response: Response;
  try {
    response = await fetcher(url, { credentials: "same-origin", signal: AbortSignal.timeout(15_000), ...init });
  } catch {
    throw new OnlineStoreStatusApiError("Sunucuya ulaşılamadı. Tekrar deneyin.");
  }
  if (!response.ok) {
    const payload = await response.json().catch(() => undefined) as { error?: { message?: string } } | undefined;
    const message = response.status === 401 ? "Oturum sona erdi." : response.status === 403 ? "Bu işlem için yetkiniz yok." : payload?.error?.message ?? fallback;
    throw new OnlineStoreStatusApiError(message);
  }
  return response;
}

export async function loadStoreStatus(terminalId: string, fetcher: typeof fetch = fetch): Promise<OnlineStoreStatus[]> {
  const response = await call(`${base(terminalId)}/`, undefined, "Restoran durumu okunamadı.", fetcher);
  return response.json() as Promise<OnlineStoreStatus[]>;
}

export async function requestStoreStatus(
  terminalId: string, provider: string, request: StoreRequest, fetcher: typeof fetch = fetch,
): Promise<OnlineStoreStatus> {
  const response = await call(`${base(terminalId)}/${encodeURIComponent(provider)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  }, "Restoran durumu değiştirilemedi.", fetcher);
  return response.json() as Promise<OnlineStoreStatus>;
}
