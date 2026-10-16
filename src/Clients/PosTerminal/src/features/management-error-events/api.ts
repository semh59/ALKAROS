import type { ErrorEventDetail, ErrorEventPage, ErrorFilter } from "./models";
import { createRequester } from "../management/http";

export interface ErrorEventsClient {
  list: (filter: ErrorFilter, page: number) => Promise<ErrorEventPage>;
  detail: (fingerprint: string) => Promise<ErrorEventDetail>;
  channelConfigured: () => Promise<boolean>;
  change: (fingerprint: string, action: "acknowledge" | "resolve") => Promise<void>;
}

export function createErrorEventsClient(fetcher: typeof fetch = fetch): ErrorEventsClient {
  const call = createRequester(fetcher, { timeoutMs: 15_000 });
  const base = "/observability/errors";
  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  return {
    list(filter, page) {
      const query = new URLSearchParams({ page: String(page), pageSize: "25" });
      for (const [key, value] of Object.entries(filter)) if (value) query.set(key, value);
      return json(`${base}/?${query}`);
    },
    detail: (fingerprint) => json(`${base}/${encodeURIComponent(fingerprint)}`),
    channelConfigured: async () => (await json<{ configured: boolean }>(`${base}/notification-channel`)).configured,
    async change(fingerprint, action) {
      await call(`${base}/${encodeURIComponent(fingerprint)}/${action}`, { method: "POST", body: "{}" });
    },
  };
}
