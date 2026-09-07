import type {
  ApiErrorBody,
  CatalogProduct,
  DisplaySnapshot,
  LoginResponse,
  MutationResult,
  NfcOrderResult,
  PairingCompleted,
  PairingCreated,
  RelayCredentialStatus,
  RuntimeConfiguration,
} from "./contracts";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
  ) {
    super(message);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    const timeout = AbortSignal.timeout(8_000);
    response = await fetch(path, {
      ...init,
      signal: init?.signal ? AbortSignal.any([init.signal, timeout]) : timeout,
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/json",
        "X-Correlation-Id": crypto.randomUUID(),
        ...init?.headers,
      },
    });
  } catch {
    throw new ApiError(
      0,
      "NETWORK_UNAVAILABLE",
      "Sunucuya ulaşılamadı. Bağlantıyı kontrol edip tekrar deneyin.",
    );
  }
  if (!response.ok) {
    let body: ApiErrorBody | undefined;
    try {
      body = (await response.json()) as ApiErrorBody;
    } catch {
      body = undefined;
    }
    throw new ApiError(
      response.status,
      body?.error?.code ?? "REQUEST_FAILED",
      body?.error?.message ?? "İşlem tamamlanamadı.",
    );
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export const api = {
  health: () => request<{ status: string }>("/health/ready"),
  login: (username: string, password: string, terminalId: string) =>
    request<LoginResponse>("/api/v1/auth/login", {
      method: "POST",
      body: JSON.stringify({ username, password, terminalId }),
    }),
  session: (terminalId: string) =>
    request<LoginResponse>(`/api/v1/auth/session?terminalId=${terminalId}`),
  runtimeConfig: (terminalId: string) =>
    request<RuntimeConfiguration>(`/api/v1/terminals/${terminalId}/runtime-configuration`),
  logout: (terminalId: string) =>
    request<void>(`/api/v1/auth/logout?terminalId=${terminalId}`, {
      method: "POST",
      body: "{}",
    }),
  catalog: (terminalId: string) =>
    request<CatalogProduct[]>(`/api/v1/terminals/${terminalId}/catalog`),
  activeOrder: (terminalId: string) =>
    request<DisplaySnapshot>(`/api/v1/terminals/${terminalId}/orders/active`),
  startOrder: (terminalId: string) =>
    request<{ orderId: string; orderNumber: string; revision: number }>(
      `/api/v1/terminals/${terminalId}/orders`,
      { method: "POST", body: "{}" },
    ),
  startTableOrder: (terminalId: string, tableId: string, expectedTableRowVersion: number) =>
    request<{ orderId: string; orderNumber: string; revision: number }>(
      `/api/v1/terminals/${terminalId}/orders/table`,
      {
        method: "POST",
        body: JSON.stringify({ tableId, expectedTableRowVersion }),
      },
    ),
  addItem: (terminalId: string, orderId: string, productId: string, expectedRevision: number) =>
    request<MutationResult>(`/api/v1/terminals/${terminalId}/orders/${orderId}/items`, {
      method: "POST",
      body: JSON.stringify({ productId, quantity: 1, expectedRevision }),
    }),
  changeQuantity: (
    terminalId: string,
    orderId: string,
    itemId: string,
    quantity: number,
    expectedRevision: number,
  ) =>
    request<MutationResult>(
      `/api/v1/terminals/${terminalId}/orders/${orderId}/items/${itemId}`,
      { method: "PATCH", body: JSON.stringify({ quantity, expectedRevision }) },
    ),
  removeItem: (
    terminalId: string,
    orderId: string,
    itemId: string,
    expectedRevision: number,
  ) =>
    request<MutationResult>(
      `/api/v1/terminals/${terminalId}/orders/${orderId}/items/${itemId}?expectedRevision=${expectedRevision}`,
      { method: "DELETE" },
    ),
  submitOrder: (terminalId: string, orderId: string, expectedRevision: number) =>
    request<{ rowVersion: number }>(
      `/api/v1/terminals/${terminalId}/orders/${orderId}/submit`,
      {
        method: "POST",
        body: JSON.stringify({ operationId: crypto.randomUUID(), expectedRevision }),
      },
    ),
  createPairing: (displayId: string) =>
    request<PairingCreated>("/api/v1/customer-displays/pairing-requests", {
      method: "POST",
      body: JSON.stringify({ displayId }),
    }),
  approvePairing: (terminalId: string, code: string) =>
    request<void>(`/api/v1/terminals/${terminalId}/pairings/approve`, {
      method: "POST",
      body: JSON.stringify({ code }),
    }),
  completePairing: (requestId: string, secret: string) =>
    request<PairingCompleted>(
      `/api/v1/customer-displays/pairing-requests/${requestId}/complete`,
      { method: "POST", body: JSON.stringify({ secret }) },
    ),
  snapshot: (displayId: string) =>
    request<DisplaySnapshot>(`/api/v1/customer-displays/${displayId}/snapshot`),
  revokeDisplay: (terminalId: string) =>
    request<{ revoked: number }>(`/api/v1/terminals/${terminalId}/display-sessions/revoke`, {
      method: "POST",
      body: "{}",
    }),
  // V12-NFC-001/004: no terminalId, no session — the tapped table's own id
  // is the only context this unauthenticated surface has.
  nfcCatalog: (tableId: string) =>
    request<CatalogProduct[]>(`/api/v1/nfc/tables/${tableId}/catalog`),
  placeNfcOrder: (
    tableId: string,
    items: { id: string; productId: string; quantity: number }[],
    submissionId: string,
  ) =>
    request<NfcOrderResult>(`/api/v1/nfc/tables/${tableId}/orders`, {
      method: "POST",
      body: JSON.stringify({ items, id: submissionId }),
    }),
  // V12-QRT-003: manager-only. saveRelayCredential never returns the value
  // back; relayCredentialStatus reports only configured/updatedAt.
  saveRelayCredential: (
    terminalId: string,
    cloudflareApiToken: string,
    accountId: string,
    zoneId: string,
    baseDomain: string,
  ) =>
    request<void>(`/api/v1/terminals/${terminalId}/relay-credential/`, {
      method: "POST",
      body: JSON.stringify({ cloudflareApiToken, accountId, zoneId, baseDomain }),
    }),
  relayCredentialStatus: (terminalId: string) =>
    request<RelayCredentialStatus>(`/api/v1/terminals/${terminalId}/relay-credential/status`),
  // V12-QRT-001: chains CreateTunnel -> GetTunnelToken -> CreateDnsRecord on
  // the backend; returns only the resulting hostname, never the tunnel
  // run-token.
  provisionRelayTunnel: (terminalId: string, subdomainLabel: string) =>
    request<{ hostname: string }>(`/api/v1/terminals/${terminalId}/relay-credential/provision`, {
      method: "POST",
      body: JSON.stringify({ subdomainLabel }),
    }),
};
