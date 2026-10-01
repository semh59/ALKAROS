import { createRequester } from "../management/http";
import type { ProductMarginReport } from "./models";

export interface ProductMarginClient {
  report: (from: string, to: string) => Promise<ProductMarginReport>;
}

export function createProductMarginClient(fetcher: typeof fetch = fetch): ProductMarginClient {
  const call = createRequester(fetcher, { prefix: "/reports/product-margin" });
  return { report: async (from, to) => (await call(`?from=${from}&to=${to}`)).json() as Promise<ProductMarginReport> };
}
