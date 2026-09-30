import { createRequester } from "../management/http";
import type { OrderInvoiceDetail, OrderInvoiceList } from "./models";

export interface OrderInvoicesClient {
  list: (from: string, to: string) => Promise<OrderInvoiceList>;
  detail: (orderId: string) => Promise<OrderInvoiceDetail>;
}

export function createOrderInvoicesClient(fetcher: typeof fetch = fetch): OrderInvoicesClient {
  const call = createRequester(fetcher, { prefix: "/order-invoices" });
  return {
    list: async (from, to) => (await call(`?from=${from}&to=${to}`)).json() as Promise<OrderInvoiceList>,
    detail: async (orderId) => (await call(`/by-order/${encodeURIComponent(orderId)}`)).json() as Promise<OrderInvoiceDetail>,
  };
}
