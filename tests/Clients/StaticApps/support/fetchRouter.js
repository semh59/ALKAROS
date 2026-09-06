import { vi } from "vitest";

/**
 * A minimal fetch stub for these tests: each app under test only cares
 * about response.ok / response.status / response.json(), so this returns
 * a plain object rather than depending on a real Response/Headers global.
 *
 * @param {Array<{ test: (url: string, init?: RequestInit) => boolean, status?: number, body?: unknown }>} routes
 *   Checked in order; the first match wins. Every call is also recorded on
 *   the returned mock's `.mock.calls` for assertions on call order/shape.
 */
export function installFetchRouter(routes) {
  const fetchMock = vi.fn(async (url, init) => {
    const href = typeof url === "string" ? url : String(url);
    const route = routes.find((candidate) => candidate.test(href, init));
    if (!route) {
      throw new Error(`No fetch route matched ${init?.method ?? "GET"} ${href}`);
    }
    const status = route.status ?? 200;
    return {
      ok: status >= 200 && status < 300,
      status,
      json: async () => route.body ?? {},
    };
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}
