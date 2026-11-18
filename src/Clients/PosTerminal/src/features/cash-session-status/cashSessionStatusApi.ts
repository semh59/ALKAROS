import { notifySessionExpired } from "../../api";

export type CashSessionState = "open" | "notOwned" | "closed" | "forbidden" | "unknown";

/** Where the cashier opens, counts and closes the drawer. */
export const cashSessionPageHref = "/cashier/payments/cash-session/index.html";

/** The drawer page that brings the cashier back to the current page once the drawer is open (it accepts only a same-origin path). */
export const cashSessionOpenHref = () => `${cashSessionPageHref}?returnTo=${encodeURIComponent(window.location.pathname + window.location.search)}`;

/** The same page opened on its no-sale reason form: the form stays there, where the server asks for the reason and writes the audit record. */
export const cashSessionNoSaleHref = () => `${cashSessionOpenHref()}&open=no-sale`;

/**
 * Whether this terminal has an open drawer, and whether it is the signed-in user's: cash is taken only into the own session.
 * Only the server's 404 means "closed"; 403 means this user may not use
 * the drawer; every other failure (429, 5xx, no network) is "unknown", because calling it closed would tell a cashier with a working
 * drawer to open another one.
 */
export async function readCashSessionState(terminalId: string, userId: string, fetcher: typeof fetch = fetch): Promise<{ state: CashSessionState; cashSessionId?: string }> {
  let response: Response;
  try {
    response = await fetcher(`/api/v1/terminals/${encodeURIComponent(terminalId)}/cash-sessions/active`, {
      credentials: "same-origin",
      signal: AbortSignal.timeout(8_000),
    });
  } catch {
    return { state: "unknown" };
  }
  if (response.ok) {
    try {
      const session = await response.json();
      return { state: session.cashierUserId === userId ? "open" : "notOwned", cashSessionId: session.cashSessionId };
    } catch {
      return { state: "unknown" };
    }
  }
  if (response.status === 404) return { state: "closed" };
  if (response.status === 403) return { state: "forbidden" };
  if (response.status === 401) notifySessionExpired();
  return { state: "unknown" };
}

/** The name of the user whose drawer session is open, for the header; null when it cannot be read (the header then names no one). */
export async function readCashSessionOwnerName(terminalId: string, fetcher: typeof fetch = fetch): Promise<string | null> {
  try {
    const response = await fetcher(`/api/v1/terminals/${encodeURIComponent(terminalId)}/cash-sessions/active/owner`, {
      credentials: "same-origin",
      signal: AbortSignal.timeout(8_000),
    });
    const name = response.ok ? (await response.json()).displayName : null;
    return typeof name === "string" && name ? name : null;
  } catch {
    return null;
  }
}

/**
 * Whether the own open session may be opened without a sale: the drawer is switched on and the user holds the kick permission
 * (the server answer, so the link is never offered where the drawer page would only say it cannot be used); null when it could not be read.
 */
export async function readNoSaleAvailable(terminalId: string, cashSessionId: string | undefined, fetcher: typeof fetch = fetch): Promise<boolean | null> {
  if (!cashSessionId) return false;
  try {
    const drawer = await fetcher(`/api/v1/terminals/${encodeURIComponent(terminalId)}/cash-sessions/${encodeURIComponent(cashSessionId)}/cash-drawer`, { credentials: "same-origin", signal: AbortSignal.timeout(8_000) });
    if (!drawer.ok) return null;
    const status = await drawer.json();
    return status?.enabled === true && status?.canKick === true;
  } catch {
    return null;
  }
}
