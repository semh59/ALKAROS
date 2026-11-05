import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useCallback, useEffect, useRef, useState } from "react";
import { notifySessionExpired } from "../../api";
import { BillingSplitApiError, createBillFromOrder } from "../billing";
import { playNewItemChime } from "../../audioAlerts";
import { formatClockMinutes, formatMoney } from "../../format";
import { cancelOrderText, cancelReasonText } from "../../strings";
import { PendingChecksApiError, cancelPendingCheckOrder, collectionHref, loadPendingChecks, recallPendingCheck, type PendingCheck } from "./pendingChecksApi";
import { hasNewCheck } from "./usePendingChecksAlert";
import "./pending-checks.css";

// V1-RMD-287: the live push below refreshes the list at once; this poll is only the safety net for a
// missed event (the link was down, or a browser throttled the tab), so it can be slow.
const REFRESH_MS = 60_000;
const HUB_RETRY_CAP_MS = 15_000;
const hubRetryDelay = (attempt: number) => Math.min(HUB_RETRY_CAP_MS, 1_000 * 2 ** Math.min(attempt, 4));

const sentAt = formatClockMinutes;

// What a cashier tells a waiter apart by: where it came from and when it was sent, not the technical number.
function checkTitle(check: PendingCheck): string {
  const place = check.tableNumber && check.tableNumber !== "—" ? `Masa ${check.tableNumber}` : "Hızlı satış";
  return `${place} · ${sentAt(check.createdAt)}`;
}

/**
 * The till queue of checks that waiters sent over (the send-to-cashier action). Rows are told apart by check
 * number, send time and what is on them - never by the table alone, because a new party may already
 * sit at the same table. Tapping a row opens (or creates) its bill and goes straight to collection.
 * Nothing here touches the table.
 */
