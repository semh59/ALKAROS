import type { ActiveDelegation, OpenTightening, PendingGrant } from "./models";

export class AuthorizationDecisionApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) {
    super(message);
  }
}

export interface AuthorizationDecisionsData {
  pendingGrants: PendingGrant[];
  delegations: ActiveDelegation[];
  tightenings: OpenTightening[];
}

export interface AuthorizationDecisionsClient {
  load: () => Promise<AuthorizationDecisionsData>;
  approve: (grantId: string) => Promise<void>;
  deny: (grantId: string) => Promise<void>;
  revokeDelegation: (delegationId: string) => Promise<void>;
  clearTightening: (tighteningId: string) => Promise<void>;
}

export function createAuthorizationDecisionsClient(
  fetcher: typeof fetch = fetch,
): AuthorizationDecisionsClient {
  const prefix = "/api/v1/management/authorization";

  async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
    let response: Response;
    try {
      response = await fetcher(`${prefix}${path}`, {
        ...options,
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json",
          "X-Correlation-Id": crypto.randomUUID(),
          ...options.headers,
        },
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new AuthorizationDecisionApiError(
        0,
        "NETWORK_UNAVAILABLE",
        "Yetki sunucusuna ulaşılamadı.",
      );
    }
    if (!response.ok) {
      const body = (await response.json().catch(() => undefined)) as
        | { error?: { code?: string; message?: string } }
        | undefined;
      throw new AuthorizationDecisionApiError(
        response.status,
        body?.error?.code ?? "REQUEST_FAILED",
        body?.error?.message ?? "Yetki işlemi tamamlanamadı.",
      );
    }
    return response.status === 204 ? (undefined as T) : (response.json() as Promise<T>);
  }

  return {
    load: async () => {
      const [pendingGrants, delegations, tightenings] = await Promise.all([
        request<PendingGrant[]>("/pending-grants"),
        request<ActiveDelegation[]>("/delegations"),
        request<OpenTightening[]>("/behavioural-tightenings"),
      ]);
      return { pendingGrants, delegations, tightenings };
    },
    approve: (grantId) => request<void>(`/grants/${grantId}/approve`, { method: "POST" }),
    deny: (grantId) => request<void>(`/grants/${grantId}/deny`, { method: "POST" }),
    revokeDelegation: (delegationId) =>
      request<void>(`/delegations/${delegationId}/revoke`, { method: "POST" }),
    clearTightening: (tighteningId) =>
      request<void>(`/behavioural-tightenings/${tighteningId}/clear`, { method: "POST" }),
  };
}
