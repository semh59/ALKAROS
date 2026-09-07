import type {
  CreateTableInput,
  CreateZoneInput,
  FloorPlan,
  SaveFloorPlanInput,
  SaveFloorPlanResult,
  TableActionRequest,
  TableRecord,
  TableZone,
} from "./models";

export class TableManagementApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) {
    super(message);
  }
}

export interface TableManagementClient {
  listZones: () => Promise<readonly TableZone[]>;
  listTables: () => Promise<readonly TableRecord[]>;
  createZone: (input: CreateZoneInput) => Promise<TableZone>;
  createTable: (input: CreateTableInput) => Promise<TableRecord>;
  getFloorPlan: (zoneId: string) => Promise<FloorPlan>;
  saveFloorPlan: (zoneId: string, input: SaveFloorPlanInput) => Promise<SaveFloorPlanResult>;
  execute: (request: TableActionRequest) => Promise<void>;
}

interface RequestOptions {
  method?: string;
  body?: unknown;
}

function createRequest(terminalId: string, fetcher: typeof fetch): TableManagementClient {
  const prefix = `/api/v1/terminals/${encodeURIComponent(terminalId)}/table-management`;

  async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
    let response: Response;
    try {
      response = await fetcher(`${prefix}${path}`, {
        method: options.method ?? "GET",
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID() },
        body: options.body === undefined ? undefined : JSON.stringify(options.body),
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new TableManagementApiError(0, "NETWORK_UNAVAILABLE", "Sunucuya ulaşılamadı. Tekrar deneyin.");
    }
    if (!response.ok) {
      const body = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
      throw new TableManagementApiError(response.status, body?.error?.code ?? "REQUEST_FAILED", body?.error?.message ?? "İşlem tamamlanamadı.");
    }
    if (response.status === 204) return undefined as T;
    return response.json() as Promise<T>;
  }

  return {
    listZones: () => request<TableZone[]>("/zones"),
    listTables: () => request<TableRecord[]>("/tables"),
    createZone: (input) => request<TableZone>("/zones", { method: "POST", body: { expectedRowVersion: 0, ...input } }),
    createTable: (input) => request<TableRecord>("/tables", { method: "POST", body: { expectedRowVersion: 0, ...input } }),
    getFloorPlan: (zoneId) => request<FloorPlan>(`/floor-plans/${encodeURIComponent(zoneId)}`),
    saveFloorPlan: (zoneId, input) => request<SaveFloorPlanResult>(`/floor-plans/${encodeURIComponent(zoneId)}`, { method: "PUT", body: input }),
    execute: async ({ table, action, reason, partySize, targetTableId, targetTableVersion, participantTableIds, participantTableVersions, mergeGroupId }) => {
      if (action === "SetOccupied" || action === "SetAvailable" || action === "SetCleaning" || action === "SetOutOfService") {
        await request(`/tables/${table.tableId}/status`, { method: "POST", body: { expectedRowVersion: table.rowVersion, status: action.slice(3) } });
        return;
      }
      if (action === "Reserve") {
        await request("/reservations", { method: "POST", body: { tableId: table.tableId, expectedTableRowVersion: table.rowVersion, partySize: partySize ?? table.capacity ?? 2, reason } });
        return;
      }
      if (action === "ClaimReservation" || action === "CancelReservation") {
        // Found by an independent audit (2026-09-07): unreachable until
        // TableRecord carried the reservation's own id/row version
        // (V1-RMD-117/118) — there was nowhere to route this request to.
        if (!table.activeReservationId) {
          throw new TableManagementApiError(409, "RESERVATION_MISSING", "Bu masa için aktif rezervasyon bulunamadı.");
        }
        const path = `/reservations/${encodeURIComponent(table.activeReservationId)}/${action === "ClaimReservation" ? "claim" : "cancel"}`;
        const body: Record<string, unknown> = {
          expectedReservationRowVersion: table.reservationRowVersion,
          expectedTableRowVersion: table.rowVersion,
        };
        if (action === "CancelReservation") body.reason = reason;
        await request(path, { method: "POST", body });
        return;
      }
      if (action === "Transfer") {
        if (!targetTableId) throw new TableManagementApiError(400, "TARGET_REQUIRED", "Hedef masa gerekli.");
        if (targetTableVersion === undefined) throw new TableManagementApiError(400, "TARGET_VERSION_REQUIRED", "Hedef masanın güncel sürümü gerekli.");
        await request("/transfers", { method: "POST", body: { sourceTableId: table.tableId, expectedSourceRowVersion: table.rowVersion, targetTableId, expectedTargetRowVersion: targetTableVersion, reason } });
        return;
      }
      if (action === "Merge") {
        await request("/merges", { method: "POST", body: { primaryTableId: table.tableId, expectedPrimaryRowVersion: table.rowVersion, participants: (participantTableVersions ?? (participantTableIds ?? []).map((tableId) => ({ tableId, rowVersion: 0 }))).map(({ tableId, rowVersion }) => ({ tableId, expectedRowVersion: rowVersion })), reason } });
        return;
      }
      if (action === "Unmerge") {
        if (!mergeGroupId) throw new TableManagementApiError(400, "MERGE_GROUP_REQUIRED", "Birleşim kimliği gerekli.");
        await request(`/merges/${encodeURIComponent(mergeGroupId)}/unmerge`, { method: "POST", body: { expectedPrimaryRowVersion: table.rowVersion, participants: (participantTableVersions ?? []).map(({ tableId, rowVersion }) => ({ tableId, expectedRowVersion: rowVersion })), reason } });
        return;
      }
      throw new TableManagementApiError(409, "UNSUPPORTED_ACTION", "Bu masa işlemi bu ekranda kullanılamıyor.");
    },
  };
}

export function createTableManagementClient(terminalId: string, fetcher: typeof fetch = fetch): TableManagementClient {
  if (!terminalId.trim()) throw new Error("terminalId is required");
  return createRequest(terminalId, fetcher);
}
