// @vitest-environment jsdom

import { describe, expect, it, vi } from "vitest";
import { registerSessionExpiredHandler } from "../../api";
import { createCatalogManagementClient, CatalogApiError } from "./catalogApi";
import { isCatalogEmpty } from "./models";

const response = (body: unknown, status = 200, ok = true) => ({ ok, status, json: vi.fn().mockResolvedValue(body) }) as unknown as Response;

describe("catalog management client", () => {
  it("loads all versioned manager resources with bounded pages", async () => {
    const fetcher = vi.fn().mockResolvedValue(response({ items: [], nextCursor: null }));
    await createCatalogManagementClient(fetcher).load();
    expect(fetcher).toHaveBeenCalledTimes(7);
    expect(fetcher).toHaveBeenCalledWith("/api/v1/management/catalog/product-modifier-assignments?limit=100", expect.objectContaining({ credentials: "same-origin" }));
    expect(fetcher).toHaveBeenCalledWith("/api/v1/management/catalog/products?limit=100", expect.objectContaining({ credentials: "same-origin" }));
  });

  it("follows the cursor until every page of a resource is loaded", async () => {
    const fetcher = vi.fn().mockImplementation(async (url: string) => {
      if (url.startsWith("/api/v1/management/catalog/products?") && !url.includes("cursor=")) return response({ items: [{ id: "p-1" }], nextCursor: "next page/1" });
      if (url.includes("cursor=next%20page%2F1")) return response({ items: [{ id: "p-2" }], nextCursor: null });
      return response({ items: [], nextCursor: null });
    });
    const data = await createCatalogManagementClient(fetcher).load();
    expect(data.products.map((item) => (item as { id: string }).id)).toEqual(["p-1", "p-2"]);
    expect(fetcher).toHaveBeenCalledWith("/api/v1/management/catalog/products?limit=100&cursor=next%20page%2F1", expect.anything());
  });

  it("stops with an error instead of silently truncating a runaway cursor chain", async () => {
    const fetcher = vi.fn().mockResolvedValue(response({ items: [], nextCursor: "again" }));
    await expect(createCatalogManagementClient(fetcher).load()).rejects.toMatchObject({ code: "CATALOG_TOO_LARGE" });
  });

  it("reports a 401 to the shared session-expired handler", async () => {
    const handler = vi.fn();
    registerSessionExpiredHandler(handler);
    const fetcher = vi.fn().mockResolvedValue(response({ error: { code: "UNAUTHORIZED", message: "Oturum gerekli." } }, 401, false));
    await expect(createCatalogManagementClient(fetcher).setAvailability("p-1", false)).rejects.toBeInstanceOf(CatalogApiError);
    registerSessionExpiredHandler(null);
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it("treats a catalog with no records at all as empty", () => {
    expect(isCatalogEmpty({ categories: [], taxes: [], products: [], modifierGroups: [], modifiers: [], prices: [], assignments: [] })).toBe(true);
    expect(isCatalogEmpty({ categories: [], taxes: [{ id: "t" }], products: [], modifierGroups: [], modifiers: [], prices: [], assignments: [] } as never)).toBe(false);
  });

  it("posts a typed product create and preserves server conflict details", async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(response({ id: "p-1" })).mockResolvedValueOnce(response({ error: { code: "DUPLICATE_SKU", message: "The product SKU already exists." } }, 409, false));
    const client = createCatalogManagementClient(fetcher);
    await client.create({ kind: "products", value: { id: "p-1", sku: "ESP-01", name: "Espresso", productType: "MenuItem", stockMode: "Untracked", categoryId: null, taxProfileId: null, description: null, printerRoutePolicy: null, displayOrder: 0, currentPrice: 95 } });
    expect(fetcher).toHaveBeenNthCalledWith(1, "/api/v1/management/catalog/products", expect.objectContaining({ method: "POST", body: expect.stringContaining("ESP-01") }));
    await expect(client.create({ kind: "products", value: { id: "p-2", sku: "ESP-01", name: "Duplicate", productType: "MenuItem", stockMode: "Untracked", categoryId: null, taxProfileId: null, description: null, printerRoutePolicy: null, displayOrder: 0, currentPrice: 95 } })).rejects.toMatchObject({ status: 409, code: "DUPLICATE_SKU" } satisfies Partial<CatalogApiError>);
  });

  it("posts a typed modifier group create to its own resource", async () => {
    // Found by an independent audit (2026-09-07): modifierGroups had no
    // create() branch at all — an operator could create individual
    // modifiers but never the group their mandatory ModifierGroupId must
    // reference, since /modifier-groups was unreachable from this client.
    const fetcher = vi.fn().mockResolvedValueOnce(response({ id: "g-1" }));
    const client = createCatalogManagementClient(fetcher);
    await client.create({ kind: "modifierGroups", value: { id: "g-1", code: "MILK", name: "Süt seçimi", selectionType: "SelectOne", minSelections: 0, maxSelections: 1 } });
    expect(fetcher).toHaveBeenNthCalledWith(1, "/api/v1/management/catalog/modifier-groups", expect.objectContaining({ method: "POST", body: expect.stringContaining("MILK") }));
  });

  it("posts a product modifier group assignment to its own resource", async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(response({ id: "a-1" }));
    await createCatalogManagementClient(fetcher).create({ kind: "assignments", value: { id: "a-1", productId: "p-1", modifierGroupId: "g-1" } });
    expect(fetcher).toHaveBeenNthCalledWith(1, "/api/v1/management/catalog/product-modifier-assignments", expect.objectContaining({ method: "POST", body: expect.stringContaining("g-1") }));
  });

  it("posts a prep-time update, including the null-to-clear case", async () => {
    // V1-WTR-017.
    const fetcher = vi.fn().mockResolvedValueOnce(response({ id: "p-1" })).mockResolvedValueOnce(response({ id: "p-1" }));
    const client = createCatalogManagementClient(fetcher);
    await client.setPrepTime("p-1", 12);
    expect(fetcher).toHaveBeenNthCalledWith(1, "/api/v1/management/catalog/products/p-1/prep-time", expect.objectContaining({ method: "POST", body: JSON.stringify({ prepTimeMinutes: 12 }) }));
    await client.setPrepTime("p-1", null);
    expect(fetcher).toHaveBeenNthCalledWith(2, "/api/v1/management/catalog/products/p-1/prep-time", expect.objectContaining({ method: "POST", body: JSON.stringify({ prepTimeMinutes: null }) }));
  });
});
