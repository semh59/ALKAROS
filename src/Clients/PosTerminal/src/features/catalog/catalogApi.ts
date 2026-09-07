import type { CatalogCreateInput, CatalogData } from "./models";

export class CatalogApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string) { super(message); }
}

export interface CatalogManagementClient {
  load: () => Promise<CatalogData>;
  create: (input: CatalogCreateInput) => Promise<unknown>;
  setAvailability: (productId: string, isAvailable: boolean) => Promise<unknown>;
}

interface Page<T> { items: T[]; nextCursor: string | null }

export function createCatalogManagementClient(fetcher: typeof fetch = fetch): CatalogManagementClient {
  const prefix = "/api/v1/management/catalog";
  async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
    let response: Response;
    try {
      response = await fetcher(`${prefix}${path}`, {
        ...options,
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-Correlation-Id": crypto.randomUUID(), ...options.headers },
        signal: AbortSignal.timeout(8_000),
      });
    } catch {
      throw new CatalogApiError(0, "NETWORK_UNAVAILABLE", "Katalog sunucusuna ulaşılamadı.");
    }
    if (!response.ok) {
      const body = await response.json().catch(() => undefined) as { error?: { code?: string; message?: string } } | undefined;
      throw new CatalogApiError(response.status, body?.error?.code ?? "REQUEST_FAILED", body?.error?.message ?? "Katalog işlemi tamamlanamadı.");
    }
    return response.status === 204 ? undefined as T : response.json() as Promise<T>;
  }
  const list = <T>(path: string) => request<Page<T> | T[]>(`${path}?limit=100`).then((result) => Array.isArray(result) ? result : result.items);
  return {
    load: async () => {
      const [categories, taxes, products, modifierGroups, modifiers, prices] = await Promise.all([
        list("/categories"), list("/tax-profiles"), list("/products"), list("/modifier-groups"), list("/modifiers"), list("/prices"),
      ]);
      return { categories, taxes, products, modifierGroups, modifiers, prices } as CatalogData;
    },
    create: (input) => {
      const paths = { categories: "/categories", taxes: "/tax-profiles", products: "/products", modifierGroups: "/modifier-groups", modifiers: "/modifiers", prices: "/prices" } as const;
      return request(`${paths[input.kind]}`, { method: "POST", body: JSON.stringify(input.value) });
    },
    setAvailability: (productId, isAvailable) =>
      request(`/products/${productId}/availability`, { method: "POST", body: JSON.stringify({ isAvailable }) }),
  };
}
