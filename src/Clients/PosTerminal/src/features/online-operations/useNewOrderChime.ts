import { useEffect } from "react";
import { playNewItemChime } from "../../audioAlerts";
import { loadOnlineOperations } from "./onlineOperationsApi";

const REFRESH_MS = 20_000;

/**
 * Sounds the new-order chime on screens that do not show the order queue themselves. The first read is only the
 * baseline (a shift that starts with orders already waiting stays quiet); after it, an order id not seen before chimes.
 */
export function useNewOrderChime(terminalId: string, enabled: boolean, fetcher: typeof fetch = fetch) {
  useEffect(() => {
    if (!enabled) return;
    let known: Set<string> | null = null;
    let stopped = false;
    const check = async () => {
      try {
        const queue = await loadOnlineOperations(terminalId, "all", fetcher);
        if (stopped) return;
        const ids = new Set(queue.orders.map((order) => order.orderId));
        if (known !== null && queue.orders.some((order) => !known!.has(order.orderId))) playNewItemChime();
        known = ids;
      } catch {
        // A failed poll only means this tick is silent; the next one tries again.
      }
    };
    void check();
    const timer = window.setInterval(() => void check(), REFRESH_MS);
    return () => {
      stopped = true;
      window.clearInterval(timer);
    };
  }, [terminalId, enabled, fetcher]);
}
