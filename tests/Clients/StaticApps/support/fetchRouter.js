import { vi } from "vitest";

/**
 * A minimal fetch stub for these tests: each app under test only cares
 * about response.ok / response.status / response.json(), so this returns
 * a plain object rather than depending on a real Response/Headers global.
 *
 * @param {Array<{ test: (url: string, init?: RequestInit) => boolean, status?: number, body?: unknown, headers?: Record<string, string>, delayMs?: number }>} routes
 *   Checked in order; the first match wins. Every call is also recorded on
 *   the returned mock's `.mock.calls` for assertions on call order/shape.
 *
 * `headers.get(name)` (V1-RMD-205): found while adding new routes to this
 * suite — cashier-app.js's fetchWholeCatalogAsync reads
 * `response.headers.get('X-Next-Cursor')` on every real fetch, but this
 * stub never gave a route a `headers` object at all, so every cashier-app
 * test that reaches catalog loading threw synchronously (caught by
 * loadCatalog's own try/catch, silently leaving state.products empty) —
 * a pre-existing bug in this shared test double, independent of the new
 * routes. A route with no `headers` now answers `null` for any header
 * name, which is exactly "no next page" to the real pagination loop.
 *
 * `delayMs` (V1-RMD-213): a route can defer its own resolution — used to
 * open a real window during an in-flight `await` (e.g. a send that is
 * still awaiting the server) for a test to act inside, instead of a flat
 * 0ms tick that some races never reproduce with.
 */
export function installFetchRouter(routes) {
  const fetchMock = vi.fn(async (url, init) => {
    const href = typeof url === "string" ? url : String(url);
    const route = routes.find((candidate) => candidate.test(href, init));
    if (!route) {
      throw new Error(`No fetch route matched ${init?.method ?? "GET"} ${href}`);
    }
    if (route.delayMs) await new Promise((resolve) => setTimeout(resolve, route.delayMs));
    const status = route.status ?? 200;
    return {
      ok: status >= 200 && status < 300,
      status,
      json: async () => route.body ?? {},
      headers: { get: (name) => route.headers?.[name] ?? null },
    };
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}
