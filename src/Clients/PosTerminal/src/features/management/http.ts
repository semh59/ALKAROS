export class ManagementApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

const fallbackByStatus = (status: number): string =>
  status === 401 ? "Oturum sona erdi; yeniden giriş yapın."
    : status === 403 ? "Bu işlem için yetkiniz yok."
    : status === 409 ? "Kayıt başka biri tarafından değiştirildi; yenileyip tekrar deneyin."
    : "İşlem tamamlanamadı.";

interface RequesterOptions { prefix?: string; timeoutMs?: number; idempotentPosts?: boolean }

/** One requester per feature client; the server's own Turkish reason is shown as is, only a missing one falls back. */
export function createRequester(fetcher: typeof fetch, { prefix = "", timeoutMs = 8_000, idempotentPosts = false }: RequesterOptions = {}) {
  return async function call(path: string, init: RequestInit = {}): Promise<Response> {
    let response: Response;
    try {
      response = await fetcher(`/api/v1/management${prefix}${path}`, {
        ...init,
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json",
          "X-Correlation-Id": crypto.randomUUID(),
          ...(idempotentPosts && init.method === "POST" ? { "X-Idempotency-Key": crypto.randomUUID() } : {}),
        },
        signal: AbortSignal.timeout(timeoutMs),
      });
    } catch {
      throw new ManagementApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Bağlantıyı kontrol edip tekrar deneyin.");
    }
    if (response.ok) return response;
    const body = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
    throw new ManagementApiError(response.status, body?.error?.code ?? "REQUEST_FAILED", body?.error?.message ?? fallbackByStatus(response.status));
  };
}
