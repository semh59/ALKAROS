import type { SettingHistoryEntry, SettingRecord } from "./models";

export class SettingsApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

const fallbackByStatus = (status: number): string =>
  status === 401 ? "Oturum sona erdi; yeniden giriş yapın."
    : status === 403 ? "Bu işlem için yetkiniz yok."
    : "İşlem tamamlanamadı.";

export interface SettingsClient {
  listSettings: () => Promise<readonly SettingRecord[]>;
  history: (key: string) => Promise<readonly SettingHistoryEntry[]>;
}

export function createSettingsClient(fetcher: typeof fetch = fetch): SettingsClient {
  async function json<T>(path: string): Promise<T> {
    let response: Response;
    try {
      response = await fetcher(`/api/v1/management/settings${path}`, {
        credentials: "same-origin",
        headers: { "X-Correlation-Id": crypto.randomUUID() },
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new SettingsApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Bağlantıyı kontrol edip tekrar deneyin.");
    }
    if (response.ok) return response.json() as Promise<T>;
    const body = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
    throw new SettingsApiError(response.status, body?.error?.code ?? "REQUEST_FAILED", body?.error?.message ?? fallbackByStatus(response.status));
  }
  return {
    listSettings: () => json("/"),
    history: (key) => json(`/${encodeURIComponent(key)}/history`),
  };
}
