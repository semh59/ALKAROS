import { describe, expect, it, vi } from "vitest";
import { createCostClient } from "./api";

const reply = (body: unknown) => Promise.resolve(new Response(JSON.stringify(body), { status: 200 }));

describe("cost client", () => {
  it("sends the tracking unit of every stock item with a cost calculation", async () => {
    const fetcher = vi.fn((url: string, _init?: RequestInit) => reply(url.endsWith("/inventory/stock-items")
      ? [{ id: "i1", name: "Un", trackingUnitCode: "kg" }, { id: "i2", name: "Süt", trackingUnitCode: "l" }]
      : { id: "s1" }));
    await createCostClient(fetcher as unknown as typeof fetch).calculate("v1", "2026-09-30");
    const post = fetcher.mock.calls.find(([, init]) => init?.method === "POST")!;
    expect(post[0]).toBe("/api/v1/management/recipes/v1/cost-snapshots");
    expect(JSON.parse(post[1]!.body as string)).toEqual({ costBasisDate: "2026-09-30", stockItemUnits: { i1: "kg", i2: "l" } });
  });

  it("treats a missing effective snapshot as no cost", async () => {
    const fetcher = vi.fn(() => Promise.resolve(new Response(JSON.stringify({ error: { code: "NOT_FOUND", message: "yok" } }), { status: 404 })));
    expect(await createCostClient(fetcher as unknown as typeof fetch).getEffective("v1", "2026-09-30")).toBeNull();
  });
});
