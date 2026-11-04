import { useCallback, useEffect, useRef, useState } from "react";
import { cashSessionText } from "../../strings";
import { cashSessionPageHref, readCashSessionState, type CashSessionState } from "./cashSessionStatusApi";
import "./cash-session-status.css";

export interface CashSessionStatus {
  /** Null until the first answer. */
  state: CashSessionState | null;
  reload: () => void;
}

/** Reads the drawer state now, and again whenever the cashier returns to this tab (the drawer is opened on another page). */
export function useCashSessionState(terminalId: string, enabled: boolean): CashSessionStatus | null {
  const [state, setState] = useState<CashSessionState | null>(null);
  const latest = useRef(0);
  const reading = useRef(false);
  // The newest read wins, so a manual retry is never swallowed by an older one still in flight.
  const reload = useCallback(() => {
    const mine = ++latest.current;
    reading.current = true;
    void readCashSessionState(terminalId).then((next) => {
      if (mine !== latest.current) return;
      reading.current = false;
      setState(next);
    });
  }, [terminalId]);

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

  return enabled ? { state, reload } : null;
}

export function CashSessionLink({ status, className = "header-action" }: { status: CashSessionStatus; className?: string }) {
  const key = status.state ?? "checking";
  if (key === "forbidden") return <span className={`${className} cash-session-link cash-session-link--forbidden`} role="status">{cashSessionText.forbidden}</span>;
  return <a className={`${className} cash-session-link cash-session-link--${key}`} href={cashSessionPageHref}>{cashSessionText[key]}</a>;
}

/** Shown above the work area while nothing says the drawer is open. */
export function CashSessionBand({ status }: { status: CashSessionStatus }) {
  if (status.state !== "closed" && status.state !== "unknown") return null;
  const closed = status.state === "closed";
  return <div className="cash-session-band" role="status">
    <span>{closed ? cashSessionText.closedBand : cashSessionText.unknownBand}</span>
    {closed
      ? <a href={cashSessionPageHref}>{cashSessionText.openAction}</a>
      : <button data-telemetry-id="b.features.cash-session-status.1" type="button" onClick={status.reload}>{cashSessionText.retry}</button>}
  </div>;
}
