import { describe, expect, it, vi } from "vitest";
import { createKitchenOperationsClient, KitchenOperationsApiError, loadKitchenRuntimeConfiguration } from "./kitchenApi";

const payloads = {
  tickets: [{ id: "ticket-1", orderId: "order-1", ticketNumber: "KT-001", stationId: "hot-line", status: "Queued", rowVersion: 1, createdAt: "2026-08-26T00:00:00Z", updatedAt: null, acceptedAt: null, readyAt: null, cancelledAt: null, items: [] }],
  printers: [], routes: [], unknownDeliveries: [], backups: [], health: { snapshotId: "health-1", databaseStatus: "Healthy", diskStatus: "Healthy", lastBackupStatus: "Healthy", freeDiskBytes: 100, databaseSizeBytes: 100, capturedAt: "2026-08-26T00:00:00Z" },
  liveSync: { enabled: true },
};

describe("kitchen operations API client", () => {
  it("loads the authenticated authoritative kitchen station before station-scoped requests", async () => {
    const fetcher = vi.fn(async () => new Response(JSON.stringify({ kitchenStationId: " kitchen-main " }), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    }));

    await expect(loadKitchenRuntimeConfiguration("terminal/1", fetcher)).resolves.toEqual({ kitchenStationId: "kitchen-main" });
    expect(fetcher).toHaveBeenCalledWith(
      "/api/v1/terminals/terminal%2F1/runtime-configuration",
      expect.objectContaining({ credentials: "same-origin" }),
    );
  });

  it("fails closed when runtime configuration omits the kitchen station", async () => {
    const fetcher = vi.fn(async () => new Response(JSON.stringify({}), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    }));

    await expect(loadKitchenRuntimeConfiguration("terminal-1", fetcher)).rejects.toMatchObject({
      status: 502,
      code: "INVALID_KITCHEN_CONFIGURATION",
    });
  });

  it("loads every production surface with same-origin credentials and station scope", async () => {
    const fetcher = vi.fn(async (url: RequestInfo | URL, _init?: RequestInit) => {
      const path = String(url);
      return new Response(JSON.stringify(path.includes("health") ? payloads.health : path.includes("tickets") ? payloads.tickets : path.includes("backups") ? payloads.backups : path.includes("printers") ? payloads.printers : path.includes("routes") ? payloads.routes : path.includes("live-sync") ? payloads.liveSync : payloads.unknownDeliveries), { status: 200, headers: { "Content-Type": "application/json" } });
    });
    const client = createKitchenOperationsClient("terminal/1", "hot line", fetcher);
    const result = await client.load();
    expect(result.tickets).toHaveLength(1);
    expect(result.liveSyncEnabled).toBe(true);
    expect(fetcher).toHaveBeenCalledWith(expect.stringContaining("stationId=hot%20line"), expect.objectContaining({ credentials: "same-origin" }));
  });

  // Independent review (2026-09-13): the previous test only proved
  // load() *can* return true — a client that ignored the endpoint and
  // hardcoded true would have passed it too. This proves the field is
  // actually read from the response, not defaulted.
  it("reads liveSyncEnabled: false from the endpoint rather than defaulting", async () => {
    const fetcher = vi.fn(async (url: RequestInfo | URL) => {
      const path = String(url);
      return new Response(JSON.stringify(path.includes("live-sync") ? { enabled: false } : path.includes("health") ? payloads.health : path.includes("tickets") ? payloads.tickets : path.includes("backups") ? payloads.backups : path.includes("printers") ? payloads.printers : path.includes("routes") ? payloads.routes : payloads.unknownDeliveries), { status: 200, headers: { "Content-Type": "application/json" } });
    });
    const client = createKitchenOperationsClient("terminal-1", "hot-line", fetcher);
    const result = await client.load();
    expect(result.liveSyncEnabled).toBe(false);
  });

  it("keeps HTTP conflict and network errors typed", async () => {
    const conflict = createKitchenOperationsClient("terminal-1", "hot-line", vi.fn(async () => new Response(JSON.stringify({ error: { code: "CONCURRENT_MODIFICATION", message: "changed" } }), { status: 409 })));
    await expect(conflict.transitionTicket("ticket-1", "Ready", 1)).rejects.toMatchObject({ status: 409, code: "CONCURRENT_MODIFICATION" });
    const network = createKitchenOperationsClient("terminal-1", "hot-line", vi.fn(async () => { throw new Error("offline"); }));
    await expect(network.load()).rejects.toBeInstanceOf(KitchenOperationsApiError);
  });
});
