import { describe, expect, it, vi } from "vitest";
import { createKitchenOperationsClient, KitchenOperationsApiError, loadKitchenRuntimeConfiguration } from "./kitchenApi";

const payloads = {
  tickets: [{ id: "ticket-1", orderId: "order-1", ticketNumber: "KT-001", stationId: "hot-line", status: "Queued", rowVersion: 1, createdAt: "2026-08-26T00:00:00Z", updatedAt: null, acceptedAt: null, readyAt: null, cancelledAt: null, items: [] }],
  printers: [], routes: [], unknownDeliveries: [], backups: [], health: { snapshotId: "health-1", databaseStatus: "Healthy", diskStatus: "Healthy", lastBackupStatus: "Healthy", freeDiskBytes: 100, databaseSizeBytes: 100, capturedAt: "2026-08-26T00:00:00Z" },
  liveSync: { enabled: true, denseModeThreshold: 9 },
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
    expect(result.denseModeThreshold).toBe(9);
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

  // V1-KIT-013/V1-KDS-006: same reasoning as the liveSyncEnabled test above
  // — a hardcoded 9 would also have passed the "loads every production
  // surface" test's own denseModeThreshold assertion, since that test's
  // fixture value happens to be 9 too. This proves it is actually read.
  it("reads denseModeThreshold: 15 from the endpoint rather than defaulting to 9", async () => {
    const fetcher = vi.fn(async (url: RequestInfo | URL) => {
      const path = String(url);
      return new Response(JSON.stringify(path.includes("live-sync") ? { enabled: true, denseModeThreshold: 15 } : path.includes("health") ? payloads.health : path.includes("tickets") ? payloads.tickets : path.includes("backups") ? payloads.backups : path.includes("printers") ? payloads.printers : path.includes("routes") ? payloads.routes : payloads.unknownDeliveries), { status: 200, headers: { "Content-Type": "application/json" } });
    });
    const client = createKitchenOperationsClient("terminal-1", "hot-line", fetcher);
    const result = await client.load();
    expect(result.denseModeThreshold).toBe(15);
  });

  // V1-RMD-219: found by an independent audit (2026-09-16) - categories
  // used to be fetched from Catalog's own manager-cookie-protected
  // endpoint (/api/v1/management/catalog/categories), which kitchen-chef
  // (kitchen.routing.manage, never catalog.manage) always got a 401 from,
  // silently swallowed into an empty list. This proves categories now
  // come from this module's own terminal-scoped /categories path.
  it("reads categories from this module's own terminal-scoped path, not Catalog's management endpoint", async () => {
    const categories = [{ id: "category-1", name: "Izgara" }];
    const fetcher = vi.fn(async (url: RequestInfo | URL) => {
      const path = String(url);
      expect(path).not.toContain("/management/catalog/categories");
      return new Response(JSON.stringify(path.includes("/categories") ? categories : path.includes("health") ? payloads.health : path.includes("tickets") ? payloads.tickets : path.includes("backups") ? payloads.backups : path.includes("printers") ? payloads.printers : path.includes("routes") ? payloads.routes : path.includes("live-sync") ? payloads.liveSync : payloads.unknownDeliveries), { status: 200, headers: { "Content-Type": "application/json" } });
    });
    const client = createKitchenOperationsClient("terminal-1", "hot-line", fetcher);
    const result = await client.load();
    expect(result.categories).toEqual(categories);
    expect(fetcher).toHaveBeenCalledWith(
      expect.stringMatching(/\/kitchen-operations\/categories$/),
      expect.objectContaining({ credentials: "same-origin" }),
    );
  });

  it("keeps HTTP conflict and network errors typed", async () => {
    const conflict = createKitchenOperationsClient("terminal-1", "hot-line", vi.fn(async () => new Response(JSON.stringify({ error: { code: "CONCURRENT_MODIFICATION", message: "changed" } }), { status: 409 })));
    await expect(conflict.transitionTicket("ticket-1", "Ready", 1)).rejects.toMatchObject({ status: 409, code: "CONCURRENT_MODIFICATION" });
    const network = createKitchenOperationsClient("terminal-1", "hot-line", vi.fn(async () => { throw new Error("offline"); }));
    await expect(network.load()).rejects.toBeInstanceOf(KitchenOperationsApiError);
  });
  // V1-RMD-293: there is no server-side "every failed print job across a station" endpoint - load() fans out
  // one GET per currently loaded ticket instead. Two tickets prove it does not just read the first one.
  it("fetches print jobs per loaded ticket and surfaces only Failed/DeadLetter ones", async () => {
    const twoTickets = [
      { ...payloads.tickets[0], id: "ticket-1" },
      { ...payloads.tickets[0], id: "ticket-2", ticketNumber: "KT-002" },
    ];
    const printJobsByTicket: Record<string, unknown[]> = {
      "ticket-1": [
        { id: "job-1", ticketId: "ticket-1", printerId: "printer-1", status: "Failed", attemptCount: 2, maxAttempts: 5, failedAt: "2026-09-26T00:00:00Z", createdAt: "2026-09-26T00:00:00Z" },
        { id: "job-2", ticketId: "ticket-1", printerId: "printer-1", status: "Printed", attemptCount: 1, maxAttempts: 5, failedAt: null, createdAt: "2026-09-26T00:00:00Z" },
      ],
      "ticket-2": [
        { id: "job-3", ticketId: "ticket-2", printerId: "printer-2", status: "DeadLetter", attemptCount: 5, maxAttempts: 5, failedAt: "2026-09-26T00:05:00Z", createdAt: "2026-09-26T00:00:00Z" },
      ],
    };
    const fetcher = vi.fn(async (url: RequestInfo | URL) => {
      const path = String(url);
      if (path.includes("/print-jobs?ticketId=")) {
        const ticketId = new URL(path, "http://localhost").searchParams.get("ticketId")!;
        return new Response(JSON.stringify(printJobsByTicket[ticketId] ?? []), { status: 200, headers: { "Content-Type": "application/json" } });
      }
      return new Response(JSON.stringify(path.includes("health") ? payloads.health : path.includes("tickets") ? twoTickets : path.includes("backups") ? payloads.backups : path.includes("printers") ? payloads.printers : path.includes("routes") ? payloads.routes : path.includes("live-sync") ? payloads.liveSync : payloads.unknownDeliveries), { status: 200, headers: { "Content-Type": "application/json" } });
    });
    const client = createKitchenOperationsClient("terminal-1", "hot-line", fetcher);

    const result = await client.load();

    expect(fetcher).toHaveBeenCalledWith(expect.stringContaining("/print-jobs?ticketId=ticket-1"), expect.anything());
    expect(fetcher).toHaveBeenCalledWith(expect.stringContaining("/print-jobs?ticketId=ticket-2"), expect.anything());
    expect(result.printJobFailures).toHaveLength(2);
    expect(result.printJobFailures!.map((job) => job.id).sort()).toEqual(["job-1", "job-3"]);
    expect(result.printJobFailures!.some((job) => job.id === "job-2")).toBe(false);
  });
});
