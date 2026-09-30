import { createRequester } from "../management/http";
import type { ChannelFilter, ChannelReport } from "./models";

export interface ChannelReportClient {
  report: (from: string, to: string, source: ChannelFilter) => Promise<ChannelReport>;
}

export function createChannelReportClient(fetcher: typeof fetch = fetch): ChannelReportClient {
  const call = createRequester(fetcher, { prefix: "/reports/channels" });
  return {
    report: async (from, to, source) =>
      (await call(`?from=${from}&to=${to}${source ? `&source=${source}` : ""}`)).json() as Promise<ChannelReport>,
  };
}
