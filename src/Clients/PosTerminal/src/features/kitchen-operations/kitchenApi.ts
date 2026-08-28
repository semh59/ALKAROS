import type {
  KitchenBackup,
  KitchenData,
  KitchenHealthSnapshot,
  KitchenPrinter,
  KitchenPrinterRoute,
  KitchenTicket,
  KitchenUnknownDelivery,
} from "./models";

export class KitchenOperationsApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) {
    super(message);
  }
}

export interface KitchenOperationsClient {
  load: () => Promise<KitchenData>;
  transitionItem: (ticketId: string, itemId: string, targetState: string, expectedTicketRowVersion: number, expectedItemRowVersion: number, reason?: string) => Promise<KitchenTicket>;
  transitionTicket: (ticketId: string, targetState: string, expectedRowVersion: number, reason?: string) => Promise<KitchenTicket>;
  approveReprint: (deliveryId: string, reason: string) => Promise<KitchenUnknownDelivery>;
  rejectReprint: (deliveryId: string, reason: string) => Promise<KitchenUnknownDelivery>;
}

export interface KitchenRuntimeConfiguration {
  kitchenStationId: string;
}

export async function loadKitchenRuntimeConfiguration(
  terminalId: string,
  fetcher: typeof fetch = fetch,
): Promise<KitchenRuntimeConfiguration> {
  if (!terminalId.trim()) throw new Error("terminalId is required");
  let response: Response;
  try {
    response = await fetcher(
      `/api/v1/terminals/${encodeURIComponent(terminalId)}/runtime-configuration`,
      {
        credentials: "same-origin",
        headers: { "X-Correlation-Id": crypto.randomUUID() },
        signal: AbortSignal.timeout(8_000),
      },
    );
  } catch {
    throw new KitchenOperationsApiError(0, "NETWORK_UNAVAILABLE", "Mutfak yapılandırması alınamadı. Tekrar deneyin.");
  }

  if (!response.ok) {
    const body = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
    throw new KitchenOperationsApiError(
      response.status,
      body?.error?.code ?? "CONFIGURATION_REQUEST_FAILED",
      body?.error?.message ?? "Mutfak yapılandırması alınamadı.",
    );
  }

  const configuration = await response.json() as Partial<KitchenRuntimeConfiguration>;
  const kitchenStationId = configuration.kitchenStationId?.trim();
  if (!kitchenStationId) {
    throw new KitchenOperationsApiError(502, "INVALID_KITCHEN_CONFIGURATION", "Mutfak istasyonu yapılandırılmamış.");
  }
  return { kitchenStationId };
}

export function createKitchenOperationsClient(terminalId: string, stationId: string, fetcher: typeof fetch = fetch): KitchenOperationsClient {
  if (!terminalId.trim()) throw new Error("terminalId is required");
  if (!stationId.trim()) throw new Error("stationId is required");
  const prefix = `/api/v1/terminals/${encodeURIComponent(terminalId)}/kitchen-operations`;

  async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
    let response: Response;
    try {
      response = await fetcher(`${prefix}${path}`, {
        ...options,
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID(), ...options.headers },
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new KitchenOperationsApiError(0, "NETWORK_UNAVAILABLE", "Mutfak sunucusuna ulaşılamadı. Tekrar deneyin.");
    }
    if (!response.ok) {
      const body = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
      throw new KitchenOperationsApiError(response.status, body?.error?.code ?? "REQUEST_FAILED", body?.error?.message ?? "Mutfak işlemi tamamlanamadı.");
    }
    return response.status === 204 ? undefined as T : response.json() as Promise<T>;
  }

  const get = <T>(path: string) => request<T>(path);
  return {
    load: async () => {
      const health = await get<KitchenHealthSnapshot | null>("/operations/health/latest").catch((error: unknown) => {
        if (error instanceof KitchenOperationsApiError && error.status === 404) return null;
        throw error;
      });
      const [tickets, printers, routes, unknownDeliveries, backups] = await Promise.all([
        get<KitchenTicket[]>(`/tickets?stationId=${encodeURIComponent(stationId)}`),
        get<KitchenPrinter[]>("/printers"),
        get<KitchenPrinterRoute[]>("/routes"),
        get<KitchenUnknownDelivery[]>("/deliveries/unknown"),
        get<KitchenBackup[]>("/operations/backups/recent?limit=20"),
      ]);
      return { tickets, printers, routes, unknownDeliveries, health, backups };
    },
    transitionItem: (ticketId, itemId, targetState, expectedTicketRowVersion, expectedItemRowVersion, reason) => request<KitchenTicket>(`/tickets/${encodeURIComponent(ticketId)}/items/${encodeURIComponent(itemId)}/transition`, { method: "POST", body: JSON.stringify({ targetState, expectedTicketRowVersion, expectedItemRowVersion, reason }) }),
    transitionTicket: (ticketId, targetState, expectedRowVersion, reason) => request<KitchenTicket>(`/tickets/${encodeURIComponent(ticketId)}/transition`, { method: "POST", body: JSON.stringify({ targetState, expectedRowVersion, reason }) }),
    approveReprint: (deliveryId, reason) => request<KitchenUnknownDelivery>(`/deliveries/${encodeURIComponent(deliveryId)}/reprint-approval`, { method: "POST", body: JSON.stringify({ reason }) }),
    rejectReprint: (deliveryId, reason) => request<KitchenUnknownDelivery>(`/deliveries/${encodeURIComponent(deliveryId)}/reprint-rejection`, { method: "POST", body: JSON.stringify({ reason }) }),
  };
}
