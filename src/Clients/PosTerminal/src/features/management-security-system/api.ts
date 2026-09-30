import type {
  Alert, AlertAction, CloseSettledResult, DiagnosticBundle, HealthCheck, MaintenanceJob, OrderBacklog, RestoreAttempt, RpoStatus, UserLookup,
} from "./models";

export class SecurityApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

const fallbackByStatus = (status: number): string =>
  status === 401 ? "Oturum sona erdi; yeniden giriş yapın."
    : status === 403 ? "Bu işlem için yetkiniz yok."
    : status === 409 ? "Kayıt başka biri tarafından değiştirildi; yenileyip tekrar deneyin."
    : "İşlem tamamlanamadı.";

function createRequester(fetcher: typeof fetch) {
  return async function call(path: string, init: RequestInit = {}): Promise<Response> {
    let response: Response;
    try {
      response = await fetcher(`/api/v1/management${path}`, {
        ...init,
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID() },
        signal: AbortSignal.timeout(15_000),
      });
    } catch {
      throw new SecurityApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Bağlantıyı kontrol edip tekrar deneyin.");
    }
    if (response.ok) return response;
    const body = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
    throw new SecurityApiError(response.status, body?.error?.code ?? "REQUEST_FAILED", body?.error?.message ?? fallbackByStatus(response.status));
  };
}

export interface SecurityClient {
  lookupUser: (username: string) => Promise<UserLookup | null>;
  revokeSessions: (userId: string) => Promise<number>;
  forceUnlock: (userId: string) => Promise<void>;
  listJobs: () => Promise<readonly MaintenanceJob[]>;
  runJob: (name: string) => Promise<MaintenanceJob>;
  backupRpo: () => Promise<readonly RpoStatus[]>;
  restoreAttempts: () => Promise<readonly RestoreAttempt[]>;
  diagnosticBundle: (input: { correlationIds: readonly string[]; windowStart: string; windowEnd: string; reason: string }) => Promise<DiagnosticBundle>;
  orderBacklog: () => Promise<OrderBacklog>;
  closeSettled: (dryRun: boolean) => Promise<CloseSettledResult>;
}

export function createSecurityClient(fetcher: typeof fetch = fetch): SecurityClient {
  const call = createRequester(fetcher);
  const json = async <T>(path: string, init?: RequestInit): Promise<T> => (await call(path, init)).json() as Promise<T>;
  const post = <T>(path: string, body: unknown = {}) => json<T>(path, { method: "POST", body: JSON.stringify(body) });
  return {
    async lookupUser(username) {
      try {
        return await json<UserLookup>(`/security/users/lookup?username=${encodeURIComponent(username)}`);
      } catch (reason) {
        if (reason instanceof SecurityApiError && reason.status === 404) return null;
        throw reason;
      }
    },
    revokeSessions: async (userId) => (await post<{ revokedSessions: number }>(`/security/users/${userId}/revoke-sessions`)).revokedSessions,
    forceUnlock: async (userId) => { await post(`/security/users/${userId}/force-unlock`); },
    listJobs: () => json("/security/maintenance/jobs"),
    runJob: (name) => post(`/security/maintenance/jobs/${encodeURIComponent(name)}/run`),
    backupRpo: () => json("/security/backup/rpo"),
    restoreAttempts: () => json("/security/backup/restore-attempts"),
    diagnosticBundle: (input) => post("/security/diagnostic-bundle", input),
    orderBacklog: () => json("/security/orders/backlog"),
    closeSettled: (dryRun) => post(`/security/orders/close-settled?dryRun=${dryRun}`),
  };
}

export interface SystemClient {
  activeAlerts: () => Promise<readonly Alert[]>;
  unhealthyChecks: () => Promise<readonly HealthCheck[]>;
  actOnAlert: (alert: Alert, action: AlertAction, reason: string) => Promise<void>;
}

export function createSystemClient(fetcher: typeof fetch = fetch): SystemClient {
  const call = createRequester(fetcher);
  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  return {
    activeAlerts: () => json("/observability/alerts/active"),
    unhealthyChecks: () => json("/observability/health-checks/unhealthy"),
    async actOnAlert(alert, action, reason) {
      const body = action === "resolve"
        ? { expectedRowVersion: alert.rowVersion, resolutionReason: reason }
        : { expectedRowVersion: alert.rowVersion, reason: reason || null };
      await call(`/observability/alerts/${alert.alertId}/${action}`, { method: "POST", body: JSON.stringify(body) });
    },
  };
}
