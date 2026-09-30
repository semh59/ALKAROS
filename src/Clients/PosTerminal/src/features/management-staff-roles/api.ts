export class StaffApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

export interface UserLookup { userId: string; displayName: string; active: boolean; isLocked: boolean }

export interface StaffClient {
  createUser: (username: string, password: string, displayName: string) => Promise<void>;
  lookupUser: (username: string) => Promise<UserLookup | null>;
  setActive: (userId: string, active: boolean) => Promise<void>;
}

const fallbackByStatus = (status: number): string =>
  status === 401 ? "Oturum sona erdi; yeniden giriş yapın."
    : status === 403 ? "Bu işlem için yetkiniz yok."
    : "İşlem tamamlanamadı.";

export function createStaffClient(fetcher: typeof fetch = fetch): StaffClient {
  async function call(path: string, init: RequestInit = {}): Promise<Response> {
    let response: Response;
    try {
      response = await fetcher(`/api/v1/management${path}`, {
        ...init,
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID() },
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new StaffApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Bağlantıyı kontrol edip tekrar deneyin.");
    }
    if (response.ok) return response;
    const body = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
    throw new StaffApiError(response.status, body?.error?.code ?? "REQUEST_FAILED", body?.error?.message ?? fallbackByStatus(response.status));
  }
  return {
    createUser: (username, password, displayName) => call("/users", { method: "POST", body: JSON.stringify({ username, password, displayName }) }).then(() => undefined),
    async lookupUser(username) {
      try {
        return await (await call(`/security/users/lookup?username=${encodeURIComponent(username)}`)).json() as UserLookup;
      } catch (reason) {
        if (reason instanceof StaffApiError && reason.status === 404) return null;
        throw reason;
      }
    },
    setActive: (userId, active) => call(`/security/users/${userId}/${active ? "reactivate" : "deactivate"}`, { method: "POST", body: "{}" }).then(() => undefined),
  };
}
