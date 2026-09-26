/** V12-OUI-003: each online platform's API settings; a secret field only ever says whether it is set. */

export interface OnlinePlatformCredentialField {
  name: string;
  isSecret: boolean;
  configured: boolean;
  /** The stored value of a non-secret field; always null for a secret one. */
  value: string | null;
}

export interface OnlinePlatformCredentials {
  provider: string;
  fields: OnlinePlatformCredentialField[];
  updatedAt: string | null;
}

export interface OnlinePlatformCredentialChange {
  values: Record<string, string>;
  cleared: string[];
}

export class OnlinePlatformCredentialsApiError extends Error {
  constructor(public readonly status: number, message: string, public readonly field: string | null = null) { super(message); }
}

const platformLabels: Record<string, string> = {
  yemeksepeti: "Yemeksepeti",
  "trendyol-go": "Trendyol Go",
  "migros-yemek": "Migros Yemek",
};

const fieldLabels: Record<string, string> = {
  "api-base-url": "API adresi",
  "chain-id": "Zincir kimliği",
  "vendor-id": "Restoran kimliği",
  "client-id": "İstemci kimliği",
  "client-secret": "İstemci gizli anahtarı",
  "webhook-secret": "Sipariş bildirimi doğrulama anahtarı",
  "supplier-id": "Satıcı kimliği",
  "api-key": "API anahtarı",
  "api-secret": "API gizli anahtarı",
};

/** A platform or field this screen does not know yet is never shown by its raw id. */
export const platformLabel = (provider: string) => platformLabels[provider] ?? "Diğer platform";
export const fieldLabel = (name: string) => fieldLabels[name] ?? "Ek alan";

const base = (terminalId: string) => `/api/v1/terminals/${encodeURIComponent(terminalId)}/online-platform-credentials`;

async function call(url: string, init: RequestInit | undefined, fallback: string, fetcher: typeof fetch): Promise<Response> {
  let response: Response;
  try {
    response = await fetcher(url, { credentials: "same-origin", signal: AbortSignal.timeout(8_000), ...init });
  } catch {
    throw new OnlinePlatformCredentialsApiError(0, "Sunucuya ulaşılamadı. Tekrar deneyin.");
  }
  if (!response.ok) {
    // The server's own Turkish message when it sends one; never a raw status code or English text.
    const payload = await response.json().catch(() => undefined) as { error?: { message?: string; field?: string | null } } | undefined;
    const message = response.status === 401 ? "Oturum sona erdi." : response.status === 403 ? "Bu işlem için yetkiniz yok." : payload?.error?.message ?? fallback;
    throw new OnlinePlatformCredentialsApiError(response.status, message, payload?.error?.field ?? null);
  }
  return response;
}

export async function loadOnlinePlatformCredentials(terminalId: string, fetcher: typeof fetch = fetch): Promise<OnlinePlatformCredentials[]> {
  if (!terminalId.trim()) throw new Error("terminalId is required");
  const response = await call(`${base(terminalId)}/`, undefined, "Platform bilgileri okunamadı.", fetcher);
  return ((await response.json()) as { platforms: OnlinePlatformCredentials[] }).platforms;
}

export async function saveOnlinePlatformCredentials(
  terminalId: string,
  provider: string,
  change: OnlinePlatformCredentialChange,
  fetcher: typeof fetch = fetch,
): Promise<OnlinePlatformCredentials> {
  const response = await call(
    `${base(terminalId)}/${encodeURIComponent(provider)}`,
    { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(change) },
    "Platform bilgileri kaydedilemedi.",
    fetcher,
  );
  return response.json() as Promise<OnlinePlatformCredentials>;
}

/**
 * What a form edit means for the store: a changed non-secret value is set and an emptied one removed; a typed
 * secret is set; a field marked for removal is removed. An untouched field is left out, so a stored secret
 * never has to be typed again to change something else.
 */
export function describeChange(
  platform: OnlinePlatformCredentials,
  drafts: Readonly<Record<string, string>>,
  removals: ReadonlySet<string>,
): OnlinePlatformCredentialChange {
  const values: Record<string, string> = {};
  const cleared: string[] = [];
  for (const field of platform.fields) {
    if (removals.has(field.name)) {
      if (field.configured) cleared.push(field.name);
      continue;
    }
    const typed = drafts[field.name];
    if (typed === undefined) continue;
    const draft = typed.trim();
    if (field.isSecret) {
      if (draft !== "") values[field.name] = draft;
    } else if (draft !== (field.value ?? "")) {
      if (draft !== "") values[field.name] = draft;
      else if (field.configured) cleared.push(field.name);
    }
  }
  return { values, cleared };
}
