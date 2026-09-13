import type {
  KitchenBackup,
  KitchenCategory,
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
  // V1-KIT-009: reverses an item's most recent transition within its short
  // undo window — no targetState, there is only one valid direction.
  undoItem: (ticketId: string, itemId: string, expectedTicketRowVersion: number, expectedItemRowVersion: number) => Promise<KitchenTicket>;
  transitionTicket: (ticketId: string, targetState: string, expectedRowVersion: number, reason?: string) => Promise<KitchenTicket>;
  approveReprint: (deliveryId: string, reason: string) => Promise<KitchenUnknownDelivery>;
  rejectReprint: (deliveryId: string, reason: string) => Promise<KitchenUnknownDelivery>;
  createCategoryRoute: (categoryId: string, printerId: string) => Promise<KitchenPrinterRoute>;
}

interface Page<T> { items: T[]; nextCursor: string | null }

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

  // Categories live under Catalog's own management prefix, not this
  // module's terminal-scoped one - same cross-feature read CatalogWorkspace
  // itself does, reused here only for the routing form's dropdown.
  async function getCategories(): Promise<KitchenCategory[]> {
    let response: Response;
    try {
      response = await fetcher("/api/v1/management/catalog/categories?limit=100", {
        credentials: "same-origin",
        headers: { "X-Correlation-Id": crypto.randomUUID() },
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new KitchenOperationsApiError(0, "NETWORK_UNAVAILABLE", "Kategori listesi alınamadı.");
    }
    if (!response.ok) return [];
    const result = await response.json() as Page<KitchenCategory> | KitchenCategory[];
    return Array.isArray(result) ? result : result.items;
  }

  return {
    load: async () => {
      const health = await get<KitchenHealthSnapshot | null>("/operations/health/latest").catch((error: unknown) => {
        if (error instanceof KitchenOperationsApiError && error.status === 404) return null;
        throw error;
      });
      const [tickets, printers, routes, categories, unknownDeliveries, backups, liveSync] = await Promise.all([
        get<KitchenTicket[]>(`/tickets?stationId=${encodeURIComponent(stationId)}`),
        get<KitchenPrinter[]>("/printers"),
        get<KitchenPrinterRoute[]>("/routes"),
        getCategories(),
        get<KitchenUnknownDelivery[]>("/deliveries/unknown"),
        get<KitchenBackup[]>("/operations/backups/recent?limit=20"),
        get<{ enabled: boolean }>("/operations/live-sync"),
      ]);
      return { tickets, printers, routes, categories, unknownDeliveries, health, backups, liveSyncEnabled: liveSync.enabled };
    },
    transitionItem: (ticketId, itemId, targetState, expectedTicketRowVersion, expectedItemRowVersion, reason) => request<KitchenTicket>(`/tickets/${encodeURIComponent(ticketId)}/items/${encodeURIComponent(itemId)}/transition`, { method: "POST", body: JSON.stringify({ targetState, expectedTicketRowVersion, expectedItemRowVersion, reason }) }),
    undoItem: (ticketId, itemId, expectedTicketRowVersion, expectedItemRowVersion) => request<KitchenTicket>(`/tickets/${encodeURIComponent(ticketId)}/items/${encodeURIComponent(itemId)}/undo`, { method: "POST", body: JSON.stringify({ expectedTicketRowVersion, expectedItemRowVersion }) }),
    transitionTicket: (ticketId, targetState, expectedRowVersion, reason) => request<KitchenTicket>(`/tickets/${encodeURIComponent(ticketId)}/transition`, { method: "POST", body: JSON.stringify({ targetState, expectedRowVersion, reason }) }),
    approveReprint: (deliveryId, reason) => request<KitchenUnknownDelivery>(`/deliveries/${encodeURIComponent(deliveryId)}/reprint-approval`, { method: "POST", body: JSON.stringify({ reason }) }),
    rejectReprint: (deliveryId, reason) => request<KitchenUnknownDelivery>(`/deliveries/${encodeURIComponent(deliveryId)}/reprint-rejection`, { method: "POST", body: JSON.stringify({ reason }) }),
    // A brand-new route needs its own id - client-generated, same
    // convention as every other caller-generated id in this codebase (an
    // order line's own id, for one). RouteLevel "Category" is the only
    // level this form offers; Item/Product/DailySpecial routes are edited
    // through the existing PUT (no create UI for those yet).
    createCategoryRoute: (categoryId, printerId) => request<KitchenPrinterRoute>(`/routes/${encodeURIComponent(crypto.randomUUID())}`, {
      method: "POST",
      body: JSON.stringify({ routeLevel: "Category", printerId, categoryId, isActive: true }),
    }),
  };
}
