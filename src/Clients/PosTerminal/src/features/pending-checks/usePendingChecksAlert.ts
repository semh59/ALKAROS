import { useEffect, useState } from "react";
import { playNewItemChime } from "../../audioAlerts";
import { loadPendingChecks, type PendingCheck } from "./pendingChecksApi";

const REFRESH_MS = 20_000;

/** True when a check the till had not seen before is in the list; the first read is only the baseline. */
export function hasNewCheck(known: ReadonlySet<string> | null, checks: readonly PendingCheck[]): boolean {
  return known !== null && checks.some((check) => !known.has(check.orderId));
}

/**
 * Keeps the till aware of checks that waiters sent over while another screen is open: the count for a badge and the
 * new-order chime. A shift that starts with checks already waiting stays quiet; after the first read a check id not
 * seen before chimes.
 */
export function usePendingChecksAlert(terminalId: string, enabled: boolean, fetcher: typeof fetch = fetch): number {
  const [count, setCount] = useState(0);
  useEffect(() => {
    if (!enabled) return;
    let known: Set<string> | null = null;
    let stopped = false;
    const check = async () => {
      try {
        const checks = await loadPendingChecks(terminalId, fetcher);
        if (stopped) return;
        if (hasNewCheck(known, checks)) playNewItemChime();
        known = new Set(checks.map((item) => item.orderId));
        setCount(checks.length);
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
  return count;
}
