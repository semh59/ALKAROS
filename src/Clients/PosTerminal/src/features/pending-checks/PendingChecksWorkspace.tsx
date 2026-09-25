import { useCallback, useEffect, useState } from "react";
import { createBillFromOrder } from "../billing";
import { formatMoney } from "../../format";
import { PendingChecksApiError, collectionHref, loadPendingChecks, recallPendingCheck, type PendingCheck } from "./pendingChecksApi";
import "./pending-checks.css";

const REFRESH_MS = 10_000;

function sentAt(iso: string): string {
  return new Date(iso).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });
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

  const load = useCallback(async () => {
    try {
      setChecks(await loadPendingChecks(terminalId));
      setError(undefined);
    } catch (reason) {
      setError(reason instanceof PendingChecksApiError ? reason.message : "Bekleyen hesaplar okunamadı.");
    }
  }, [terminalId]);

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => void load(), REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [load]);

  const collect = async (check: PendingCheck) => {
    setOpeningOrderId(check.orderId);
    try {
      const billId = check.billId ?? (await createBillFromOrder(terminalId, check.orderId)).billId;
      navigateTo(collectionHref(billId));
    } catch (reason) {
      setOpeningOrderId(undefined);
      setError(reason instanceof Error && reason.message ? reason.message : "Hesap açılamadı. Tekrar deneyin.");
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

  return (
    <section className="pending-checks" aria-label="Bekleyen hesaplar">
      {error && <p className="pending-checks__error" role="alert">{error}</p>}
      {checks === null && !error && <p className="pending-checks__empty">Bekleyen hesaplar yükleniyor…</p>}
      {checks !== null && checks.length === 0 && <p className="pending-checks__empty">Ödeme bekleyen hesap yok.</p>}
      <ul className="pending-checks__list">
        {(checks ?? []).map((check) => {
          const remaining = Math.max(0, check.total - check.paidAmount);
          return (
            <li key={check.orderId}>
              <button
                type="button"
                className="pending-checks__row"
                disabled={openingOrderId !== undefined}
                onClick={() => void collect(check)}
                aria-label={`${check.orderNumber} hesabını tahsil et`}
              >
                <span className="pending-checks__number">{check.orderNumber}</span>
                <span className="pending-checks__meta">
                  Masa {check.tableNumber} · {sentAt(check.createdAt)} · {check.itemCount} kalem
                </span>
                {check.itemPreview && <span className="pending-checks__preview">{check.itemPreview}</span>}
                <span className="pending-checks__amount">
                  {check.paidAmount > 0
                    ? <>Kalan {formatMoney(remaining)} <small>(toplam {formatMoney(check.total)})</small></>
                    : formatMoney(check.total)}
                </span>
                {openingOrderId === check.orderId && <span className="pending-checks__opening">Açılıyor…</span>}
              </button>
              {check.paidAmount === 0 && check.tableId && (
                <button
                  type="button"
                  className="pending-checks__recall"
                  disabled={openingOrderId !== undefined}
                  onClick={() => void recall(check)}
                  aria-label={`${check.orderNumber} hesabını masaya geri gönder`}
                >
                  Yanlışlıkla gönderildi: masaya geri gönder
                </button>
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}
