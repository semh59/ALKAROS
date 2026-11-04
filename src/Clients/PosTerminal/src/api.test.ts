import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError, api } from "./api";

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

const plainErrorResponse = (status: number) =>
  new Response("Bad gateway", { status, headers: { "Content-Type": "text/plain" } });

describe("ApiError retry hint", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("carries the Retry-After seconds of a rate-limited answer and nothing without the header", async () => {
    const limited = (headers: Record<string, string>) =>
      new Response(JSON.stringify({ error: { code: "RATE_LIMITED", message: "x" } }), { status: 429, headers });
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce(limited({ "Content-Type": "application/json", "Retry-After": "9" }))
      .mockResolvedValueOnce(limited({ "Content-Type": "application/json" })));

    const first = await api.createPairing("d").catch((reason: unknown) => reason);
    const second = await api.createPairing("d").catch((reason: unknown) => reason);

    expect(first).toBeInstanceOf(ApiError);
    expect((first as ApiError).retryAfterSeconds).toBe(9);
    expect((second as ApiError).retryAfterSeconds).toBeUndefined();
  });

  it.each([
    ["86400", 300],
    ["301", 300],
    ["300", 300],
    ["299", 299],
    ["0", undefined],
    ["-3", undefined],
    ["abc", undefined],
    ["", undefined],
  ])("reads Retry-After %j as %j: capped so a screen is never parked for a day, never zero or negative", async (header, expected) => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(
      new Response("{}", { status: 429, headers: { "Content-Type": "application/json", "Retry-After": header } })));

    const failure = await api.createPairing("d").catch((reason: unknown) => reason);

    expect((failure as ApiError).retryAfterSeconds).toBe(expected);
  });
});

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

describe("api.catalog paging", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("follows the Host's next-cursor header until the catalog is complete", async () => {
    const seen: string[] = [];
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      seen.push(path);
      if (path.endsWith("/catalog")) {
        return new Response(JSON.stringify([{ productId: "p1" }]), { status: 200, headers: { "Content-Type": "application/json", "X-Next-Cursor": "c 1" } });
      }
      return new Response(JSON.stringify([{ productId: "p2" }]), { status: 200, headers: { "Content-Type": "application/json" } });
    }));

    const products = await api.catalog("t-1");

    expect(products.map((product) => (product as unknown as { productId: string }).productId)).toEqual(["p1", "p2"]);
    expect(seen).toEqual(["/api/v1/terminals/t-1/catalog", "/api/v1/terminals/t-1/catalog?cursor=c%201"]);
  });
});

describe("api.submitOrder operation key", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  const stubFetch = (outcomes: Array<"fail" | "refused" | "offline" | "ok">) => {
    const keys: string[] = [];
    vi.stubGlobal("fetch", vi.fn(async (_path: string, init: RequestInit) => {
      keys.push((JSON.parse(String(init.body)) as { operationId: string }).operationId);
      const outcome = outcomes.shift();
      if (outcome === "offline") throw new TypeError("Failed to fetch");
      if (outcome === "fail") return plainErrorResponse(502);
      return outcome === "refused" ? plainErrorResponse(409) : jsonResponse({ rowVersion: 2 });
    }));
    return keys;
  };

  it("sends the same key when the same order revision is submitted again after a failure", async () => {
    const keys = stubFetch(["fail", "ok"]);

    await expect(api.submitOrder("t1", "o1", 3)).rejects.toBeInstanceOf(ApiError);
    await api.submitOrder("t1", "o1", 3);

    expect(keys).toHaveLength(2);
    expect(keys[1]).toBe(keys[0]);
  });

  it("uses a new key for another revision and once the order was accepted", async () => {
    const keys = stubFetch(["fail", "ok", "ok"]);

    await expect(api.submitOrder("t1", "o2", 3)).rejects.toBeInstanceOf(ApiError);
    await api.submitOrder("t1", "o2", 4);
    await api.submitOrder("t1", "o2", 4);

    expect(new Set(keys).size).toBe(3);
  });

  it("keeps the key when the call never got an answer", async () => {
    const keys = stubFetch(["offline", "ok"]);

    await expect(api.submitOrder("t1", "o4", 3)).rejects.toBeInstanceOf(Error);
    await api.submitOrder("t1", "o4", 3);

    expect(keys[1]).toBe(keys[0]);
  });

  it("uses a new key after the server refused the revision for good", async () => {
    const keys = stubFetch(["refused", "ok"]);

    await expect(api.submitOrder("t1", "o3", 3)).rejects.toBeInstanceOf(ApiError);
    await api.submitOrder("t1", "o3", 3);

    expect(keys[1]).not.toBe(keys[0]);
  });
});
