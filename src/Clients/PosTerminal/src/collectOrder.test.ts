import { afterEach, describe, expect, it, vi } from "vitest";
import { collectionHrefForOrder } from "./collectOrder";

afterEach(() => vi.unstubAllGlobals());

describe("collectionHrefForOrder", () => {
  it("goes straight to the bill the order already has, opening nothing", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    expect(await collectionHrefForOrder("t-1", "o-1", "b-1")).toBe("/cashier/payments/split-payment/index.html?billId=b-1");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("opens a bill from the order when it has none, then points at that bill", async () => {
    const fetchMock = vi.fn(async (..._args: unknown[]) => new Response(JSON.stringify({ billId: "b-new" }), { status: 200, headers: { "content-type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);

    expect(await collectionHrefForOrder("t-1", "o-1", null)).toBe("/cashier/payments/split-payment/index.html?billId=b-new");
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(String(fetchMock.mock.calls[0][0])).toContain("/terminals/t-1/billing/bills/from-order/o-1");
  });
});
