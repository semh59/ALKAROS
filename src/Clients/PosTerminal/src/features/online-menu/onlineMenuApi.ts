/** V12-OUI-005: the online food screen's Menu tab — platform codes, what the platform shows, publishing. */

export interface OnlineMenuProduct {
  productId: string;
  name: string;
  catalogSku: string;
  catalogPrice: number | null;
  active: boolean;
  externalSku: string | null;
  publishedPrice: number | null;
  saleState: "OnSale" | "SoldOut" | "Unknown" | string;
}

export interface OnlineMenuPlatformProduct {
  id: string;
  name: string;
  active: boolean;
  mappedProductId: string | null;
}

export interface OnlineMenuPublication {
  publicationId: string;
  status: string;
  requestedAt: string;
  itemCount: number;
  validationErrorCount: number;
  hasError: boolean;
  deliveredAt: string | null;
}

export interface OnlineMenuUnmappedCode {
  code: string;
  orderCount: number;
  lastSeenAt: string;
}

export interface OnlineMenu {
  provider: string;
  channel: string;
  products: OnlineMenuProduct[];
  platformProducts: OnlineMenuPlatformProduct[] | null;
  platformMenuUnavailable: boolean;
  menus: { menuId: string; name: string }[];
  publications: OnlineMenuPublication[];
  unmappedCodes: OnlineMenuUnmappedCode[];
}

export interface OnlinePlatform {
  provider: string;
  displayName: string;
}

export interface PublishResult {
  status: string;
  itemCount: number;
  validationErrors: { productId: string; code: string }[];
}

export class OnlineMenuApiError extends Error {}

const saleLabels: Record<string, string> = { OnSale: "Satışta", SoldOut: "Satışta değil", Unknown: "Henüz bildirilmedi" };
const publicationLabels: Record<string, string> = {
  Pending: "Gönderiliyor",
  Delivered: "Yayınlandı",
  Unchanged: "Değişiklik yok",
  NothingToPublish: "Yayınlanacak ürün yok",
  Superseded: "Yerine yenisi gönderildi",
};
const validationLabels: Record<string, string> = {
  PriceMissing: "fiyatı yok",
  ModifiersNotSupported: "zorunlu seçimleri platforma aktarılamıyor",
  ExternalIdUnavailable: "platform kodu yok veya platform menüsünde değil",
};

/** Every server value shown to a person goes through these dictionaries; an unknown one is never shown raw. */
export const saleLabel = (state: string) => saleLabels[state] ?? "Bilinmiyor";
export const publicationLabel = (publication: Pick<OnlineMenuPublication, "status" | "hasError">) =>
  publication.status === "Pending" && publication.hasError ? "Gönderilemedi, yeniden denenecek" : publicationLabels[publication.status] ?? "Bilinmiyor";
export const validationLabel = (code: string) => validationLabels[code] ?? "yayınlanamadı";

const base = (terminalId: string) => `/api/v1/terminals/${encodeURIComponent(terminalId)}`;

async function call(url: string, init: RequestInit | undefined, fallback: string, fetcher: typeof fetch): Promise<Response> {
  let response: Response;
  try {
    response = await fetcher(url, { credentials: "same-origin", signal: AbortSignal.timeout(15_000), ...init });
  } catch {
    throw new OnlineMenuApiError("Sunucuya ulaşılamadı. Tekrar deneyin.");
  }
  if (!response.ok) {
    // The server's own Turkish message when it sends one; never a raw status code or English text.
    const payload = await response.json().catch(() => undefined) as { error?: { message?: string } } | undefined;
    const message = response.status === 401 ? "Oturum sona erdi." : response.status === 403 ? "Bu işlem için yetkiniz yok." : payload?.error?.message ?? fallback;
    throw new OnlineMenuApiError(message);
  }
  return response;
}

export async function loadPlatforms(terminalId: string, fetcher: typeof fetch = fetch): Promise<OnlinePlatform[]> {
  const response = await call(`${base(terminalId)}/online-channels`, undefined, "Platformlar okunamadı.", fetcher);
  return ((await response.json()) as { platforms: OnlinePlatform[] }).platforms.map(({ provider, displayName }) => ({ provider, displayName }));
}

export async function loadOnlineMenu(terminalId: string, provider: string, fetcher: typeof fetch = fetch): Promise<OnlineMenu> {
  const response = await call(`${base(terminalId)}/online-menu/${encodeURIComponent(provider)}`, undefined, "Menü bilgileri okunamadı.", fetcher);
  return response.json() as Promise<OnlineMenu>;
}

export async function saveMapping(terminalId: string, provider: string, productId: string, externalSku: string, fetcher: typeof fetch = fetch): Promise<void> {
  await call(`${base(terminalId)}/online-menu/${encodeURIComponent(provider)}/mappings/${encodeURIComponent(productId)}`,
    { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ externalSku }) }, "Eşleme kaydedilemedi.", fetcher);
}

export async function removeMapping(terminalId: string, provider: string, productId: string, fetcher: typeof fetch = fetch): Promise<void> {
  await call(`${base(terminalId)}/online-menu/${encodeURIComponent(provider)}/mappings/${encodeURIComponent(productId)}`,
    { method: "DELETE" }, "Eşleme kaldırılamadı.", fetcher);
}

export async function publishMenu(terminalId: string, channel: string, menuId: string, fetcher: typeof fetch = fetch): Promise<PublishResult> {
  const response = await call(`${base(terminalId)}/online-ordering/catalog-publications`,
    { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ channel, menuId }) }, "Menü yayınlanamadı.", fetcher);
  return response.json() as Promise<PublishResult>;
}
