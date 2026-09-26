import { useCallback, useEffect, useId, useRef, useState } from "react";
import { formatMoney } from "../../format";
import {
  OnlineOperationsApiError,
  acceptQrOrder,
  cancelOnlineOrder,
  cancellationReasonLabels,
  handOverOnlineOrder,
  loadCustomerNote,
  loadOnlineOperations,
  outcomeLabel,
  reasonLabel,
  rejectQrOrder,
  sourceLabel,
  statusLabel,
  type CancellationReason,
  type OnlineOperationsOrder,
  type OnlineOperationsQueue,
  type SourceFilter,
} from "./onlineOperationsApi";
import "./online-operations.css";

const REFRESH_MS = 20_000;

const filters: readonly { value: SourceFilter; label: string }[] = [
  { value: "all", label: "Tümü" },
  { value: "qr", label: "QR" },
  { value: "online", label: "Yemeksepeti" },
];

const time = (iso: string) => new Date(iso).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });

/**
 * V12-OUI-001: one queue for QR orders waiting for staff and online-channel orders. Every action is sent
 * to the contract that owns it with the version of the order this screen showed, and the list is then
 * reloaded from the server, so what the operator sees is always the persisted result — an action taken
 * on an outdated screen is refused and explained, never applied.
 */
export function OnlineOperationsWorkspace({ terminalId }: { terminalId: string }) {
  const [filter, setFilter] = useState<SourceFilter>("all");
  const [queue, setQueue] = useState<OnlineOperationsQueue | null>(null);
  // A failed reload and a refused action are separate: reloading after a refused action must not
  // erase the reason the operator needs to read.
  const [loadError, setLoadError] = useState<string>();
  const [actionError, setActionError] = useState<string>();
  const [notice, setNotice] = useState<string>();
  const [busyOrderId, setBusyOrderId] = useState<string>();
  const [rejecting, setRejecting] = useState<OnlineOperationsOrder>();
  const [rejectReason, setRejectReason] = useState("");
  const [cancelling, setCancelling] = useState<OnlineOperationsOrder>();
  const [cancelReason, setCancelReason] = useState<CancellationReason>("ItemUnavailable");
  // V12-RMD-007: which order's customer note is open, and its text ("" while loading, null when there is none).
  const [noteOrderId, setNoteOrderId] = useState<string>();
  const [noteText, setNoteText] = useState<string | null>("");
  const headingId = useId();
  const reasonId = useId();

  // V12-RMD-006: every load is numbered; an answer that arrives after a newer load started (a slow poll, or the
  // previous filter's request) is dropped instead of overwriting the newer queue.
  const loadSequence = useRef(0);
  const load = useCallback(async () => {
    const sequence = ++loadSequence.current;
    try {
      const next = await loadOnlineOperations(terminalId, filter);
      if (sequence !== loadSequence.current) return;
      setQueue(next);
      setLoadError(undefined);
    } catch (reason) {
      if (sequence !== loadSequence.current) return;
      setLoadError(reason instanceof OnlineOperationsApiError ? reason.message : "Online siparişler okunamadı.");
    }
  }, [terminalId, filter]);

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => void load(), REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [load]);

  const act = async (order: OnlineOperationsOrder, action: () => Promise<void>, done: string) => {
    setBusyOrderId(order.orderId);
    setNotice(undefined);
    setActionError(undefined);
    try {
      await action();
      setNotice(done);
    } catch (reason) {
      setActionError(reason instanceof OnlineOperationsApiError ? reason.message : "İşlem tamamlanamadı. Tekrar deneyin.");
    } finally {
      setBusyOrderId(undefined);
      setRejecting(undefined);
      setCancelling(undefined);
      await load();
    }
  };

  const openNote = async (order: OnlineOperationsOrder) => {
    setNoteOrderId(order.orderId);
    setNoteText("");
    setActionError(undefined);
    try {
      setNoteText(await loadCustomerNote(terminalId, order));
    } catch (reason) {
      setNoteOrderId(undefined);
      setActionError(reason instanceof OnlineOperationsApiError ? reason.message : "Müşteri notu açılamadı.");
    }
  };

  const orders = queue?.orders ?? [];
  const problems = queue?.problems ?? [];

  return (
    <section className="online-ops" aria-labelledby={headingId}>
      <h2 id={headingId} className="online-ops__title">Online sipariş kuyruğu</h2>

      <div className="online-ops__filters" role="group" aria-label="Kaynağa göre süz">
        {filters.map((option) => (
          <button
            key={option.value}
            type="button"
            className="online-ops__filter"
            aria-pressed={filter === option.value}
            onClick={() => setFilter(option.value)}
          >
            {option.label}
          </button>
        ))}
      </div>

      <p className="online-ops__notice" role="status" aria-live="polite">{notice ?? ""}</p>
      {actionError && <p className="online-ops__error" role="alert">{actionError}</p>}
      {loadError && <p className="online-ops__error" role="alert">{loadError}</p>}
      {queue === null && !loadError && <p className="online-ops__empty">Online siparişler yükleniyor…</p>}
      {queue !== null && orders.length === 0 && <p className="online-ops__empty">İşlem bekleyen sipariş yok.</p>}

      <ul className="online-ops__list" aria-label="İşlem bekleyen siparişler">
        {orders.map((order) => {
          const busy = busyOrderId !== undefined;
          const title = order.source === "Qr" ? `Masa ${order.tableNumber ?? "—"}` : order.displayCode ?? order.orderNumber;
          return (
            <li key={order.orderId} className="online-ops__order">
              <div className="online-ops__summary">
                <span className={`online-ops__source online-ops__source--${order.source.toLowerCase()}`}>{sourceLabel(order.source)}</span>
                <span className="online-ops__name">{title}</span>
                <span className="online-ops__meta">
                  {statusLabel(order.status)} · {time(order.createdAt)} · {order.itemCount} kalem
                </span>
                <span className="online-ops__amount">{formatMoney(order.total)}</span>
              </div>
              <div className="online-ops__actions">
                {order.source === "Qr" ? (
                  <>
                    <button type="button" disabled={busy} onClick={() => void act(order, () => acceptQrOrder(terminalId, order), `${title} siparişi onaylandı.`)}>
                      Onayla
                    </button>
                    <button type="button" className="online-ops__secondary" disabled={busy} onClick={() => { setRejecting(order); setRejectReason(""); }}>
                      Reddet
                    </button>
                  </>
                ) : (
                  <>
                    <button type="button" disabled={busy} onClick={() => void act(order, () => handOverOnlineOrder(terminalId, order), `${title} siparişi kuryeye teslim edildi.`)}>
                      Kuryeye teslim et
                    </button>
                    <button type="button" className="online-ops__secondary" disabled={busy} onClick={() => { setCancelling(order); setCancelReason("ItemUnavailable"); }}>
                      İptal et
                    </button>
                    <button type="button" className="online-ops__secondary" onClick={() => void openNote(order)}>
                      Müşteri notu
                    </button>
                  </>
                )}
              </div>

              {noteOrderId === order.orderId && (
                <p className="online-ops__note" role="status" aria-live="polite">
                  {noteText === "" ? "Müşteri notu açılıyor…" : noteText ?? "Müşteri notu yok."}
                </p>
              )}

              {rejecting?.orderId === order.orderId && (
                <form
                  className="online-ops__confirm"
                  aria-label={`${title} siparişini reddet`}
                  onSubmit={(event) => {
                    event.preventDefault();
                    if (!rejectReason.trim()) return;
                    void act(order, () => rejectQrOrder(terminalId, order, rejectReason.trim()), `${title} siparişi reddedildi.`);
                  }}
                >
                  <label htmlFor={reasonId}>Ret gerekçesi</label>
                  <input id={reasonId} value={rejectReason} maxLength={200} required onChange={(event) => setRejectReason(event.target.value)} />
                  <button type="submit" disabled={busy || !rejectReason.trim()}>Reddi onayla</button>
                  <button type="button" className="online-ops__secondary" onClick={() => setRejecting(undefined)}>Vazgeç</button>
                </form>
              )}

              {cancelling?.orderId === order.orderId && (
                <form
                  className="online-ops__confirm"
                  aria-label={`${title} siparişini iptal et`}
                  onSubmit={(event) => {
                    event.preventDefault();
                    void act(order, () => cancelOnlineOrder(terminalId, order, cancelReason), `${title} siparişi iptal edildi, sağlayıcıya bildiriliyor.`);
                  }}
                >
                  <fieldset>
                    <legend>İptal gerekçesi</legend>
                    {(Object.keys(cancellationReasonLabels) as CancellationReason[]).map((value) => (
                      <label key={value} className="online-ops__radio">
                        <input type="radio" name={`cancel-${order.orderId}`} value={value} checked={cancelReason === value} onChange={() => setCancelReason(value)} />
                        {cancellationReasonLabels[value]}
                      </label>
                    ))}
                  </fieldset>
                  <button type="submit" disabled={busy}>İptali onayla</button>
                  <button type="button" className="online-ops__secondary" onClick={() => setCancelling(undefined)}>Vazgeç</button>
                </form>
              )}
            </li>
          );
        })}
      </ul>

      {filter !== "qr" && (
        <section className="online-ops__problems" aria-label="Eşleme ve aktarım sorunları">
          <h3>Eşleme ve aktarım sorunları (son 24 saat)</h3>
          {problems.length === 0 ? (
            <p className="online-ops__empty">Sorun yok.</p>
          ) : (
            <table className="online-ops__table">
              <caption className="online-ops__sr">Sağlayıcıdan gelip siparişe dönüşmeyen veya işlenemeyen olaylar</caption>
              <thead>
                <tr><th scope="col">Saat</th><th scope="col">Sağlayıcı sipariş no</th><th scope="col">Durum</th><th scope="col">Neden</th><th scope="col">Deneme</th></tr>
              </thead>
              <tbody>
                {problems.map((problem) => (
                  <tr key={problem.inboxId}>
                    <td>{time(problem.receivedAt)}</td>
                    <td>{problem.externalOrderId}</td>
                    <td>{outcomeLabel(problem.outcome)}</td>
                    <td>{reasonLabel(problem.reason)}</td>
                    <td>{problem.attempts}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {queue && (
            <ul className="online-ops__retries" aria-label="Yeniden deneme durumu">
              <li>İşlenmeyi bekleyen sağlayıcı olayı: {queue.retries.pendingProviderEvents}</li>
              <li>Yeniden denenen katalog yayını: {queue.retries.catalogPublicationsRetrying}</li>
              <li>Stok bilgisi gönderilemeyen ürün: {queue.retries.availabilityDivergences}</li>
            </ul>
          )}
        </section>
      )}
    </section>
  );
}
