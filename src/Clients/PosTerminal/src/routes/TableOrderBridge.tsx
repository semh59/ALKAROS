import { useEffect, useState } from "react";
import { api, notifySessionExpired } from "../api";
import type { TableOrder } from "../contracts";
import { formatMoney } from "../format";
import { BillingSplitApiError } from "../features/billing";
import { collectionHrefForOrder } from "../collectOrder";

interface BridgeTable {
  tableId: string;
  tableNumber: string;
  currentOrderId: string | null;
  currentBillId: string | null;
  rowVersion: number;
  currentOrderTotal?: number;
}

interface TableOrderBridgeProps {
  terminalId: string;
  table: BridgeTable;
  /** Whether the table can take or continue an order right now. */
  orderAllowed: boolean;
  canManage: boolean;
  canCollect: boolean;
  /** Reading the bill lines needs the order permission, which the bill permission does not imply. */
  canReadLines: boolean;
  busy: boolean;
  error?: string;
  onStartOrder: () => void;
  /** Opens the table move form; absent when the table cannot be moved. */
  onTransfer?: () => void;
  navigateTo?: (href: string) => void;
}

/** What the cashier can do with the selected table: take or continue its order, and collect its check. */
export function TableOrderBridge({
  terminalId, table, orderAllowed, canManage, canCollect, canReadLines, busy, error, onStartOrder, onTransfer,
  navigateTo = (href: string) => window.location.assign(href),
}: TableOrderBridgeProps) {
  const [collecting, setCollecting] = useState(false);
  const [collectError, setCollectError] = useState<string>();
  const [bill, setBill] = useState<TableOrder | "failed" | null>(null);
  const hasCheck = table.currentOrderId !== null || table.currentBillId !== null;
  const billed = table.currentBillId !== null;
  const { tableId, currentOrderId } = table;
  useEffect(() => {
    if (!currentOrderId || !canReadLines) return;
    let current = true;
    api.tableOrder(terminalId, tableId).then(
      (order) => { if (current) setBill(order); },
      () => { if (current) setBill("failed"); },
    );
    return () => { current = false; };
  }, [terminalId, tableId, currentOrderId, canReadLines, table.currentBillId, table.rowVersion, table.currentOrderTotal]);
  const billingHref = table.currentBillId ? `/billing?billId=${encodeURIComponent(table.currentBillId)}` : `/billing?orderId=${encodeURIComponent(table.currentOrderId ?? "")}`;
  const lines = bill && bill !== "failed" ? bill.items.filter((item) => item.status !== "Cancelled" && item.status !== "Waste") : [];
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
  return <section className="table-order-bridge" aria-label="Masa hesabı">
    <div>
      <span className="table-order-bridge__kicker">MASA HESABI</span>
      <h4>{table.tableNumber}</h4>
      <p id="table-order-bridge-note">{!canManage
        ? hasCheck && canCollect ? "Bu masadaki hesabı alın." : "Bu masada sizin yapabileceğiniz bir işlem yok."
        : billed ? "Hesap açıldığı için ürün eklenemez. Eklemek için 'Hesabı al' ile açılan ekranda 'Hesabı iptal et' deyin; ödeme alındıysa önce iade gerekir."
          : table.currentOrderId ? "Bu masadaki siparişe devam edin ya da hesabını alın." : "Bu masa seçildiğinde sipariş ve mutfak kaydı birlikte açılır."}</p>
    </div>
    {hasCheck ? !canReadLines ? null : bill === "failed" ? <p className="table-order-bridge__lines-note">Hesap kalemleri alınamadı. Masayı yenileyip tekrar deneyin.</p>
      : bill && <>
        <ul className="table-order-bridge__lines" aria-label="Hesap kalemleri">
          {lines.map((line) => <li key={line.itemId}><span>{line.quantity} × {line.productName}</span><span>{formatMoney(line.totalPrice)}</span></li>)}
        </ul>
        <p className="table-order-bridge__total"><span>Toplam</span><strong>{formatMoney(bill.totalAmount)}</strong></p>
      </> : <p className="table-order-bridge__lines-note">Bu masada açık hesap yok.</p>}
    <div className="table-order-bridge__actions">
      {hasCheck && canCollect && <button data-telemetry-id="b.routes.tableorderbridge.2"
        className="primary"
        type="button"
        disabled={busy || collecting}
        aria-busy={collecting}
        onClick={() => void collect()}
      >{collecting ? "Hesap açılıyor…" : "Öde"}</button>}
      {canManage && <button data-telemetry-id="b.routes.tableorderbridge.1"
        className="secondary"
        type="button"
        disabled={!orderAllowed || busy || billed}
        aria-busy={busy}
        aria-describedby="table-order-bridge-note"
        onClick={onStartOrder}
      >{busy ? "Sipariş hazırlanıyor…" : table.currentOrderId ? "Ürün ekle" : "Sipariş al"}</button>}
      {hasCheck && canCollect && <button data-telemetry-id="b.routes.tableorderbridge.split" className="secondary" type="button" disabled={busy || collecting} onClick={() => navigateTo(billingHref)}>Böl</button>}
      {hasCheck && canManage && <button data-telemetry-id="b.routes.tableorderbridge.transfer" className="secondary" type="button" disabled={busy || collecting || !onTransfer} onClick={onTransfer}>Masa taşı</button>}
      {table.currentBillId && canCollect && <button data-telemetry-id="b.routes.tableorderbridge.cancel" className="secondary" type="button" disabled={busy || collecting} onClick={() => navigateTo(billingHref)}>İptal / iade</button>}
    </div>
    {canManage && !orderAllowed && <small className="table-order-bridge__reason">Masa şu anda sipariş almaya uygun değil (rezerve, temizlikte veya kapalı); durumunu masa kartından değiştirin.</small>}
    {(error ?? collectError) && <p className="table-order-bridge__error" role="alert">{error ?? collectError}</p>}
  </section>;
}
