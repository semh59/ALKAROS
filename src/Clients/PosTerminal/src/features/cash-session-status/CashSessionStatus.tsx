import { useCallback, useEffect, useRef, useState } from "react";
import { cashSessionText } from "../../strings";
import { cashSessionNoSaleHref, cashSessionOpenHref, readCashSessionOwnerName, readCashSessionState, readNoSaleAvailable, type CashSessionState } from "./cashSessionStatusApi";
import "./cash-session-status.css";

export interface CashSessionStatus {
  /** Null until the first answer. */
  state: CashSessionState | null;
  /** Who holds the open session when it is another user's; null when unknown. */
  ownerName?: string | null;
  /** The own drawer is open and may be opened without a sale. */
  noSaleAvailable?: boolean;
  reload: () => void;
}

/** Reads the drawer state now, and again whenever the cashier returns to this tab (the drawer is opened on another page). */
export function useCashSessionState(terminalId: string, userId: string, enabled: boolean): CashSessionStatus | null {
  const [state, setState] = useState<CashSessionState | null>(null);
  const [ownerName, setOwnerName] = useState<string | null>(null);
  const [noSaleAvailable, setNoSaleAvailable] = useState(false);
  const latest = useRef(0);
  const reading = useRef(false);
  // The newest read wins, so a manual retry is never swallowed by an older one still in flight.
  const reload = useCallback(() => {
    const mine = ++latest.current;
    reading.current = true;
    void (async () => {
      const { state: next, cashSessionId } = await readCashSessionState(terminalId, userId);
      const owner = next === "notOwned" ? await readCashSessionOwnerName(terminalId) : null;
      if (mine !== latest.current) return;
      reading.current = false;
      setOwnerName(owner);
      setState(next);
      if (next !== "open") {
        setNoSaleAvailable(false);
        return;
      }
      // The drawer status never holds the header back; a read that fails keeps what the link showed before.
      const noSale = await readNoSaleAvailable(terminalId, cashSessionId);
      if (mine === latest.current && noSale !== null) setNoSaleAvailable(noSale);
    })();
  }, [terminalId, userId]);

  useEffect(() => {
    if (!enabled) return;
    setState(null);
    reload();
    // Returning to the tab fires focus and visibilitychange together; one read serves both.
    const refresh = () => { if (!reading.current && document.visibilityState === "visible") reload(); };
    window.addEventListener("focus", refresh);
    document.addEventListener("visibilitychange", refresh);
    return () => {
      window.removeEventListener("focus", refresh);
      document.removeEventListener("visibilitychange", refresh);
    };
  }, [enabled, reload]);

  return enabled ? { state, ownerName, noSaleAvailable, reload } : null;
}

export function CashSessionLink({ status, className = "header-action" }: { status: CashSessionStatus; className?: string }) {
  const key = status.state ?? "checking";
  if (key === "forbidden") return <span className={`${className} cash-session-link cash-session-link--forbidden`} role="status">{cashSessionText.forbidden}</span>;
  const text = key === "notOwned" && status.ownerName ? `${cashSessionText.notOwned} (${status.ownerName})` : cashSessionText[key];
  return <a className={`${className} cash-session-link cash-session-link--${key}`} href={cashSessionOpenHref()} title={text}>{text}</a>;
}

/** Opens the drawer without a sale, through the reason form of the drawer page; only the user's own open drawer has one. */
export function NoSaleLink({ status, className = "header-action" }: { status: CashSessionStatus; className?: string }) {
  if (!status.noSaleAvailable) return null;
  return <a className={`${className} no-sale-link`} href={cashSessionNoSaleHref()} title={cashSessionText.noSaleHint}>{cashSessionText.noSaleAction}</a>;
}

/** Shown above the work area while nothing says the user's own drawer is open. */
export function CashSessionBand({ status }: { status: CashSessionStatus }) {
  const { state } = status;
  if (state !== "closed" && state !== "notOwned" && state !== "unknown") return null;
  return <div className="cash-session-band" role="status">
    <span>{cashSessionText[`${state}Band`]}</span>
    {state !== "unknown"
      ? <a href={cashSessionOpenHref()}>{state === "closed" ? cashSessionText.openAction : cashSessionText.viewAction}</a>
      : <button data-telemetry-id="b.features.cash-session-status.1" type="button" onClick={status.reload}>{cashSessionText.retry}</button>}
  </div>;
}
