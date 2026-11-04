import { notifySessionExpired } from "../../api";

export type CashSessionState = "open" | "closed" | "forbidden" | "unknown";

/** Where the cashier opens, counts and closes the drawer. */
export const cashSessionPageHref = "/cashier/payments/cash-session/index.html";

/**
 * Whether this terminal has an open drawer. Only the server's 404 means "closed"; 403 means this user may not use
 * the drawer; every other failure (429, 5xx, no network) is "unknown", because calling it closed would tell a cashier with a working
 * drawer to open another one.
 */
export async function readCashSessionState(terminalId: string, fetcher: typeof fetch = fetch): Promise<CashSessionState> {
  let response: Response;
  try {
    response = await fetcher(`/api/v1/terminals/${encodeURIComponent(terminalId)}/cash-sessions/active`, {
      credentials: "same-origin",
      signal: AbortSignal.timeout(8_000),
    });
  } catch {
    return "unknown";
  }
  if (response.ok) return "open";
  if (response.status === 404) return "closed";
  if (response.status === 403) return "forbidden";
  if (response.status === 401) notifySessionExpired();
  return "unknown";
}
