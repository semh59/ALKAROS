import { useCallback, useEffect, useRef, useState } from "react";
import { formatClockMinutes, formatMoney } from "../../format";
import { recentSalesText } from "../../strings";
import { RecentSalesApiError, loadRecentSales, methodLabel, refundHref, type RecentSale, type RecentSales } from "./recentSalesApi";
import "./recent-sales.css";

const REFRESH_MS = 60_000;

const saleTitle = (sale: RecentSale) =>
  `${sale.tableNumber ? `Masa ${sale.tableNumber}` : recentSalesText.quickSale} · ${formatClockMinutes(sale.closedAt)}`;

/** The paid bills of the cashier's open session; a tap opens the payment page at its refund section. */
export function RecentSalesWorkspace({ terminalId, navigateTo = (href: string) => window.location.assign(href), closedBandShown = false }: {
  terminalId: string;
  navigateTo?: (href: string) => void;
  /** The shell band above already says the till is closed (and offers to open it), so the list does not say it twice. */
  closedBandShown?: boolean;
}) {
  const [recent, setRecent] = useState<RecentSales | null>(null);
  const [error, setError] = useState<string>();
  const [loadedAt, setLoadedAt] = useState<Date>();

  const latestLoad = useRef(0);

  const load = useCallback(async () => {
    // The poll and the refresh button can overlap, and a late answer must not overwrite a newer one.
    const ticket = ++latestLoad.current;
    try {
      const loaded = await loadRecentSales(terminalId);
      if (ticket !== latestLoad.current) return;
      setRecent({ hasOpenSession: loaded.hasOpenSession === true, sales: loaded.sales ?? [] });
      setLoadedAt(new Date());
      setError(undefined);
    } catch (reason) {
      if (ticket !== latestLoad.current) return;
      setError(reason instanceof RecentSalesApiError ? reason.message : recentSalesText.failed);
    }
  }, [terminalId]);

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => void load(), REFRESH_MS);
    const loads = latestLoad;
    return () => {
      window.clearInterval(timer);
      loads.current++;
    };
  }, [load]);

  return (
    <section className="recent-sales" aria-label={recentSalesText.title}>
      <button data-telemetry-id="b.recent-sales.recentsalesworkspace.1" type="button" className="recent-sales__refresh" onClick={() => void load()}>{recentSalesText.refresh}</button>
      {error && <p className="recent-sales__error" role="alert">{error}</p>}
      {error && recent !== null && loadedAt && <p className="recent-sales__stale" role="status">{recentSalesText.stale(formatClockMinutes(loadedAt))}</p>}
      {recent === null && !error && <p className="recent-sales__empty">{recentSalesText.loading}</p>}
      {recent !== null && !recent.hasOpenSession && !closedBandShown && <p className="recent-sales__empty">{recentSalesText.noSession}</p>}
      {recent !== null && recent.hasOpenSession && recent.sales.length === 0 && <p className="recent-sales__empty">{recentSalesText.empty}</p>}
      <ul className="recent-sales__list">
        {(recent?.sales ?? []).map((sale) => (
          <li key={sale.billId}>
            <button data-telemetry-id="b.recent-sales.recentsalesworkspace.2"
              type="button"
              className="recent-sales__row"
              aria-label={`${saleTitle(sale)}, ${sale.methods.map(methodLabel).join(", ")}, ${formatMoney(sale.amount)}: iade sayfasını aç`}
              onClick={() => navigateTo(refundHref(sale.billId))}
            >
              <span className="recent-sales__title">{saleTitle(sale)}</span>
              <span className="recent-sales__methods">{sale.methods.map(methodLabel).join(", ")}</span>
              <span className="recent-sales__amount">{formatMoney(sale.amount)}</span>
              <span className="recent-sales__action">{recentSalesText.open}</span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}
