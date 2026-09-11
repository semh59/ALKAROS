// @vitest-environment jsdom

import { describe, expect, it, vi } from "vitest";
import { createCatalogManagementClient, CatalogApiError } from "./catalogApi";

const response = (body: unknown, status = 200, ok = true) => ({ ok, status, json: vi.fn().mockResolvedValue(body) }) as unknown as Response;

describe("catalog management client", () => {
  it("loads all versioned manager resources with bounded pages", async () => {
    const fetcher = vi.fn().mockResolvedValue(response({ items: [], nextCursor: null }));
    await createCatalogManagementClient(fetcher).load();
    expect(fetcher).toHaveBeenCalledTimes(6);
    expect(fetcher).toHaveBeenCalledWith("/api/v1/management/catalog/products?limit=100", expect.objectContaining({ credentials: "same-origin" }));
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
