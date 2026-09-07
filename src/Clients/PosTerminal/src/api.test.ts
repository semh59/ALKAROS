import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError, api } from "./api";

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

const plainErrorResponse = (status: number) =>
  new Response("Bad gateway", { status, headers: { "Content-Type": "text/plain" } });

/**
 * V12-QRT-001: `placeNfcOrder`'s bounded, backed-off retry — the client-side
 * half of "no outage queue on the transport, so durability comes from an
 * idempotent client retry instead" (api.ts's own top-of-file comment).
 * Fake timers replace the real 500ms/1s/2s backoff so this runs instantly.
 */
describe("api.placeNfcOrder retry", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  const placedOrder = {
    orderId: "o1",
    tableId: "table-1",
    tableNumber: "T-01",
    status: "Accepted",
    rowVersion: 1,
    totalAmount: 60,
    items: [],
    createdAt: new Date().toISOString(),
  };

  it("retries a transient 502 and resolves once the connector comes back", async () => {
    let calls = 0;
    vi.stubGlobal("fetch", vi.fn(async () => {
      calls += 1;
      return calls === 1 ? plainErrorResponse(502) : jsonResponse(placedOrder);
    }));

    const promise = api.placeNfcOrder("table-1", [{ id: "i1", productId: "p1", quantity: 1 }], "sub-1");
    await vi.runAllTimersAsync();
    const result = await promise;

    expect(calls).toBe(2);
    expect(result.orderId).toBe("o1");
  });

  it("does not retry a definitive rejection (409) — fails on the first attempt", async () => {
    let calls = 0;
    vi.stubGlobal("fetch", vi.fn(async () => {
      calls += 1;
      return jsonResponse({ error: { code: "TABLE_NOT_AVAILABLE", message: "Masada kendi kendine sipariş verilemiyor." } }, 409);
    }));

    const promise = api.placeNfcOrder("table-1", [{ id: "i1", productId: "p1", quantity: 1 }], "sub-1");
    const assertion = expect(promise).rejects.toMatchObject({ status: 409, code: "TABLE_NOT_AVAILABLE" } satisfies Partial<ApiError>);
    await vi.runAllTimersAsync();
    await assertion;
    expect(calls).toBe(1);
  });

  it("gives up after exhausting the retry budget on a sustained outage", async () => {
    let calls = 0;
    vi.stubGlobal("fetch", vi.fn(async () => {
      calls += 1;
      return plainErrorResponse(502);
    }));

    const promise = api.placeNfcOrder("table-1", [{ id: "i1", productId: "p1", quantity: 1 }], "sub-1");
    const assertion = expect(promise).rejects.toBeInstanceOf(ApiError);
    await vi.runAllTimersAsync();
    await assertion;
    // 1 initial attempt + 3 retries = 4 total, never more.
    expect(calls).toBe(4);
  });

  it("every retry replays the exact same submission id — never a fresh one", async () => {
    const submittedIds: string[] = [];
    vi.stubGlobal("fetch", vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      const body = JSON.parse(String(init?.body)) as { id: string };
      submittedIds.push(body.id);
      return submittedIds.length === 1 ? plainErrorResponse(503) : jsonResponse(placedOrder);
    }));

    const promise = api.placeNfcOrder("table-1", [{ id: "i1", productId: "p1", quantity: 1 }], "sub-stable");
    await vi.runAllTimersAsync();
    await promise;

    expect(submittedIds).toEqual(["sub-stable", "sub-stable"]);
  });
});
