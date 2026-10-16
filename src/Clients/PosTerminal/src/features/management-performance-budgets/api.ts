import { createRequester } from "../management/http";
import type { PerformanceBudget, PerformanceBudgetList } from "./models";

export interface PerformanceBudgetsClient {
  list: () => Promise<PerformanceBudgetList>;
  save: (budget: PerformanceBudget) => Promise<void>;
  remove: (metric: string, name: string) => Promise<void>;
}

export function createPerformanceBudgetsClient(fetcher: typeof fetch = fetch): PerformanceBudgetsClient {
  const call = createRequester(fetcher, { timeoutMs: 15_000 });
  const base = "/observability/performance-budgets/";
  return {
    async list() { return (await call(base)).json() as Promise<PerformanceBudgetList>; },
    async save(budget) { await call(base, { method: "PUT", body: JSON.stringify(budget) }); },
    async remove(metric, name) { await call(`${base}?${new URLSearchParams({ metric, name })}`, { method: "DELETE" }); },
  };
}
