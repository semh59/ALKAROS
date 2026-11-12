import { useState } from "react";
import { notifySessionExpired } from "../api";
import { BillingSplitApiError } from "../features/billing";
import { collectionHrefForOrder } from "../collectOrder";

interface BridgeTable {
  tableNumber: string;
  currentOrderId: string | null;
  currentBillId: string | null;
}

interface TableOrderBridgeProps {
  terminalId: string;
  table: BridgeTable;
  /** Whether the table can take or continue an order right now. */
  orderAllowed: boolean;
  canManage: boolean;
  canCollect: boolean;
  busy: boolean;
  error?: string;
  onStartOrder: () => void;
  navigateTo?: (href: string) => void;
}

/** What the cashier can do with the selected table: take or continue its order, and collect its check. */
export function TableOrderBridge({
  terminalId, table, orderAllowed, canManage, canCollect, busy, error, onStartOrder,
  navigateTo = (href: string) => window.location.assign(href),
}: TableOrderBridgeProps) {
  const [collecting, setCollecting] = useState(false);
  const [collectError, setCollectError] = useState<string>();
  const hasCheck = table.currentOrderId !== null || table.currentBillId !== null;
  const billed = table.currentBillId !== null;
  const collect = async () => {
    if (!table.currentOrderId && !table.currentBillId) return;
    setCollecting(true);
    setCollectError(undefined);
    try {
      navigateTo(await collectionHrefForOrder(terminalId, table.currentOrderId ?? "", table.currentBillId));
    } catch (reason) {
      if (reason instanceof BillingSplitApiError && reason.status === 401) notifySessionExpired();
      setCollectError(reason instanceof BillingSplitApiError ? reason.message : "Hesap açılamadı. Tekrar deneyin.");
      setCollecting(false);
    }
  };
  return <section className="table-order-bridge" aria-label="Masa sipariş bağlamı">
    <div>
      <span className="table-order-bridge__kicker">SİPARİŞ BAĞLAMI</span>
      <strong>{table.tableNumber}</strong>
      <p id="table-order-bridge-note">{!canManage
        ? hasCheck && canCollect ? "Bu masadaki hesabı alın." : "Bu masada sizin yapabileceğiniz bir işlem yok."
        : billed ? "Hesap açıldığı için ürün eklenemez. Eklemek için 'Hesabı al' ile açılan ekranda 'Hesabı iptal et' deyin; ödeme alındıysa önce iade gerekir."
          : table.currentOrderId ? "Bu masadaki siparişe devam edin ya da hesabını alın." : "Bu masa seçildiğinde sipariş ve mutfak kaydı birlikte açılır."}</p>
    </div>
    <div className="table-order-bridge__actions">
      {canManage && <button data-telemetry-id="b.routes.tableorderbridge.1"
        className="primary"
        type="button"
        disabled={!orderAllowed || busy || billed}
        aria-busy={busy}
        aria-describedby="table-order-bridge-note"
        onClick={onStartOrder}
      >{busy ? "Sipariş hazırlanıyor…" : table.currentOrderId ? "Siparişe devam et" : "Masada sipariş aç"}</button>}
      {hasCheck && canCollect && <button data-telemetry-id="b.routes.tableorderbridge.2"
        type="button"
        disabled={busy || collecting}
        aria-busy={collecting}
        onClick={() => void collect()}
      >{collecting ? "Hesap açılıyor…" : "Hesabı al"}</button>}
    </div>
    {canManage && !orderAllowed && <small className="table-order-bridge__reason">Masa şu anda sipariş almaya uygun değil (rezerve, temizlikte veya kapalı); durumunu masa kartından değiştirin.</small>}
    {(error ?? collectError) && <p className="table-order-bridge__error" role="alert">{error ?? collectError}</p>}
  </section>;
}
