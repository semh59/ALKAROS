// @vitest-environment jsdom

import { describe, expect, it, vi } from "vitest";
import { createTableManagementClient, TableManagementApiError } from "./tableApi";

function response(body: unknown, init: { status?: number; ok?: boolean } = {}): Response {
  return { ok: init.ok ?? true, status: init.status ?? 200, json: vi.fn().mockResolvedValue(body) } as unknown as Response;
}

describe("table management client", () => {
  it("uses the versioned terminal-bound contract for reads and manager creates", async () => {
    const fetcher = vi.fn()
      .mockResolvedValueOnce(response([]))
      .mockResolvedValueOnce(response({ tableId: "table-11", tableNumber: "S-11" }));
    const client = createTableManagementClient("terminal/1", fetcher);

    await client.listZones();
    await client.createTable({ tableNumber: "S-11", zoneId: null, capacity: 4 });

    expect(fetcher).toHaveBeenNthCalledWith(1, "/api/v1/terminals/terminal%2F1/table-management/zones", expect.objectContaining({ method: "GET", credentials: "same-origin" }));
    expect(fetcher).toHaveBeenNthCalledWith(2, "/api/v1/terminals/terminal%2F1/table-management/tables", expect.objectContaining({ method: "POST", body: JSON.stringify({ expectedRowVersion: 0, tableNumber: "S-11", zoneId: null, capacity: 4 }) }));
  });

  it("sends authoritative row versions for status, transfer, merge and unmerge mutations", async () => {
    const fetcher = vi.fn().mockResolvedValue(response(undefined, { status: 204 }));
    const client = createTableManagementClient("terminal-1", fetcher);
    const table = {
      tableId: "table-10", tableNumber: "S-10", zoneId: null, capacity: 2, active: true,
      status: "Occupied" as const, currentOrderId: "order-1", currentBillId: null, rowVersion: 9,
      allowedCommands: ["Transfer", "Merge"],
    };

    await client.execute({ table, action: "SetAvailable" });
    await client.execute({ table, action: "Transfer", targetTableId: "table-09", targetTableVersion: 4, reason: "Servis" });
    await client.execute({ table, action: "Merge", participantTableVersions: [{ tableId: "table-09", rowVersion: 4 }], reason: "Birleştir" });
    await client.execute({ table, action: "Unmerge", mergeGroupId: "merge/group", participantTableVersions: [{ tableId: "table-09", rowVersion: 4 }], reason: "Ayır" });

    expect(fetcher).toHaveBeenNthCalledWith(1, "/api/v1/terminals/terminal-1/table-management/tables/table-10/status", expect.objectContaining({ body: JSON.stringify({ expectedRowVersion: 9, status: "Available" }) }));
    expect(fetcher).toHaveBeenNthCalledWith(2, "/api/v1/terminals/terminal-1/table-management/transfers", expect.objectContaining({ body: JSON.stringify({ sourceTableId: "table-10", expectedSourceRowVersion: 9, targetTableId: "table-09", expectedTargetRowVersion: 4, reason: "Servis" }) }));
    expect(fetcher).toHaveBeenNthCalledWith(3, "/api/v1/terminals/terminal-1/table-management/merges", expect.objectContaining({ body: JSON.stringify({ primaryTableId: "table-10", expectedPrimaryRowVersion: 9, participants: [{ tableId: "table-09", expectedRowVersion: 4 }], reason: "Birleştir" }) }));
    expect(fetcher).toHaveBeenNthCalledWith(4, "/api/v1/terminals/terminal-1/table-management/merges/merge%2Fgroup/unmerge", expect.objectContaining({ body: JSON.stringify({ expectedPrimaryRowVersion: 9, participants: [{ tableId: "table-09", expectedRowVersion: 4 }], reason: "Ayır" }) }));
  });

  it("preserves server status and error envelope", async () => {
    const fetcher = vi.fn().mockResolvedValue(response({ error: { code: "CONCURRENT_MODIFICATION", message: "row version conflict" } }, { status: 409, ok: false }));
    const client = createTableManagementClient("terminal-1", fetcher);
    await expect(client.listTables()).rejects.toMatchObject({ status: 409, code: "CONCURRENT_MODIFICATION" } satisfies Partial<TableManagementApiError>);
  });

  it("reads and atomically saves the encoded floor-plan route", async () => {
    const fetcher = vi.fn()
      .mockResolvedValueOnce(response({ zoneId: "zone/1", tables: [] }))
      .mockResolvedValueOnce(response({ floorPlan: { zoneId: "zone/1", tables: [] }, warnings: [] }));
    const client = createTableManagementClient("terminal-1", fetcher);
    await client.getFloorPlan("zone/1");
    await client.saveFloorPlan("zone/1", { expectedRowVersion: 2, canvasWidth: 1000, canvasHeight: 600, tables: [] });

    expect(fetcher).toHaveBeenNthCalledWith(1, "/api/v1/terminals/terminal-1/table-management/floor-plans/zone%2F1", expect.objectContaining({ method: "GET" }));
    expect(fetcher).toHaveBeenNthCalledWith(2, "/api/v1/terminals/terminal-1/table-management/floor-plans/zone%2F1", expect.objectContaining({ method: "PUT", body: JSON.stringify({ expectedRowVersion: 2, canvasWidth: 1000, canvasHeight: 600, tables: [] }) }));
  });
});
