import type { ActionFilter, ActionSummary, Journey, RequestSummary, StaffMovement } from "./models";
import { createRequester } from "../management/http";

export interface UserJourneysClient {
  actions: (filter: ActionFilter) => Promise<readonly ActionSummary[]>;
  requests: (sort: string, minMs: number) => Promise<readonly RequestSummary[]>;
  journey: (traceId: string) => Promise<Journey>;
  staffMovements: () => Promise<readonly StaffMovement[]>;
}

export function createUserJourneysClient(fetcher: typeof fetch = fetch): UserJourneysClient {
  const call = createRequester(fetcher, { timeoutMs: 15_000 });
  const base = "/observability/journeys";
  const json = async <T>(path: string): Promise<T> => (await call(path)).json() as Promise<T>;
  return {
    actions(filter) {
      const query = new URLSearchParams({ sort: filter.sort });
      if (filter.role) query.set("role", filter.role);
      if (filter.terminalId) query.set("terminalId", filter.terminalId);
      return json(`${base}/actions?${query}`);
    },
    requests: (sort, minMs) => json(`${base}/requests?${new URLSearchParams({ sort, minMs: String(minMs) })}`),
    journey: (traceId) => json(`${base}/requests/${encodeURIComponent(traceId)}`),
    staffMovements: () => json(`${base}/staff-movements`),
  };
}
