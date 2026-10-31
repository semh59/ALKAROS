import { createRequester } from "../management/http";
import type { TableQrCode, TableQrRow } from "./models";

export interface TableQrClient {
  list: () => Promise<TableQrRow[]>;
  issue: (tableId: string) => Promise<TableQrCode>;
  rotate: (tableId: string) => Promise<TableQrCode>;
  revoke: (tableId: string) => Promise<void>;
}

export function createTableQrClient(fetcher: typeof fetch = fetch): TableQrClient {
  const call = createRequester(fetcher, { prefix: "/qr-tables" });
  const post = (tableId: string, action: string) => call(`/${tableId}/${action}`, { method: "POST" });
  return {
    list: async () => (await call("")).json() as Promise<TableQrRow[]>,
    issue: async (tableId) => (await post(tableId, "issue")).json() as Promise<TableQrCode>,
    rotate: async (tableId) => (await post(tableId, "rotate")).json() as Promise<TableQrCode>,
    revoke: async (tableId) => { await post(tableId, "revoke"); },
  };
}
