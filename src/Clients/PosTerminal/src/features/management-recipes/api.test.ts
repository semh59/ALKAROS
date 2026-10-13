import { describe, expect, it, vi } from "vitest";
import { createRecipesClient } from "./api";

const base = "/api/v1/management";

function harness(status = 200, body: unknown = {}) {
  const fetcher = vi.fn((_url: string, _init?: RequestInit) => Promise.resolve(new Response(status === 204 ? null : JSON.stringify(body), { status })));
  const last = () => {
    const [url, init] = fetcher.mock.calls.at(-1)!;
    return { url, method: init?.method ?? "GET", body: init?.body ? JSON.parse(init.body as string) : undefined };
  };
  return { client: createRecipesClient(fetcher as unknown as typeof fetch), last };
}

describe("recipes client routes", () => {
  it("maps every write to the server's route, method and body", async () => {
    const { client, last } = harness();
    const cases: [() => Promise<unknown>, string, string, unknown][] = [
      [() => client.createRecipe("PIZ", "Pizza", " "), "POST", `${base}/recipes`, { code: "PIZ", name: "Pizza", description: null }],
      [() => client.createFirstVersion("r1", 4, "porsiyon"), "POST", `${base}/recipes/r1/versions`, { yieldQuantity: 4, yieldUnitCode: "porsiyon" }],
      [() => client.createNextVersion("r1"), "POST", `${base}/recipes/r1/versions/next`, undefined],
      [() => client.addIngredient("v1", { ingredientItemId: "s1", quantity: 2, unitCode: "kg", lossPercentage: 0 }), "POST", `${base}/recipes/versions/v1/ingredients`,
        { ingredientItemId: "s1", quantity: 2, unitCode: "kg", lossPercentage: 0 }],
      [() => client.removeIngredient("v1", "s1"), "DELETE", `${base}/recipes/versions/v1/ingredients/s1`, undefined],
      [() => client.activate("r1", 3), "POST", `${base}/recipes/r1/versions/3/activate`, undefined],
      [() => client.lock("v1"), "POST", `${base}/recipes/versions/v1/lock`, undefined],
      [() => client.createConversion("kasa", "adet", 12), "POST", `${base}/recipes/unit-conversions`, { fromUnitCode: "kasa", toUnitCode: "adet", factor: 12 }],
      [() => client.assignMapping("p1", "r1"), "POST", `${base}/recipes/products/p1/mapping`, { recipeId: "r1", isActive: true, notes: null }],
      [() => client.removeMapping("p1"), "DELETE", `${base}/recipes/products/p1/mapping`, undefined],
    ];
    for (const [call, method, url, body] of cases) {
      await call();
      expect(last()).toEqual({ url, method, body });
    }
  });

  it("sends an idempotency key with every POST", async () => {
    const fetcher = vi.fn((_url: string, _init?: RequestInit) => Promise.resolve(new Response("{}", { status: 200 })));
    await createRecipesClient(fetcher as unknown as typeof fetch).lock("v1");
    const headers = fetcher.mock.calls[0][1]!.headers as Record<string, string>;
    expect(headers["X-Idempotency-Key"]).toMatch(/^[0-9a-f-]{36}$/);
  });

  it("treats a product without a mapping as no mapping", async () => {
    const { client, last } = harness(404, { error: { code: "NOT_FOUND", message: "yok" } });
    expect(await client.getMapping("p1")).toBeNull();
    expect(last().url).toBe(`${base}/recipes/products/p1/mapping`);
  });

  it("lists recipes, versions, conversions and stock items from the server", async () => {
    const { client, last } = harness(200, []);
    await client.listRecipes();
    expect(last().url).toBe(`${base}/recipes`);
    await client.listVersions("r1");
    expect(last().url).toBe(`${base}/recipes/r1/versions`);
    await client.listConversions();
    expect(last().url).toBe(`${base}/recipes/unit-conversions`);
    await client.listStockItems();
    expect(last().url).toBe(`${base}/inventory/stock-items`);
  });
});
