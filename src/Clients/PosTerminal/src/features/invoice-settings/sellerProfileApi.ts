/** The business as it appears on invoices; the server keeps one and replaces it on save. */
export interface SellerProfile {
  legalName: string;
  taxIdKind: "Vkn" | "Tckn";
  taxIdNumber: string;
  taxOffice: string;
  address: string;
  district: string;
  city: string;
  email: string | null;
}

export class SellerProfileApiError extends Error {}

const base = (terminalId: string) => `/api/v1/terminals/${encodeURIComponent(terminalId)}/invoice-settings/seller-profile`;

async function call(url: string, init: RequestInit | undefined, fallback: string, fetcher: typeof fetch): Promise<Response> {
  let response: Response;
  try {
    response = await fetcher(url, { credentials: "same-origin", signal: AbortSignal.timeout(15_000), ...init });
  } catch {
    throw new SellerProfileApiError("Sunucuya ulaşılamadı. Tekrar deneyin.");
  }
  if (!response.ok) {
    // The server's own Turkish message when it sends one; never a raw status code or English text.
    const payload = await response.json().catch(() => undefined) as { error?: { message?: string } } | undefined;
    const message = response.status === 401 ? "Oturum sona erdi." : response.status === 403 ? "Bu işlem için yetkiniz yok." : payload?.error?.message ?? fallback;
    throw new SellerProfileApiError(message);
  }
  return response;
}

export async function loadSellerProfile(terminalId: string, fetcher: typeof fetch = fetch): Promise<SellerProfile | null> {
  const response = await call(base(terminalId), undefined, "Fatura bilgileri okunamadı.", fetcher);
  return ((await response.json()) as { profile: SellerProfile | null }).profile;
}

export async function saveSellerProfile(terminalId: string, profile: SellerProfile, fetcher: typeof fetch = fetch): Promise<void> {
  await call(base(terminalId),
    { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(profile) }, "Fatura bilgileri kaydedilemedi.", fetcher);
}
