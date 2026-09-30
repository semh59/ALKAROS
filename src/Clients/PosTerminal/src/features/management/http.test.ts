import { describe, expect, it, vi } from "vitest";
import { ManagementApiError, createRequester } from "./http";

const reply = (status: number, body?: unknown) => vi.fn().mockResolvedValue(new Response(body === undefined ? null : JSON.stringify(body), { status }));

describe("management requester", () => {
  it("prefixes the path and marks only POST with an idempotency key when asked", async () => {
    const fetcher = reply(200, {});
    const call = createRequester(fetcher, { prefix: "/inventory", idempotentPosts: true });
    await call("/stock-items", { method: "POST" });
    await call("/stock-items");
    const [postUrl, postInit] = fetcher.mock.calls[0];
    const getInit = fetcher.mock.calls[1][1];
    expect(postUrl).toBe("/api/v1/management/inventory/stock-items");
    expect(postInit.headers["X-Idempotency-Key"]).toBeTruthy();
    expect(getInit.headers["X-Idempotency-Key"]).toBeUndefined();
    expect(getInit.headers["X-Correlation-Id"]).toBeTruthy();
  });

  it("shows the server's own Turkish reason and falls back per status when there is none", async () => {
    await expect(createRequester(reply(409, { error: { code: "X", message: "Sunucu gerekçesi." } }))("/a")).rejects.toMatchObject({ status: 409, code: "X", message: "Sunucu gerekçesi." });
    await expect(createRequester(reply(409))("/a")).rejects.toMatchObject({ message: "Kayıt başka biri tarafından değiştirildi; yenileyip tekrar deneyin." });
    await expect(createRequester(reply(403))("/a")).rejects.toMatchObject({ message: "Bu işlem için yetkiniz yok." });
    await expect(createRequester(reply(500))("/a")).rejects.toMatchObject({ message: "İşlem tamamlanamadı." });
  });

  it("turns a network failure into a Turkish error", async () => {
    const error = await createRequester(vi.fn().mockRejectedValue(new TypeError("fetch failed")))("/a").catch((reason: unknown) => reason);
    expect(error).toBeInstanceOf(ManagementApiError);
    expect(error).toMatchObject({ status: 0, code: "NETWORK_UNAVAILABLE", message: "Sunucuya ulaşılamadı. Bağlantıyı kontrol edip tekrar deneyin." });
  });
});