export function PendingChecksWorkspace({ terminalId, navigateTo = (href: string) => window.location.assign(href) }: {
  terminalId: string;
  navigateTo?: (href: string) => void;
}) {
  const [checks, setChecks] = useState<readonly PendingCheck[] | null>(null);
  const [error, setError] = useState<string>();
  const [openingOrderId, setOpeningOrderId] = useState<string>();
  const [liveLinkUp, setLiveLinkUp] = useState(true);
  const [loadedAt, setLoadedAt] = useState<Date>();
  const [cancelNotice, setCancelNotice] = useState<string>();
  const [cancelling, setCancelling] = useState<{ orderId: string; choice: string; reason: string; confirming: boolean; busy: boolean; key: string; error?: string }>();
  const knownIds = useRef<ReadonlySet<string> | null>(null);
  const latestLoad = useRef(0);

  const load = useCallback(async () => {
    // The poll, the live push and a recall can overlap; only the newest request may write the list.
    const ticket = ++latestLoad.current;
    try {
      const loaded = await loadPendingChecks(terminalId);
      if (ticket !== latestLoad.current) return;
      if (hasNewCheck(knownIds.current, loaded)) playNewItemChime();
      knownIds.current = new Set(loaded.map((check) => check.orderId));
      setChecks(loaded);
      setCancelling((current) => current && loaded.some((row) => row.orderId === current.orderId && row.paidAmount === 0) ? current : undefined);
      setLoadedAt(new Date());
      setError(undefined);
    } catch (reason) {
      if (ticket !== latestLoad.current) return;
      setError(reason instanceof PendingChecksApiError ? reason.message : "Bekleyen hesaplar okunamadı.");
    }
  }, [terminalId]);

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => void load(), REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [load]);

  // A waiter sending a check over (or taking one back) reaches the till through the same device hub the
  // waiter app listens to. Retries forever with a capped backoff and reloads on every (re)connect, the same
  // rules as the help-request link in Cashier.tsx (V1-RMD-286). A failed push only means the slow poll
  // above is what shows the change.
  useEffect(() => {
    let connection: ReturnType<HubConnectionBuilder["build"]>;
    try {
      connection = new HubConnectionBuilder()
        .withUrl(`/hubs/waiter-order-status?terminalId=${encodeURIComponent(terminalId)}`)
        .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) => hubRetryDelay(context.previousRetryCount) })
        .configureLogging(LogLevel.Warning)
        .build();
    } catch {
      return;
    }
    let disposed = false;
    let attempt = 0;
    let retryTimer: number | undefined;
    const start = () => {
      connection.start().then(() => { attempt = 0; setLiveLinkUp(true); void load(); }).catch(() => {
        if (disposed) return;
        setLiveLinkUp(false);
        retryTimer = window.setTimeout(start, hubRetryDelay(attempt++));
      });
    };
    connection.on("PendingChecksChanged", () => { void load(); });
    connection.onreconnected(() => { attempt = 0; setLiveLinkUp(true); void load(); });
    connection.onreconnecting(() => setLiveLinkUp(false));
    connection.onclose(() => {
      if (disposed) return;
      setLiveLinkUp(false);
      retryTimer = window.setTimeout(start, hubRetryDelay(attempt++));
    });
    start();
    return () => {
      disposed = true;
      window.clearTimeout(retryTimer);
      void connection.stop();
    };
  }, [terminalId, load]);

  const collect = async (check: PendingCheck) => {
    setOpeningOrderId(check.orderId);
    try {
      const billId = check.billId ?? (await createBillFromOrder(terminalId, check.orderId)).billId;
      navigateTo(collectionHref(billId));
    } catch (reason) {
      setOpeningOrderId(undefined);
      if (reason instanceof BillingSplitApiError && reason.status === 401) notifySessionExpired();
      setError(reason instanceof PendingChecksApiError || reason instanceof BillingSplitApiError ? reason.message : "Hesap açılamadı. Tekrar deneyin.");
    }
  };

  const recall = async (check: PendingCheck) => {
    if (!check.tableId) return;
    setOpeningOrderId(check.orderId);
    try {
      await recallPendingCheck(terminalId, check.orderId, check.tableId);
      setOpeningOrderId(undefined);
      await load();
    } catch (reason) {
      setOpeningOrderId(undefined);
      setError(reason instanceof PendingChecksApiError ? reason.message : "Hesap masaya geri gönderilemedi.");
    }
  };

  const cancelOrder = async (check: PendingCheck, reason: string, key: string) => {
    setCancelling((current) => current && { ...current, busy: true });
    try {
      await cancelPendingCheckOrder(terminalId, check.orderId, reason, key);
      setCancelNotice(undefined);
      setCancelling(undefined);
      await load();
    } catch (failure) {
      const message = failure instanceof PendingChecksApiError ? failure.message : cancelOrderText.failed;
      // The form keeps the typed reason and shows the failure beside the button. A lost answer may still have cancelled the order, so the retry carries the same key and the server replays instead of cancelling twice; a changed list closes the form itself.
      setCancelling((current) => current && { ...current, busy: false, confirming: false, error: message });
      setCancelNotice(message);
      await load();
    }
  };

  return (
    <section className="pending-checks" aria-label="Bekleyen hesaplar">
      {error && <p className="pending-checks__error" role="alert">{error}</p>}
      {cancelNotice && !cancelling && <p className="pending-checks__error" role="alert">{cancelNotice}</p>}
      {!liveLinkUp && <p className="pending-checks__offline" role="status">Canlı güncelleme yok: liste en geç dakikada bir yenilenir, yeni hesap geldiğinde ses çalmayabilir.</p>}
      {error && checks !== null && loadedAt && <p className="pending-checks__stale" role="status">Aşağıdaki liste eski olabilir: son yenileme {sentAt(loadedAt.toISOString())}.</p>}
      {checks === null && !error && <p className="pending-checks__empty">Bekleyen hesaplar yükleniyor…</p>}
      {checks !== null && checks.length === 0 && <p className="pending-checks__empty">Ödeme bekleyen hesap yok.</p>}
      <ul className="pending-checks__list">
        {(checks ?? []).map((check) => {
          const remaining = Math.max(0, check.total - check.paidAmount);
          return (
            <li key={check.orderId} className="pending-checks__card">
              <button data-telemetry-id="b.pending-checks.pendingchecksworkspace.1"
                type="button"
                className="pending-checks__row"
                disabled={openingOrderId !== undefined}
                onClick={() => void collect(check)}
                aria-label={`${checkTitle(check)} hesabını tahsil et`}
              >
                <span className="pending-checks__number">{checkTitle(check)}</span>
                <span className="pending-checks__meta">{check.itemCount} kalem · {check.orderNumber}</span>
                {check.itemPreview && <span className="pending-checks__preview">{check.itemPreview}</span>}
                <span className="pending-checks__amount">
                  {check.paidAmount > 0
                    ? <>Kalan {formatMoney(remaining)} <small>(toplam {formatMoney(check.total)})</small></>
                    : formatMoney(check.total)}
                </span>
                {openingOrderId === check.orderId && <span className="pending-checks__opening">Açılıyor…</span>}
              </button>
              {check.paidAmount === 0 && check.tableId && (
                <button data-telemetry-id="b.pending-checks.pendingchecksworkspace.2"
                  type="button"
                  className="pending-checks__recall"
                  disabled={openingOrderId !== undefined}
                  onClick={() => void recall(check)}
                  aria-label={`Yanlışlıkla gönderildi: masaya geri gönder, ${checkTitle(check)}`}
                >
                  Yanlışlıkla gönderildi: masaya geri gönder
                </button>
              )}
              {check.paidAmount === 0 && (cancelling?.orderId === check.orderId ? (
                <div className="pending-checks__cancel">
                  <label>
                    <span>{cancelReasonText.label}</span>
                    <select
                      aria-label={cancelReasonText.label}
                      value={cancelling.choice}
                      disabled={cancelling.busy}
                      onChange={(event) => {
                        const choice = event.target.value;
                        setCancelling({ ...cancelling, choice, reason: choice === cancelReasonText.other ? "" : choice, confirming: false });
                      }}
                    >
                      <option value="">{cancelReasonText.placeholder}</option>
                      {cancelReasonText.presets.map((preset) => <option key={preset} value={preset}>{preset}</option>)}
                      <option value={cancelReasonText.other}>{cancelReasonText.other}</option>
                    </select>
                  </label>
                  {cancelling.choice === cancelReasonText.other && (
                    <label>
                      <span>{cancelReasonText.write}</span>
                      <input type="text" maxLength={500} value={cancelling.reason} disabled={cancelling.busy}
                        onChange={(event) => setCancelling({ ...cancelling, reason: event.target.value, confirming: false })} />
                    </label>
                  )}
                  {cancelling.error && <span className="pending-checks__cancel-error" role="alert">{cancelling.error}</span>}
                  {cancelling.busy
                    ? <span role="status">{cancelOrderText.running}</span>
                    : cancelling.confirming
                      ? <>
                        <span role="alert">{cancelReasonText.confirmQuestion}</span>
                        <button data-telemetry-id="b.pending-checks.pendingchecksworkspace.4" type="button" className="pending-checks__cancel-confirm" onClick={() => void cancelOrder(check, cancelling.reason.trim(), cancelling.key)}>{cancelOrderText.confirmYes}</button>
                        <button data-telemetry-id="b.pending-checks.pendingchecksworkspace.5" type="button" className="pending-checks__recall" onClick={() => setCancelling({ ...cancelling, confirming: false })}>{cancelReasonText.dismiss}</button>
                      </>
                      : <>
                        <button data-telemetry-id="b.pending-checks.pendingchecksworkspace.6" type="button" className="pending-checks__recall" disabled={!cancelling.reason.trim()} onClick={() => setCancelling({ ...cancelling, confirming: true })}>{cancelOrderText.action}</button>
                        <button data-telemetry-id="b.pending-checks.pendingchecksworkspace.7" type="button" className="pending-checks__recall" onClick={() => setCancelling(undefined)}>{cancelReasonText.dismiss}</button>
                      </>}
                </div>
              ) : (
                <button data-telemetry-id="b.pending-checks.pendingchecksworkspace.3"
                  type="button"
                  className="pending-checks__recall"
                  disabled={openingOrderId !== undefined || cancelling !== undefined}
                  onClick={() => { setCancelNotice(undefined); setCancelling({ orderId: check.orderId, choice: "", reason: "", confirming: false, busy: false, key: crypto.randomUUID() }); }}
                  aria-label={`${cancelOrderText.open}, ${checkTitle(check)}`}
                >
                  {cancelOrderText.open}
                </button>
              ))}
            </li>
          );
        })}
      </ul>
    </section>
  );
}
