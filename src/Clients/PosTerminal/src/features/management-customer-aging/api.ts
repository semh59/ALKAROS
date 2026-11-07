import { createRequester } from "../management/http";
import type { CustomerAgingReport } from "./models";

export interface CustomerAgingClient {
  report: () => Promise<CustomerAgingReport>;
}

export function createCustomerAgingClient(fetcher: typeof fetch = fetch): CustomerAgingClient {
  const call = createRequester(fetcher, { prefix: "/reports/customer-aging" });
  return { report: async () => (await call("/")).json() as Promise<CustomerAgingReport> };
}
